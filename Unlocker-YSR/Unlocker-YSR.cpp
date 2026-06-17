//#include <windows.h>
//#include <string>  
//
//#pragma comment(lib, "ntdll.lib")
//EXTERN_C NTSTATUS __stdcall LdrAddRefDll(ULONG Flags, PVOID BaseAddress);
//
//bool bExit = false;
//struct IPCData* pIPCData = nullptr;
//
//enum class IPCStatus : int
//{
//	Error = -1,
//	None = 0,
//	HostAwaiting = 1,
//	ClientReady = 2,
//	ClientExit = 3,
//	HostExit = 4
//};
//
//struct __declspec(align(8)) IPCData
//{
//	ULONG64 Address;
//	int Value;
//	IPCStatus Status;
//};
//
//template<typename T, typename Func>
//class MemoryGuard
//{
//	T pResource;
//	Func pFunc;
//public:
//	MemoryGuard(T pAddress, Func pFunc) : pResource(pAddress), pFunc(pFunc) {}
//	~MemoryGuard() { if (pResource) pFunc(pResource); }
//	operator T() const { return pResource; }
//	T Get() const { return pResource; }
//	operator bool() const { return pResource != nullptr && pResource != INVALID_HANDLE_VALUE; }
//};
//
//using HandleGuard = MemoryGuard<HANDLE, decltype(&CloseHandle)>;
//using MappedMemoryGuard = MemoryGuard<LPVOID, decltype(&UnmapViewOfFile)>;
//
//template<typename T>
//T Clamp(T val, T min, T max)
//{
//	return val < min ? min : val > max ? max : val;
//}
//
//BOOL __declspec(noinline) OnWinError(const char* szFunction, DWORD dwError)
//{
//	char szMessage[256];
//	wsprintfA(szMessage, "%s failed with error %d", szFunction, dwError);
//	MessageBoxA(nullptr, szMessage, "Error", MB_ICONERROR);
//
//	if (pIPCData)
//		pIPCData->Status = IPCStatus::Error;
//
//	return FALSE;
//}
//
//DWORD __stdcall ThreadProc(LPVOID lpParameter)
//{
//	const auto hModule = static_cast<HMODULE>(lpParameter);
//	LdrAddRefDll(1, hModule);
//
//	constexpr auto szGuid = "BDA1DCAE-96C9-4B4A-A5A4-A43DC86DB253";
//
//	const auto hMapFile = HandleGuard(OpenFileMappingA(FILE_MAP_READ | FILE_MAP_WRITE, FALSE, szGuid), CloseHandle);
//	if (!hMapFile)
//		return OnWinError("OpenFileMapping", GetLastError());
//
//	const auto lpView = MappedMemoryGuard(MapViewOfFile(hMapFile, FILE_MAP_READ | FILE_MAP_WRITE, 0, 0, 0), UnmapViewOfFile);
//	if (!lpView)
//		return OnWinError("MapViewOfFile", GetLastError());
//
//	pIPCData = static_cast<IPCData*>(lpView.Get());
//
//	// the address shouldn't change, so we make a copy to make sure it's not changed by the host
//	const auto pFpsValue = reinterpret_cast<int*>(pIPCData->Address);
//
//	// check if the address is valid
//	MEMORY_BASIC_INFORMATION mbi{};
//	if (!VirtualQuery(pFpsValue, &mbi, sizeof(mbi)))
//		return OnWinError("VirtualQuery", GetLastError());
//
//	if (mbi.Protect != PAGE_READWRITE)
//		return OnWinError("VirtualQuery", ERROR_INVALID_ADDRESS);
//
//	pIPCData->Status = IPCStatus::ClientReady;
//
//	while (pIPCData->Status != IPCStatus::HostExit)
//	{
//		//const auto targetValue = Clamp(pIPCData->Value, 1, 1000);
//		//*pFpsValue = targetValue;
//
//
//		int currentValue = *pFpsValue;
//
//		// --- 方法一：使用 C++ 的 std::string (推荐，更现代、更安全) ---
//		// 构造一个包含信息的字符串
//		std::string debugMessage = "Read IPC Value: " + std::to_string(currentValue) + "\n";
//		//// 将 C++ string 转换为 C 风格字符串并输出
//		//OutputDebugStringA(debugMessage.c_str());
//		Sleep(62);
//
//
//		//Sleep(62);
//	}
//
//	pIPCData->Status = IPCStatus::ClientExit;
//	return 0;
//}
//
//BOOL __stdcall DllMain(HINSTANCE hInstance, DWORD fdwReason, LPVOID lpReserved)
//{
//	if (hInstance)
//		DisableThreadLibraryCalls(hInstance);
//
//	if (!GetModuleHandleA("mhypbase.dll"))
//		return TRUE;
//
//	if (fdwReason == DLL_PROCESS_ATTACH)
//	{
//		const auto hThread = CreateThread(nullptr, 0, ThreadProc, hInstance, 0, nullptr);
//		if (!hThread)
//			return OnWinError("CreateThread", GetLastError());
//
//		CloseHandle(hThread);
//	}
//
//	return TRUE;
//}
//
//EXTERN_C __declspec(dllexport) LRESULT __stdcall WndProc(int code, WPARAM wParam, LPARAM lParam)
//{
//	return CallNextHookEx(nullptr, code, wParam, lParam);
//}












#include <array>
//#include <vector>
//#include <memory>
//#include <unordered_map>
//#include <atomic>
//#include <windows.h>
//#include <vector>
//#include <cstdint>
//#include <Psapi.h>
//#include <cstdio>
//#include <string>

//防止windows.h定义min和max宏，影响std::min/std::max
#define NOMINMAX 
#include <windows.h>
#include "PatternScanner.hpp"
#include "MinHookManager.h" // 包含 MinHook 管理类
#include "HookUtility.h"

#pragma comment(lib, "ntdll.lib")

EXTERN_C NTSTATUS __stdcall LdrAddRefDll(ULONG Flags, PVOID BaseAddress);

float FovValue = 45.0f;
bool FovValueVaild = false;
bool IsInstalledFovHook = false;
bool bExit = false;
struct IPCData* pIPCData = nullptr;
bool IsStarRail = false;
bool IsInstalledFrameHook = false;
//constexpr auto szGuid = "BDA1DCAE-96C9-4B4A-A5A4-A43DC86DB253";
const char* szGuid = "guid";

// 加密后的字符串数据
namespace encrypted_strings {
	//Genshin FOV Adjustment MEM_code
    constexpr auto fov_code = XorString::encrypt("40 53 48 83 EC 60 0F 29 74 24 ? 48 8B D9 0F 28 F1 E8 ? ? ? ? 48 85 C0 0F 84 ? ? ? ? E8 ? ? ? ? 48 8B C8");
	//Force FPS MEM_code
    constexpr auto force_fps_code = XorString::encrypt("E8 ? ? ? ? 85 C0 7E 0E E8 ? ? ? ? 0F 57 C0 F3 0F 2A C0 EB 08 ?");
	//Genshin szGuid 
	constexpr auto genshin_guid = XorString::encrypt("727B2975-0BB3-022D-AB4B-54BEB6A6C687");
	//StarRail szGuid
	constexpr auto starrail_guid = XorString::encrypt("741E764D-0EB9-EE23-BC9B-EEE71F0D64C1");
	//StarRail FOV Adjustment MEM_code
	//constexpr auto sr_fov_code = XorString::encrypt("83 EC 28 48 85 C9 74 15 48 8B 41 10 48 85 C0 74 0C 48 8B C8 48 83 C4 28 E9 ?? ?? ?? FF 48 8B D1 48 8D 4C 24 30 E8 ?? ?? ?? FF 48 8D 4C 24 40 48 8B 10 E8 ?? ?? ?? FF 48 8B 08 E8 ?? ?? ?? FF CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC");
	//Genshin DisablePlayerPerspectiveBlur MEM_code
	constexpr auto disable_genshin_blur_code = XorString::encrypt("E8 ? ? ? ? 48 8B BE ? ? ? ? 80 3D ? ? ? ? ? 0F 85 ? ? ? ? 80 BE ? ? ? ? ? 74 11");
	// 251231 DisableDisplayGenshinFog MEM_code
    constexpr auto display_genshin_fog_code = XorString::encrypt("0F B6 02 88 01 8B 42 04 89 41 04 F3 0F 10 52 ? F3 0F 10 4A ? F3 0F 10 42 ? 8B 42 08");
}

enum class IPCStatus : int
{
    Error = -1,
    None = 0,
    HostAwaiting = 1,
    ClientReady = 2,
    ClientExit = 3,
    HostExit = 4
};

struct __declspec(align(8)) IPCData
{
    ULONG64 Address;
    int Value;
    IPCStatus Status;
	int PreventIllegalToolStatus;
    int ProcessPriorityStatus;
    int FOVgenshinValue1;
    int FOVgenshinStatus1;
    int FOVgenshinStatusFix;
	int DisableGenshinBlurStatus;
	int DisplayGenshinFogStatus;
    int GenshinAdvanceToolMask;
};


// 消息框线程函数6
static DWORD WINAPI MessageBoxThreadDisplayGenshinFogHookWarn(LPVOID) {
    MessageBoxA(nullptr, "去除Genshin雾气钩子安装失败。将无法去除虚化！", "警告", MB_ICONWARNING);
    return 0;
}

//// 251231去除Genshin雾气钩子相关
//typedef int(*HookDisplayFog_t)(__int64 a1, __int64 a2);
//HookDisplayFog_t g_original_HookDisplayFog = nullptr;
//__declspec(align(16)) static uint8_t g_fakeFogStruct[64];
//static bool g_hook_display_fog = false;
//static bool _isinstalled_display_genshin_fog_hook = false;
//
//__int64 HookDisplayFog(__int64 a1, __int64 a2)
//{
//    if (g_hook_display_fog && a2)
//    {
//        memcpy(g_fakeFogStruct, (void*)a2, sizeof(g_fakeFogStruct));
//        g_fakeFogStruct[0] = 0;
//        return g_original_HookDisplayFog(a1, (uintptr_t)g_fakeFogStruct);
//    }
//
//    return g_original_HookDisplayFog(a1, a2);
//}




// 260130去除Genshin雾气钩子相关fix
typedef int(*HookDisplayFog_t)(void* a1, void* a2);  // 使用void*而不是__int64
HookDisplayFog_t g_original_HookDisplayFog = nullptr;

// 使用更大的缓冲区，比如256字节，并确保16字节对齐
struct alignas(16) FakeFogStruct {
    uint8_t data[64];
};
static FakeFogStruct g_fakeFogStruct;
static bool g_hook_display_fog = true;
static bool _isinstalled_display_genshin_fog_hook = false;

// 参数和返回类型都匹配Rust
int HookDisplayFog(void* a1, void* a2)
{
    if (g_hook_display_fog && a2)
    {
        memcpy(&g_fakeFogStruct, a2, sizeof(g_fakeFogStruct));
        g_fakeFogStruct.data[0] = 0;
        return g_original_HookDisplayFog(a1, (void*)&g_fakeFogStruct);
    }

    return g_original_HookDisplayFog(a1, a2);
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



// 消息框线程函数5
static DWORD WINAPI MessageBoxThreadPlayerPerspectiveHookWarn(LPVOID) {
    MessageBoxA(nullptr, "去除Genshin虚化钩子安装失败。将无法去除虚化！", "警告", MB_ICONWARNING);
    return 0;
}

// 251207去除Genshin虚化钩子相关
typedef void* (*HookPlayer_Perspective_t)(void* RCX, float Display, void* R8);
HookPlayer_Perspective_t g_original_Player_Perspective = nullptr;
static bool g_hookplayer_perspective = false; 
static bool _isinstalled_disable_genshin_blur_hook = false;

void* HookPlayer_Perspective(void* RCX, float Display, void* R8)
{
    if (g_hookplayer_perspective == true)
    {
        Display = 1.f;
    }
    return g_original_Player_Perspective(RCX, Display, R8);
}

bool InstallPlayerPerspectiveHook()
{
    auto _PlayerPerspectiveHook_code = XorString::decrypt(encrypted_strings::disable_genshin_blur_code.data(), encrypted_strings::disable_genshin_blur_code.size());
    //DebugPrint("[DLL] Decrypted: %s\n", _PlayerPerspectiveHook_code.c_str());
    void* Player_PerspectiveAddr = (void*)PatternScanner::Scan(_PlayerPerspectiveHook_code.c_str());
    if (!Player_PerspectiveAddr) {
        //MessageBoxA(nullptr, "HookPlayer_Perspective search failed!", "PatternScanner", MB_OK | MB_ICONERROR);
        return false;
    }
    Player_PerspectiveAddr = (void*)PatternScanner::ResolveRelativeAddress((uintptr_t)Player_PerspectiveAddr);
    if (!Player_PerspectiveAddr) {
        //MessageBoxA(nullptr, "HookPlayer_Perspective search failed!", "PatternScanner", MB_OK | MB_ICONERROR);
        return false;
    }
    if (!MinHookManager::Add(Player_PerspectiveAddr, &HookPlayer_Perspective, (void**)&g_original_Player_Perspective)) {
        //MessageBoxA(nullptr, "HookPlayer_Perspective install failed!", "MinHook", MB_OK | MB_ICONERROR);
        return false;
    }
    return true;
}


//251106 ChangeFOVforGenshin
static std::atomic<bool> g_fovInstalled(false);
typedef int(*HookChangeFOV_t)(__int64 a1, float a2);
HookChangeFOV_t g_original_HookChangeFOV = nullptr;

static float g_lastForcedFovValue = -1.0f;
const float& TARGET_FOV = FovValue;
static bool g_bEnableFOVHook = true;  // 默认启用
static bool g_bEnableFOVFix = false;  // 修复FOV开关
static bool fovrealfix = false;  


// 添加全局变量用于记录执行状态260131
static volatile time_t last_exec_time_fov = 0;
// 定义checkgamestate函数指针类型 260131
typedef int(__cdecl* CheckGameStateFunc)(bool);
// 定义fov执行状态标志260131
static bool g_advantols_need_enablefov_test = false;

// 定义是否去除草地 260225
static int g_need_removegrass = -1;
// 定义不支持热更新的函数指针 260225
typedef int(__cdecl* UpdateConfigNoHotLoadFunc)(int);

// 监控fov执行状态的线程函数260131
DWORD WINAPI monitor_fov_loop_execution(LPVOID lpParam)
{
    time_t previous_time = 0;
    int consecutive_same_time = 0;

	// 获取genshin_advan_tol.dll模块句柄 260131
    HMODULE h_ckgenshinDll = GetModuleHandleA("genshin_advan_tol.dll");


    CheckGameStateFunc updatestate = nullptr;
	// 260131 获取CheckGameState函数地址并调用
    if (h_ckgenshinDll)
    {
        // 获取函数地址
        updatestate = (CheckGameStateFunc)GetProcAddress(h_ckgenshinDll, "CheckGameState");
        if (updatestate) 
        {
			updatestate(false);
        }
    }

    // 260225 声明用于 UpdateConfigNoHotLoad 的指针
    UpdateConfigNoHotLoadFunc updatestatenohot = nullptr;
    // 260225 获取UpdateConfigNoHotLoad函数地址并调用
    if (h_ckgenshinDll)
    {
        updatestatenohot = (UpdateConfigNoHotLoadFunc)GetProcAddress(h_ckgenshinDll, "UpdateConfigNoHotLoad");
        if (updatestatenohot)
        {
            int result = updatestatenohot(-1);
        }

    }


    while (true) 
    {
        Sleep(500);  
        time_t current_time = last_exec_time_fov;

        if (current_time > previous_time) 
        {
            // 时间戳有变化，说明xxx在最近1秒内执行过
            //OutputDebugStringA("xxx is executing in the loop\n");

            if (updatestate != NULL)
            {
                updatestate(true);
            }

            previous_time = current_time;
            consecutive_same_time = 0;
        }
        else if (current_time == previous_time && previous_time != 0) {
            consecutive_same_time++;

            if (consecutive_same_time >= 2) 
            {
                // 连续2次检查时间戳没变化，说明xxx已停止执行
                //OutputDebugStringA("xxx has stopped executing\n");

                if (updatestate != NULL)
                {
                    updatestate(false);
                }
            }
        }


        // 260225 获取UpdateConfigNoHotLoad函数地址并调用
        if (updatestatenohot)
        {
            if (g_need_removegrass == 1) 
            {
                int result = updatestatenohot(1);
            }
            if (g_need_removegrass == 2)
            {
                int result = updatestatenohot(2);
            }
        }

    }
}


// 251229带平滑过渡的版本
__int64 HookChangeFOV(__int64 a1, float ChangeFovValue)
{
    // 记录执行时间戳 260131
    last_exec_time_fov = time(NULL);


    float originalFov = ChangeFovValue; // 保存原始值用于监控

    if (!g_bEnableFOVFix)
    {
        // 根据标志决定是否修改FOV值
        if (g_bEnableFOVHook) {
            ChangeFovValue = TARGET_FOV;
        }
    }
    else
    {
        if (g_bEnableFOVHook)
        {
            // 添加调用计数和时间戳信息
            static int callCount = 0;
            static __int64 last_a1 = 0;
            static DWORD lastCallTime = 0;

            // 平滑过渡相关变量
            static float currentFov = 0.0f; // 当前FOV值
            static float targetFov = 0.0f;  // 目标FOV值
            static bool isTransitioning = false; // 是否正在过渡
            static DWORD transitionStartTime = 0; // 过渡开始时间
            const DWORD TRANSITION_DURATION = 715; // 过渡持续时间(毫秒)

            callCount++;
            DWORD currentTime = GetTickCount();
            DWORD timeSinceLastCall = (lastCallTime > 0) ? (currentTime - lastCallTime) : 0;
            lastCallTime = currentTime;

            // 构建详细的调试信息
            std::string debugMsg = "30lau Call " + std::to_string(callCount) +
                ": originalFOV=" + std::to_string(originalFov) +
                ", fovrealfix=" + (fovrealfix ? "true" : "false") +
                ", a1=0x" + std::to_string(a1) +
                ", timeSinceLast=" + std::to_string(timeSinceLastCall) + "ms";

            // 检查是否是不同的相机对象
            bool cameraChanged = (last_a1 != 0 && last_a1 != a1);
            if (cameraChanged) {
                debugMsg += " [Camera Changed!]";
            }
            last_a1 = a1;

            //OutputDebugStringA(debugMsg.c_str());

            // 初始化currentFov
            if (currentFov == 0.0f) {
                currentFov = originalFov;
            }

            if (originalFov > 31.0f)
            {
                float newTargetFov = 0.0f;

                //// 确定目标FOV值
                //if (cameraChanged == false)
                //{
                //    newTargetFov = TARGET_FOV;
                //}
                //else if (cameraChanged == true)
                //{
                //    if (originalFov > 35.0f)
                //    {
                //        newTargetFov = 45.0f;
                //    }
                //    else
                //    {
                //        newTargetFov = originalFov; // 保持原值
                //    }
                //}


                if (!cameraChanged) // 相机未改变
                {
                    newTargetFov = TARGET_FOV;
                }
                else // 相机改变
                {
                    if (originalFov > 35.0f)
                    {
                        newTargetFov = 45.0f;
                    }
                    else
                    {
                        newTargetFov = originalFov; // 保持原值
                        // 如果不需要改变FOV，直接使用原值，不进行过渡
                        ChangeFovValue = originalFov;
                        if (g_original_HookChangeFOV) {
                            int ret = g_original_HookChangeFOV(a1, ChangeFovValue);
                            return static_cast<__int64>(ret);
                        }
                        return 0;
                    }
                }


                // 检查目标值是否改变，如果改变则开始新的过渡
                if (abs(newTargetFov - targetFov) > 0.01f)
                {
                    targetFov = newTargetFov;
                    isTransitioning = true;
                    transitionStartTime = currentTime;
                }

                // 执行平滑过渡
                if (isTransitioning)
                {
                    DWORD elapsedTime = currentTime - transitionStartTime;

                    if (elapsedTime >= TRANSITION_DURATION)
                    {
                        // 过渡完成
                        currentFov = targetFov;
                        isTransitioning = false;
                    }
                    else
                    {
                        // 计算过渡进度 (0.0 到 1.0)
                        float progress = static_cast<float>(elapsedTime) / static_cast<float>(TRANSITION_DURATION);

                        // 使用平滑插值函数 (ease-in-out)
                        // 可选的插值方式:
                        // 1. 线性插值
                        // float smoothProgress = progress;

                        // 2. ease-in-out (推荐，更自然)
                        float smoothProgress = progress < 0.5f
                            ? 2.0f * progress * progress
                            : 1.0f - pow(-2.0f * progress + 2.0f, 2.0f) / 2.0f;

                        // 3. ease-out (快速开始，慢慢结束)
                        // float smoothProgress = 1.0f - (1.0f - progress) * (1.0f - progress);

                        // 计算当前FOV值
                        float startFov = currentFov;
                        currentFov = startFov + (targetFov - startFov) * smoothProgress;
                    }

                    debugMsg += " [Transitioning: " + std::to_string(currentFov) +
                        " -> " + std::to_string(targetFov) + "]";
                }

                ChangeFovValue = currentFov;
            }
        }
    }

    // 调用原始函数
    if (g_original_HookChangeFOV) {
        int ret = g_original_HookChangeFOV(a1, ChangeFovValue);
        return static_cast<__int64>(ret);
    }

    return 0;
}


//// 251229不带平滑过渡的版本
//__int64 HookChangeFOV(__int64 a1, float ChangeFovValue)
//{
//    float originalFov = ChangeFovValue; // 保存原始值用于监控
//
//    if (!g_bEnableFOVFix)
//    {
//        // 根据标志决定是否修改FOV值
//        if (g_bEnableFOVHook) {
//            ChangeFovValue = TARGET_FOV;
//        }
//    }
//    else
//    {
//        if (g_bEnableFOVHook)
//        {
//            // 添加调用计数和时间戳信息
//            static int callCount = 0;
//            static __int64 last_a1 = 0;
//            static DWORD lastCallTime = 0;
//
//            callCount++;
//            DWORD currentTime = GetTickCount();
//            DWORD timeSinceLastCall = (lastCallTime > 0) ? (currentTime - lastCallTime) : 0;
//            lastCallTime = currentTime;
//
//            // 构建详细的调试信息
//            std::string debugMsg = "30lau Call " + std::to_string(callCount) +
//                ": originalFOV=" + std::to_string(originalFov) +
//                ", fovrealfix=" + (fovrealfix ? "true" : "false") +
//                ", a1=0x" + std::to_string(a1) +
//                ", timeSinceLast=" + std::to_string(timeSinceLastCall) + "ms";
//
//            // 检查是否是不同的相机对象
//            bool cameraChanged = (last_a1 != 0 && last_a1 != a1);
//            if (cameraChanged) {
//                debugMsg += " [Camera Changed!]";
//            }
//            last_a1 = a1;
//
//            //OutputDebugStringA(debugMsg.c_str());
//
//
//            if (originalFov > 31.0f) 
//            {
//                if (cameraChanged == false)
//                {
//                    ChangeFovValue = TARGET_FOV;
//                }
//                if (cameraChanged == true)
//                {
//                    if (originalFov > 35.0f)
//                    {
//                        ChangeFovValue = 45.0f;
//                    }
//                }
//            }
//        }
//    }
//
//    // 调用原始函数
//    if (g_original_HookChangeFOV) {
//        int ret = g_original_HookChangeFOV(a1, ChangeFovValue);
//        return static_cast<__int64>(ret);
//    }
//
//    return 0;
//}


bool InstallFovHook() {
    auto _fov_code = XorString::decrypt(encrypted_strings::fov_code.data(), encrypted_strings::fov_code.size());
    //void* ChangeFOVAddr = (void*)PatternScanner::Scan("40 53 48 83 EC 60 0F 29 74 24 ? 48 8B D9 0F 28 F1 E8 ? ? ? ? 48 85 C0 0F 84 ? ? ? ? E8 ? ? ? ? 48 8B C8");

    void* ChangeFOVAddr = (void*)PatternScanner::Scan(_fov_code.c_str());
    if (!ChangeFOVAddr) {
        return false;
    }

    if (!MinHookManager::Add(ChangeFOVAddr, &HookChangeFOV, (void**)&g_original_HookChangeFOV)) {
        uintptr_t resolved = PatternScanner::ResolveRelativeAddress((uintptr_t)ChangeFOVAddr);
        if (resolved) {
            if (!MinHookManager::Add((void*)resolved, &HookChangeFOV, (void**)&g_original_HookChangeFOV)) {
                return false;
            }
        }
        else {
            return false;
        }
    }

    return true;
}

// 消息框线程函数
static DWORD WINAPI MessageBoxThreadFOVHookWarn(LPVOID) {
    MessageBoxA(nullptr, "FOV视角调整钩子安装失败。将无法更改FOV！", "警告", MB_ICONWARNING);
    return 0;
}

DWORD WINAPI FovInstallerThread(LPVOID lpParameter)
{
    Sleep(100);

    // 只尝试一次安装
    if (InstallFovHook())
    {
        g_fovInstalled.store(true);
        OutputDebugStringA("30lau:FOVhook installed successfully!");
        return 0;  // 成功
    }
    else
    {
        OutputDebugStringA("30lau:FOVhook installed failed!");
        CreateThread(nullptr, 0, MessageBoxThreadFOVHookWarn, nullptr, 0, nullptr);
        return 1;  // 失败
    }
}

void StartFovInstallerThread()
{
    if (g_fovInstalled.load()) return;

    const auto hThread = CreateThread(nullptr, 0, FovInstallerThread, nullptr, 0, nullptr);
    if (hThread) CloseHandle(hThread);
}






//260115 
bool _isinstallframehook6 = false;

typedef int(*GameUpdate_t)(__int64 a1, const char* a2);
GameUpdate_t g_original_GameUpdate = nullptr;

typedef int(*Set_FrameCount_t)(int value);
Set_FrameCount_t g_original_Set_FrameCount = nullptr;

__int64 HookGameUpdate(__int64 a1, const char* a2)
{
    //if (!GameUpdateInit)
    //{
    //    GameUpdateInit = true;
    //}

    //if (menu.enable_fps_override)
    //{
    //    g_original_Set_FrameCount(menu.selected_fps);
    //}

    //if (menu.enable_syncount_override)
    //{
    //    g_original_Set_SyncCount(false);
    //}


    if (true)
    {
        g_original_Set_FrameCount(1000);
    }

    return g_original_GameUpdate(a1, a2);
}


bool InstallFrameHook() 
{
    void* GameUpdateAddr = (void*)PatternScanner::Scan("E8 ? ? ? ? 48 8D 4C 24 ? 8B F8 FF 15 ? ? ? ? E8 ? ? ? ?");
    GameUpdateAddr = (void*)PatternScanner::ResolveRelativeAddress((uintptr_t)GameUpdateAddr);
    if (!GameUpdateAddr) {
        MessageBoxA(nullptr, "HookGameUpdate search failed!", "PatternScanner", MB_OK | MB_ICONERROR);
    }
    if (!MinHookManager::Add(GameUpdateAddr, &HookGameUpdate, (void**)&g_original_GameUpdate)) {
        MessageBoxA(nullptr, "HookGameUpdate install failed!", "MinHook", MB_OK | MB_ICONERROR);
    }

    void* Set_FrameCountAddr = (void*)PatternScanner::Scan("E8 ? ? ? ? E8 ? ? ? ? 83 F8 1F 0F 9C 05 ? ? ? ? 48 8B 05 ? ? ? ? ");
    Set_FrameCountAddr = (void*)PatternScanner::ResolveRelativeAddress((uintptr_t)Set_FrameCountAddr);
    Set_FrameCountAddr = (void*)PatternScanner::ResolveRelativeAddress((uintptr_t)Set_FrameCountAddr);
    if (!Set_FrameCountAddr) {
        MessageBoxA(nullptr, "Set_FrameCountAddr search failed!", "PatternScanner", MB_OK | MB_ICONERROR);
    }
    g_original_Set_FrameCount = (Set_FrameCount_t)Set_FrameCountAddr;

	return true;
}









// 260102
bool _isenabled_frame_fix = false;
bool _frame_fix_mode = false;
int fpsvalue1 = 60;

// 260307 防报错10612-4001 抽帧修复
bool _frame_fix_mode_v2 = false;

// 帧数限制钩子相关
typedef int(*HookGet_FrameCount_t)();
HookGet_FrameCount_t g_original_HookGet_FrameCount = nullptr;

//// 帧数限制钩子函数
//int HookGet_FrameCount() {
//    int ret = g_original_HookGet_FrameCount();
//    if (ret >= 60) return 60;
//    else if (ret >= 45) return 45;
//    else if (ret >= 30) return 30;
//    //避免值为-1时不生效250908
//    else if (ret < 0) return 60;
//    //ret = 60;
//    return ret;
//}

// 260119全局变量保存模块基址
static HMODULE GI_unityPlayerModule = nullptr;


// 帧数限制钩子函数
int HookGet_FrameCount() {
    int ret = 60;
    // 260102新增帧数修复
    if (_frame_fix_mode == true)
    {
        if (!_isenabled_frame_fix)
        {
            ret = g_original_HookGet_FrameCount();
        }
        else
        {
            ret = fpsvalue1;
        }
    }
    else
    {
        if (_frame_fix_mode_v2 == false) 
        {
            //DebugPrint("[DLL] HookGet_Frame mode V1");

            ret = g_original_HookGet_FrameCount();
            if (ret >= 60) return 60;
            else if (ret >= 45) return 45;
            else if (ret >= 30) return 30;
            //避免值为-1时不生效250908
            else if (ret < 0) return 60;
            //ret = 60;
        }
        else
        {
            //DebugPrint("[DLL] HookGet_Frame mode V2");

            // 260307 防报错10612-4001 地址抓取
            if (false)
            {
                // --- 新增日志控制逻辑开始 ---
                static int last_ret = -999; // 记录上次的值，初始化为一个不可能的值
                static std::chrono::steady_clock::time_point last_tick = std::chrono::steady_clock::now(); // 记录上次时间

                // 获取当前时间
                auto now = std::chrono::steady_clock::now();
                // 计算距离上次调用经过的毫秒数
                auto elapsed_ms = std::chrono::duration_cast<std::chrono::milliseconds>(now - last_tick).count();

                // 判断条件：值改变 OR (值未改变但距离上次输出已超过100ms)
                /*if (ret != last_ret || elapsed_ms > 100)*/
                if (elapsed_ms > 50)
                {
                    // 更新状态
                    last_ret = ret;
                    last_tick = now;

                    void* caller = _ReturnAddress();

                    uintptr_t offset = 0;

                    // 【新增】计算偏移量
                    if (GI_unityPlayerModule) {
                        offset = (uintptr_t)caller - (uintptr_t)GI_unityPlayerModule;
                    }

                    //打印日志，增加偏移量字段
                    // 提示：0x%08llX 会以 8位宽度、大写十六进制 打印偏移量，方便和 x64dbg/IDA 对比
                    DebugPrint("[DLL] ret: %d, caller: %p, UnityPlayer+offset: 0x%08llX\n",ret, caller, (unsigned long long)offset);
                }
                // --- 新增日志控制逻辑结束 ---
            }




            // 1. 获取调用者地址
            void* caller = _ReturnAddress();
            uintptr_t offset = 0;

            // 2. 计算相对于 UnityPlayer.dll 的偏移量
            if (GI_unityPlayerModule) {
                offset = (uintptr_t)caller - (uintptr_t)GI_unityPlayerModule;
            }

            // 3. 判断逻辑
            // 如果调用者是特定的偏移 (0xE52EE85)，则强制返回 60
            // 注意：日志里是 %p 打印，可能省略了前导0，比较时建议用十六进制数值
            // 防止抽帧 v6.5 260408
            //if (offset != 0x10C61AB2)
            //{
            //    //DebugPrint("[DLL] detected addr now return 60");
            //    return 60;
            //}

            // 防止抽帧 v6.6 260520
            if (offset != 0x0EAAD725)
            {
                //DebugPrint("[DLL] detected addr now return 60");
                return 60;
            }

            //if (offset != 0x0619EB09 && offset != 0x093D6186 && offset != 0x0DC79BD2 && offset != 0x0EEF6C1C && offset != 0x08CE237A && offset != 0x0ACCB056 && offset != 0x11238DE5 && offset != 0x077167E9 && offset != 0x0EA67652 && offset != 0x06129253 && offset != 0x10C61AB2 && offset != 0x0CED6041 && offset != 0x08B1A2E5)
            //{
            //    //DebugPrint("[DLL] detected addr now return 60");
            //    return 60;
            //}

            // 4. 其他情况：返回原函数值或执行其他逻辑
            // 如果你需要原函数的值，必须调用原函数
            if (g_original_HookGet_FrameCount) {
                int original_val = g_original_HookGet_FrameCount();
                return original_val;
            }
        }
    }

    return ret;
}

// 安装帧数读取钩子
bool InstallFrameReadHook() {

    //获取主模块基地址 260307
    GI_unityPlayerModule = GetModuleHandleA(NULL);

    auto _force_fps_code = XorString::decrypt(encrypted_strings::force_fps_code.data(), encrypted_strings::force_fps_code.size());
    // 使用 PatternScanner 查找帧数读取函数
    //uintptr_t Get_FrameCountAddr = PatternScanner::Scan("E8 ? ? ? ? 85 C0 7E 0E E8 ? ? ? ? 0F 57 C0 F3 0F 2A C0 EB 08 ?");
    uintptr_t Get_FrameCountAddr = PatternScanner::Scan(_force_fps_code.c_str());
    if (!Get_FrameCountAddr) {
        //MessageBoxA(nullptr, "HookGet_FrameCount search failed!", "PatternScanner", MB_OK | MB_ICONERROR);
        return false;
    }

    // 解析相对地址 (两次解析)
    Get_FrameCountAddr = PatternScanner::ResolveRelativeAddress(Get_FrameCountAddr);
    Get_FrameCountAddr = PatternScanner::ResolveRelativeAddress(Get_FrameCountAddr);
    if (!Get_FrameCountAddr) {
        //MessageBoxA(nullptr, "HookGet_FrameCount resolve failed!", "PatternScanner", MB_OK | MB_ICONERROR);
        return false;
    }

    // 使用 MinHookManager 创建钩子
    if (!MinHookManager::Add(reinterpret_cast<void*>(Get_FrameCountAddr),
        reinterpret_cast<void*>(&HookGet_FrameCount),
        reinterpret_cast<void**>(&g_original_HookGet_FrameCount))) {
        //MessageBoxA(nullptr, "Failed to create frame read hook", "MinHook", MB_OK | MB_ICONERROR);
        return false;
    }

    return true;
}

template<typename T, typename Func>
class MemoryGuard
{
    T pResource;
    Func pFunc;
public:
    MemoryGuard(T pAddress, Func pFunc) : pResource(pAddress), pFunc(pFunc) {}
    ~MemoryGuard() { if (pResource) pFunc(pResource); }
    operator T() const { return pResource; }
    T Get() const { return pResource; }
    operator bool() const { return pResource != nullptr && pResource != INVALID_HANDLE_VALUE; }
};

using HandleGuard = MemoryGuard<HANDLE, decltype(&CloseHandle)>;
using MappedMemoryGuard = MemoryGuard<LPVOID, decltype(&UnmapViewOfFile)>;

BOOL __declspec(noinline) OnWinError(const char* szFunction, DWORD dwError)
{
    char szMessage[256];
    wsprintfA(szMessage, "%s failed with error %d", szFunction, dwError);
    MessageBoxA(nullptr, szMessage, "Error", MB_ICONERROR);

    if (pIPCData)
        pIPCData->Status = IPCStatus::Error;

    return FALSE;
}

// 消息框线程函数0
static DWORD WINAPI MessageBoxThreadHookWarn(LPVOID) {
    MessageBoxA(nullptr, "帧数读取钩子安装失败。可能导致10612-4001错误", "警告", MB_ICONWARNING);
    return 0;
}

// 消息框线程函数1
static DWORD WINAPI MessageBoxThreadHookOK(LPVOID) {
    MessageBoxA(nullptr, "帧数读取钩子安装成功!", "Notification", MB_ICONINFORMATION);
    return 0;
}

static void InstallGenshinFrameHook()
{
    // 检查全局条件
    if (!IsStarRail)
    {
        // 尝试安装钩子
        if (!InstallFrameReadHook())
        {
            // 安装失败
            OutputDebugStringA("30lau:Framehook installed failed!");
            CreateThread(nullptr, 0, MessageBoxThreadHookWarn, nullptr, 0, nullptr);
        }
        else
        {
            // 安装成功
            OutputDebugStringA("30lau:Framehook installed successfully!");
            //CreateThread(nullptr, 0, MessageBoxThreadHookOK, nullptr, 0, nullptr);
        }
    }
    else
    {
        // 可选：如果 IsStarRail 为 true，也可以输出一条调试信息
        OutputDebugStringA("30lau:Framehook installation skipped because IsStarRail is true.");
    }
}

DWORD GetPriorityClassFromStatus(int status) {
    DWORD priorityClass;
    switch (status) {
    case 0: return REALTIME_PRIORITY_CLASS;
    case 1: return HIGH_PRIORITY_CLASS;
    case 2: return ABOVE_NORMAL_PRIORITY_CLASS;
    case 3: return NORMAL_PRIORITY_CLASS;
    case 4: return BELOW_NORMAL_PRIORITY_CLASS;
    case 5: return IDLE_PRIORITY_CLASS;
    default:
        OutputDebugStringA("30lau Warning: Invalid ProcessPriorityStatus value, defaulting to NORMAL.\n");
        return NORMAL_PRIORITY_CLASS;
    }
}


// 260117 解析原神高级工具掩码
// 定义配置结构体
struct GenshinAdvancedConfig {
    bool EnableImmediateOpenTeam;
    bool HideQuestBanner;
    bool DisableShowDamageText;
    bool DisableEventCameraMove;
    bool RedirectCombineEntry;
	bool HideGenshinUID;
	bool RedirectCombineEntryKey;
    bool EnableRemoveGrass;
};

// 解析掩码
GenshinAdvancedConfig ParseGenshinConfigMask(int configMask) {
    GenshinAdvancedConfig config;

    config.EnableImmediateOpenTeam = (configMask & (1 << 4)) != 0;
    config.HideQuestBanner = (configMask & (1 << 0)) != 0;
    config.DisableShowDamageText = (configMask & (1 << 1)) != 0;
    config.DisableEventCameraMove = (configMask & (1 << 3)) != 0;
    config.RedirectCombineEntry = (configMask & (1 << 5)) != 0;
	config.HideGenshinUID = (configMask & (1 << 6)) != 0;
	config.RedirectCombineEntryKey = (configMask & (1 << 7)) != 0;
    config.EnableRemoveGrass = (configMask & (1 << 8)) != 0;

    return config;
}

void ReplaceOptionValue(std::string& config, const std::string& optionSection, const std::string& newValue) {
    size_t pos = config.find(optionSection);
    if (pos != std::string::npos) {
        size_t valuePos = config.find("Value =", pos);
        if (valuePos != std::string::npos) {
            // 找到下一个section的位置，如果没有就用字符串末尾
            size_t nextOptionPos = config.find("[Option_", pos + 1);
            if (nextOptionPos == std::string::npos) {
                nextOptionPos = config.length(); // 使用字符串末尾
            }

            // 确保valuePos在当前section内
            if (valuePos < nextOptionPos) {
                size_t valueStart = valuePos + 8; // "Value = " 的长度
                size_t valueEnd = config.find("\n", valueStart);

                // 如果找不到换行符，使用字符串末尾
                if (valueEnd == std::string::npos || valueEnd > nextOptionPos) {
                    valueEnd = nextOptionPos;
                }

                // 去除末尾可能的空白字符
                while (valueEnd > valueStart && (config[valueEnd - 1] == ' ' || config[valueEnd - 1] == '\r')) {
                    valueEnd--;
                }

                config.replace(valueStart, valueEnd - valueStart, newValue);
            }
        }
    }
}

// 根据掩码更新配置
std::string UpdateConfigByMask(const char* originalConfig, int configMask) {
    GenshinAdvancedConfig config = ParseGenshinConfigMask(configMask);
    std::string updatedConfig = originalConfig;
    ReplaceOptionValue(updatedConfig, "[Option_6]", config.EnableImmediateOpenTeam ? "True" : "False");
    ReplaceOptionValue(updatedConfig, "[Option_7]", config.HideQuestBanner ? "True" : "False");
    ReplaceOptionValue(updatedConfig, "[Option_8]", config.DisableShowDamageText ? "True" : "False");
    ReplaceOptionValue(updatedConfig, "[Option_10]", config.DisableEventCameraMove ? "True" : "False");
    ReplaceOptionValue(updatedConfig, "[Option_11]", config.RedirectCombineEntry ? "True" : "False");
    ReplaceOptionValue(updatedConfig, "[Option_12]", config.HideGenshinUID ? "True" : "False");
    ReplaceOptionValue(updatedConfig, "[Option_13]", config.RedirectCombineEntryKey ? "True" : "False");
    ReplaceOptionValue(updatedConfig, "[Option_14]", config.EnableRemoveGrass ? "True" : "False");
    return updatedConfig;
}

// 260117 CLibrary.dll UpdateConfig函数类型定义
typedef int (*UpdateConfigFunc)(char*);

typedef int (*UpdateConfigFunca)(char*);

const char* configinitialize = R"([General]
            Name = FPS-UNLOCKER
            Description = 一个用于解锁帧数限制的工具
            Developer = a
            DllName = fps.dll
            Version = 6.2.0+
            MD5 = xxxxxx

            [Option_1]
            Name = DelayTime
            Type = Int
            Value = 300

            [Option_2]
            Name = EnableUnlockFPS
            Type = Bool
            Value = False

            [Option_3]
            Name = Fps
            Type = Int
            Value = 120

            [Option_4]
            Name = EnableCustomFOV
            Type = Bool
            Value = False

            [Option_5]
            Name = FOV
            Type = Float
            Value = 0.0

            [Option_6]
            Name = EnableImmediateOpenTeam
            Type = Bool
            Value = False

            [Option_7]
            Name = HideQuestBanner
            Type = Bool
            Value = False

            [Option_8]
            Name = DisableShowDamageText
            Type = Bool
            Value = False

            [Option_9]
            Name = UsingTouchScreen
            Type = Bool
            Value = False

            [Option_10]
            Name = DisableEventCameraMove
            Type = Bool
            Value = False

            [Option_11]
            Name = RedirectCombineEntry
            Type = Bool
            Value = False

            [Option_12]
            Name = HideGenshinUID
            Type = Bool
            Value = False
            
            [Option_13]
            Name = RedirectCombineEntryKey
            Type = Bool
            Value = False

            [Option_14]
            Name = EnableRemoveGrass
            Type = Bool
            Value = False)";

void WriteToLogFile(const char* filename, const char* content)
{
    std::ofstream logFile(filename, std::ios::app);
    if (logFile.is_open())
    {
        logFile << content << std::endl;
        logFile.close();
    }
}


DWORD __stdcall ThreadProc(LPVOID lpParameter)
{
    const auto hModule = static_cast<HMODULE>(lpParameter);
    LdrAddRefDll(1, hModule);

    //constexpr auto szGuid = "BDA1DCAE-96C9-4B4A-A5A4-A43DC86DB253";
    auto _genshin_guid = XorString::decrypt(encrypted_strings::genshin_guid.data(), encrypted_strings::genshin_guid.size());
    auto _starrail_guid = XorString::decrypt(encrypted_strings::starrail_guid.data(), encrypted_strings::starrail_guid.size());
	//根据游戏类型选择GUID
    if (IsStarRail) 
    {
        szGuid = _starrail_guid.c_str();
    }
    else
    {
        szGuid = _genshin_guid.c_str();
    }

    const auto hMapFile = HandleGuard(OpenFileMappingA(FILE_MAP_READ | FILE_MAP_WRITE, FALSE, szGuid), CloseHandle);
    if (!hMapFile)
        return OnWinError("OpenFileMapping", GetLastError());

    const auto lpView = MappedMemoryGuard(MapViewOfFile(hMapFile, FILE_MAP_READ | FILE_MAP_WRITE, 0, 0, 0), UnmapViewOfFile);
    if (!lpView)
        return OnWinError("MapViewOfFile", GetLastError());

    pIPCData = static_cast<IPCData*>(lpView.Get());
    const auto pFpsValue = reinterpret_cast<int*>(pIPCData->Address);

    // 检查地址有效性
    MEMORY_BASIC_INFORMATION mbi{};
    if (!VirtualQuery(pFpsValue, &mbi, sizeof(mbi)))
        return OnWinError("VirtualQuery", GetLastError());

    if (mbi.Protect != PAGE_READWRITE)
        return OnWinError("VirtualQuery", ERROR_INVALID_ADDRESS);

    pIPCData->Status = IPCStatus::ClientReady;


    //if (!IsStarRail)
    //{
    //    // 安装帧数读取钩子
    //    if (!InstallFrameReadHook()) {
    //        // 创建独立线程显示消息框（不阻塞主线程）
    //        OutputDebugStringA("Framehook installed failed!");
    //        CreateThread(nullptr, 0, MessageBoxThreadHookWarn, nullptr, 0, nullptr);
    //    }
    //    else
    //    {
    //        OutputDebugStringA("Framehook installed successfully!");
    //        CreateThread(nullptr, 0, MessageBoxThreadHookOK, nullptr, 0, nullptr);
    //    }
    //}

    static int lastProcessPriorityStatus = -10;



    // 260117
    // 获取高级设置插件 DLL 句柄
    HMODULE h_dvgenshinDll = GetModuleHandleA("genshin_advan_tol.dll");
    UpdateConfigFunc updateConfig = nullptr;

    if (h_dvgenshinDll) {
        // 获取函数地址
        updateConfig = (UpdateConfigFunc)GetProcAddress(h_dvgenshinDll, "UpdateConfig");
        if (!updateConfig) {

        }
        else {
            int result = updateConfig(const_cast<char*>(configinitialize));
            Sleep(500);
			// 启动fov监控线程260131
            CreateThread(nullptr, 0, monitor_fov_loop_execution, nullptr, 0, nullptr);
			// 根据高级设置是否启用FOV调整功能来监控游戏状态260131
            g_advantols_need_enablefov_test = true;
        }
    }
    else {

    }


    //// 260307
    //// 获取a的获取高级设置插件 DLL 句柄
    //HMODULE h_dvgenshinDll_a = GetModuleHandleA("aadv_addon.dll");
    //UpdateConfigFunca updateConfig_a = nullptr;

    //if (h_dvgenshinDll_a) {
    //    // 获取函数地址
    //    updateConfig_a = (UpdateConfigFunca)GetProcAddress(h_dvgenshinDll_a, "UpdateConfig");
    //    if (!updateConfig_a) {

    //    }
    //    else {
    //        int result_a = updateConfig_a(const_cast<char*>(configinitialize));
    //        Sleep(500);
    //    }
    //}
    //else {

    //}




    while (pIPCData->Status != IPCStatus::HostExit)
    {
		//打印当前PreventIllegalTool状态
        int currentPreventStatus = pIPCData->PreventIllegalToolStatus;
        std::string statusString = std::to_string(currentPreventStatus);
        std::string fullMessage = "30lau PreventIllegalTool:" + statusString;
        //OutputDebugStringA(fullMessage.c_str());

        //打印当前ProcessPriorityStatus状态
        int currentPriorityStatus = pIPCData->ProcessPriorityStatus;
        std::string statusString1 = std::to_string(currentPriorityStatus);
        std::string fullMessage1 = "30lau Genshin ProcessPriorityStatus:" + statusString1;
        //OutputDebugStringA(fullMessage1.c_str());
        //SetPriorityClass(GetCurrentProcess(), REALTIME_PRIORITY_CLASS);
        //if (pIPCData->ProcessPriorityStatus != -1) 
        //{
        //    SetPriorityClass(GetCurrentProcess(), GetPriorityClassFromStatus(pIPCData->ProcessPriorityStatus));
        //}

        if (pIPCData->ProcessPriorityStatus != -1 && IsStarRail == false)
        {
            // 只有当状态值发生变化时才设置优先级
            if (pIPCData->ProcessPriorityStatus != lastProcessPriorityStatus)
            {
                SetPriorityClass(GetCurrentProcess(), GetPriorityClassFromStatus(pIPCData->ProcessPriorityStatus));
                OutputDebugStringA(fullMessage1.c_str());
                lastProcessPriorityStatus = pIPCData->ProcessPriorityStatus; // 更新保存的状态值
            }
        }

        if (pIPCData->ProcessPriorityStatus != -1 && IsStarRail == true)
        {
            // 只有当状态值发生变化时才设置优先级
            if (pIPCData->ProcessPriorityStatus != lastProcessPriorityStatus)
            {
                SetPriorityClass(GetCurrentProcess(), GetPriorityClassFromStatus(pIPCData->ProcessPriorityStatus));
                std::string fullMessage2 = "30lau StarRail ProcessPriorityStatus:" + statusString1;
                OutputDebugStringA(fullMessage2.c_str());
                lastProcessPriorityStatus = pIPCData->ProcessPriorityStatus; // 更新保存的状态值
            }
        }



    //    if(currentPreventStatus == 1)
    //    {
    //        if(IsInstalledFrameHook==false)
    //        {
    //            InstallGenshinFrameHook();
				//IsInstalledFrameHook = true;
    //        }
    //        *pFpsValue = pIPCData->Value;
    //    }
    //    else if (currentPreventStatus == 2)
    //    {
    //        *pFpsValue = pIPCData->Value;
    //    }

        //251106 ChangeFOVforGenshin
        int currentFOVgenshinValue = pIPCData->FOVgenshinValue1;
        int currentFOVgenshinStatus = pIPCData->FOVgenshinStatus1;

		//251122 ChangeFOVforGenshinFIX选项
        int currentFOVgenshinFixStatus = pIPCData->FOVgenshinStatusFix;

        if (currentFOVgenshinStatus == 0)
        {
            //OutputDebugStringA("IPC FOV功能未初始化333");
        }


		// 如果用户开启了高级设置，则开启FOV功能用于监控游戏状态260131
        if (g_advantols_need_enablefov_test == true && IsInstalledFovHook == false)
        {
            // 如果用户开启了高级设置，则先关闭FOV功能，若用户开启了FOV调整功能，则currentFOVgenshinStatus后续会重新开启260201
            g_bEnableFOVHook = false;

            IsInstalledFovHook = true;
            //OutputDebugStringA("已开启FOV功能333");
            StartFovInstallerThread();
        }


        if (currentFOVgenshinStatus == 1 && IsInstalledFovHook == false)
        {
			IsInstalledFovHook = true;
            //OutputDebugStringA("已开启FOV功能333");
            StartFovInstallerThread();
        }


        if (currentFOVgenshinStatus == 1)
        {
			g_bEnableFOVHook = true;
        }

        if (currentFOVgenshinStatus == 2)
        {
			g_bEnableFOVHook = false;
        }

        if (currentFOVgenshinValue != 1000)
        {
            FovValue = currentFOVgenshinValue;
            //// 将FovValue转换为字符串
            //std::string fovStr = std::to_string(FovValue);
            //OutputDebugStringA(fovStr.c_str());
            if (currentFOVgenshinValue == 45) 
            {
                g_bEnableFOVHook = false;
            }
        }

		//251122 ChangeFOVforGenshinFIX选项
        if (currentFOVgenshinFixStatus != 1000)
        {
            if(currentFOVgenshinFixStatus == 1)
            {
                g_bEnableFOVFix = false;
            }
            if (currentFOVgenshinFixStatus == 2)
            {
                g_bEnableFOVFix = true;
            }
        }

        //251207 DisableGenshinBlur
        int currentDisableGenshinBlurStatus = pIPCData->DisableGenshinBlurStatus;
        if (currentDisableGenshinBlurStatus == 1)
        {
            _isinstalled_disable_genshin_blur_hook = true;
            g_hookplayer_perspective = false;
        }
        if (currentDisableGenshinBlurStatus == 2 && _isinstalled_disable_genshin_blur_hook == false)
        {
            if (!InstallPlayerPerspectiveHook())
            {
                CreateThread(nullptr, 0, MessageBoxThreadPlayerPerspectiveHookWarn, nullptr, 0, nullptr);
            }
            _isinstalled_disable_genshin_blur_hook = true;
        }
        if (currentDisableGenshinBlurStatus == 2)
        {
            g_hookplayer_perspective = true;
        }

		//251231 DisplayGenshinFog
        int currentDisplayGenshinFogStatus = pIPCData->DisplayGenshinFogStatus;
        if (currentDisplayGenshinFogStatus == 1)
        {
            _isinstalled_display_genshin_fog_hook = true;
            g_hook_display_fog = false;
        }
        if (currentDisplayGenshinFogStatus == 2 && _isinstalled_display_genshin_fog_hook == false)
        {
            if (!InstallDisplayGenshinFogHook())
            {
                CreateThread(nullptr, 0, MessageBoxThreadDisplayGenshinFogHookWarn, nullptr, 0, nullptr);
            }
            _isinstalled_display_genshin_fog_hook = true;
        }
        if (currentDisplayGenshinFogStatus == 2)
        {
            g_hook_display_fog = true;
        }

        // 260102 
        if (currentPreventStatus == 1)
        {
            if (IsInstalledFrameHook == false)
            {
                InstallGenshinFrameHook();
                _frame_fix_mode = false;
                IsInstalledFrameHook = true;
            }
            *pFpsValue = pIPCData->Value;
        }
        else if (currentPreventStatus == 2)
        {
            *pFpsValue = pIPCData->Value;
        }
        else if (currentPreventStatus == 3)
        {
            if (IsInstalledFrameHook == false)
            {
                InstallGenshinFrameHook();
                _frame_fix_mode = true;
                IsInstalledFrameHook = true;
            }
            *pFpsValue = pIPCData->Value;
            _isenabled_frame_fix = true;
			fpsvalue1 = pIPCData->Value;
        }
        else if (currentPreventStatus == 4)
        {
            *pFpsValue = pIPCData->Value;
            _isenabled_frame_fix = false;
        }
        else if (currentPreventStatus == 5)
        {
            if (IsInstalledFrameHook == false)
            {
                InstallGenshinFrameHook();
                _frame_fix_mode = false;
                IsInstalledFrameHook = true;
                _frame_fix_mode_v2 = true;
            }
            *pFpsValue = pIPCData->Value;
        }

        ////260115
        //if (!_isinstallframehook6) {
        //    InstallFrameHook();
        //    _isinstallframehook6 = true;
        //}


        // 260117    
        // 完整使用示例
        if (pIPCData->GenshinAdvanceToolMask != -1)
        {
            static int lastMask = -1;  // 静态变量保存上一次的值
            int ysconfigMask = pIPCData->GenshinAdvanceToolMask;

            // 首次执行或值发生变化时才执行
            if (lastMask != ysconfigMask)
            {
                // 根据掩码更新配置
                std::string updatedConfig = UpdateConfigByMask(configinitialize, ysconfigMask);

                // 调用更新函数
                if (updateConfig != nullptr)
                {
                    int result = updateConfig(const_cast<char*>(updatedConfig.c_str()));
                }

                // 更新lastMask为当前值
                lastMask = ysconfigMask;

                //std::string fullMessage277 = "30lau GenshinAdvancedToolMask:" + std::to_string(ysconfigMask);
                //OutputDebugStringA(fullMessage277.c_str());

                // 使用
                //WriteToLogFile("debuggenshin.log", updatedConfig.c_str());
            }


            // 决定使用前是否去除草地，草地不支持热更新 260225
            //std::string grassStatusMsg = "[DLL] 666 30lau EnableRemoveGrass Status: " + std::string(isEnableRemoveGrass ? "True" : "False");
            // 输出调试信息
            //OutputDebugStringA(grassStatusMsg.c_str());

            if (g_need_removegrass == -1)
            {
                bool isEnableRemoveGrass = (ysconfigMask & (1 << 8)) != 0;
                if (isEnableRemoveGrass)
                {
                    g_need_removegrass = 2;
                }
                else
                {
                    g_need_removegrass = 1;
                }
            }
        }


        //*pFpsValue = pIPCData->Value;
        Sleep(62);
    }



    //while (pIPCData->Status != IPCStatus::HostExit)
    //{
    //    *pFpsValue = pIPCData->Value;
    //    Sleep(62);
    //}



    //while (pIPCData->Status != IPCStatus::HostExit)
    //{
    //    *pFpsValue = pIPCData->Value;

    //    //OutputDebugStringA("full");

    //    // 1. 读取共享内存中的状态
    //    bool currentPreventStatus = pIPCData->PreventIllegalTool;

    //    // 2. 检查状态是否发生变化
    //    if (currentPreventStatus != lastPreventStatus)
    //    {
    //        // 3. 构建消息字符串
    //        std::string message = "PreventIllegalTool status changed to: ";
    //        message += (currentPreventStatus ? "Enabled (true)" : "Disabled (false)");

    //        // 4. 弹出MessageBox（仅一次）
    //        MessageBoxA(nullptr, message.c_str(), "Status Change", MB_OK | MB_ICONINFORMATION);

    //        // 5. 更新上一次的状态
    //        lastPreventStatus = currentPreventStatus;
    //    }

    //    // 6. 构建调试信息字符串（仍然每次循环都输出到调试器）
    //    std::string debugMessage = "PreventIllegalTool Status: ";
    //    debugMessage += (currentPreventStatus ? "Enabled (true)" : "Disabled (false)");
    //    debugMessage += " | Target FPS: " + std::to_string(pIPCData->Value) + "\n";
    //    std::string fullMessage = "123" + debugMessage;
    //    OutputDebugStringA(fullMessage.c_str());

    //    Sleep(62);
    //}



    pIPCData->Status = IPCStatus::ClientExit;
    return 0;
}

BOOL __stdcall DllMain(HINSTANCE hInstance, DWORD fdwReason, LPVOID lpReserved)
{
    if (hInstance)
        DisableThreadLibraryCalls(hInstance);

    //if (!GetModuleHandleA("mhypbase.dll"))
    //    return TRUE;

    //if (!GetModuleHandleA("mhypbase.dll") && !GetModuleHandleA("starrail.exe"))
    //{
    //    return TRUE;
    //}


    if (GetModuleHandleA("mhypbase.dll") != NULL)
    {
        //szGuid = "727B2975-0BB3-022D-AB4B-54BEB6A6C687";

        // 如果mhypbase存在，那么检查starrail
        if (GetModuleHandleA("starrail.exe") != NULL)
        {
            //szGuid = "741E764D-0EB9-EE23-BC9B-EEE71F0D64C1";
            IsStarRail = true;
        }
    }
    else
    {
        // mhypbase不存在
        if (GetModuleHandleA("starrail.exe") != NULL)
        {
            //szGuid = "741E764D-0EB9-EE23-BC9B-EEE71F0D64C1";
            IsStarRail = true;
        }
        else
        {
            return TRUE; // 两个都不存在，返回TRUE
        }
    }


    if (fdwReason == DLL_PROCESS_ATTACH)
    {
        //// 安装帧数读取钩子
        //if (!InstallFrameReadHook()) {
        //    MessageBoxA(nullptr, "Failed to install frame read hook", "Error", MB_ICONERROR);
        //}

        //// 设置进程优先级为Idle
        //SetPriorityClass(GetCurrentProcess(), IDLE_PRIORITY_CLASS);

        const auto hThread = CreateThread(nullptr, 0, ThreadProc, hInstance, 0, nullptr);
        if (!hThread)
            return OnWinError("CreateThread", GetLastError());

        CloseHandle(hThread);
    }
    else if (fdwReason == DLL_PROCESS_DETACH)
    {
        // 禁用所有钩子
        MinHookManager::DisableAllHooks();
    }

    return TRUE;
}

EXTERN_C __declspec(dllexport) LRESULT __stdcall WndProc(int code, WPARAM wParam, LPARAM lParam)
{
    return CallNextHookEx(nullptr, code, wParam, lParam);
}