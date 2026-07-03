// Alltools.cpp
#include "pch.h"
#include <windows.h>
#include <vector>
#include <string>
#include <Psapi.h>
#include <chrono>
#include <sstream>
#include <algorithm>
#include <cctype>
#include "MinHookManager.h"
#include "PatternScanner.hpp"

#pragma comment(lib, "psapi.lib")


// 全局配置加载标志
static bool ConfigLoaded = false;
//全局游戏状态标志
static bool GameInited = false;

// 是否需要去除草地 260225
static int NeedRemoveGrass = -1;

// 配置结构
struct Config {
    bool hide_uid = false;  // 默认启用隐藏UID
    bool hide_damage_text = false; // 默认启用隐藏伤害文本
    bool hide_DisableEventCameraMove = false; // 默认启用禁止事件相机移动
    bool hide_quest_banner = false; // 默认隐藏任务横幅
    bool enable_remove_team_anim = false; // 默认启用移除队伍动画
    bool enable_craft_redirection = false; // 默认启用合成台重定向
    bool enable_craft_redirection_key = false; // 默认启用快捷键合成
    bool hide_grass = false; // 默认启用草地 260225
};

static Config g_config;

namespace config
{
    static std::string current_config;

    void LoadConfig(const std::string& config_str)
    {
        current_config = config_str;
    }

    std::string GetConfig()
    {
        return current_config;
    }
}

// 辅助函数：修剪字符串两端的空白字符
static std::string trim(const std::string& str) {
    size_t start = str.find_first_not_of(" \t\n\r");
    if (start == std::string::npos) return "";

    size_t end = str.find_last_not_of(" \t\n\r");
    return str.substr(start, end - start + 1);
}

// 辅助函数：将字符串转换为小写
static std::string toLower(const std::string& str) {
    std::string result = str;
    std::transform(result.begin(), result.end(), result.begin(),
        [](unsigned char c) { return std::tolower(c); });
    return result;
}

// 辅助函数：解析INI格式的配置字符串
static void ParseConfig(const std::string& config_str) {
    std::istringstream stream(config_str);
    std::string line;
    std::string current_section;

    while (std::getline(stream, line)) {
        // 修剪行
        line = trim(line);
        if (line.empty()) continue;

        // 检查是否是节（section）
        if (line.front() == '[' && line.back() == ']') {
            current_section = toLower(line.substr(1, line.length() - 2));
            continue;
        }

        // 解析键值对
        size_t equals_pos = line.find('=');
        if (equals_pos != std::string::npos) {
            std::string key = trim(line.substr(0, equals_pos));
            std::string value = trim(line.substr(equals_pos + 1));

            // 根据键名设置对应的配置值
            if (toLower(key) == "name") {
                // 在Option_X节中，Name字段表示选项名称
                if (current_section.find("option_") != std::string::npos) {
                    // 根据选项名称设置对应的配置值
                    if (toLower(value) == "hideuid") {
                        // 这个会在后面根据Value来设置
                    }
                }
            }
            else if (toLower(key) == "value") {
                // 根据当前所在的节来设置对应的配置
                if (current_section == "option_7") { // HideQuestBanner
                    g_config.hide_quest_banner = (toLower(value) == "true");
                }
                else if (current_section == "option_8") { // DisableShowDamageText
                    g_config.hide_damage_text = (toLower(value) == "true");
                }
                else if (current_section == "option_10") { // DisableEventCameraMove
                    g_config.hide_DisableEventCameraMove = (toLower(value) == "true");
                }
                else if (current_section == "option_11") { // RedirectCombineEntry
                    g_config.enable_craft_redirection = (toLower(value) == "true");
                }
                else if (current_section == "option_12") { // HideUID
                    g_config.hide_uid = (toLower(value) == "true");
                }
                else if (current_section == "option_13") { // RedirectCombineEntryKey
                    g_config.enable_craft_redirection_key = (toLower(value) == "true");
                }
                else if (current_section == "option_14") { // EnableRemoveGrass
                    g_config.hide_grass = (toLower(value) == "true");
                }
                else if (current_section == "option_6") { // EnableImmediateOpenTeam
                    g_config.enable_remove_team_anim = (toLower(value) == "true");
                }
            }
        }
    }
}

DWORD WINAPI ReadConfigThread(LPVOID lpParam)
{
    while(true)
    {
        // 每隔1秒读取一次配置
        Sleep(115);
        // 获取配置字符串
        std::string config_str = config::GetConfig();
        // 解析配置并应用到g_config
        ParseConfig(config_str);
	}
}




//数字伤害显示相关
struct Il2CppObject {
    void* klass;
    void* monitor;
};

// IL2CPP 结构
struct Il2CppString {
    void* klass;
    void* monitor;
    int length;
    wchar_t chars[32];


    //数字伤害显示相关
    Il2CppObject object;
    int32_t lengthdam;
    char16_t charsdam[32];
};

// 合成台相关函数指针
uintptr_t g_pCraftEntryPartnerFunc = 0;
uintptr_t g_pCraftEntryFunc = 0;

// 队伍进入及配置检测函数指针类型定义
using CheckCanEnter_t = bool(__fastcall*)();
CheckCanEnter_t g_pCheckCanEnter = nullptr;

// 打开队伍页面函数指针类型定义
using OpenTeamPageAccordingly_t = void(__fastcall*)(bool param);
OpenTeamPageAccordingly_t g_pOpenTeamPageAccordingly = nullptr;

// 打开队伍函数指针类型定义
using OpenTeam_t = void(__fastcall*)();
OpenTeam_t g_pOpenTeam = nullptr;
OpenTeam_t g_original_OpenTeam = nullptr;


// ShowOneDamageTextEx 函数指针类型定义
typedef void (*ShowOneDamageTextEx_t)(void* p_this,int type_,int damage_type,int show_type,float damage,Il2CppString* show_text,void* world_pos,void* attackee,int element_reaction_type);
static ShowOneDamageTextEx_t original_ShowOneDamageTextEx = nullptr;

// EventCameraMove 函数指针类型定义
typedef bool(__fastcall* EventCameraMove_t)(void* pThis, void* event);
EventCameraMove_t original_EventCameraMove = nullptr;



// 函数指针类型定义
typedef Il2CppString* (__stdcall* FindString_t)(const char*);
typedef void* (__stdcall* FindGameObject_t)(Il2CppString*);
typedef void(__stdcall* SetActive_t)(void*, bool);
typedef void(__stdcall* SetupQuestBanner_t)(void*);
// 260225 去除草地
typedef Il2CppString* (__stdcall* GetGrass_t)(void*);

// 全局函数指针
static FindString_t g_find_string = nullptr;
static FindGameObject_t g_find_game_object = nullptr;
static SetActive_t g_set_active = nullptr;
static SetupQuestBanner_t g_original_setup_quest_banner = nullptr;
// 260225 去除草地
static GetGrass_t g_grass_name = nullptr; // 新增


// 260225 去除草地
void __stdcall Hook_Grass(void* p_this, bool active) {
    // 1. 配置必须开启 (hide_grass)
    // 2. active 必须为 true (只有当游戏试图“显示”物体时才检查，隐藏操作无需拦截)
    // 3. 必须成功获取到 GetName 函数
    //if (g_config.hide_grass && active && g_grass_name) 

    if (true && active && g_grass_name)
    {
        //OutputDebugStringA("[DLL] REMOVE grass on");

        Il2CppString* name = g_grass_name(p_this);
        if (name) {
            // 检查名称匹配规则：
            // 包含 "Grass" 且 不包含 "Eff" (特效) 且 不包含 "Monster" (怪物)
            if (wcsstr(name->chars, L"Grass") &&
                !wcsstr(name->chars, L"Eff") &&
                !wcsstr(name->chars, L"Monster")) {
                // 满足条件，直接返回，不调用原始 SetActive
                // 这意味着该物体永远不会被“激活”（显示），从而实现除草
                return;
            }
        }
    }

    // 不满足拦截条件，调用原始函数
    if (g_set_active) {
        g_set_active(p_this, active);
    }
}











// OpenTeam 钩子 - 移除队伍动画
void __fastcall Hook_OpenTeam() {
    if (g_config.enable_remove_team_anim && g_pCheckCanEnter && g_pOpenTeamPageAccordingly) {
        // 检查是否可以进入队伍页面
        if (g_pCheckCanEnter()) {
            // 直接打开队伍页面，跳过动画
            g_pOpenTeamPageAccordingly(false);
            return;
        }
    }

    // 调用原函数
    if (g_original_OpenTeam) {
        g_original_OpenTeam();
    }
}


void __fastcall hook_ShowOneDamageTextEx(void* p_this,int type_,int damage_type,int show_type,float damage,Il2CppString* show_text,void* world_pos,void* attackee,int element_reaction_type) 
{
    if (g_config.hide_damage_text) 
    {
        // 如果启用隐藏伤害文本，直接返回
        return;
	}
    else
    {
        // 开启伤害显示，调用原函数
        return original_ShowOneDamageTextEx(p_this, type_, damage_type, show_type, damage,show_text, world_pos, attackee, element_reaction_type);
    }
}


bool __fastcall hook_EventCameraMove(void* pThis, void* event) 
{
    if (g_config.hide_DisableEventCameraMove) 
    {
        return true;  
    }

    if (original_EventCameraMove) 
    {
        return original_EventCameraMove(pThis, event);
    }
}


// 隐藏任务横幅的函数
void HideQuestBannerFunction()
{
    if (!g_config.hide_quest_banner || !g_find_string || !g_find_game_object || !g_set_active)
    {
        return;
    }

    // 隐藏任务横幅逻辑
    const char* bannerPath = "Canvas/Pages/InLevelMapPage/GrpMap/GrpPointTips/Layout/QuestBanner";
    Il2CppString* bannerStr = g_find_string(bannerPath);
    if (bannerStr) {
        void* bannerObj = g_find_game_object(bannerStr);
        if (bannerObj) {
            g_set_active(bannerObj, false);
        }
    }
}


static DWORD lastHideQuestBannerTime = 0;
void UpdateFunction()
{
    DWORD now = GetTickCount();

    if (now - lastHideQuestBannerTime >= 150)
    {
        lastHideQuestBannerTime = now;
        HideQuestBannerFunction();
    }
}


// 隐藏UID的函数
void HideUIDFunction()
{

    if (!g_find_string || !g_find_game_object || !g_set_active)
    {
        return;
    }

    if (g_config.hide_uid)
    {
        // 隐藏UID逻辑
        const char* uid_path = "/BetaWatermarkCanvas(Clone)/Panel/TxtUID";
        Il2CppString* str_obj = g_find_string(uid_path);
        if (str_obj) {
            void* uid_obj = g_find_game_object(str_obj);
            if (uid_obj) {
                g_set_active(uid_obj, false);
            }
        }
    }
    else
    {
        // 显示UID逻辑
        const char* uid_path = "/BetaWatermarkCanvas(Clone)/Panel/TxtUID";
        Il2CppString* str_obj = g_find_string(uid_path);
        if (str_obj) {
            void* uid_obj = g_find_game_object(str_obj);
            if (uid_obj) {
                g_set_active(uid_obj, true);
            }
        }
    }

}




//// 隐藏UID的函数
//void HideUIDFunction()
//{
//    if (!g_config.hide_uid || !g_find_string || !g_find_game_object || !g_set_active) 
//    {
//        return;
//    }
//
//    // 隐藏UID逻辑
//    const char* uid_path = "/BetaWatermarkCanvas(Clone)/Panel/TxtUID";
//    Il2CppString* str_obj = g_find_string(uid_path);
//    if (str_obj) {
//        void* uid_obj = g_find_game_object(str_obj);
//        if (uid_obj) {
//            g_set_active(uid_obj, false);
//        }
//    }
//}

// Hook函数
static void __stdcall Hook_SetupQuestBanner(void* p_this) {
    // 调用原始函数
    if (g_original_setup_quest_banner) 
    {
        g_original_setup_quest_banner(p_this);
    }

    // 在原始函数调用后隐藏UID
    HideUIDFunction();
	// 隐藏任务横幅
    HideQuestBannerFunction();
}





// 合成台重定向相关
//std::atomic<bool> g_bRequestOpenCraft = false;
typedef bool(__fastcall* CraftEntryPartner_t)(Il2CppString*, void*, void*, void*, void*);
typedef void(__fastcall* CraftEntry_t)(void*);
// 260128保留F12状态跟踪
static bool bF12Pressed = false;

bool OpenCraftMenu() 
{
    if (!g_find_string || !g_pCraftEntryPartnerFunc) 
    {
        //OutputDebugStringA("[F12Craft] ERROR: Functions not initialized!");
        return false;
    }

    // 使用SEH保护（类似于Rust的try_seh）
    __try {
        // 转换为函数指针
        FindString_t pFindString = (FindString_t)g_find_string;
        CraftEntryPartner_t pCraftEntryPartner = (CraftEntryPartner_t)g_pCraftEntryPartnerFunc;

        //OutputDebugStringA("[F12Craft] Calling FindString...");

        // 调用FindString获取"SynthesisPage"的字符串对象
        Il2CppString* strObj = pFindString("SynthesisPage");
        if (!strObj) {
            //OutputDebugStringA("[F12Craft] ERROR: FindString returned null!");
            return false;
        }

        //OutputDebugStringA("[F12Craft] Calling CraftEntryPartner...");

        // 调用CraftEntryPartner打开合成台
        bool success = pCraftEntryPartner(strObj, nullptr, nullptr, nullptr, nullptr);

        if (success) {
            //OutputDebugStringA("[F12Craft] Craft menu opened successfully!");
        }
        else {
            //OutputDebugStringA("[F12Craft] CraftEntryPartner returned false!");
        }

        return success;
    }
    __except (EXCEPTION_EXECUTE_HANDLER) {
        DWORD code = GetExceptionCode();
        //OutputDebugStringA("[F12Craft] EXCEPTION in OpenCraftMenu!");
        char buffer[256];
        sprintf_s(buffer, "[F12Craft] Exception code: 0x%X", code);
        //OutputDebugStringA(buffer);
        return false;
    }
}



// 钩子函数 - 拦截原始的CraftEntry调用
static CraftEntry_t original_CraftEntry = nullptr;
void __fastcall Hook_CraftEntry(void* pThis)
{

    if (g_config.enable_craft_redirection == true) 
    {
        if (OpenCraftMenu())
        {
            return; // 成功打开了合成台，跳过原始调用
        }
    }

    // 否则调用原始函数
    if (original_CraftEntry) 
    {
        original_CraftEntry(pThis);
    }
}



//// 计时器相关
//static std::chrono::steady_clock::time_point g_last_uid_hide_time;
//static constexpr std::chrono::seconds HIDE_UID_INTERVAL{ 10 };

// 260201
static std::chrono::steady_clock::time_point first_uid_hide_time;
static constexpr std::chrono::seconds HIDE_UID_INTERVAL_TIME{ 3 };
static bool first_uid_hide_time_initialized = false;  // 新增：标记是否已初始化

// GameUpdate Hook
typedef int(*GameUpdate_t)(__int64 a1, const char* a2);
GameUpdate_t g_original_GameUpdate = nullptr;

__int64 HookGameUpdate(__int64 a1, const char* a2)
{
    // 调用原始函数
    auto result = g_original_GameUpdate(a1, a2);


    //// 检查是否需要隐藏UID
    //if (g_config.hide_uid) {
    //    auto now = std::chrono::steady_clock::now();
    //    auto elapsed = std::chrono::duration_cast<std::chrono::seconds>(now - g_last_uid_hide_time);

    //    // 每10秒执行一次
    //    if (elapsed >= HIDE_UID_INTERVAL) {
    //        HideUIDFunction();
    //        g_last_uid_hide_time = now;
    //    }
    //}

    // 静态变量记录上一次的hide_uid状态 260201
    static bool last_hide_uid_state = !g_config.hide_uid; // 初始化为相反值，确保第一次会执行
    static bool first_hide_uid_state = false;


    // 初始化计时器（在游戏初始化后）
    if (!first_uid_hide_time_initialized && GameInited == true)
    {
        first_uid_hide_time = std::chrono::steady_clock::now();
        first_uid_hide_time_initialized = true;
        //OutputDebugStringA("UID hide timer started!");
    }

    // 首次隐藏UID逻辑（10秒后执行）
    if (!first_hide_uid_state && first_uid_hide_time_initialized)
    {
        auto now = std::chrono::steady_clock::now();
        auto elapsed = std::chrono::duration_cast<std::chrono::seconds>(now - first_uid_hide_time);

        if (elapsed >= HIDE_UID_INTERVAL_TIME)
        {
            //OutputDebugStringA("hide OR display UID first!!!");
            first_hide_uid_state = true;
            HideUIDFunction();
        }
    }


    // 检查状态是否变化
    if (g_config.hide_uid != last_hide_uid_state)
    {
        HideUIDFunction();

        //OutputDebugStringA("hide OR display UID!!!");

        // 更新记录的状态
        last_hide_uid_state = g_config.hide_uid;
    }



	//// 合成台重定向按键检测
 //   if (g_config.enable_craft_redirection_key == true && g_config.enable_craft_redirection == true && GameInited == true)
 //   {
 //       bool bCurrentF12State = (GetAsyncKeyState(VK_F12) & 0x8000) != 0;
 //       // 检测按键按下（边缘触发）
 //       if (bCurrentF12State && !bF12Pressed)
 //       {
 //           OutputDebugStringA("[F12Craft] F12 pressed in FOV hook");

 //           // 尝试打开合成菜单
 //           if (g_find_string && g_pCraftEntryPartnerFunc)
 //           {
 //               OpenCraftMenu();
 //           }
 //           else
 //           {
 //               OutputDebugStringA("[F12Craft] Functions not initialized yet!");
 //           }
 //       }
 //       // 更新按键状态
 //       bF12Pressed = bCurrentF12State;
 //   }



	// 防止按键触发过快的合成台打开的版本 260201
    static ULONGLONG ullLastCraftOpenTime = 0;  // 上次打开合成台的时间
    static const ULONGLONG CRAFT_COOLDOWN_MS = 1500;  // 冷却时间：1.5秒

    // 合成台重定向按键检测
    if (g_config.enable_craft_redirection_key == true && g_config.enable_craft_redirection == true && GameInited == true)
    {
        bool bCurrentF12State = (GetAsyncKeyState(VK_F12) & 0x8000) != 0;
        // 检测按键按下（边缘触发）
        if (bCurrentF12State && !bF12Pressed)
        {
            //OutputDebugStringA("[F12Craft] F12 pressed in FOV hook");

            // 获取当前时间（64位，不会回绕）
            ULONGLONG ullCurrentTime = GetTickCount64();

            // 检查是否已过冷却时间
            if (ullCurrentTime - ullLastCraftOpenTime >= CRAFT_COOLDOWN_MS)
            {
                // 尝试打开合成菜单
                if (g_find_string && g_pCraftEntryPartnerFunc)
                {
                    OpenCraftMenu();
                    ullLastCraftOpenTime = ullCurrentTime;  // 更新最后打开时间
                    //OutputDebugStringA("[F12Craft] Craft menu opened");
                }
                else
                {
                    //OutputDebugStringA("[F12Craft] Functions not initialized yet!");
                }
            }
            else
            {
                ULONGLONG ullRemainingTime = CRAFT_COOLDOWN_MS - (ullCurrentTime - ullLastCraftOpenTime);
                char szDebugMsg[128];
                sprintf_s(szDebugMsg, "[F12Craft] Cooldown active, wait %.1f seconds", ullRemainingTime / 1000.0f);
                //OutputDebugStringA(szDebugMsg);
            }
        }
        // 更新按键状态
        bF12Pressed = bCurrentF12State;
    }

	// 调用更新函数，处理任务横幅隐藏等逻辑
	UpdateFunction();

    return result;
}


bool InstallGameUpdateHook()
{
    void* GameUpdateAddr = (void*)PatternScanner::ScanMain("E8 ? ? ? ? 48 8D 4C 24 ? 8B F8 FF 15 ? ? ? ? E8 ? ? ? ?");
    GameUpdateAddr = (void*)PatternScanner::ResolveRelativeAddress((uintptr_t)GameUpdateAddr);

    if (!GameUpdateAddr) {
        //MessageBoxA(nullptr, "HookGameUpdate search failed!", "Error", MB_OK | MB_ICONERROR);
        return false;
    }

    if (!MinHookManager::Add(GameUpdateAddr, (void*)&HookGameUpdate, (void**)&g_original_GameUpdate)) {
        //MessageBoxA(nullptr, "HookGameUpdate install hook failed!", "Error", MB_OK | MB_ICONERROR);
        return false;
    }

    return true;
}


// 初始化线程
DWORD WINAPI InitializeThread(LPVOID lpParam)
{
    while(!ConfigLoaded)
    {
        Sleep(1000);
	}

    // 检查是否需要移除草地
    while (true) 
    {
        if (NeedRemoveGrass != -1) 
        {
            break;
        }
        //OutputDebugStringA("[DLL] checking");
        Sleep(500);
    }

    Sleep(100);

    //// 初始化计时器
    //g_last_uid_hide_time = std::chrono::steady_clock::now();

	// 启动配置读取线程
    CreateThread(nullptr, 0, ReadConfigThread, nullptr, 0, nullptr);

    // 错误收集容器 260201
    std::vector<std::string> errors;

    //// 扫描 SetupQuestBanner
    //uintptr_t setup_quest_banner_addr = PatternScanner::ScanMain("41 57 41 56 56 57 55 53 48 81 EC ? ? ? ? 0F 29 BC 24 ? ? ? ? 0F 29 B4 24 ? ? ? ? 48 89 CE 80 3D ? ? ? ? 00 0F 85 ? ? ? ? 48 8B 96");
    //if (!setup_quest_banner_addr)
    //{
    //    //MessageBoxA(nullptr, "failed!", "SetupQuestBanner", MB_OK | MB_ICONERROR);
    //    //return 1;

    //    errors.push_back("scan SetupQuestBanner failed!");
    //}


    // 扫描 FindString
    uintptr_t find_string_addr = PatternScanner::ScanMain("56 48 83 ec 20 48 89 ce e8 ? ? ? ? 48 89 f1 89 c2 48 83 c4 20 5e e9 ? ? ? ? cc cc cc cc");   
    if (!find_string_addr)
    {
        //MessageBoxA(nullptr, "failed!", "FindString", MB_OK | MB_ICONERROR);
        //return 1;

        errors.push_back("scan FindString failed!");
    }
    else
    {
        g_find_string = (FindString_t)find_string_addr;
    }



    // 扫描 FindGameObject (需要解析跳转)
    uintptr_t find_game_object_addr = PatternScanner::ScanMain("E9 ? ? ? ? 66 66 2E 0F 1F 84 00 ? ? ? ? E9 ? ? ? ? 66 66 2E 0F 1F 84 00 ? ? ? ? E9 ? ? ? ? 66 66 2E 0F 1F 84 00 ? ? ? ? 48 83 EC ? C7 44 24 ? 00 00 00 00 48 8D 54 24");
    // fufu offset 260131
    //uintptr_t find_game_object_addr = PatternScanner::ScanMain("40 53 48 83 EC ? 48 89 4C 24 ? 48 8D 54 24 ? 48 8D 4C 24 ? E8 ? ? ? ? 48 8B 08 48 85 C9 75 ? 48 8D 48 ? E8 ? ? ? ? 48 8B 4C 24 ? 48 8B D8 48 85 C9 74 ? 48 83 7C 24 ? 00 76");
    if (!find_game_object_addr)
    {
        //MessageBoxA(nullptr, "failed!", "FindGameObject", MB_OK | MB_ICONERROR);
        //return 1;

        errors.push_back("scan FindGameObj failed!");
    }


    // 扫描 SetActive (需要解析跳转)
    uintptr_t set_active_addr = PatternScanner::ScanMain("E9 ? ? ? ? 66 66 2E 0F 1F 84 00 ? ? ? ? E9 ? ? ? ? 66 66 2E 0F 1F 84 00 ? ? ? ? E9 ? ? ? ? 66 66 2E 0F 1F 84 00 ? ? ? ? E9 ? ? ? ? 66 66 2E 0F 1F 84 00 ? ? ? ? E9 ? ? ? ? 66 66 2E 0F 1F 84 00 ? ? ? ? E9 ? ? ? ? 66 66 2E 0F 1F 84 00 ? ? ? ? E9 ? ? ? ? 66 66 2E 0F 1F 84 00 ? ? ? ? E9 ? ? ? ? 66 66 2E 0F 1F 84 00 ? ? ? ? E9 ? ? ? ? 66 66 2E 0F 1F 84 00 ? ? ? ? E9 ? ? ? ? 66 66 2E 0F 1F 84 00 ? ? ? ? E9 ? ? ? ? 66 66 2E 0F 1F 84 00 ? ? ? ? E9 ? ? ? ? 66 66 2E 0F 1F 84 00 ? ? ? ? E9 ? ? ? ? 66 66 2E 0F 1F 84 00 ? ? ? ? E9 ? ? ? ? 66 66 2E 0F 1F 84 00 ? ? ? ? E9 ? ? ? ? 66 66 2E 0F 1F 84 00 ? ? ? ? E9 ? ? ? ? 66 66 2E 0F 1F 84 00 ? ? ? ? E9 ? ? ? ? 66 66 2E 0F 1F 84 00 ? ? ? ? E9 ? ? ? ? 66 66 2E 0F 1F 84 00 ? ? ? ? 45 31 C9");
    // fufu offset 260131
    //uintptr_t set_active_addr = PatternScanner::ScanMain("E8 ? ? ? ? 48 8B 56 ? 48 85 D2 0F 84 ? ? ? ? 80 3D ? ? ? ? 0 0F 85 ? ? ? ? 48 89 D1 E8 ? ? ? ? 48 85 C0 0F 84 ? ? ? ? 48 89 C1");
    if (!set_active_addr)
    {
        //MessageBoxA(nullptr, "failed!", "SetActive", MB_OK | MB_ICONERROR);
        //return 1;

        errors.push_back("scan SetActive failed!");
    }


	// 扫描 damage text ShowOneDamageTextEx
    std::string pattern_dam = "41 57 41 56 41 55 41 54 56 57 55 53 48 81 EC D8 01 00 00 44 0F 29 AC 24 C0 01 00 00 44 0F 29 A4 24 B0 01 00 00";
    std::string pattern_dam2 = "41 57 41 56 41 55 41 54 56 57 55 53 48 81 EC ? ? ? ? 44 0F 29 9C 24 ? ? ? ? 44 0F 29 94 24 ? ? ? ? 44 0F 29 8C 24 ? ? ? ? 44 0F 29 84 24 ? ? ? ? 0F 29 BC 24 ? ? ? ? 0F 29 B4 24 ? ? ? ? 44 89 CF 45 89 C4";
    std::string pattern_dam3 = "41 57 41 56 41 55 41 54 56 57 55 53 48 81 EC ? ? ? ? 44 0F 29 9C 24 ? ? ? ? 44 0F 29 94 24 ? ? ? ? 44 0F 29 8C 24 ? ? ? ? 44 0F 29 84 24 ? ? ? ? 0F 29 BC 24 ? ? ? ? 0F 29 B4 24 ? ? ? ? 44 89 CF";
    uintptr_t targetAddrdam = PatternScanner::ScanMain(pattern_dam);
    if (!targetAddrdam) {
        targetAddrdam = PatternScanner::ScanMain(pattern_dam2);
    }
    if (!targetAddrdam) {
        targetAddrdam = PatternScanner::ScanMain(pattern_dam3);
    }
    if (!targetAddrdam)
    {
        errors.push_back("scan HideDamageText failed!");
    }
    if (targetAddrdam == 0) 
    {
        //MessageBoxA(nullptr, "failed!", "HideDamageText", MB_ICONERROR);
        //return 1;

        errors.push_back("scan HideDamageText failed!");
    }


    // 扫描 EventCameraMove 函数地址
    //std::string cam_pattern = "41 57 41 56 56 57 55 53 48 83 EC ? 48 89 D7 49 89 CE 80 3D ? ? ? ? 00 0F 85 ? ? ? ? 80 3D ? ? ? ? 00";
    // Ver6.6fix 260520
    std::string cam_pattern1 = "56 57 53 48 83 EC 40 48 89 D7 48 89 CB 80 3D ? ? ? ? 00 0F 85 ? ? ? ? 80 3D ? ? ? ? 00";
    // Ver<=6.5 old 250520 
    std::string cam_pattern2 =  "41 57 41 56 56 57 55 53 48 83 EC ? 48 89 D7 49 89 CE 80 3D ? ? ? ? 00 0F 85 ? ? ? ? 80 3D ? ? ? ? 00";
    uintptr_t eventCameraMoveAddr = 0;

    // 先扫描 pattern1
    std::vector<uintptr_t> addresses = PatternScanner::MultipleScan(cam_pattern1);

    // 如果没找到，再扫描 pattern2
    if (addresses.empty())
    {
        addresses = PatternScanner::MultipleScan(cam_pattern2);
    }

    // 取结果
    if (!addresses.empty())
    {
        eventCameraMoveAddr = addresses[0];
    }

    if (!eventCameraMoveAddr)
    {
        errors.push_back("scan EventCameraMove failed!");
    }


    // 扫描 EventCameraMove 函数地址
    // Ver<=6.5 old 250520 
    //std::string cam_pattern = "41 57 41 56 56 57 55 53 48 83 EC ? 48 89 D7 49 89 CE 80 3D ? ? ? ? 00 0F 85 ? ? ? ? 80 3D ? ? ? ? 00";
    //uintptr_t eventCameraMoveAddr = PatternScanner::ScanMain(cam_pattern);
    //if (!eventCameraMoveAddr) 
    //{
    //    //MessageBoxA(nullptr, "failed!", "EventCameraMove", MB_OK | MB_ICONERROR);
    //    //return 1;
    //    errors.push_back("scan EventCameraMove failed!");
    //}




    // =============================================================================================
	// 队伍相关函数扫描
    // =============================================================================================
    uintptr_t checkCanEnterAddr = PatternScanner::ScanMain("56 48 81 ec 80 00 00 00 80 3d ? ? ? ? 00 0f 84 ? ? ? ? 80 3d ? ? ? ? 00");
    if (!checkCanEnterAddr) 
    {
        //MessageBoxA(nullptr, "failed!", "CheckCanEnter", MB_OK | MB_ICONERROR);
        //return 1;

        errors.push_back("scan CheckCanEnter failed!");
    }
    else
    {
        g_pCheckCanEnter = (CheckCanEnter_t)checkCanEnterAddr;
    }


	// 扫描打开队伍页面函数地址
    uintptr_t openTeamPageAddr = PatternScanner::ScanMain("56 57 53 48 83 ec 20 89 cb 80 3d ? ? ? ? 00 74 7a 80 3d ? ? ? ? 00 48 8b 05");
    if (!openTeamPageAddr) 
    {
        //MessageBoxA(nullptr, "failed!", "openTeamPage", MB_OK | MB_ICONERROR);
        //return 1;

        errors.push_back("scan openTeamPage failed!");
    }
    else 
    {
        g_pOpenTeamPageAccordingly = (OpenTeamPageAccordingly_t)openTeamPageAddr;
    }


	// 扫描打开队伍函数地址
    uintptr_t openTeamAddr = PatternScanner::ScanMain("48 83 EC ? 80 3D ? ? ? ? 00 75 ? 48 8B 0D ? ? ? ? 80 B9 ? ? ? ? 00 0F 84 ? ? ? ? B9 ? ? ? ? E8 ? ? ? ? 84 C0 75");
    if (!openTeamAddr)
    {
        //MessageBoxA(nullptr, "failed!", "openTeam", MB_OK | MB_ICONERROR);
        //return 1;

        errors.push_back("scan openTeam failed!");
    }
    // =============================================================================================


	// 合成台相关函数扫描
    g_pCraftEntryPartnerFunc = PatternScanner::ScanMain("41 57 41 56 41 55 41 54 56 57 55 53 48 81 EC ? ? ? ? 4D 89 ? 4C 89 C6 49 89 D4 49 89 CE");
    if (!g_pCraftEntryPartnerFunc) 
    {
        //MessageBoxA(nullptr, "failed!", "CraftEntryPartner", MB_OK | MB_ICONERROR);
        //return 1;

        errors.push_back("scan CraftEntryPartner failed!");
    }

    g_pCraftEntryFunc = PatternScanner::ScanMain("41 56 56 57 53 48 83 EC 58 49 89 CE 80 3D ? ? ? ? 00 0F 84 ? ? ? ? 80 3D ? ? ? ? 00 48 8B 0D ? ? ? ? 0F 85");
    if (!g_pCraftEntryFunc) 
    {
        //MessageBoxA(nullptr, "failed!", "CraftEntry", MB_OK | MB_ICONERROR);
        //return 1;

        errors.push_back("scan CraftEntry failed!");
    }


    // 草地去除相关函数扫描
    if (NeedRemoveGrass == 2)
    {
        uintptr_t get_grass_addr = PatternScanner::ScanMain("40 53 48 81 EC ?? ?? ?? ?? 48 8B D9 48 85 C9 0F 84 ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 85 C0 0F 84 ?? ?? ?? ?? 48 8B 10 48 8B C8 FF 52 ?? 48 85 C0 0F 85 ?? ?? ?? ?? 48 8B CB E8 ?? ?? ?? ??");
        if (!get_grass_addr)
        {
            errors.push_back("scan RemoveGrass failed!");
        }
        else
        {
            // 【关键修复】将扫描到的地址赋值给全局指针 g_grass_name
            g_grass_name = (GetGrass_t)get_grass_addr;
        }
    }



    if (find_game_object_addr)
    {
        // 解析相对跳转
        g_find_game_object = (FindGameObject_t)PatternScanner::ResolveRelativeAddress(find_game_object_addr, 1, 5);
        if (!g_find_game_object)
        {
            //MessageBoxA(nullptr, "failed!", "ResolveRelative", MB_OK | MB_ICONERROR);
            //return 1;

			errors.push_back("ResolveRelative FindGameObject failed!");
        }

		//fufu 260131 直接赋值
        //g_find_game_object = reinterpret_cast<FindGameObject_t>(find_game_object_addr);
    }
    if (set_active_addr)
    {
        // 解析相对跳转
        g_set_active = (SetActive_t)PatternScanner::ResolveRelativeAddress(set_active_addr, 1, 5);
        if (!g_set_active)
        {
            //MessageBoxA(nullptr, "failed!", "ResolveRelative", MB_OK | MB_ICONERROR);
            //return 1;

			errors.push_back("ResolveRelative SetActive failed!");
        }
    }




  //  if (!MinHookManager::Add((void*)setup_quest_banner_addr, (void*)Hook_SetupQuestBanner, (void**)&g_original_setup_quest_banner))
  //  {
  //      //MessageBoxA(nullptr, "Failed to install hook!", "SetupQuestBanner", MB_OK | MB_ICONERROR);
  //      //return 1;

		//errors.push_back("hook SetupQuestBanner failed!");
  //  }


    bool successdam = MinHookManager::Add(reinterpret_cast<void*>(targetAddrdam),reinterpret_cast<void*>(&hook_ShowOneDamageTextEx),reinterpret_cast<void**>(&original_ShowOneDamageTextEx));
    if (!successdam) 
    {
        //MessageBoxA(nullptr, "Failed to install hook!", "HideDamageText", MB_ICONERROR);
		//return 1;

		errors.push_back("hook HideDamageText failed!");
    }


    if (!MinHookManager::Add((void*)eventCameraMoveAddr,(void*)hook_EventCameraMove,(void**)&original_EventCameraMove)) 
    {
        //MessageBoxA(nullptr, "Failed to install hook!", "EventCameraMove", MB_ICONERROR);
        //return 1;

		errors.push_back("hook EventCameraMove failed!");
    }


    bool successopenteam = MinHookManager::Add((void*)openTeamAddr, (void*)&Hook_OpenTeam, (void**)&g_original_OpenTeam);
    if (!successopenteam)
    {
        //MessageBoxA(nullptr, "Failed to install hook!", "OpenTeam", MB_ICONERROR);
        //return 1;

		errors.push_back("hook OpenTeam failed!");
    }


    if (!MinHookManager::Add((void*)g_pCraftEntryFunc,(void*)&Hook_CraftEntry,(void**)&original_CraftEntry))
    {
        //MessageBoxA(nullptr, "Failed to install hook!", "CraftEntry", MB_ICONERROR);
        //return 1;

		errors.push_back("hook CraftEntry failed!");
    }


    // 安装去除草地钩子 260225
    if (NeedRemoveGrass == 2) 
    {
        if (g_set_active && g_grass_name)
        {
            SetActive_t pOriginalSetActive = g_set_active; // 保存原始地址

            if (!MinHookManager::Add((void*)pOriginalSetActive, (void*)&Hook_Grass, (void**)&g_set_active))
            {
                errors.push_back("hook RemoveGrass failed!");
            }
        }
        else
        {
            errors.push_back("hook RemoveGrass skipped: RemoveGrass address is null");
        }
    }


    // 安装GameUpdate Hook
    if (!InstallGameUpdateHook()) {
        //MessageBoxA(nullptr, "Failed to install gameupdate hook!", "Error", MB_ICONERROR);
        //return 1;

		errors.push_back("hook GameUpdate failed!");
    }


    // 检查是否有错误
    if (!errors.empty())
    {
        // 构建错误消息
        std::string errorMsg = "DLL Errors：\n\n";
        for (size_t i = 0; i < errors.size(); ++i)
        {
            errorMsg += std::to_string(i + 1) + ". " + errors[i] + "\n";
        }

        errorMsg += "\nTotal Errors: " + std::to_string(errors.size()) + " mt";

        MessageBoxA(nullptr, errorMsg.c_str(), "An error occurred", MB_OK | MB_ICONERROR);
        return 1;
    }


    //MessageBoxA(nullptr, "HideUID DLL initialized successfully!", "HideUID", MB_ICONINFORMATION);
	OutputDebugStringA("[AdvanGenshinTols] DLL initialized successfully!");


    return 0;
}


// DLL入口点
BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved) {
    switch (ul_reason_for_call) {
    case DLL_PROCESS_ATTACH: {
        // 禁用线程调用以减少开销
        DisableThreadLibraryCalls(hModule);

        // 检查是否是目标进程
        HMODULE hYuanShen = GetModuleHandleA("YuanShen.exe");
        HMODULE hGenshinImpact = GetModuleHandleA("GenshinImpact.exe");

        // 如果都不是目标进程，直接返回TRUE（DLL加载成功但不初始化）
        if (hYuanShen == NULL && hGenshinImpact == NULL) {
            return TRUE;
        }

        // 创建初始化线程
        CreateThread(nullptr, 0, InitializeThread, nullptr, 0, nullptr);
        break;
    }
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}


extern "C" __declspec(dllexport) int UpdateConfig(char* IN_config) {
    config::LoadConfig(std::string(IN_config));
    ConfigLoaded = true;
    return 0;
}

extern "C" __declspec(dllexport) int CheckGameState(bool state) 
{
	GameInited = state;
	return 0;
}

extern "C" __declspec(dllexport) int UpdateConfigNoHotLoad(int statenohot)
{
    NeedRemoveGrass = statenohot;
    return 0;
}