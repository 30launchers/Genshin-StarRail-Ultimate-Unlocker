#include <array>
#include <vector>
#include <memory>
#include <unordered_map>
#include <atomic>

//防止windows.h定义min和max宏，影响std::min/std::max
#define NOMINMAX 
#include <windows.h>
#include <vector>
#include <cstdint>
#include <Psapi.h>
#include <cstdio>
#include "PatternScanner.hpp"
#include "MinHookManager.h" // 包含 MinHook 管理类
#include "HookUtility.h"

#pragma comment(lib, "Psapi.lib")


int _gameSelection = 0; //1=StarRail 2=Genshin

// 加密后的字符串数据
namespace encrypted_strings {
    //Genshin FOV Adjustment MEM_code
    //constexpr auto fov_code = XorString::encrypt("40 53 48 83 EC 60 0F 29 74 24 ? 48 8B D9 0F 28 F1 E8 ? ? ? ? 48 85 C0 0F 84 ? ? ? ? E8 ? ? ? ? 48 8B C8");
    //Force FPS MEM_code
    //constexpr auto force_fps_code = XorString::encrypt("E8 ? ? ? ? 85 C0 7E 0E E8 ? ? ? ? 0F 57 C0 F3 0F 2A C0 EB 08 ?");
    //Map szGuid 
    constexpr auto map_guid = XorString::encrypt("3FDA00E1-3F3D-A33B-706C-3A11FEBFA687");
    //RDE szGuid
    constexpr auto ReadyEvent_guid = XorString::encrypt("89597B6A-FA92-21BD-1E7D-07AAAF1DCDE8");
    //CSM szGuid
    constexpr auto ConsumedEvent_guid = XorString::encrypt("C1B99146-EB12-08F4-A972-561CAFE52C78");
    //StarRail FOV Adjustment MEM_code
    constexpr auto sr_fov_code = XorString::encrypt("83 EC 28 48 85 C9 74 15 48 8B 41 10 48 85 C0 74 0C 48 8B C8 48 83 C4 28 E9 ?? ?? ?? FF 48 8B D1 48 8D 4C 24 30 E8 ?? ?? ?? FF 48 8D 4C 24 40 48 8B 10 E8 ?? ?? ?? FF 48 8B 08 E8 ?? ?? ?? FF CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC");

    // 251231 DisableDisplayGenshinFog MEM_code
    constexpr auto display_genshin_fog_code = XorString::encrypt("0F B6 02 88 01 8B 42 04 89 41 04 F3 0F 10 52 ? F3 0F 10 4A ? F3 0F 10 42 ? 8B 42 08");
}

// 1. 定义枚举，确保与 C# 的值匹配
enum class IPCStatus : int
{
    Idle = 0,
    Running = 1,
    Error = 2
};

// 2. 定义结构体，并使用 pack 指令确保1字节对齐
#pragma pack(push, 1)
struct SharedData {
    char currentstring[256]; // 必须是固定大小的字符数组
    IPCStatus Status;
    int SRFovValue;
	int SRFovChangeEnabled;
	int SRFovChageFix;
	int SRFovHookSpeed;
	int SRFovHookDepth;
	int SRFovHookRunTime;
	// 260202 DisableDisplayGenshinFog
    int GenshinFog;
};
#pragma pack(pop)

// 线程参数结构
struct GameThreadData {
    SharedData* pSharedDataTr;
    HANDLE hMapFileTr;
    int SRFovValueTr;
	int SRFovChangeEnabledTr;
	int SRFovChageFixTr;
	int SRFovHookSpeedTr;
	int SRFovHookDepthTr;
    int SRFovHookRunTimeTr;
};

BOOL __declspec(noinline) OnWinError(const char* szFunction, DWORD dwError)
{
    char szMessage[256];
    wsprintfA(szMessage, "%s failed with error %d", szFunction, dwError);
    MessageBoxA(nullptr, szMessage, "Error", MB_ICONERROR);

    //if (pIPCData)
    //    pIPCData->Status = IPCStatus::Error;

    return FALSE;
}

// 枚举窗口的回调数据
struct EnumWindowsData {
    DWORD processId;
    HWND foundWindow;
};

// 枚举回调函数
BOOL CALLBACK EnumWindowsProc(HWND hwnd, LPARAM lParam) {
    EnumWindowsData* data = (EnumWindowsData*)lParam;

    DWORD pid = 0;
    GetWindowThreadProcessId(hwnd, &pid);

    if (pid == data->processId && IsWindowVisible(hwnd)) {
        if (GetWindowTextLengthA(hwnd) > 0) {
            char className[256];
            GetClassNameA(hwnd, className, sizeof(className));

            if (strcmp(className, "UnityWndClass") == 0) {
                data->foundWindow = hwnd;
                return FALSE; // 停止枚举
            }
        }
    }
    return TRUE;
}

// 检查Unity主窗口是否存在（单次检查）
bool IsUnityWindowReady() {
    EnumWindowsData data;
    data.processId = GetCurrentProcessId();
    data.foundWindow = NULL;

    EnumWindows(EnumWindowsProc, (LPARAM)&data);

    return (data.foundWindow != NULL);
}

// 循环等待Unity主窗口出现
bool WaitForUnityWindow(DWORD timeoutMs = 30000) {
    DWORD startTime = GetTickCount();

    while (GetTickCount() - startTime < timeoutMs) {
        if (IsUnityWindowReady()) {
            return true; // 找到了
        }
        Sleep(2);
    }

    return false; // 超时
}

float GlobalSRFovValue = 45.0f;
int GlobalSRFovChangeEnabled = 0;
int GlobalSRFovChageFix = 0;

// StarRail视野(FOV)钩子相关
typedef void(*SetFieldOfView_t)(void* _this, float value);

// 钩子函数指针类型
using HookFunctionPtr = void(*)(void*, float);

// 前置声明
class FOVHookManager;
extern std::vector<uintptr_t> g_qualifiedAddresses;
extern FOVHookManager g_fovHookManager;

// 移除 constexpr，改为可配置的全局变量
extern int g_maxHookCount;

// 全局状态变量
static int g_currentTestInstanceId = 0;
static bool g_foundTarget = false;
static std::mutex g_testMutex;
static std::condition_variable g_testCV;
static bool g_testFinished = false;

// 钩子管理器类定义
class FOVHookManager {
private:
    struct HookInstance {
        uintptr_t address;
        SetFieldOfView_t originalFunc;
        bool isHooked;
        int instanceId;
        void* customData;
        int callCount;

        HookInstance() : address(0), originalFunc(nullptr), isHooked(false),
            instanceId(-1), customData(nullptr), callCount(0) {
        }
    };

    std::vector<HookInstance> m_instances;
    int m_activeInstances;
    int m_maxInstances; // 动态最大实例数
    mutable std::mutex m_mutex;

public:
    FOVHookManager() : m_activeInstances(0), m_maxInstances(0) {
    }

    // 新增：初始化钩子管理器，设置最大实例数
    void Initialize(int maxInstances) {
        std::lock_guard<std::mutex> lock(m_mutex);

        if (m_maxInstances > 0) {
            //DebugPrint("[DLL] WARNING: FOVHookManager already initialized with %d instances\n", m_maxInstances);
            return;
        }

        m_maxInstances = maxInstances;
        m_instances.resize(maxInstances);

        for (int i = 0; i < maxInstances; i++) {
            m_instances[i].instanceId = i;
        }

        //DebugPrint("[DLL] FOVHookManager initialized with %d max instances\n", maxInstances);
    }

    // 新增：重置管理器（用于重新初始化）
    void Reset() {
        std::lock_guard<std::mutex> lock(m_mutex);

        // 卸载所有现有钩子
        for (auto& instance : m_instances) {
            if (instance.isHooked) {
                MinHookManager::Remove(reinterpret_cast<void*>(instance.address));
            }
        }

        m_instances.clear();
        m_activeInstances = 0;
        m_maxInstances = 0;

        //DebugPrint("[DLL] FOVHookManager reset completed\n");
    }

    // 获取最大实例数
    int GetMaxInstanceCount() const {
        std::lock_guard<std::mutex> lock(m_mutex);
        return m_maxInstances;
    }

    // 添加钩子实例
    bool AddInstance(uintptr_t address, int instanceId) {
        std::lock_guard<std::mutex> lock(m_mutex);

        if (m_maxInstances == 0) {
            //DebugPrint("[DLL] ERROR: FOVHookManager not initialized\n");
            return false;
        }

        if (instanceId < 0 || instanceId >= m_maxInstances) {
            //DebugPrint("[DLL] ERROR: Invalid instance ID: %d (max: %d)\n", instanceId, m_maxInstances - 1);
            return false;
        }

        if (m_instances[instanceId].isHooked) {
            //DebugPrint("[DLL] ERROR: Instance %d is already hooked\n", instanceId);
            return false;
        }

        m_instances[instanceId].address = address;
        m_activeInstances++;

        //DebugPrint("[DLL] Added hook instance %d at address: 0x%p\n",instanceId, (void*)address);

        return true;
    }

    // 安装特定实例的钩子
    bool InstallInstanceHook(int instanceId, HookFunctionPtr hookFunc) {
        std::lock_guard<std::mutex> lock(m_mutex);

        if (instanceId < 0 || instanceId >= m_maxInstances) {
            //DebugPrint("[DLL] ERROR: Invalid instance ID for installation: %d\n", instanceId);
            return false;
        }

        auto& instance = m_instances[instanceId];
        if (!instance.address) {
            //DebugPrint("[DLL] ERROR: Instance %d has no valid address\n", instanceId);
            return false;
        }

        if (instance.isHooked) {
            //DebugPrint("[DLL] ERROR: Instance %d is already hooked\n", instanceId);
            return false;
        }

        if (!hookFunc) {
            //DebugPrint("[DLL] ERROR: No hook function for instance %d\n", instanceId);
            return false;
        }

        if (MinHookManager::Add(
            reinterpret_cast<void*>(instance.address),
            reinterpret_cast<void*>(hookFunc),
            reinterpret_cast<void**>(&instance.originalFunc))) {

            instance.isHooked = true;
            //DebugPrint("[DLL] SUCCESS: Hook instance %d installed at 0x%p\n",instanceId, (void*)instance.address);

            return true;
        }

        //DebugPrint("[DLL] ERROR: Failed to install hook for instance %d\n", instanceId);
        return false;
    }

    // 获取实例的原始函数
    SetFieldOfView_t GetOriginalFunction(int instanceId) {
        std::lock_guard<std::mutex> lock(m_mutex);
        if (instanceId < 0 || instanceId >= m_maxInstances) return nullptr;
        return m_instances[instanceId].originalFunc;
    }

    // 调用原始函数
    bool CallOriginalFunction(int instanceId, void* _this, float value) {
        auto originalFunc = GetOriginalFunction(instanceId);
        if (originalFunc) {
            originalFunc(_this, value);
            return true;
        }
        return false;
    }

    // 卸载特定实例
    bool RemoveInstance(int instanceId) {
        std::lock_guard<std::mutex> lock(m_mutex);

        if (instanceId < 0 || instanceId >= m_maxInstances) return false;

        auto& instance = m_instances[instanceId];
        if (instance.isHooked) {
            if (MinHookManager::Remove(reinterpret_cast<void*>(instance.address))) {
                //DebugPrint("[DLL] Removed hook instance %d\n", instanceId);
                instance.isHooked = false;
                m_activeInstances--;
                return true;
            }
            else {
                //DebugPrint("[DLL] Failed to remove hook instance %d\n", instanceId);
                return false;
            }
        }

        instance = HookInstance();
        instance.instanceId = instanceId;
        return true;
    }

    // 设置实例的自定义数据
    void SetCustomData(int instanceId, void* data) {
        std::lock_guard<std::mutex> lock(m_mutex);
        if (instanceId >= 0 && instanceId < m_maxInstances) {
            m_instances[instanceId].customData = data;
        }
    }

    // 获取实例的自定义数据
    void* GetCustomData(int instanceId) {
        std::lock_guard<std::mutex> lock(m_mutex);
        if (instanceId >= 0 && instanceId < m_maxInstances) {
            return m_instances[instanceId].customData;
        }
        return nullptr;
    }

    // 获取实例信息
    HookInstance GetInstanceInfo(int instanceId) {
        std::lock_guard<std::mutex> lock(m_mutex);
        if (instanceId >= 0 && instanceId < m_maxInstances) {
            return m_instances[instanceId];
        }
        return HookInstance();
    }

    // 获取所有活动实例
    std::vector<int> GetActiveInstances() {
        std::lock_guard<std::mutex> lock(m_mutex);
        std::vector<int> active;
        for (int i = 0; i < m_maxInstances; i++) {
            if (m_instances[i].isHooked) {
                active.push_back(i);
            }
        }
        return active;
    }

    // 获取实例的地址信息
    uintptr_t GetInstanceAddress(int instanceId) {
        std::lock_guard<std::mutex> lock(m_mutex);
        if (instanceId >= 0 && instanceId < m_maxInstances) {
            return m_instances[instanceId].address;
        }
        return 0;
    }

    // 记录调用统计信息
    void RecordCall(int instanceId, void* _this, float value) {
        std::lock_guard<std::mutex> lock(m_mutex);
        if (instanceId >= 0 && instanceId < m_maxInstances) {
            auto& instance = m_instances[instanceId];
            instance.callCount++;
        }
    }

    // 打印调用统计报告
    void PrintCallStatistics() {
        std::lock_guard<std::mutex> lock(m_mutex);
        //DebugPrint("[DLL] ===== HOOK CALL STATISTICS REPORT =====\n");
        int activeCount = 0;
        int inactiveCount = 0;

        for (int i = 0; i < m_maxInstances; i++) {
            auto& instance = m_instances[i];
            if (instance.isHooked) {
                if (instance.callCount > 0) {
                    //DebugPrint("[DLL] Instance %d: Addr 0x%p - ACTIVE (Calls: %d)\n",i, (void*)instance.address, instance.callCount);
                    activeCount++;
                }
                else {
                    inactiveCount++;
                }
            }
        }

        //DebugPrint("[DLL] Summary: %d active hooks, %d inactive hooks\n", activeCount, inactiveCount);
        //DebugPrint("[DLL] ===== END OF STATISTICS REPORT =====\n");
    }

    // 清理不活跃的钩子实例
    void CleanupInactiveHooks(const std::vector<int>& keepInstances) {
        std::lock_guard<std::mutex> lock(m_mutex);
        std::vector<int> instancesToRemove;

        //DebugPrint("[DLL] ===== CLEANING UP INACTIVE HOOKS =====\n");
        //DebugPrint("[DLL] Keeping instances: ");
        for (int instanceId : keepInstances) {
            //DebugPrint("%d ", instanceId);
        }
        //DebugPrint("\n");

        //DebugPrint("[DLL] Current instance status before cleanup:\n");
        for (int i = 0; i < m_maxInstances; i++) {
            auto& instance = m_instances[i];
            if (instance.isHooked) {
                //DebugPrint("[DLL]   Instance %d: calls=%d, addr=0x%p\n",i, instance.callCount, (void*)instance.address);
            }
        }

        for (int i = 0; i < m_maxInstances; i++) {
            auto& instance = m_instances[i];

            if (std::find(keepInstances.begin(), keepInstances.end(), i) != keepInstances.end()) {
                //DebugPrint("[DLL] Keeping instance %d (in keep list)\n", i);
                continue;
            }

            if (instance.isHooked && instance.callCount > 0) {
                //DebugPrint("[DLL] Keeping instance %d (has %d calls)\n", i, instance.callCount);
                continue;
            }

            if (instance.isHooked) {
                instancesToRemove.push_back(i);
                //DebugPrint("[DLL] Marking instance %d for removal (0 calls, addr: 0x%p)\n",i, (void*)instance.address);
            }
        }

        for (int instanceId : instancesToRemove) {
            auto& instance = m_instances[instanceId];
            if (MinHookManager::Remove(reinterpret_cast<void*>(instance.address))) {
                instance.isHooked = false;
                m_activeInstances--;
                //DebugPrint("[DLL] SUCCESS: Removed inactive hook instance %d\n", instanceId);
            }
            else {
                //DebugPrint("[DLL] ERROR: Failed to remove hook instance %d\n", instanceId);
            }
        }

        //DebugPrint("[DLL] Cleanup completed. Removed %zu inactive hooks.\n", instancesToRemove.size());
        //DebugPrint("[DLL] Remaining active instances: %d\n", m_activeInstances);
        //DebugPrint("[DLL] ===== END CLEANUP =====\n");
    }

    // 获取当前活跃的实例ID列表
    std::vector<int> GetHookedInstanceIds() {
        std::lock_guard<std::mutex> lock(m_mutex);
        std::vector<int> hooked;
        for (int i = 0; i < m_maxInstances; i++) {
            if (m_instances[i].isHooked) {
                hooked.push_back(i);
            }
        }
        return hooked;
    }

    // 获取调用计数
    int GetCallCount(int instanceId) {
        std::lock_guard<std::mutex> lock(m_mutex);
        if (instanceId >= 0 && instanceId < m_maxInstances) {
            return m_instances[instanceId].callCount;
        }
        return 0;
    }

    int GetActiveInstanceCount() const {
        std::lock_guard<std::mutex> lock(m_mutex);
        return m_activeInstances;
    }
};

// 全局变量定义
std::vector<uintptr_t> g_qualifiedAddresses;
FOVHookManager g_fovHookManager;
int g_maxHookCount = 150; // 默认值，可通过 InstallSRFOVHook 参数修改

bool _foundsrfovtarget = false;
int timesAtThirty = 0;
bool _isfirstfoundsrfov = true;

// 260119全局变量保存模块基址
static HMODULE g_unityPlayerModule = nullptr;

// 动态钩子函数实现
class DynamicHookFunction {
public:
    static void CallHook(int instanceId, void* _this, float value) {
        g_fovHookManager.RecordCall(instanceId, _this, value);

        if (instanceId == g_currentTestInstanceId) {

            float _oldvalue = value;

            if (_oldvalue == 30.0f)
            {
                timesAtThirty++;

                if (timesAtThirty == 1)
                {
                    _isfirstfoundsrfov = false;
                }
                else if (timesAtThirty == 2)
                {
                }
            }
            if (_oldvalue >= 31.0f)
            {
                _isfirstfoundsrfov = true;
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
            if(GlobalSRFovChageFix == 1)
            {
                fovfixestate = false;
			}

            if (_oldvalue == 30.0f || _foundsrfovtarget == true)
            {
                if (!_foundsrfovtarget)
                {
                    //DebugPrint("[DLL] SUCCESS: Found target FOV function at instance %d! Value: %.4f\n", instanceId, _oldvalue);


                    // 获取内存地址 260119
                    uintptr_t instanceAddress = g_fovHookManager.GetInstanceAddress(instanceId);

                    // 计算相对于UnityPlayer.dll的偏移量
                    uintptr_t offset = 0;
                    if (g_unityPlayerModule) {
                        offset = instanceAddress - (uintptr_t)g_unityPlayerModule;
                    }

                    DebugPrint("[DLL] SUCCESS: Found target FOV function at instance %d!\n", instanceId);
                    //DebugPrint("[DLL]   Value: %.4f\n", _oldvalue);
                    //DebugPrint("[DLL]   Address: 0x%p\n", (void*)instanceAddress);
                    //DebugPrint("[DLL]   Module: UnityPlayer.dll (0x%p)\n", g_unityPlayerModule);
                    DebugPrint("[DLL]   Offset: 0x%X\n", (unsigned int)offset);


                    _foundsrfovtarget = true;
                }

                g_foundTarget = true;
                //g_fovHookManager.CallOriginalFunction(instanceId, _this, 100.0f);

                if (GlobalSRFovChangeEnabled == 2 && GlobalSRFovValue != 45.0f)
                {
                    //如果是第一次fov的值为30.0f,则暂时先不启用fov修复
                    if (_isfirstfoundsrfov == true)
                    {
                        //DebugPrint("[DLL] _isfirstfoundsrfov true");
                        if (!fovfixestate)
                        {
                            g_fovHookManager.CallOriginalFunction(instanceId, _this, GlobalSRFovValue);
                        }
                        if (fovfixestate)
                        {
                            g_fovHookManager.CallOriginalFunction(instanceId, _this, value);
                        }
                    }
                    if (_isfirstfoundsrfov == false)
                    {
                        //DebugPrint("[DLL] _isfirstfoundsrfov false");
                        g_fovHookManager.CallOriginalFunction(instanceId, _this, GlobalSRFovValue);
                    }
                }
                else
                {
                    g_fovHookManager.CallOriginalFunction(instanceId, _this, value);
                    //DebugPrint("[DLL] orginal mode");
                }


                //g_fovHookManager.CallOriginalFunction(instanceId, _this, value);
                //DebugPrint("[DLL] SUCCESS: Found target FOV function at instance %d! Value: %.4f\n", instanceId, _oldvalue);

                {
                    std::lock_guard<std::mutex> lock(g_testMutex);
                    g_testFinished = true;
                }
                g_testCV.notify_one();
            }
            else
            {
                g_fovHookManager.CallOriginalFunction(instanceId, _this, value);

                //DebugPrint("[DLL] Instance %d is NOT target (value: %.4f), removing hook immediately\n",instanceId, _oldvalue);

                g_fovHookManager.RemoveInstance(instanceId);

                {
                    std::lock_guard<std::mutex> lock(g_testMutex);
                    g_currentTestInstanceId++;
                    if (g_currentTestInstanceId >= g_qualifiedAddresses.size() ||
                        g_currentTestInstanceId >= g_maxHookCount) {
                        g_testFinished = true;
                    }
                }
                g_testCV.notify_one();
            }
        }
        else {
            g_fovHookManager.CallOriginalFunction(instanceId, _this, value);
        }
    }
};

// 动态生成钩子函数 - 最大支持500个（可根据需要调整）
template<int N>
void HookSetFieldOfView_Template(void* _this, float value) {
    DynamicHookFunction::CallHook(N, _this, value);
}

// 生成钩子函数指针的类
class HookFunctionGenerator {
private:
    static constexpr int MAX_HOOKS = 500; // 编译时最大支持数
    std::array<HookFunctionPtr, MAX_HOOKS> m_hookFunctions;

    template<int... Is>
    constexpr std::array<HookFunctionPtr, MAX_HOOKS> generateArray(std::integer_sequence<int, Is...>) {
        return { &HookSetFieldOfView_Template<Is>... };
    }

public:
    constexpr HookFunctionGenerator() : m_hookFunctions(generateArray(std::make_integer_sequence<int, MAX_HOOKS>{})) {}

    constexpr HookFunctionPtr operator[](size_t index) const {
        return index < MAX_HOOKS ? m_hookFunctions[index] : nullptr;
    }

    constexpr size_t size() const { return MAX_HOOKS; }
};

// 全局钩子函数生成器
static constexpr HookFunctionGenerator g_hookFunctions;

void PrintMemoryDetails(uintptr_t address, const char* label) {
    if (IsBadReadPtr((void*)address, sizeof(double))) {
        //DebugPrint("[DLL] Memory at 0x%p (%s) is not readable, skipping.\n", (void*)address, label);
        return;
    }

    uint8_t  byteVal;
    int16_t  wordVal_signed;
    int32_t  intVal_signed;
    int64_t  int64Val_signed;
    float    floatVal;
    double   doubleVal;

    memcpy(&byteVal, (void*)address, sizeof(byteVal));
    memcpy(&wordVal_signed, (void*)address, sizeof(wordVal_signed));
    memcpy(&intVal_signed, (void*)address, sizeof(intVal_signed));
    memcpy(&int64Val_signed, (void*)address, sizeof(int64Val_signed));
    memcpy(&floatVal, (void*)address, sizeof(floatVal));
    memcpy(&doubleVal, (void*)address, sizeof(doubleVal));

    char buffer[1024];

    //sprintf_s(buffer, 1024,
    //    "[DLL] Mem @ 0x%p (%s) | Byte: %u | Word: %d (0x%04X) | Int: %d (0x%08X) | Int64: %lld | Float: %.4f | Double: %e\n",
    //    (void*)address,
    //    label,
    //    (unsigned int)byteVal,
    //    wordVal_signed,
    //    (unsigned short)wordVal_signed,
    //    intVal_signed,
    //    (unsigned int)intVal_signed,
    //    int64Val_signed,
    //    floatVal,
    //    doubleVal
    //);

    //DebugPrint(buffer);
}

void CheckForZeroFloatBeforeAddress(uintptr_t startAddress, const char* label, size_t maxScanBytes) {
    //DebugPrint("[DLL] --- Starting scan for zero-float before 0x%p (%s) ---\n", (void*)startAddress, label);

    const float EPSILON = 1e-6f;

    uintptr_t currentScanAddress = startAddress - 1;
    uintptr_t scanBoundary = startAddress - maxScanBytes;

    if (currentScanAddress < scanBoundary) {
        //DebugPrint("[DLL] Scan boundary is invalid, skipping.\n");
        return;
    }

    while (currentScanAddress >= scanBoundary) {
        if (IsBadReadPtr((void*)currentScanAddress, sizeof(double))) {
            //DebugPrint("[DLL] Address 0x%p became unreadable during scan, stopping.\n", (void*)currentScanAddress);
            break;
        }

        uint8_t currentByte = *(uint8_t*)currentScanAddress;
        if (currentByte == 0xFF) {
            //DebugPrint("[DLL] Scan stopped at 0x%p due to terminator 0xFF.\n", (void*)currentScanAddress);
            break;
        }

        float potentialFloat = *(float*)currentScanAddress;

        if (fabsf(potentialFloat) < EPSILON) {
            int16_t  wordVal_signed;
            int32_t  intVal_signed;
            int64_t  int64Val_signed;

            memcpy(&wordVal_signed, (void*)currentScanAddress, sizeof(wordVal_signed));
            memcpy(&intVal_signed, (void*)currentScanAddress, sizeof(intVal_signed));
            memcpy(&int64Val_signed, (void*)currentScanAddress, sizeof(int64Val_signed));

            bool word_is_negative = (wordVal_signed < 0);
            bool int_is_positive = (intVal_signed > 0);
            bool int64_is_positive = (int64Val_signed > 0);

            if (word_is_negative && int_is_positive && int64_is_positive) {
                g_qualifiedAddresses.push_back(currentScanAddress);

                char zeroFloatLabel[128];
                sprintf_s(zeroFloatLabel, sizeof(zeroFloatLabel), "%s -> QUALIFIED ZERO FLOAT", label);
                PrintMemoryDetails(currentScanAddress, zeroFloatLabel);
            }
        }

        currentScanAddress--;
    }

    //DebugPrint("[DLL] --- Scan finished for 0x%p (%s) ---\n\n", (void*)startAddress, label);
}

// 测试线程函数
void TestHookInstances(int SrFovHookSpeed) {
    //DebugPrint("[DLL] ===== STARTING SEQUENTIAL HOOK TESTING =====\n");
    //DebugPrint("[DLL] Total qualified addresses to test: %zu\n", g_qualifiedAddresses.size());
    //DebugPrint("[DLL] Maximum hooks to install: %d\n", g_maxHookCount);

    int currentTestingInstance = -1;

    while (!g_testFinished) {
        std::unique_lock<std::mutex> lock(g_testMutex);

        if (g_currentTestInstanceId >= g_qualifiedAddresses.size() ||
            g_currentTestInstanceId >= g_maxHookCount) {
            //DebugPrint("[DLL] No more instances to test. Total tested: %d\n", g_currentTestInstanceId);
            g_testFinished = true;
            break;
        }

        currentTestingInstance = g_currentTestInstanceId;
        uintptr_t currentAddr = g_qualifiedAddresses[currentTestingInstance];

        //DebugPrint("[DLL] Installing test instance %d at address: 0x%p\n",currentTestingInstance, (void*)currentAddr);

        if (g_fovHookManager.AddInstance(currentAddr, currentTestingInstance)) {
            HookFunctionPtr hookFunc = g_hookFunctions[currentTestingInstance];
            if (g_fovHookManager.InstallInstanceHook(currentTestingInstance, hookFunc)) {
                //DebugPrint("[DLL] Installed hook for test instance %d. Waiting for FOV call...\n",currentTestingInstance);

                if (g_testCV.wait_for(lock, std::chrono::milliseconds(SrFovHookSpeed), []() {
                    return g_testFinished;
                    })) {
                    //DebugPrint("[DLL] Test instance %d completed with result: %s\n",currentTestingInstance, g_foundTarget ? "FOUND TARGET" : "CONTINUE TESTING");
                }
                else {
                    //DebugPrint("[DLL] TIMEOUT: No FOV call detected for instance %d within timeout\n",currentTestingInstance);

                    g_fovHookManager.RemoveInstance(currentTestingInstance);
                    g_currentTestInstanceId++;
                }
            }
            else {

                //DebugPrint("[DLL] ERROR: Failed to install hook for instance %d\n",currentTestingInstance);
                g_currentTestInstanceId++;
            }
        }
        else {
            //DebugPrint("[DLL] ERROR: Failed to add instance %d\n", currentTestingInstance);
            g_currentTestInstanceId++;
        }
    }

    //DebugPrint("[DLL] ===== SEQUENTIAL TESTING COMPLETED =====\n");
    //DebugPrint("[DLL] Final result: %s\n",
    //    g_foundTarget ?
    //    std::string("SUCCESS - Found target FOV function at instance " + std::to_string(g_currentTestInstanceId)).c_str() :
    //    "FAILED - No target FOV function found");

    if (g_foundTarget == 0)
    {
        OnWinError("No matched pattern! FOV hook failed!", 0);
    }
    if (g_foundTarget != 0)
    {
        //OnWinError("FOV Hook Successfully!", 0);
    }
}

// 修改后的安装函数 - 新增参数 maxHookCount
bool InstallSRFOVHook(int maxHookCount = 600,int SrFovHookSpeed = 150,int SrFovHookRtime = 1000) {
    // 设置全局最大钩子数量
    g_maxHookCount = maxHookCount;

    //DebugPrint("[DLL] ===== INITIALIZING FOV HOOK SYSTEM =====\n");
    //DebugPrint("[DLL] Maximum hook count set to: %d\n", g_maxHookCount);

    // 检查是否超过编译时最大值
    if (g_maxHookCount > g_hookFunctions.size()) {
        //DebugPrint("[DLL] WARNING: Requested hook count (%d) exceeds maximum compiled hooks (%zu)\n",
        //    g_maxHookCount, g_hookFunctions.size());
        //DebugPrint("[DLL] Adjusting to maximum available: %zu\n", g_hookFunctions.size());
        //g_maxHookCount = static_cast<int>(g_hookFunctions.size());
    }

    // 初始化钩子管理器
    g_fovHookManager.Initialize(g_maxHookCount);

    // 重置全局状态
    g_currentTestInstanceId = 0;
    g_foundTarget = false;
    g_testFinished = false;

    // 清空之前的合格地址列表
    g_qualifiedAddresses.clear();

    // 获取 UnityPlayer.dll 模块句柄
    HMODULE unityPlayerModule = GetModuleHandleA("UnityPlayer.dll");
    if (!unityPlayerModule) {
        //DebugPrint("[DLL] Failed to get UnityPlayer.dll module handle\n");
        return false;
    }

    // 获取 UnityPlayer.dll 模块句柄并保存到全局变量 260119
    g_unityPlayerModule = GetModuleHandleA("UnityPlayer.dll");
    if (!g_unityPlayerModule) {
        DebugPrint("[DLL] Failed to get UnityPlayer.dll module handle\n");
        return false;
    }

    // 计算 set_field_of_view 函数地址
    //3.7版本
    uintptr_t setFieldOfViewAddr = (uintptr_t)unityPlayerModule + 0x9d2d90;
    //3.6版本
    //uintptr_t setFieldOfViewAddr = (uintptr_t)unityPlayerModule + 0xef6ff0;
    //DebugPrint("[DLL] UnityPlayer.dll base address: 0x%p\n", unityPlayerModule);
    //DebugPrint("[DLL] set_field_of_view calculated address: 0x%p\n", (void*)setFieldOfViewAddr);

    // 验证地址是否可读
    if (IsBadReadPtr((void*)setFieldOfViewAddr, sizeof(void*))) {
        //DebugPrint("[DLL1] set_field_of_view address is not readable\n");
        return false;
    }

    // 打印硬编码地址的内存信息
    PrintMemoryDetails(setFieldOfViewAddr, "Hardcoded FOV Addr");

    auto _srfovpattern = XorString::decrypt(encrypted_strings::sr_fov_code.data(), encrypted_strings::sr_fov_code.size());
    std::vector<uintptr_t> setFieldOfViewAddrs = PatternScanner::MultipleScan(_srfovpattern);
    //std::vector<uintptr_t> setFieldOfViewAddrs = PatternScanner::MultipleScan("83 EC 28 48 85 C9 74 15 48 8B 41 10 48 85 C0 74 0C 48 8B C8 48 83 C4 28 E9 ?? ?? ?? FF 48 8B D1 48 8D 4C 24 30 E8 ?? ?? ?? FF 48 8D 4C 24 40 48 8B 10 E8 ?? ?? ?? FF 48 8B 08 E8 ?? ?? ?? FF CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC");
    if (setFieldOfViewAddrs.empty()) {
        //DebugPrint("[DLL] Failed to find FOV pattern\n");
        return false;
    }

    // 使用用户指定的最大值来限制结果
    if (setFieldOfViewAddrs.size() > static_cast<size_t>(g_maxHookCount)) {
        setFieldOfViewAddrs.resize(g_maxHookCount);
        //DebugPrint("[DLL] INFO: Found more than %d results, truncating to the first %d.\n",g_maxHookCount, g_maxHookCount);
    }

    // 对每个找到的地址进行扫描和过滤
    for (size_t i = 0; i < setFieldOfViewAddrs.size(); i++) {
        char label[64];
        sprintf_s(label, sizeof(label), "Pattern Match %zu", i);
        CheckForZeroFloatBeforeAddress(setFieldOfViewAddrs[i], label, 200);
    }

    //DebugPrint("[DLL] SUMMARY: Found a total of %zu pattern matches.\n", setFieldOfViewAddrs.size());
    //DebugPrint("[DLL] QUALIFIED: Found %zu qualified addresses for testing.\n", g_qualifiedAddresses.size());

    // 再次检查是否超过最大钩子数
    if (g_qualifiedAddresses.size() > static_cast<size_t>(g_maxHookCount)) {
        //DebugPrint("[DLL] INFO: Qualified addresses (%zu) exceed max hook count (%d), truncating.\n",g_qualifiedAddresses.size(), g_maxHookCount);
        g_qualifiedAddresses.resize(g_maxHookCount);
    }

    // 检查是否有合格的地址
    if (g_qualifiedAddresses.empty()) {
        //DebugPrint("[DLL] ERROR: No qualified addresses found for hooking!\n");
        return false;
    }

    // 直接等待Unity窗口（阻塞）
    if (WaitForUnityWindow(30000))
    {
        //DebugPrint("[DLL] Found Unity window, proceeding with hook installation.\n");
		Sleep(SrFovHookRtime); // 等待以确保稳定
    }
    else
    {
        MessageBoxA(NULL, "超时：未找到Unity窗口", "失败", MB_OK);
        return false;
    }

    // 启动顺序测试线程
    //std::thread testThread(TestHookInstances);

	int a = SrFovHookSpeed;
    //std::thread testThread([](){TestHookInstances(a);});
    // 使用值捕获 [=] 或 [a]
    std::thread testThread([a](){TestHookInstances(a);});

    testThread.detach();

    //DebugPrint("[DLL] Sequential hook testing started. Testing up to %d addresses one by one.\n",std::min(static_cast<int>(g_qualifiedAddresses.size()), g_maxHookCount));

    return true;
}

DWORD WINAPI WriteFovInfiniteLoop(LPVOID lpParam)
{
    GameThreadData* pThreadData = (GameThreadData*)lpParam;
    while (true)
    {
        GlobalSRFovValue = pThreadData->SRFovValueTr;
		GlobalSRFovChangeEnabled = pThreadData->SRFovChangeEnabledTr;
		GlobalSRFovChageFix = pThreadData->SRFovChageFixTr;

        //DebugPrint("[DLL] Current FOV value %f\n", GlobalSRFovValue);

        Sleep(21);
    }
    return 0;
}


//260119 从配置文件读取SRFOVHookAddress
std::string GetSRFOVHookAddressFromConfig()
{
    char dllPath[MAX_PATH];
    HMODULE hModule = NULL;

    // 获取DLL模块句柄
    GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS |
        GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
        (LPCSTR)GetSRFOVHookAddressFromConfig, &hModule);

    // 获取DLL路径
    GetModuleFileNameA(hModule, dllPath, MAX_PATH);

    // 构建配置文件路径
    std::string configPath = dllPath;
    size_t lastSlash = configPath.find_last_of("\\/");
    if (lastSlash != std::string::npos)
    {
        configPath = configPath.substr(0, lastSlash + 1) + "fps_config.json";
    }

    // 读取文件内容
    std::ifstream configFile(configPath);
    if (!configFile.is_open())
    {
        return "";
    }

    std::stringstream buffer;
    buffer << configFile.rdbuf();
    std::string jsonContent = buffer.str();
    configFile.close();

    // 查找"SRFOVHookAddress"字段
    std::string searchKey = "\"SRFOVHookAddress\"";
    size_t keyPos = jsonContent.find(searchKey);
    if (keyPos == std::string::npos)
    {
        return "";
    }

    // 跳过key和冒号，找到值的起始位置
    size_t colonPos = jsonContent.find(':', keyPos);
    if (colonPos == std::string::npos)
    {
        return "";
    }

    // 跳过冒号和可能的空格
    size_t valueStart = colonPos + 1;
    while (valueStart < jsonContent.length() &&
        (jsonContent[valueStart] == ' ' || jsonContent[valueStart] == '\t' ||
            jsonContent[valueStart] == '\n' || jsonContent[valueStart] == '\r'))
    {
        valueStart++;
    }

    // 检查是否是null
    if (jsonContent.substr(valueStart, 4) == "null")
    {
        return "";
    }

    // 检查是否是字符串
    if (jsonContent[valueStart] != '\"')
    {
        return "";
    }

    // 找到字符串结束位置
    size_t stringEnd = jsonContent.find('\"', valueStart + 1);
    if (stringEnd == std::string::npos)
    {
        return "";
    }

    // 提取字符串内容
    return jsonContent.substr(valueStart + 1, stringEnd - valueStart - 1);
}



// StarRail视野(FOV)钩子相关 260119
typedef void(*SetFieldOfView_t_l)(void* _this, float value);
SetFieldOfView_t_l g_original_SetFieldOfView_l = nullptr;

int timesAtThirtylocal = 0;
bool _isfirstfoundsrfovlocal = true;

void HookSetFieldOfViewlocal(void* _this, float value) 
{
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
    if (GlobalSRFovChageFix == 1)
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
}

// 260119 安装视野钩子函数(当能读取到地址时)
bool InstallSRFOVHook1(const char* srfovaddress) {
    // 获取 UnityPlayer.dll 模块句柄
    HMODULE unityPlayerModule = GetModuleHandleA("UnityPlayer.dll");
    if (!unityPlayerModule) {
        DebugPrint("[DLL] Failed to get UnityPlayer.dll module handle\n");
        return false;
    }

    // 计算 set_field_of_view 函数地址
    //uintptr_t setFieldOfViewAddr = (uintptr_t)unityPlayerModule + 0xf5efd0;

    // 将十六进制字符串转换为uintptr_t
    uintptr_t offset = std::strtoull(srfovaddress, nullptr, 16);
    uintptr_t setFieldOfViewAddr = (uintptr_t)unityPlayerModule + offset;

    // 验证地址是否可读
    if (IsBadReadPtr((void*)setFieldOfViewAddr, sizeof(void*))) {
        DebugPrint("[DLL] set_field_of_view address is not readable\n");
        return false;
    }

    // 使用 MinHookManager 创建钩子
    if (!MinHookManager::Add(reinterpret_cast<void*>(setFieldOfViewAddr),
        reinterpret_cast<void*>(&HookSetFieldOfViewlocal),
        reinterpret_cast<void**>(&g_original_SetFieldOfView_l))) {
        //DebugPrint("[DLL] Failed to create FOV hook\n");
        DebugPrint("[DLL] ERROR: Failed to create FOV hook at address 0x%p\n", (void*)setFieldOfViewAddr);
        return false;
    }

    return true;
}



DWORD WINAPI GameRunningTask(LPVOID lpParam)
{
    // 现在可以通过pThreadData访问共享数据和文件句柄
    GameThreadData* pThreadData = (GameThreadData*)lpParam;
    SharedData* pSharedData = pThreadData->pSharedDataTr;
    HANDLE hMapFile = pThreadData->hMapFileTr;

    int _SRFovHookDepthTr = pThreadData->SRFovHookDepthTr;
    int _SRFovHookSpeedTr = pThreadData->SRFovHookSpeedTr;
	int _SRFovHookRunTimeTr = pThreadData->SRFovHookRunTimeTr;
	//创建视野写入线程
    const auto hThread = CreateThread(nullptr, 0, WriteFovInfiniteLoop, pThreadData, 0, nullptr);
    if (!hThread)
        return OnWinError("CreateThreadFov", GetLastError());
    CloseHandle(hThread);


    // 读取配置文件中的SRFOVHookAddress 260119
    std::string srfovHookAddress = GetSRFOVHookAddressFromConfig();
    //DebugPrint("[DLL] srfovadress: %s\n", srfovHookAddress.c_str());
    if (srfovHookAddress.empty())
    {
        srfovHookAddress = "default";
        DebugPrint("[DLL] srfovmode: Auto");
    }
    else
    {
        DebugPrint("[DLL] srfovmode: Manual,HkAddress: %s\n", srfovHookAddress.c_str());
        InstallSRFOVHook1(srfovHookAddress.c_str());
		return 0;
    }    


    // 安装视野钩子
    if (!InstallSRFOVHook(_SRFovHookDepthTr,_SRFovHookSpeedTr,_SRFovHookRunTimeTr))
    {
        OnWinError("视野钩子安装失败。", GetLastError());
		return 1;
    }

    return 0;
}


void RunLogic()
{
    //DebugPrint("[DLL] Logic thread started.\n");

    auto _mapName = XorString::decrypt(encrypted_strings::map_guid.data(), encrypted_strings::map_guid.size());
    auto _hDataReadyEvent = XorString::decrypt(encrypted_strings::ReadyEvent_guid.data(), encrypted_strings::ReadyEvent_guid.size());
    auto _hDataConsumedEvent = XorString::decrypt(encrypted_strings::ConsumedEvent_guid.data(), encrypted_strings::ConsumedEvent_guid.size());
    //const char* mapName = "3FDA00E1-3F3D-A33B-706C-3A11FEBFA687";
    const int mapSize = sizeof(SharedData);

    // 打开事件句柄
    //HANDLE hDataReadyEvent = OpenEventA(EVENT_ALL_ACCESS, FALSE, "89597B6A-FA92-21BD-1E7D-07AAAF1DCDE8");
    //HANDLE hDataConsumedEvent = OpenEventA(EVENT_ALL_ACCESS, FALSE, "C1B99146-EB12-08F4-A972-561CAFE52C78");
    HANDLE hDataReadyEvent = OpenEventA(EVENT_ALL_ACCESS, FALSE, _hDataReadyEvent.c_str());
    HANDLE hDataConsumedEvent = OpenEventA(EVENT_ALL_ACCESS, FALSE, _hDataConsumedEvent.c_str());

    if (!hDataReadyEvent || !hDataConsumedEvent)
    {
        //DebugPrint("[DLL] 无法打开事件句柄，错误代码: %d\n", GetLastError());
        OnWinError("OpenEvent", GetLastError());
        return;
    }

    //DebugPrint("[DLL] 成功打开事件句柄\n");

    // 打开已命名的文件映射对象
    //HANDLE hMapFile = OpenFileMappingA(FILE_MAP_READ | FILE_MAP_WRITE, FALSE, mapName);
    HANDLE hMapFile = OpenFileMappingA(FILE_MAP_READ | FILE_MAP_WRITE, FALSE, _mapName.c_str());  

    if (hMapFile == NULL) {
        //DebugPrint("[DLL] 无法打开共享内存文件映射对象，错误代码: %d\n", GetLastError());
        OnWinError("OpenFileMapping", GetLastError());
        CloseHandle(hDataReadyEvent);
        CloseHandle(hDataConsumedEvent);
        return;
    }

    //DebugPrint("[DLL] 成功打开共享内存\n");

    // 将文件映射的视图映射到进程的地址空间
    SharedData* pSharedData = (SharedData*)MapViewOfFile(
        hMapFile,
        FILE_MAP_READ | FILE_MAP_WRITE,
        0,
        0,
        mapSize);

    if (pSharedData == NULL) {
        //DebugPrint("[DLL] 无法映射共享内存视图，错误代码: %d\n", GetLastError());
        OnWinError("MapViewOfFile", GetLastError());
        CloseHandle(hMapFile);
        CloseHandle(hDataReadyEvent);
        CloseHandle(hDataConsumedEvent);
        return;
    }

    //DebugPrint("[DLL] IPC 初始化完成，开始监听数据...\n");

    bool _getDataOnce = false;

    // 创建线程参数结构体并初始化
    GameThreadData* pThreadData = new GameThreadData();
    pThreadData->pSharedDataTr = pSharedData;
    pThreadData->hMapFileTr = hMapFile;

    // 循环读取数据
    while (true)
    {
        // 等待数据就绪事件
        DWORD waitResult = WaitForSingleObject(hDataReadyEvent, 2117); // 2.1秒超时

        if (waitResult == WAIT_OBJECT_0)
        {
            // 读取共享内存中的数据
            SharedData data;
            memcpy(&data, pSharedData, sizeof(SharedData));

            //DebugPrint("[DLL] 接收到数据 - SRFovValue: %d, SRFovEnabled: %d\n",data.SRFovValue, data.SRFovChangeEnabled);

            // 在这里处理接收到的数据...
            if (!_getDataOnce && data.SRFovValue != -1 && data.SRFovChangeEnabled != -1)
            {
                pThreadData->SRFovHookDepthTr = data.SRFovHookDepth;
                pThreadData->SRFovHookSpeedTr = data.SRFovHookSpeed;
				pThreadData->SRFovHookRunTimeTr = data.SRFovHookRunTime;
                // 创建管理游戏任务线程并将结构体指针传递给线程
                const auto hThreadGame = CreateThread(nullptr, 0, GameRunningTask, pThreadData, 0, nullptr);

                if (!hThreadGame)
                {
                    OnWinError("CreateGameTskThread", GetLastError());
                    return;
                }
                CloseHandle(hThreadGame);

                _getDataOnce = true;
            }

            pThreadData->SRFovValueTr = data.SRFovValue;
			pThreadData->SRFovChangeEnabledTr = data.SRFovChangeEnabled;
			pThreadData->SRFovChageFixTr = data.SRFovChageFix;


            // 通知 C# 程序数据已处理
            SetEvent(hDataConsumedEvent);
        }
        else if (waitResult == WAIT_TIMEOUT)
        {
            // 超时，继续等待
            continue;
        }
        else
        {
            // 发生错误
            //DebugPrint("[DLL] 等待事件失败，错误代码: %d\n", GetLastError());
            break;
        }
    }
}







// 消息框线程函数6
static DWORD WINAPI MessageBoxThreadDisplayGenshinFogHookWarn(LPVOID) {
    MessageBoxA(nullptr, "去除Genshin雾气钩子安装失败。将无法去除虚化！", "警告", MB_ICONWARNING);
    return 0;
}

//// 260130去除Genshin雾气钩子相关fix
//typedef int(*HookDisplayFog_t)(void* a1, void* a2);  // 使用void*而不是__int64
//HookDisplayFog_t g_original_HookDisplayFog = nullptr;
//
//// 使用更大的缓冲区，比如256字节，并确保16字节对齐
//struct alignas(16) FakeFogStruct {
//    uint8_t data[64];
//};
//static FakeFogStruct g_fakeFogStruct;
//static bool g_hook_display_fog = true;
////static bool _isinstalled_display_genshin_fog_hook = false;
//
//// 参数和返回类型都匹配Rust
//int HookDisplayFog(void* a1, void* a2)
//{
//    if (g_hook_display_fog && a2)
//    {
//        memcpy(&g_fakeFogStruct, a2, sizeof(g_fakeFogStruct));
//        g_fakeFogStruct.data[0] = 0;
//        return g_original_HookDisplayFog(a1, (void*)&g_fakeFogStruct);
//    }
//
//    return g_original_HookDisplayFog(a1, a2);
//}


// 260130去除Genshin雾气钩子相关fix改进版
// 使用与Hooks.cpp一致的函数签名
typedef __int64 (*tDisplayFog)(__int64, __int64);
std::atomic<tDisplayFog> g_original_HookDisplayFog{ nullptr };

// 使用与Hooks.cpp一致的缓冲区结构
struct SafeFogBuffer {
    __declspec(align(16)) uint8_t data[64];
    uint8_t padding[192]; // 总共256字节，确保对齐和安全
};
static SafeFogBuffer g_fogBuffer = { 0 };

// 控制变量，应该从配置系统获取
static bool g_hook_display_fog = true;

__int64 HookDisplayFog(__int64 a1, __int64 a2)
{
    // 获取原始函数指针，使用原子操作确保线程安全
    auto orig = g_original_HookDisplayFog.load();

    // 安全检查：如果原始函数指针为空，直接返回0
    if (!orig) {
        return 0;
    }

    // 如果启用了禁用雾气功能并且a2有效
    if (g_hook_display_fog && a2)
    {
        // 先清零整个缓冲区（更安全）
        memset(&g_fogBuffer, 0, sizeof(g_fogBuffer));

        // 复制原始数据（64字节）
        memcpy(g_fogBuffer.data, (void*)a2, 64);

        // 关键：将第一个字节设为0，表示禁用雾效
        g_fogBuffer.data[0] = 0;

        // 调用原始函数，传入修改后的缓冲区数据
        return orig(a1, (__int64)g_fogBuffer.data);
    }

    // 否则正常调用原始函数
    return orig(a1, a2);
}


bool InstallDisplayGenshinFogHook()
{
    auto _DisplayGenshinFogHook_code = XorString::decrypt(encrypted_strings::display_genshin_fog_code.data(), encrypted_strings::display_genshin_fog_code.size());
    //void* DisplayFogAddr = (void*)PatternScanner::Scan("0F B6 02 88 01 8B 42 04 89 41 04 F3 0F 10 52 ? F3 0F 10 4A ? F3 0F 10 42 ? 8B 42 08 ");
    void* DisplayFogAddr = (void*)PatternScanner::Scan(_DisplayGenshinFogHook_code.c_str());

    if (!DisplayFogAddr) {
        return false;
    }
    if (!MinHookManager::Add(DisplayFogAddr, &HookDisplayFog, (void**)&g_original_HookDisplayFog)) {
        return false;
    }
    return true;
}

// 260202 原神钩子逻辑线程入口
void RunLogicGenshin()
{
    auto _mapName = XorString::decrypt(encrypted_strings::map_guid.data(), encrypted_strings::map_guid.size());
    auto _hDataReadyEvent = XorString::decrypt(encrypted_strings::ReadyEvent_guid.data(), encrypted_strings::ReadyEvent_guid.size());
    auto _hDataConsumedEvent = XorString::decrypt(encrypted_strings::ConsumedEvent_guid.data(), encrypted_strings::ConsumedEvent_guid.size());
    const int mapSize = sizeof(SharedData);

    // 打开事件句柄
    HANDLE hDataReadyEvent = OpenEventA(EVENT_ALL_ACCESS, FALSE, _hDataReadyEvent.c_str());
    HANDLE hDataConsumedEvent = OpenEventA(EVENT_ALL_ACCESS, FALSE, _hDataConsumedEvent.c_str());

    if (!hDataReadyEvent || !hDataConsumedEvent)
    {
        OnWinError("OpenEvent", GetLastError());
        return;
    }


    // 打开已命名的文件映射对象
    HANDLE hMapFile = OpenFileMappingA(FILE_MAP_READ | FILE_MAP_WRITE, FALSE, _mapName.c_str());

    if (hMapFile == NULL) {
        OnWinError("OpenFileMapping", GetLastError());
        CloseHandle(hDataReadyEvent);
        CloseHandle(hDataConsumedEvent);
        return;
    }

    // 将文件映射的视图映射到进程的地址空间
    SharedData* pSharedData = (SharedData*)MapViewOfFile(
        hMapFile,
        FILE_MAP_READ | FILE_MAP_WRITE,
        0,
        0,
        mapSize);

    if (pSharedData == NULL) {
        OnWinError("MapViewOfFile", GetLastError());
        CloseHandle(hMapFile);
        CloseHandle(hDataReadyEvent);
        CloseHandle(hDataConsumedEvent);
        return;
    }

    bool _getDataOnce = false;

    // 循环读取数据
    while (true)
    {
        // 等待数据就绪事件
        DWORD waitResult = WaitForSingleObject(hDataReadyEvent, 1000 ); // 1秒超时

        if (waitResult == WAIT_OBJECT_0)
        {
            // 读取共享内存中的数据
            SharedData data;
            memcpy(&data, pSharedData, sizeof(SharedData));

            // 在这里处理接收到的数据...
            if (data.GenshinFog != -1)
            {
                if(!_getDataOnce && data.GenshinFog == 2)
                {
                    // 260202
                    if (!InstallDisplayGenshinFogHook())
                    {
                        CreateThread(nullptr, 0, MessageBoxThreadDisplayGenshinFogHookWarn, nullptr, 0, nullptr);
                    }

                    _getDataOnce = true;
                }  
                if (data.GenshinFog == 2) {
                    g_hook_display_fog = true;
                }
                if (data.GenshinFog == 1) {
                    g_hook_display_fog = false;
                }
            }

            // 通知 C# 程序数据已处理
            SetEvent(hDataConsumedEvent);
        }
        else if (waitResult == WAIT_TIMEOUT)
        {
            // 超时，继续等待
            continue;
        }
        else
        {
            // 发生错误
            //DebugPrint("[DLL] 等待事件失败，错误代码: %d\n", GetLastError());
            break;
        }
    }
}









BOOL APIENTRY DllMain(HINSTANCE hInstance, DWORD fdwReason, LPVOID lpReserved)
{
    if (hInstance)
        DisableThreadLibraryCalls(hInstance);

    // 检查是否是目标进程 260202
    HMODULE hYuanShen = GetModuleHandleA("YuanShen.exe");
    HMODULE hGenshinImpact = GetModuleHandleA("GenshinImpact.exe");
    HMODULE hStarRail = GetModuleHandleA("StarRail.exe");

    // 如果不是目标进程，直接返回TRUE（DLL加载成功但不初始化）
    if (!hYuanShen && !hGenshinImpact && !hStarRail) {
        return TRUE;
    }

    if (fdwReason == DLL_PROCESS_ATTACH)
    {
        //const auto hThread = CreateThread(nullptr, 0, (LPTHREAD_START_ROUTINE)RunLogic, nullptr, 0, nullptr);
        //if (!hThread)
        //    return OnWinError("CreateThread", GetLastError());

        //CloseHandle(hThread);


		// 260202 启动逻辑线程更改
        LPTHREAD_START_ROUTINE startRoutine = nullptr;

        // 判断当前是哪个进程
        if (hYuanShen || hGenshinImpact) {
            // ys或genshin进程，执行ys逻辑
            startRoutine = (LPTHREAD_START_ROUTINE)RunLogicGenshin;
        }
        else if (hStarRail) {
            // sr进程，执行sr逻辑
            startRoutine = (LPTHREAD_START_ROUTINE)RunLogic;
        }

        if (startRoutine) {
            const auto hThread = CreateThread(nullptr, 0, startRoutine, nullptr, 0, nullptr);
            if (!hThread)
                return OnWinError("CreateThread", GetLastError());

            CloseHandle(hThread);
        }
    }
    else if (fdwReason == DLL_PROCESS_DETACH)
    {
        // 禁用所有钩子
        //MinHookManager::DisableAllHooks();
    }

    return TRUE;

}