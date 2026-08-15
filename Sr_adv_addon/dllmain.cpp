#include "pch.h"
#include "MinHookManager.h"
#include "PatternScanner.hpp"
#include <atomic>
#include <vector>
#include <string>
#include <fstream>
#include <sstream>
#include "cJSON.h"

struct GameOffsets
{
    uintptr_t FOV = 0;
    uintptr_t GAMEOBJECT_FIND = 0;
    uintptr_t GETCOMPONENT = 0;
    uintptr_t SET_VISIBLE = 0;
    uintptr_t APPLY_DITHER = 0;
};

GameOffsets g_Offsets;

// hideuidpatern
constexpr const char* GAMEOBJECT_FIND_PATTERN = "48 FF ?? ?? ?? ?? ?? 66 0F 1F 84 00 00 00 00 00 48 83 EC 28 C7 44 24 20";
constexpr const char* GETCOMPONENT_PATTERN = "48 8B 05 ?? ?? ?? ?? 48 FF E0 66 0F 1F 44 00 00 48 8B 05 ?? ?? ?? ?? 45 31 C0 48 FF E0 0F 1F 00";

constexpr ptrdiff_t GRAPHIC_COLOR_OFFSET = 0x20;
uintptr_t SET_FOV_RVA = 0;

bool g_StarRailAdvancedMode = false;
bool g_EnableStarRailAdvancedSet = false;
bool g_EnableADJSRUID = false;
float g_SRUIDColorR = 0.0f;
float g_SRUIDColorG = 0.0f;
float g_SRUIDColorB = 0.0f;
float g_SRUIDColorA = 1.0f;
bool g_SRDisablePlayerPerspectiveBlur = false;
int GlobalSRFovChangeEnabled = 2;
float GlobalSRFovValue = 45.0f;
bool g_EnableSRFOVFix = false;

// ---------- 模式开关 ----------
bool USE_DIRECT_OFFSET_MODE = false;          // 是否使用硬编码偏移量（否则特征码扫描）
constexpr bool USE_GAMEOBJECT_FIND_SINGLE_INDEX = false; // GameObject.Find 使用单索引
constexpr bool USE_GETCOMPONENT_SINGLE_INDEX = false;   // GetComponent 使用单索引

// ---------- 最大匹配数 ----------
constexpr size_t MAX_GAMEOBJECT_FIND_COUNT = 300;
constexpr size_t MAX_GETCOMPONENT_COUNT = 300;

uintptr_t GAMEOBJECT_FIND_OFFSET = 0;
uintptr_t GETCOMPONENT_OFFSET = 0;

// ---------- hideuid ----------
using FindFunc = void* (__fastcall*)(void* str);
using GetComponentFunc = void* (__fastcall*)(void* gameObject, void* str);

struct Il2CppString {
    void* klass;
    void* monitor;
    int32_t length;
    wchar_t chars[1];
};

std::vector<FindFunc>         g_gameObjectFindList;
std::vector<GetComponentFunc> g_getComponentList;

std::atomic<bool> g_uidHidden{ true };

static std::vector<std::string> g_uidPaths = {
    "/UIRoot/AboveDialog/BetaHintDialog(Clone)/Contents/VersionText",
    "/UIRoot/Page/MobilePhoneMainPage(Clone)/Content/Content/LeftPlane/Tittle/UID/NumText"
};
// -----------------------------

BOOL __declspec(noinline) OnWinError(const char* szFunction, DWORD dwError)
{
    char szMessage[256];
    wsprintfA(szMessage, "%s failed with error %d", szFunction, dwError);
    MessageBoxA(nullptr, szMessage, "Error", MB_ICONERROR);
    return FALSE;
}

// ---------- hideuid ----------
// ---------- 辅助函数 ----------
static Il2CppString* CreateIl2CppString(const char* str) {
    if (!str) return nullptr;
    int len = MultiByteToWideChar(CP_UTF8, 0, str, -1, nullptr, 0);
    if (len <= 1) return nullptr;
    size_t size = offsetof(Il2CppString, chars) + (len - 1) * sizeof(wchar_t);
    auto* il2cppStr = reinterpret_cast<Il2CppString*>(new char[size]);
    if (!il2cppStr) return nullptr;
    memset(il2cppStr, 0, size);
    il2cppStr->length = len - 1;
    MultiByteToWideChar(CP_UTF8, 0, str, -1, il2cppStr->chars, len);
    return il2cppStr;
}

static void FreeIl2CppString(Il2CppString* str) {
    if (str) delete[] reinterpret_cast<char*>(str);
}

static void SetGraphicAlpha(void* graphic, float alpha) {
    __try {
        if (graphic) {
            float* color = reinterpret_cast<float*>(
                reinterpret_cast<uint8_t*>(graphic) + GRAPHIC_COLOR_OFFSET);
            color[3] = alpha;
        }
    }
    __except (EXCEPTION_EXECUTE_HANDLER) {}
}

static void SetGraphicColor(void* graphic, float r, float g, float b, float a) {
    __try {
        if (graphic) {
            float* color = reinterpret_cast<float*>(
                reinterpret_cast<uint8_t*>(graphic) + GRAPHIC_COLOR_OFFSET);
            color[0] = r; color[1] = g; color[2] = b; color[3] = a;
        }
    }
    __except (EXCEPTION_EXECUTE_HANDLER) {}
}
// -----------------------------

// ========== hideuid ==========
// 将每次调用独立用 SEH 包裹，防止错误地址引发异常导致整个循环中断
static void* SafeCallFind(FindFunc func, Il2CppString* str) {
    __try {
        return func(str);
    }
    __except (EXCEPTION_EXECUTE_HANDLER) {
        return nullptr;
    }
}

static void* SafeCallGetComponent(GetComponentFunc func, void* gameObject, Il2CppString* str) {
    __try {
        return func(gameObject, str);
    }
    __except (EXCEPTION_EXECUTE_HANDLER) {
        return nullptr;
    }
}
// ========== hideuid ==========

// ---------- 核心隐藏逻辑（遍历所有 Find 和 GetComponent 组合）----------
static void HideUIDByPath(const char* path) {
    if (!path || g_gameObjectFindList.empty() || g_getComponentList.empty())
        return;

    Il2CppString* pathStr = CreateIl2CppString(path);
    if (!pathStr) return;

    for (auto findFunc : g_gameObjectFindList) {
        void* gameObject = SafeCallFind(findFunc, pathStr);
        if (!gameObject) continue;

        Il2CppString* graphicName = CreateIl2CppString("UnityEngine.UI.Graphic");
        if (!graphicName) continue;

        for (auto getComp : g_getComponentList) {
            void* graphic = SafeCallGetComponent(getComp, gameObject, graphicName);
            if (!graphic) continue;

            if (g_uidHidden.load(std::memory_order_relaxed)) {
                //SetGraphicAlpha(graphic, 0.0f);
                SetGraphicColor(graphic, g_SRUIDColorR, g_SRUIDColorG, g_SRUIDColorB, g_SRUIDColorA);
            }
            else {
                //SetGraphicAlpha(graphic, 1.0f);
                SetGraphicColor(graphic, 1.0f, 1.0f, 1.0f, 0.7f);
            }
        }
        FreeIl2CppString(graphicName);
    }
    FreeIl2CppString(pathStr);
}

void LoadOffset()
{
    char dllPath[MAX_PATH]{};
    HMODULE hModule = nullptr;
    GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS,(LPCSTR)&LoadOffset,&hModule);
    GetModuleFileNameA(hModule,dllPath,MAX_PATH);

    std::string path(dllPath);
    size_t pos = path.find_last_of("\\/");
    if (pos != std::string::npos)
    {
        path = path.substr(0, pos);
    }

    path += "\\offsets.json";
    std::ifstream file(path);

    if (!file.is_open())
    {
        LOG("offsets.json not found");
        return;
    }

    std::stringstream buffer;
    buffer << file.rdbuf();
    std::string jsonText = buffer.str();
    cJSON* root = cJSON_Parse(jsonText.c_str());

    if (!root)
    {
        LOG("JSON parse failed");
        return;
    }

    cJSON* srCn = cJSON_GetObjectItem(root, "SR_CN");

    if (!srCn)
    {
        LOG("SR_CN not found");
        cJSON_Delete(root);
        return;
    }

    auto ReadOffset = [&](const char* name) -> uintptr_t
    {
        cJSON* item = cJSON_GetObjectItem(srCn, name);

        if (!item || item->type != cJSON_String)
        {
            LOG("%s not found", name);
            return 0;
        }

        return std::stoull(item->valuestring,nullptr,16);
    };


    // FPS 不读取
    g_Offsets.FOV = ReadOffset("FOV");
    g_Offsets.GAMEOBJECT_FIND = ReadOffset("GAMEOBJECT_FIND");
    g_Offsets.GETCOMPONENT = ReadOffset("GETCOMPONENT");
    g_Offsets.SET_VISIBLE = ReadOffset("SET_VISIBLE");
    g_Offsets.APPLY_DITHER = ReadOffset("APPLY_DITHER");    

    LOG("FOV: 0x%llX", g_Offsets.FOV);
    LOG("GAMEOBJECT_FIND: 0x%llX", g_Offsets.GAMEOBJECT_FIND);
    LOG("GETCOMPONENT: 0x%llX", g_Offsets.GETCOMPONENT);
    LOG("SET_VISIBLE: 0x%llX", g_Offsets.SET_VISIBLE);
    LOG("APPLY_DITHER: 0x%llX", g_Offsets.APPLY_DITHER);

    cJSON_Delete(root);
}

std::string g_ConfigPath;

void InitConfigPath()
{
    if (!g_ConfigPath.empty()) {
        return;
    }

    char dllPath[MAX_PATH]{};
    HMODULE hModule = nullptr;
    GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS,(LPCSTR)&InitConfigPath,&hModule);
    GetModuleFileNameA(hModule, dllPath, MAX_PATH);

    std::string path(dllPath);
    size_t pos = path.find_last_of("\\/");
    if (pos != std::string::npos) {
        path = path.substr(0, pos);
    }

    pos = path.find_last_of("\\/");
    if (pos != std::string::npos) {
        path = path.substr(0, pos);
    }

    g_ConfigPath = path + "\\fps_config.json";
}

void LoadNeedAddr()
{
    std::ifstream file(g_ConfigPath);

    if (!file.is_open())
    {
        LOG("fps_config.json not found");
        return;
    }

    std::stringstream buffer;
    buffer << file.rdbuf();
    std::string jsonText = buffer.str();

    cJSON* root = cJSON_Parse(jsonText.c_str());

    if (!root)
    {
        LOG("fps_config.json parse failed");
        return;
    }

    cJSON* mode = cJSON_GetObjectItem(root,"StarRailAdvancedMode");

    if (mode && mode->type == cJSON_True)
    {
        g_StarRailAdvancedMode = true;
    }
    else
    {
        g_StarRailAdvancedMode = false;
    }

    LOG("StarRailAdvancedMode: %s",g_StarRailAdvancedMode ? "true" : "false");
    cJSON_Delete(root);
}

void LoadConfig()
{
    if (g_ConfigPath.empty())
        InitConfigPath();

    std::ifstream file(g_ConfigPath);
    if (!file.is_open())
        return;

    std::stringstream buffer;
    buffer << file.rdbuf();

    cJSON* root = cJSON_Parse(buffer.str().c_str());
    if (!root)
        return;
    cJSON* item;

    item = cJSON_GetObjectItem(root, "EnableStarRailAdvancedSet");
    if (item)
        g_EnableStarRailAdvancedSet = cJSON_IsTrue(item);

    item = cJSON_GetObjectItem(root, "EnableADJSRUID");
    if (item)
        g_EnableADJSRUID = cJSON_IsTrue(item);

    item = cJSON_GetObjectItem(root, "SRUIDColorR");
    if (item)
        g_SRUIDColorR = (float)item->valuedouble;

    item = cJSON_GetObjectItem(root, "SRUIDColorG");
    if (item)
        g_SRUIDColorG = (float)item->valuedouble;

    item = cJSON_GetObjectItem(root, "SRUIDColorB");
    if (item)
        g_SRUIDColorB = (float)item->valuedouble;

    item = cJSON_GetObjectItem(root, "SRUIDColorA");
    if (item)
        g_SRUIDColorA = (float)item->valuedouble;

    item = cJSON_GetObjectItem(root, "SRDisablePlayerPerspectiveBlur");
    if (item)
        g_SRDisablePlayerPerspectiveBlur = cJSON_IsTrue(item);

    item = cJSON_GetObjectItem(root, "EnableFOVStarRailFix");
    if (item)
        g_EnableSRFOVFix = !cJSON_IsTrue(item);

    item = cJSON_GetObjectItem(root, "FOVTargetStarRail");
    if (item)
        GlobalSRFovValue = (float)item->valuedouble;

    cJSON_Delete(root);
}

// ---------- 扫描并填充函数列表（支持多种模式）----------
bool ScanAndResolveFunctions() {
    std::vector<std::string> errors;
    HMODULE gameAssembly = GetModuleHandleA("GameAssembly.dll");
    uintptr_t gameAssemblyBase = reinterpret_cast<uintptr_t>(gameAssembly);

    // -------------------- GameObject::Find --------------------
    //if constexpr (USE_DIRECT_OFFSET_MODE)
    if (USE_DIRECT_OFFSET_MODE) {
        if (!gameAssembly) {
            errors.push_back("GameAssembly.dll not found");
            LOG("[ERROR] %s", errors.back().c_str());
        }
        else {
            uintptr_t addr = gameAssemblyBase + GAMEOBJECT_FIND_OFFSET;
            LOG("[Direct] GameObject::Find at 0x%p", reinterpret_cast<void*>(addr));
            g_gameObjectFindList.push_back(reinterpret_cast<FindFunc>(addr));
        }
    }
    else {
        LOG("[Scan] Scanning GameObject::Find pattern...");
        auto results = PatternScanner::MultipleScan(GAMEOBJECT_FIND_PATTERN);
        if (results.empty()) {
            errors.push_back("GameObject::Find pattern not found");
            LOG("[ERROR] %s", errors.back().c_str());
        }
        else {
            if constexpr (USE_GAMEOBJECT_FIND_SINGLE_INDEX) {
                if (MAX_GAMEOBJECT_FIND_COUNT <= results.size() && MAX_GAMEOBJECT_FIND_COUNT > 0) {
                    size_t idx = MAX_GAMEOBJECT_FIND_COUNT - 1;
                    LOG("[OK] Found %d Find matches, using #%d", (int)results.size(), (int)MAX_GAMEOBJECT_FIND_COUNT);
                    LOG("  [#%d] 0x%p", (int)MAX_GAMEOBJECT_FIND_COUNT, reinterpret_cast<void*>(results[idx]));
                    g_gameObjectFindList.push_back(reinterpret_cast<FindFunc>(results[idx]));
                }
                else {
                    errors.push_back("GameObject::Find index out of range");
                    LOG("[ERROR] Index #%d out of range (total: %d)", (int)MAX_GAMEOBJECT_FIND_COUNT, (int)results.size());
                }
            }
            else {
                size_t count = std::min<size_t>(MAX_GAMEOBJECT_FIND_COUNT, results.size());
                LOG("[OK] Found %d Find matches, using first %d", (int)results.size(), (int)count);
                for (size_t i = 0; i < count; i++) {
                    uintptr_t absAddr = results[i];
                    uintptr_t offset = (gameAssemblyBase != 0) ? (absAddr - gameAssemblyBase) : 0;
                    LOG("  [%d] 0x%p (offset: 0x%llX)", (int)(i + 1), reinterpret_cast<void*>(absAddr), offset);
                    g_gameObjectFindList.push_back(reinterpret_cast<FindFunc>(absAddr));
                }
            }
        }
    }

    // -------------------- GetComponent --------------------
    //if constexpr (USE_DIRECT_OFFSET_MODE) 
    if (USE_DIRECT_OFFSET_MODE) {
        if (!gameAssembly) {
            errors.push_back("GameAssembly.dll not found");
            LOG("[ERROR] %s", errors.back().c_str());
        }
        else {
            uintptr_t addr = gameAssemblyBase + GETCOMPONENT_OFFSET;
            LOG("[Direct] GetComponent at 0x%p", reinterpret_cast<void*>(addr));
            g_getComponentList.push_back(reinterpret_cast<GetComponentFunc>(addr));
        }
    }
    else {
        LOG("[Scan] Scanning GetComponent pattern...");
        auto results = PatternScanner::MultipleScan(GETCOMPONENT_PATTERN);
        if (results.empty()) {
            errors.push_back("GetComponent pattern not found");
            LOG("[ERROR] %s", errors.back().c_str());
        }
        else {
            if constexpr (USE_GETCOMPONENT_SINGLE_INDEX) {
                if (MAX_GETCOMPONENT_COUNT <= results.size() && MAX_GETCOMPONENT_COUNT > 0) {
                    size_t idx = MAX_GETCOMPONENT_COUNT - 1;
                    LOG("[OK] Found %d GetComponent matches, using #%d", (int)results.size(), (int)MAX_GETCOMPONENT_COUNT);
                    LOG("  [#%d] 0x%p", (int)MAX_GETCOMPONENT_COUNT, reinterpret_cast<void*>(results[idx]));
                    g_getComponentList.push_back(reinterpret_cast<GetComponentFunc>(results[idx]));
                }
                else {
                    errors.push_back("GetComponent index out of range");
                    LOG("[ERROR] Index #%d out of range (total: %d)", (int)MAX_GETCOMPONENT_COUNT, (int)results.size());
                }
            }
            else {
                size_t count = std::min<size_t>(MAX_GETCOMPONENT_COUNT, results.size());
                LOG("[OK] Found %d GetComponent matches, using first %d", (int)results.size(), (int)count);
                for (size_t i = 0; i < count; i++) {
                    uintptr_t absAddr = results[i];
                    uintptr_t offset = (gameAssemblyBase != 0) ? (absAddr - gameAssemblyBase) : 0;
                    LOG("  [%d] 0x%p (offset: 0x%llX)", (int)(i + 1), reinterpret_cast<void*>(absAddr), offset);
                    g_getComponentList.push_back(reinterpret_cast<GetComponentFunc>(absAddr));
                }
            }
        }
    }

    return errors.empty();
}

bool last_EnableADJSRUID = false;
float last_SRUIDColorR = 0.0f;
float last_SRUIDColorG = 0.0f;
float last_SRUIDColorB = 0.0f;
float last_SRUIDColorA = 1.0f;

bool CheckSRUIDConfigChanged()
{
    if (last_EnableADJSRUID != g_EnableADJSRUID ||
        last_SRUIDColorR != g_SRUIDColorR ||
        last_SRUIDColorG != g_SRUIDColorG ||
        last_SRUIDColorB != g_SRUIDColorB ||
        last_SRUIDColorA != g_SRUIDColorA)
    {
        last_EnableADJSRUID = g_EnableADJSRUID;

        last_SRUIDColorR = g_SRUIDColorR;
        last_SRUIDColorG = g_SRUIDColorG;
        last_SRUIDColorB = g_SRUIDColorB;
        last_SRUIDColorA = g_SRUIDColorA;

        return true;
    }

    return false;
}

int timesAtThirtylocal = 0;
bool _isfirstfoundsrfovlocal = true;
std::chrono::steady_clock::time_point g_escTime;

typedef void(*SetFieldOfView_t_l)(void* _this, float value);
SetFieldOfView_t_l g_original_SetFieldOfView_l = nullptr;

// ---------- FOV Hook ----------
void __fastcall HookSetFieldOfView(void* _this, float value) {

    //static int frameCount = 0;
    //frameCount++;
    //if (frameCount % 300 != 0) return;
    //for (const auto& path : g_uidPaths) {
    //    HideUIDByPath(path.c_str());
    //}

    if (CheckSRUIDConfigChanged())
    {
        for (const auto& path : g_uidPaths)
        {
            HideUIDByPath(path.c_str());
        }
    }

    //// ESC按下重新计时
    //if (GetAsyncKeyState(VK_ESCAPE) & 1)
    //{
    //    g_escTime = std::chrono::steady_clock::now();
    //}


    //// ESC触发后500ms内执行
    //if (std::chrono::steady_clock::now() - g_escTime < std::chrono::milliseconds(700))
    //{
    //    static int frameCount = 0;
    //    frameCount++;
    //    if (frameCount % 60 != 0) return;
    //    for (const auto& path : g_uidPaths) {
    //        HideUIDByPath(path.c_str());
    //    }
    //}


    // ---------- FOV 23.0 触发 HideUID ----------
    static float lastFov = -1.0f;
    static int frameCount = 0;

    // 调试输出
    //OutputDebugStringA(("[DLL] FOV value: " + std::to_string(value) + "\n").c_str());

    // 判断当前是否为 23.0
    bool lastIs23 = std::abs(lastFov - 23.0f) < 0.001f;
    bool currentIs23 = std::abs(value - 23.0f) < 0.001f;

    // 只有「非23 -> 23」时触发
    if (!lastIs23 && currentIs23)
    {
        g_escTime = std::chrono::steady_clock::now();
        frameCount = 0;
    }

    // 保存本次 FOV
    lastFov = value;

    // 23.0 触发后的 700ms 内
    bool fovHideActive = std::chrono::steady_clock::now() - g_escTime < std::chrono::milliseconds(700);

    if (fovHideActive)
    {
        frameCount++;

        // 每 60 次执行一次
        if (frameCount % 60 == 0)
        {
            for (const auto& path : g_uidPaths)
            {
                HideUIDByPath(path.c_str());
            }
        }
    }



    float _oldvalue = value;

    // 第一次不开启fov修复
    if (_oldvalue == 30.0f)
    {
        timesAtThirtylocal++;

        if (timesAtThirtylocal == 1)
        {
            _isfirstfoundsrfovlocal = false;
        }
        else if (timesAtThirtylocal == 2)
        {
        }
    }
    if (_oldvalue >= 31.0f)
    {
        _isfirstfoundsrfovlocal = true;
    }

    bool fovfixestate = true;
    if (_oldvalue > 31.0f)
    {
        fovfixestate = false;
    }
    if (_oldvalue > 45.1f && _oldvalue < 47.5f)
    {
        fovfixestate = true;
    }
    if (_oldvalue > 31.0f && _oldvalue < 44.0f)
    {
        fovfixestate = true;
    }
    if (g_EnableSRFOVFix == 1)
    {
        fovfixestate = false;
    }

    if (GlobalSRFovChangeEnabled == 2 && GlobalSRFovValue != 45.0f)
    {
        if (_isfirstfoundsrfovlocal == true)
        {
            if (!fovfixestate)
            {
                return g_original_SetFieldOfView_l(_this, GlobalSRFovValue);
            }
            else
            {
                return g_original_SetFieldOfView_l(_this, value);
            }
        }
        else
        {
            return g_original_SetFieldOfView_l(_this, GlobalSRFovValue);
        }
    }
    else
    {
        return g_original_SetFieldOfView_l(_this, value);
    }


    // g_original_SetFieldOfView(_this, 100.0f);
    //g_original_SetFieldOfView(_this, value);  // 使用原始参数值
    //static int frameCount = 0;
    //frameCount++;
    //if (frameCount % 60 != 0) return;
    //for (const auto& path : g_uidPaths) {
    //    HideUIDByPath(path.c_str());
    //}
}

// ---------- 安装 FOV Hook ----------
bool InstallFovHook() {
    HMODULE unityPlayer = GetModuleHandleA("UnityPlayer.dll");
    if (!unityPlayer) {
        LOG("[ERROR] UnityPlayer.dll not found!");
        return false;
    }

    uintptr_t setFieldOfViewAddr = reinterpret_cast<uintptr_t>(unityPlayer) + SET_FOV_RVA;
    LOG("Target: Camera.SetFieldOfView at 0x%p", reinterpret_cast<void*>(setFieldOfViewAddr));
    if (IsBadReadPtr(reinterpret_cast<void*>(setFieldOfViewAddr), sizeof(void*))) {
        LOG("[ERROR] SetFieldOfView address invalid!");
        return false;
    }

    // 使用 MinHookManager 创建钩子
    if (!MinHookManager::Add(reinterpret_cast<void*>(setFieldOfViewAddr),
        reinterpret_cast<void*>(&HookSetFieldOfView),
        reinterpret_cast<void**>(&g_original_SetFieldOfView_l))) {
        return false;
    }
}


void RunConfig() {
    while (true) {
        LoadConfig();
        Sleep(1000);
        LOG(
            "Config reloaded: EnableStarRailAdvancedSet=%s, EnableADJSRUID=%s, FOV=%.2f, SRUIDColor=(%.2f, %.2f, %.2f, %.2f), SRDisablePlayerPerspectiveBlur=%s",
            g_EnableStarRailAdvancedSet ? "true" : "false",
            g_EnableADJSRUID ? "true" : "false",
            GlobalSRFovValue,
            g_SRUIDColorR,
            g_SRUIDColorG,
            g_SRUIDColorB,
            g_SRUIDColorA,
            g_SRDisablePlayerPerspectiveBlur ? "true" : "false"
        );

        g_uidHidden = g_EnableADJSRUID;
    }
}

void RunLogic() {
    LoadOffset();
	LoadNeedAddr();

	// 赋值给全局偏移量变量
    GETCOMPONENT_OFFSET = g_Offsets.GETCOMPONENT;
	GAMEOBJECT_FIND_OFFSET = g_Offsets.GAMEOBJECT_FIND;
	SET_FOV_RVA = g_Offsets.FOV;

    if (g_StarRailAdvancedMode == true) {
		USE_DIRECT_OFFSET_MODE = true;
    }

    ScanAndResolveFunctions();

    if (!InstallFovHook()) {
        LOG("[ERROR] Failed to install FOV hook!");
        return;
    }
}

DWORD WINAPI MainThread(LPVOID) {
    //InitConfigPath();
    //AllocConsole();
    //FILE* f;
    //freopen_s(&f, "CONOUT$", "w", stdout);
    LOG("hideSRuidpath DLL loaded");
    Sleep(1000);

    HANDLE hThread = CreateThread(nullptr, 0, (LPTHREAD_START_ROUTINE)RunLogic, nullptr, 0, nullptr);
    if (!hThread) {
        return OnWinError("CreateThread", GetLastError());
    }
    CloseHandle(hThread);

    HANDLE hThread2 = CreateThread(nullptr, 0, (LPTHREAD_START_ROUTINE)RunConfig, nullptr, 0, nullptr);
    if (!hThread2) {
        return OnWinError("CreateThread", GetLastError());
    }
    CloseHandle(hThread2);

    return 0;
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(hModule);
        HMODULE hStarRail = GetModuleHandleA("StarRail.exe");
        if (!hStarRail)
        {
            return TRUE;
        }
        CreateThread(nullptr, 0, MainThread, nullptr, 0, nullptr);
    }

    return TRUE;
}