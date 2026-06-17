using Microsoft.Win32;
using System;
using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using UnlockFps.Logging;
using UnlockFps.Utils;
using Windows.Win32.System.Threading;

using static Windows.Win32.PInvoke;
using PROCESS_INFORMATION = Windows.Win32.System.Threading.PROCESS_INFORMATION;

namespace UnlockFps.Services;

[SupportedOSPlatform("windows5.1.2600")]
public class ProcessService
{
    private uint _gameProcessId;

    private static readonly ILogger Logger = LogManager.GetLogger(nameof(ProcessService));

    private readonly Config _config;
    private int _lastProcessId;

    public ProcessService(ConfigService configService)
    {
        _config = configService.Config;
    }

    // 251230 高级设置启用标志
    private bool _isenableGenshinAdvancedSet = false;

    //// 260117 快捷打开合成台页面
    //private OpenCraftPage _openCraftPage;

    public void Start(bool ifManualLaunch, int? manualModepid = null)
    {
        //if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        //{
        //    throw new PlatformNotSupportedException("Only windows or wine is supported.");
        //}

        //var runningProcess = Process.GetProcesses()
        //    .FirstOrDefault(x => Array.IndexOf(GameConstants.GameNames, x.ProcessName) != -1);

        //if (runningProcess is not null)
        //{
        //    throw new Exception("An instance of the game is already running: " + runningProcess.Id);
        //}

        //var launchOptions = _config.LaunchOptions;
        //using var disposable = CreateProcessRaw(launchOptions, out var lpProcessInformation);


        if (_config.UseHDRGenshin)
        {
            var subKeyName = Path.GetFileName(_config.LaunchOptions.GamePath) == "YuanShen.exe" ? "原神" : "Genshin Impact";
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey($@"Software\miHoYo\{subKeyName}");
                key.SetValue("WINDOWS_HDR_ON_h3132281285", 1);
            }
            catch (Exception e)
            {
                //MessageBox.Show($@"Failed to enable HDR: {e.Message}", @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw new Exception("Failed to enable HDR: "+e.Message);
            }
        }


        //开启原神移动UI 251209
        bool _isreadyGenshinMbUIWt = false;
        string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        string mobileuiexe = Path.Combine(appDirectory, "wTE_ysr251210b.exe");


        if (_config.EnableGenshinMbUIWt)
        {
            //try
            //{
            //    // 先杀掉旧进程
            //    var processes = Process.GetProcessesByName("wTE_ysr251207a");
            //    foreach (var process in processes)
            //    {
            //        process.Kill();
            //        process.WaitForExit();
            //    }
            //}
            //catch (Exception ex)
            //{
            //    throw new Exception($"Kill {mobileuiexe} Error: {ex.Message}");
            //}

            if (File.Exists(mobileuiexe))
            {
                //// ⭐ 先创建事件（使用 OpenOrCreate 模式）
                //EventWaitHandle readyEvent = new EventWaitHandle(
                //    false,
                //    EventResetMode.ManualReset,
                //    "Global\\StarRailMobileUI_Ready251206XQX");

                //// 重置事件状态，确保是未触发状态
                //readyEvent.Reset();

                // 然后启动进程
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = mobileuiexe,
                    Arguments = "-genshin",
                    UseShellExecute = false
                };

                Process process = Process.Start(startInfo);

                //if (!ifManualLaunch)
                //{
                //    // 等待事件信号（最多30秒）
                //    if (readyEvent.WaitOne(TimeSpan.FromSeconds(30)))
                //    {
                //        //Console.WriteLine("Genshin Mobile UI initialized successfully.");
                //        _isreadyGenshinMbUIWt = true;
                //    }
                //    else
                //    {
                //        throw new Exception("Timeout waiting for process to initialize");
                //    }
                //    readyEvent.Dispose();
                //}
            }
        }
        //if (_isreadyGenshinMbUIWt && !ifManualLaunch)
        //{
        //    // 等待，确保移动UI完全准备好
        //    Thread.Sleep(300);
        //}



        //old code for genshin mobile ui
        //string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        //string mobileuiexe = Path.Combine(appDirectory, "wTE_ysr251207a.exe");

        //if (_config.EnableGenshinMbUIWt)
        //{
        //    try
        //    {
        //        var processes = Process.GetProcessesByName("wTE_ysr251207a");
        //        foreach (var process in processes)
        //        {
        //            process.Kill();
        //            process.WaitForExit(); // 等待进程完全退出
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        throw new Exception($"Kill {mobileuiexe} Error: {ex.Message}");
        //    }

        //    // 验证 mobileuiexe 是否在该目录下
        //    if (File.Exists(mobileuiexe))
        //    {
        //        string arguments = "-genshin";
        //        Process.Start(mobileuiexe, arguments);
        //    }
        //    else
        //    {
        //        throw new Exception($"File not found: {mobileuiexe}");
        //    }
        //}


        PROCESS_INFORMATION lpProcessInformation = default;
        LaunchOptions launchOptions = null;
        IDisposable disposable = null; // 声明disposable变量

        if (!ifManualLaunch)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new PlatformNotSupportedException("Only windows or wine is supported.");
            }

            var runningProcess = Process.GetProcesses()
                .FirstOrDefault(x => Array.IndexOf(GameConstants.GameNames, x.ProcessName) != -1);

            if (runningProcess is not null)
            {
                throw new Exception("An instance of the game is already running: " + runningProcess.Id);
            }

            launchOptions = _config.LaunchOptions;
            disposable = CreateProcessRaw(launchOptions, out lpProcessInformation);
        }


        //if (ifManualLaunch)
        //{
        //    // 手动模式：附加到已存在的游戏进程（而不是 CreateProcess）
        //    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        //    {
        //        throw new PlatformNotSupportedException("Only windows or wine is supported.");
        //    }

        //    var runningProcess = Process.GetProcesses()
        //        .FirstOrDefault(x => Array.IndexOf(GameConstants.GameNames, x.ProcessName) != -1);

        //    if (runningProcess is null)
        //    {
        //        throw new Exception("Manual start requested but target game process was not found.");
        //    }

        //    // 填充 PROCESS_INFORMATION，注意 hThread 在附加场景下不可用（置为 IntPtr.Zero）
        //    lpProcessInformation = default;
        //    lpProcessInformation.hProcess = (Windows.Win32.Foundation.HANDLE)runningProcess.Handle;
        //    lpProcessInformation.hThread = (Windows.Win32.Foundation.HANDLE)IntPtr.Zero;
        //    lpProcessInformation.dwProcessId = (uint)runningProcess.Id;

        //    launchOptions = _config.LaunchOptions;
        //}


        if (ifManualLaunch)
        {
            // 手动模式：使用传入的 PID（manualModepid）或回退到按进程名查找已存在的游戏进程
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new PlatformNotSupportedException("Only windows or wine is supported.");
            }

            Process runningProcess = null;

            if (manualModepid.HasValue)
            {
                try
                {
                    runningProcess = Process.GetProcessById(manualModepid.Value);
                }
                catch (ArgumentException)
                {
                    throw new Exception($"Manual start requested but process with PID {manualModepid.Value} was not found.");
                }
            }
            else
            {
                runningProcess = Process.GetProcesses()
                    .FirstOrDefault(x => Array.IndexOf(GameConstants.GameNamesSR, x.ProcessName) != -1);

                if (runningProcess is null)
                {
                    throw new Exception("Manual start requested but target game process was not found.");
                }
            }

            // 填充 PROCESS_INFORMATION，注意 hThread 在附加场景下不可用（置为 IntPtr.Zero）
            lpProcessInformation = default;
            lpProcessInformation.hProcess = (Windows.Win32.Foundation.HANDLE)runningProcess.Handle;
            lpProcessInformation.hThread = (Windows.Win32.Foundation.HANDLE)IntPtr.Zero;
            lpProcessInformation.dwProcessId = (uint)runningProcess.Id;
            lpProcessInformation.dwThreadId = 0;
            launchOptions = _config.LaunchOptions;
        }


        //123456
        //launchOptions = _config.LaunchOptions;

        // 保存进程 ID
        _gameProcessId = lpProcessInformation.dwProcessId;

    
        // 251230 新增高级设置 DLL 注入
        if (launchOptions.EnableGensinAdvancedSet == true)
        {
            string appPath22 = AppDomain.CurrentDomain.BaseDirectory;
            //string dllPath22 = Path.Combine(appPath22, @"ulk_ysr_tools\adv_addon.dll");
            string dllPath22 = Path.Combine(appPath22, @"ulk_ysr_tools\genshin_advan_tol.dll");
            var dllList2 = new List<string> { dllPath22 };
            // 初始化LauncherPro.dll
            //UpdateGameConfiguration(-1);

            _isenableGenshinAdvancedSet = true;

            if (!ProcessUtils.InjectDlls(lpProcessInformation.hProcess, dllList2))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    $"Dll Injection failed. ({Marshal.GetLastPInvokeErrorMessage()})");
            }
        }


        // 260202 原神Fog修改DLL注入
        if (_config.DisplayGenshinFog == true)
        {
            // 在注入Fog DLL之前创建共享内存
            IpcServiceSRFov.SharedMemoryWriter.Start();


            string appPath23 = AppDomain.CurrentDomain.BaseDirectory;
            string dllPath23 = Path.Combine(appPath23, @"UtilityUlk1.dll");
            var dllList23 = new List<string> { dllPath23 };


            //_isenableGenshinAdvancedSet = true;

            if (!ProcessUtils.InjectDlls(lpProcessInformation.hProcess, dllList23))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    $"Dll Injection failed. ({Marshal.GetLastPInvokeErrorMessage()})");
            }

            IpcServiceSRFov.SharedMemoryWriter.UpdateOnce(datagameset =>
            {
                //1=disable 2=enable
                datagameset.GenshinFog = 2;
                return datagameset;
            });
        }


        //string appPath22 = AppDomain.CurrentDomain.BaseDirectory;
        //string dllPath22 = Path.Combine(appPath22, @"ulk_ysr_tools\advantol_1.dll");
        //if (!launchOptions.DllList.Contains(dllPath22))
        //{
        //    launchOptions.DllList.Add(dllPath22);
        //}



        if (!ProcessUtils.InjectDlls(lpProcessInformation.hProcess, launchOptions.DllList))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                $"Dll Injection failed. ({Marshal.GetLastPInvokeErrorMessage()})");
        }

        //if (launchOptions.SuspendLoad && !ifManualLaunch && !_config.EnableGenshinMbUIWt)
        //{
        //    var retCode = ResumeThread(lpProcessInformation.hThread);
        //    if (retCode == 0xFFFFFFFF)
        //    {
        //        throw new Win32Exception(Marshal.GetLastWin32Error(),
        //            $"ResumeThread failed. ({Marshal.GetLastPInvokeErrorMessage()})");
        //    }
        //}

        //251230
        if (launchOptions.SuspendLoad || launchOptions.EnableGensinAdvancedSet)
        {
            if (!ifManualLaunch && !_config.EnableGenshinMbUIWt)
            {
                var retCode = ResumeThread(lpProcessInformation.hThread);
                if (retCode == 0xFFFFFFFF)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(),
                        $"ResumeThread failed. ({Marshal.GetLastPInvokeErrorMessage()})");
                }
            }
        }


        _lastProcessId = (int)lpProcessInformation.dwProcessId;

        // 调用实例方法
        //gameRunning(_gameProcessId);
        ShouldGenshinExit = false;

        Task.Run(() =>
        {
            gameRunning(_gameProcessId);
        });
    }

    private static unsafe IDisposable CreateProcessRaw(LaunchOptions launchOptions, out PROCESS_INFORMATION lpProcessInformation)
    {
        var lpCurrentDirectory = Path.GetDirectoryName(launchOptions.GamePath);
        var commandLine = BuildCommandLine(launchOptions);
        var lpStartupInfo = new STARTUPINFOW();
        //var dwCreationFlags = launchOptions.SuspendLoad ? PROCESS_CREATION_FLAGS.CREATE_SUSPENDED : default;

        // 251230 当 SuspendLoad 或 EnableGensinAdvancedSet 为 true 时都使用挂起模式
        var dwCreationFlags = default(PROCESS_CREATION_FLAGS);
        if (launchOptions.SuspendLoad || launchOptions.EnableGensinAdvancedSet)
        {
            dwCreationFlags |= PROCESS_CREATION_FLAGS.CREATE_SUSPENDED;
        }

        var array = ArrayPool<char>.Shared.Rent(commandLine.Length + 1);
        try
        {
            var lpCommandLine = new Span<char>(array, 0, commandLine.Length + 1);
            commandLine.CopyTo(lpCommandLine);
            lpCommandLine[^1] = '\0';

            //if (!CreateProcess(launchOptions.GamePath, ref lpCommandLine,
            //        default, default, false,
            //        dwCreationFlags, default, lpCurrentDirectory,
            //        in lpStartupInfo, out lpProcessInformation))
            //{
            //    throw new Win32Exception(Marshal.GetLastWin32Error(),
            //        $"CreateProcess failed. ({Marshal.GetLastPInvokeErrorMessage()})");
            //}

            if (!CreateProcess(launchOptions.GamePath, ref lpCommandLine,
                    default, default, false,
                    dwCreationFlags, default, lpCurrentDirectory,
                    in lpStartupInfo, out lpProcessInformation))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    $"CreateProcess failed. ({Marshal.GetLastPInvokeErrorMessage()})");
            }



            // 打印运行参数
            //Console.WriteLine($"启动参数: {commandLine}");

            //// 为 lpProcessInformation 赋默认值（所有字段为零）
            //lpProcessInformation = default;

            //Console.WriteLine("1234");
        }
        finally
        {
            ArrayPool<char>.Shared.Return(array);
        }

        return new ThreadGuard(lpProcessInformation.hThread);
    }

    private static string BuildCommandLine(LaunchOptions launchOptions)
    {
        var commandLine = new StringBuilder($"{launchOptions.GamePath} ");
        if (launchOptions.IsWindowBorderless)
        {
            commandLine.Append("-popupwindow ");
        }

        if (launchOptions.UseCustomResolution)
        {
            commandLine.Append(
                $"-screen-width {launchOptions.CustomResolutionX} -screen-height {launchOptions.CustomResolutionY} ");
        }

        commandLine.Append($"-screen-fullscreen {(launchOptions.Fullscreen ? 1 : 0)} ");
        if (launchOptions.Fullscreen)
        {
            commandLine.Append($"-window-mode {(launchOptions.IsExclusiveFullscreen ? "exclusive" : "borderless")} ");
        }

        if (launchOptions.UseMobileUI)
        {
            commandLine.Append("use_mobile_platform -is_cloud 1 -platform_type CLOUD_THIRD_PARTY_MOBILE ");
        }

        commandLine.Append($"-monitor {launchOptions.MonitorId} ");
        return commandLine.ToString();
    }

    public void KillLastProcess()
    {
        try
        {
            var process = Process.GetProcessById(_lastProcessId);
            if (Array.IndexOf(GameConstants.GameNames, process.ProcessName) != -1)
            {
                process.Kill();
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Kill process failed");
        }
    }

    public static event EventHandler GameExitRequested;
    //private static bool _shouldgameexit;
    public static bool ShouldGenshinExit = true;
    // 修改gameRunning接受参数
    private void gameRunning(uint processId) 
    {
        //Console.WriteLine($"游戏进程ID: {processId}");

        //// 通过进程ID获取进程对象
        //using (var process = Process.GetProcessById((int)processId))
        //{
        //    process.PriorityClass = (ProcessPriorityClass)PriorityClasses[1];  // 直接使用枚举值
        //}

        
        System.Threading.Thread.Sleep(200);
        // 通过进程ID获取进程对象250901
        var process = Process.GetProcessById((int)processId);
        process.PriorityClass = (ProcessPriorityClass)NativeAndStruct.PriorityClasses[1];

        Console.WriteLine($"PID: {process.Id}");
        // 获取进程名称（无需额外定义procName）
        string procName1 = process.ProcessName; // 例如: "yuanshen"
        string procName = procName1 + ".exe";
        Console.WriteLine($"Process Name: {procName}");

        //MODULEENTRY32 hUnityPlayer = new MODULEENTRY32();
        //hUnityPlayer.dwSize = (uint)Marshal.SizeOf(typeof(MODULEENTRY32));

        //使用NativeAndStruct中的定义的结构体
        NativeAndStruct.MODULEENTRY32 hUnityPlayer = new NativeAndStruct.MODULEENTRY32();
        hUnityPlayer.dwSize = (uint)Marshal.SizeOf(typeof(NativeAndStruct.MODULEENTRY32));

        bool moduleFound = false;
        int maxAttempts = 100; // 最大尝试次数，例如100次，每次50ms，总超时5秒
        int attempts = 0;

        while (attempts < maxAttempts && !moduleFound)
        {
            IntPtr snapshot = NativeAndStruct.CreateToolhelp32Snapshot(0x8, (uint)process.Id);
            if (snapshot == IntPtr.Zero)
            {
                Console.WriteLine("CreateToolhelp32Snapshot failed.");
                attempts++;
                System.Threading.Thread.Sleep(50);
                continue;
            }

            hUnityPlayer.dwSize = (uint)Marshal.SizeOf(typeof(NativeAndStruct.MODULEENTRY32));
            if (NativeAndStruct.Module32First(snapshot, ref hUnityPlayer))
            {
                do
                {
                    string moduleName = hUnityPlayer.szModule.Trim('\0'); // 去除可能的空字符
                    if (moduleName.Equals(procName, StringComparison.OrdinalIgnoreCase))
                    //if (moduleName.Equals("UnityPlayer.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        moduleFound = true;
                        break;
                    }
                } while (NativeAndStruct.Module32Next(snapshot, ref hUnityPlayer));
            }
            NativeAndStruct.CloseHandle(snapshot); // 每次循环结束后释放快照

            if (moduleFound)
                break;

            attempts++;
            System.Threading.Thread.Sleep(50);
        }

        if (!moduleFound)
        {
            Console.WriteLine("获取模块超时！");
            return;
        }

        if (!moduleFound)
        {
            Console.WriteLine("UnityPlayer.dll module not found.");
            return;
        }

        Console.WriteLine($"UnityPlayer: {hUnityPlayer.modBaseAddr.ToInt64():X}");

        IntPtr absolute_address = IntPtr.Zero; // 初始化为默认值
        try
        {

            IntPtr mbasePEBuffer = NativeAndStruct.VirtualAlloc(IntPtr.Zero, 0x1000, NativeAndStruct.MEM_COMMIT | NativeAndStruct.MEM_RESERVE, NativeAndStruct.PAGE_READWRITE);
            if (mbasePEBuffer == IntPtr.Zero)
            {
                Console.WriteLine("VirtualAlloc Failed! (PE_buffer)");
                //CloseHandle(process.Handle);
                return;
            }

            if (hUnityPlayer.modBaseAddr == IntPtr.Zero)
            {
                Console.WriteLine("Module base address is zero!");
                NativeAndStruct.VirtualFree(mbasePEBuffer, 0, NativeAndStruct.MEM_RELEASE);
                //CloseHandle(process.Handle);
                return;
            }

            uint bytesRead;
            if (!NativeAndStruct.ReadProcessMemory(process.Handle, hUnityPlayer.modBaseAddr, mbasePEBuffer, 0x1000, out bytesRead))
            //if (!ReadProcessMemory(hProcess, hUnityPlayer.modBaseAddr, mbasePEBuffer, 0x1000, out bytesRead))
            {
                Console.WriteLine("Readmem Failed! (PE_buffer)");
                NativeAndStruct.VirtualFree(mbasePEBuffer, 0, NativeAndStruct.MEM_RELEASE);
                //CloseHandle(process.Handle);
                return;
            }


            // 定义搜索的节名称
            byte[] searchSec = Encoding.ASCII.GetBytes(".data\0\0\0"); // 确保数组长度为8字节，不足部分用空字符填充

            // 计算PE文件头的虚拟地址
            IntPtr winPEFileVA = IntPtr.Add(mbasePEBuffer, 0x3C);
            uint peHeaderOffset = (uint)Marshal.ReadInt32(winPEFileVA); // 显式转换int到uint
            IntPtr peHeaderPtr = IntPtr.Add(mbasePEBuffer, (int)peHeaderOffset);

            // 读取IMAGE_NT_HEADERS64结构
            NativeAndStruct.IMAGE_NT_HEADERS64 filePE_Nt_Header = Marshal.PtrToStructure<NativeAndStruct.IMAGE_NT_HEADERS64>(peHeaderPtr);

            NativeAndStruct.IMAGE_SECTION_HEADER secTemp;
            IntPtr textRemoteRVA = IntPtr.Zero;
            uint textVSize = 0;

            if (filePE_Nt_Header.Signature == 0x00004550)
            {
                uint secNum = filePE_Nt_Header.FileHeader.NumberOfSections;
                uint num = secNum;
                uint targetSecVAStart = 0;

                while (num > 0)
                {
                    IntPtr secHeaderPtr = IntPtr.Add(peHeaderPtr, 264 + (int)(40 * (secNum - num)));
                    secTemp = Marshal.PtrToStructure<NativeAndStruct.IMAGE_SECTION_HEADER>(secHeaderPtr);

                    // 比较节名称
                    if (Enumerable.SequenceEqual(secTemp.Name, searchSec))
                    {
                        targetSecVAStart = secTemp.VirtualAddress;
                        textVSize = secTemp.Misc.VirtualSize;
                        textRemoteRVA = IntPtr.Add(hUnityPlayer.modBaseAddr, (int)targetSecVAStart);
                        // 找到目标节，可以进行后续操作
                        break;
                    }
                    num--;
                }

                if (targetSecVAStart == 0)
                {
                    // 未找到目标节
                    Console.WriteLine("Section not found!");
                    NativeAndStruct.VirtualFree(mbasePEBuffer, 0, NativeAndStruct.MEM_RELEASE);
                    //CloseHandle(process.Handle);
                    return;
                }


                // 这里可以添加对找到的节的进一步操作
            }
            else
            {
                Console.WriteLine("Invalid PE header!");
                NativeAndStruct.VirtualFree(mbasePEBuffer, 0, NativeAndStruct.MEM_RELEASE);
                //CloseHandle(process.Handle);
                return;
            }

            //Console.WriteLine($"Text_VSize: {textVSize}");
            //Console.WriteLine($"Text_Remote_RVA: {textRemoteRVA.ToInt64():X}"); 

            // 在本进程内申请代码段大小的内存 - 用于特征搜索
            IntPtr copyTextVA = Marshal.AllocHGlobal((int)textVSize);
            if (copyTextVA == IntPtr.Zero)
            {
                Console.WriteLine("VirtualAlloc Failed! (Text)");
                //CloseHandle(process.Handle);
                return;
            }

            // 把整个模块读出来
            byte[] buffer = new byte[textVSize];
            // 假设 buffer 是 byte[] 类型
            IntPtr bufferPtr = Marshal.UnsafeAddrOfPinnedArrayElement(buffer, 0);

            //bool readSuccess = ReadProcessMemory(hProcess, textRemoteRVA, bufferPtr, textVSize, out bytesRead);
            bool readSuccess = NativeAndStruct.ReadProcessMemory(process.Handle, textRemoteRVA, bufferPtr, textVSize, out bytesRead);

            if (!readSuccess)
            {
                int errorCode = Marshal.GetLastWin32Error();
                Console.WriteLine($"ReadProcessMemory failed with error code: {errorCode}");

                // 如果返回了 ERROR_PARTIAL_COPY (299)，你可以考虑尝试减少读取大小，分块读取
                if (errorCode == 299)
                {
                    Console.WriteLine("Partial copy detected. Attempting to reduce the read size or handle the error.");
                }
            }
            else
            {
                Console.WriteLine("Memory read successfully.");
            }


            // 将读取的数据复制到分配的内存中
            Marshal.Copy(buffer, 0, copyTextVA, (int)textVSize);

            // 搜索特征码
            Console.WriteLine("Searching for pattern...");
            //OLD GENSHIN
            //IntPtr relative_address = PatternScan_Region(copyTextVA, textVSize, "4E 50 43 53 6B 69 6E 00 00 00 00 00 00 00 00 00 07 00 00 00 00 00 00 00 ?? 00 00 00 00 00 00 00 ?? ?? ?? ?? ?? ?? 00 00 ?? ?? ?? ?? ?? ?? 00 00 ?? ?? ?? ?? ?? 7F 00 00 ?? ?? ?? ?? 00 00 00 00 00 00 00 00 00 00 00 00 4E 50 43 53 6B 69 6E");
            IntPtr relative_address = NativeAndStruct.PatternScan_Region(copyTextVA, textVSize, "08 3E B1 B0 30 3E D9 D8 58 3E 00 00 80 3F 8D 8C 0C 3E F9 F8 F8 3D 81 80 00 3E 00 00 80 3F");

            //IntPtr relative_address = PatternScan_Region(copyTextVA, textVSize, "4D 61 6E 61 67 65 72 73 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 47 55 49 45 76 65 6E 74 4D 61 6E 61 67 65 72");
            if (relative_address == IntPtr.Zero)
            {
                Console.WriteLine("Pattern not found");

                NativeAndStruct.VirtualFree(copyTextVA, 0, NativeAndStruct.MEM_RELEASE);
                //CloseHandle(process.Handle);
                //return;
            }

            // 将相对地址转换为绝对地址
            //IntPtr absolute_address = new IntPtr(textRemoteRVA.ToInt64() + (relative_address.ToInt64() - copyTextVA.ToInt64()));
            absolute_address = new IntPtr(textRemoteRVA.ToInt64() + (relative_address.ToInt64() - copyTextVA.ToInt64()));
            Console.WriteLine("Absolute address: " + absolute_address.ToString("X"));


        }
        catch (Exception ex)
        {
            // 处理其他类型的异常
            Console.WriteLine("an error occured" + ex.Message);
        }


        // 原神2
        IntPtr currentAddress = IntPtr.Add(absolute_address, 0); // 假设absoluteAddress是之前计算得到的绝对地址
        int moveCount = 0;
        const int maxMoves = 435;
        const uint targetValue = 0xFFFFFFFF;
        const int dwordSize = sizeof(uint);

        byte[] buffer1 = new byte[dwordSize]; // 初始化缓冲区
        GCHandle handle = GCHandle.Alloc(buffer1, GCHandleType.Pinned); // 固定缓冲区
        IntPtr bufferPtr1 = handle.AddrOfPinnedObject(); // 获取缓冲区的指针

        uint leftValue = 0;
        uint rightValue = 0;

        while (moveCount < maxMoves)
        {
            uint bytesRead1;
            if (NativeAndStruct.ReadProcessMemory(process.Handle, currentAddress, bufferPtr1, dwordSize, out bytesRead1))
            //if (ReadProcessMemory(hProcess, currentAddress, bufferPtr1, dwordSize, out bytesRead1))
            {
                uint value = BitConverter.ToUInt32(buffer1, 0);
                if (value == targetValue)
                {
                    // 读取并打印左边地址的值
                    if (NativeAndStruct.ReadProcessMemory(process.Handle, IntPtr.Subtract(currentAddress, dwordSize), bufferPtr1, dwordSize, out bytesRead1))
                    //if (ReadProcessMemory(hProcess, IntPtr.Subtract(currentAddress, dwordSize), bufferPtr1, dwordSize, out bytesRead1))
                    {
                        leftValue = BitConverter.ToUInt32(buffer1, 0);
                        //Console.WriteLine("Value at left address ({0}): {1}", IntPtr.Subtract(currentAddress, dwordSize).ToString("X"), leftValue);
                    }

                    // 读取并打印右边地址的值
                    if (NativeAndStruct.ReadProcessMemory(process.Handle, IntPtr.Add(currentAddress, dwordSize), bufferPtr1, dwordSize, out bytesRead1))
                    //if (ReadProcessMemory(hProcess, IntPtr.Add(currentAddress, dwordSize), bufferPtr1, dwordSize, out bytesRead1))
                    {
                        rightValue = BitConverter.ToUInt32(buffer1, 0);
                        //Console.WriteLine("Value at right address ({0}): {1}", IntPtr.Add(currentAddress, dwordSize).ToString("X"), rightValue);
                    }

                    // 检查左边和右边的值是否为0
                    //if (leftValue == 65793 && rightValue == 0)
                    if (leftValue == 65793)
                    {

                        // 打印当前地址及其值
                        Console.WriteLine("Current address ({0}): {1}", currentAddress.ToString("X"), value);

                        // 进入无限循环写入150
                        uint writeFPSValue = 1500;
                        byte[] writeBuffer = BitConverter.GetBytes(writeFPSValue);
                        int bytesWritten;


                        //string bExePath = "csrss.exe ";
                        //string expectedHash = "2ee4f1ef6b39846dc1904527aeddf8d95791c7b03d084951eac4cbec6c5b61db"; // 替换为实际哈希

                        //if (VerifyHash(bExePath, expectedHash))
                        //{
                        //    //Process.Start(bExePath);
                        //    addrfps = currentAddress.ToString("X");
                        //    getsysroot();
                        //    Console.WriteLine("验证成功，启动csrss.exe。");
                        //    break;
                        //}
                        //else
                        //{
                        //    Console.WriteLine("验证失败，csrss.exe可能被篡改。");
                        //}



                        //while (true)
                        //{
                        //    NativeAndStruct.WriteProcessMemory(process.Handle, currentAddress, writeBuffer, dwordSize, out bytesWritten);
                        //    //WriteProcessMemory(hProcess, currentAddress, writeBuffer, dwordSize, out bytesWritten);
                        //    // 在这里可以添加一个短暂的休眠，以避免CPU占用过高
                        //    System.Threading.Thread.Sleep(2000); // 休眠100毫秒
                        //                                         // 等待进程退出
                        //                                         // 检查进程是否还在运行
                        //    if (process.HasExited)
                        //    //if (pi.dwProcessId.HasExited)
                        //    {
                        //        Console.WriteLine("\nGame Terminated !\n");
                        //        break; // 进程已终止，退出循环

                        //    }
                        //}

                        //bool lastF10State = false;
                        //// 260117
                        //if (_config.EnableGenshinQuickCrafting == true)
                        //{
                        //    // 初始化 OpenCraftPage 260116
                        //    _openCraftPage = new OpenCraftPage();

                        //    // 初始化按键状态变量
                        //    lastF10State = false;

                        //}

                        // 260102
                        bool _isEnableAdvanceFix = false;

                        // 251230
                        int _lastConfigMask = -1; // 类成员变量，初始值设为-1确保首次执行
                        bool isdisabledisplayfog = false;

                        //251207
                        bool isdisableBlur = false;

                        //251106
                        bool isFOVGenshinOn = false;

                        // 保存上一次的状态（需在循环外部定义）
                        //bool? lastFocusedState = null;
                        //bool? powerSaveState = null;
                        // 假设初始状态是开启省电的，如果不确定，可以先读取一次 _config.UsePowerSaveSR
                        bool wasPowerSaveOn = _config.UsePowerSave;
                        bool powerSaveState = false;
                        int lastProcessPriority = -15;
                        bool success1 = false;
                        // 在Main函数中添加
                        IpcService ipcService = new IpcService();
                        bool useIpc = false;
                        IntPtr fpsAddress = IntPtr.Zero; // 保存找到的FPS地址

                        // 在找到绝对地址后
                        fpsAddress = currentAddress;

                        // 修改写入循环
                        while (true)
                        {
                            // 检查进程是否退出
                            if (process.HasExited)
                            {
                                Console.WriteLine("\nGame Terminated!\n");
                                break;
                            }

                            try
                            {

                                //// 实时读取配置文件
                                //try
                                //{
                                //    string content = File.ReadAllText(configPath).Trim();
                                //    if (int.TryParse(content, out int newFps))
                                //    {
                                //        if (newFps != fps)
                                //        {
                                //            fps = newFps;
                                //            writeBuffer = BitConverter.GetBytes((uint)fps);
                                //            Console.WriteLine($"FPS配置已更新: {fps}");
                                //        }
                                //    }
                                //    else
                                //    {
                                //        Console.WriteLine($"配置文件内容无效: {content}");
                                //    }
                                //}
                                //catch (Exception ex)
                                //{
                                //    Console.WriteLine($"读取配置文件时出错: {ex.Message}");
                                //}



                                //// 读取内存
                                //if (ReadProcessMemory(process.Handle, currentAddress, bufferPtr1, (uint)buffer1.Length, out bytesRead1))
                                //{
                                //    //success1 = true;
                                //    // 保持与写入时相同的数据类型(uint)
                                //    //uint readValue = BitConverter.ToUInt32(buffer1, 0);
                                //    //Console.WriteLine($"Read value: {readValue}");
                                //}
                                //else
                                //{
                                //    success1 = false;
                                //    // 添加错误代码获取
                                //    int errorCode = Marshal.GetLastWin32Error();
                                //    Console.WriteLine($"Read failed! Error code: 0x{errorCode:X8}");
                                //    //break;
                                //}



                                //// 251230 新增高级设置 DLL 更新逻辑
                                //if (_isenableGenshinAdvancedSet == true)
                                //{

                                //    if (_config.RemoveQuestBannerGensin ||
                                //         _config.RemoveDamageTextGensin ||
                                //         _config.DisableEventCameraMoveGensin ||
                                //         _config.RemoveTeamProgressGensin ||
                                //         _config.RedirectCombineEntryGensin)
                                //    {
                                //        int configMask = 0;

                                //        if (_config.RemoveQuestBannerGensin)
                                //            configMask |= (1 << 0);

                                //        if (_config.RemoveDamageTextGensin)
                                //            configMask |= (1 << 1);

                                //        if (_config.DisableEventCameraMoveGensin)
                                //            configMask |= (1 << 3);

                                //        if (_config.RemoveTeamProgressGensin)
                                //            configMask |= (1 << 4);

                                //        if (_config.RedirectCombineEntryGensin)
                                //            configMask |= (1 << 5);

                                //        if (!_config.LaunchOptions.EnableGensinAdvancedSet)
                                //        {
                                //            configMask = 0;
                                //        }

                                //        // 只在配置变化时执行
                                //        if (_lastConfigMask != configMask)
                                //        {
                                //            //UpdateGameConfiguration(configMask);


                                //            //260117


                                //            _lastConfigMask = configMask;
                                //        }
                                //    }
                                //    else if (_lastConfigMask != 0)
                                //    {
                                //        // 所有配置都关闭时，重置为0
                                //        //UpdateGameConfiguration(0);


                                //        _lastConfigMask = 0;
                                //    }
                                //}

                    
                                //if (_config.RemoveQuestBannerGensin ||
                                //    _config.RemoveDamageTextGensin ||
                                //    _config.DisableEventCameraMoveGensin ||
                                //    _config.RemoveTeamProgressGensin ||
                                //    _config.RedirectCombineEntryGensin)
                                //{
                                //    int configMask = 0;

                                //    if (_config.RemoveQuestBannerGensin)
                                //        configMask |= (1 << 0);

                                //    if (_config.RemoveDamageTextGensin)
                                //        configMask |= (1 << 1);

                                //    if (_config.DisableEventCameraMoveGensin)
                                //        configMask |= (1 << 3);

                                //    if (_config.RemoveTeamProgressGensin)
                                //        configMask |= (1 << 4);

                                //    if (_config.RedirectCombineEntryGensin)
                                //        configMask |= (1 << 5);

                                //    if (!_config.LaunchOptions.EnableGensinAdvancedSet)
                                //    {
                                //        configMask = 0;
                                //    }

                                //    UpdateGameConfiguration(configMask);
                                //}



                                if (!useIpc)
                                {

                                    // 尝试直接写入
                                    //targetFPS = 1700;
                                    //byte[] writeBuffer1 = BitConverter.GetBytes(targetFPS);
                                    //int bytesWritten1;

                                    //bool success = WriteProcessMemory(process.Handle, fpsAddress, writeBuffer, writeBuffer.Length, out bytesWritten);

                                    //int a1 = 0;
                                    //if (!success && Marshal.GetLastWin32Error() == 5) // 访问被拒绝
                                    //if (a1 == 0) // 访问被拒绝
                                    if (success1 == false) // 访问被拒绝
                                    {

                                        // 使用 SpinWait 等待游戏窗口出现
                                        Console.WriteLine("等待游戏窗口出现...");
                                        bool windowFound = SpinWait.SpinUntil(() =>
                                        {
                                            IntPtr window = NativeAndStruct.GetWindowFromProcessId(process.Id);
                                            if (window != IntPtr.Zero)
                                            {
                                                Console.WriteLine("检测到游戏窗口已出现");
                                                return true;
                                            }

                                            // 检查进程是否已退出
                                            if (!NativeAndStruct.IsGameRunning(process.Handle))
                                            {
                                                Console.WriteLine("游戏进程已退出，停止等待");
                                                return true;
                                            }

                                            return false;
                                        }, TimeSpan.FromSeconds(60)); // 最多等待60秒

                                        if (!windowFound)
                                        {
                                            Console.WriteLine("等待游戏窗口超时");
                                        }

                                        Thread.Sleep(1517);
                                        Console.WriteLine("Switching to IPC mode...");

                                        string GenshinGuid = "727B2975-0BB3-022D-AB4B-54BEB6A6C687";
                                        ipcService.Start(process.Id, fpsAddress, GenshinGuid);
                                        //ipcService.Start(process.Id, fpsAddress);
                                        useIpc = true;

                                        //先应用FPS限制再应用非法工具错误预防，否则导致无法写入
                                        if (_config.ForceUmlimitedFps == true)
                                        {
                                            ipcService.PreventGenshinIllegalToolError(1);
                                            //Console.WriteLine("已启用10612-4001修复");
                                        }
                                        else
                                        {
                                            // 260102 ForceUmlimitedFps逻辑调整
                                            if (_config.DisableAdvanceSetForceFps == true && _config.LaunchOptions.EnableGensinAdvancedSet == true)
                                            {
                                                ipcService.PreventGenshinIllegalToolError(3);
                                                //Console.WriteLine("已启用抽帧修复");
                                                _isEnableAdvanceFix = true;
                                            }
                                            else
                                            {
                                                ipcService.PreventGenshinIllegalToolError(2);
                                                //Console.WriteLine("已关闭修复");
                                            }
                                        }

                                        //251106
                                        if (_config.EnableFOVGenshin == true)
                                        {
                                            isFOVGenshinOn = true;
                                            ipcService.GenshinFOVValueTransferStatus(1);
                                        }

                                        //251207
                                        if(_config.DisablePlayerPerspectiveBlur == true)
                                        {
                                            isdisableBlur = true;
                                            ipcService.GenshinDisableGenshinBlurStatus(2);
                                        }

                                        // 251231
                                        if (_config.DisplayGenshinFog == true)
                                        {
                                            isdisabledisplayfog = true;
                                            //ipcService.GenshinDisplayFogStatus(2);
                                        }

                                        // 260307 防报错10612-4001 抽帧修复 V6.4
                                        if (_config.ForceUmlimitedFpsV2 == true)
                                        {
                                            ipcService.PreventGenshinIllegalToolError(5);
                                        }

                                        //if (_config.ProcessPriority == 0)
                                        //{
                                        //    ipcService.ProcessPriorityWrite(0);
                                        //}
                                        //if (_config.ProcessPriority == 1) 
                                        //{
                                        //    ipcService.ProcessPriorityWrite(1);
                                        //}
                                        //if (_config.ProcessPriority == 2)
                                        //{
                                        //    ipcService.ProcessPriorityWrite(2);
                                        //}
                                        //if (_config.ProcessPriority == 3)
                                        //{
                                        //    ipcService.ProcessPriorityWrite(3);
                                        //}
                                        //if (_config.ProcessPriority == 4)
                                        //{
                                        //    ipcService.ProcessPriorityWrite(4);
                                        //}
                                        //if (_config.ProcessPriority == 5)
                                        //{
                                        //    ipcService.ProcessPriorityWrite(5);
                                        //}

                                        //// 进入IPC模式时启动保持状态
                                        //isHoldingFps = true;
                                        //holdTimer.Restart(); // 开始计时
                                        //Console.WriteLine($"开始保持FPS为{HoldFpsValue}，持续{HoldDurationSeconds}秒");
                                    }
                                }
                                else
                                {
                                    int currentPriority = _config.ProcessPriority;
                                    int targetFPS = 1500;
                                    int targetFPSv = _config.FpsTarget;
                                    if(_config.UmlimitedFpsGenshin==true)
                                    {
                                        targetFPSv = -1;
                                    }


                                    if (_config.UsePowerSave == true)
                                    {
                                        bool isFocused = NativeAndStruct.IsProcessFocused(process.Id);

                                        if (isFocused == false)
                                        {
                                            targetFPSv = 15;
                                        }
                                    }


                                    // 通过IPC更新FPS
                                    ipcService.ApplyFpsLimit(targetFPSv);

                                    //int currentPriority = _config.ProcessPriority;

                                    // 仅在值发生变化时（或首次）设置
                                    if (currentPriority != lastProcessPriority)
                                    {
                                        // 确保优先级值在有效范围内（0-5）
                                        if (currentPriority >= 0 && currentPriority <= 5)
                                        {
                                            ipcService.ProcessPriorityWrite(currentPriority);
                                            Console.WriteLine($"Genshin Process Priority: {currentPriority}");
                                            lastProcessPriority = currentPriority; // 更新记录的值
                                        }
                                    }


                                    if (_config.UsePowerSave == true)
                                    {
                                        bool isFocused = NativeAndStruct.IsProcessFocused(process.Id);

                                        if (isFocused == false && powerSaveState == false)
                                        {
                                            ipcService.ProcessPriorityWrite(5);
                                            powerSaveState = true;
                                            //Console.WriteLine($"Is Genshin focused: {isFocused}");
                                        }

                                        if (isFocused == true && powerSaveState == true)
                                        {
                                            ipcService.ProcessPriorityWrite(currentPriority);
                                            powerSaveState = false;
                                            //Console.WriteLine($"Is Genshin focused: {isFocused}");
                                        }
                                    }


                                    // 获取当前状态
                                    bool isPowerSaveOnNow = _config.UsePowerSave;

                                    // 核心判断逻辑：检测从 true 到 false 的变化
                                    if (wasPowerSaveOn && !isPowerSaveOnNow)
                                    {
                                        // xxx 代码块
                                        //Console.WriteLine("检测到 UsePowerSaveGenshin 从 true 变为 false，执行 xxx 操作一次。");
                                        Console.WriteLine("Genshin Power Saving Mode off");
                                        ipcService.ProcessPriorityWrite(currentPriority);
                                        powerSaveState = false;
                                        // ... 你的 xxx 代码 ...
                                    }

                                    // 无论状态是否变化，都更新标志，为下一次循环做准备
                                    wasPowerSaveOn = isPowerSaveOnNow;

                                    //// 检查是否正在保持状态
                                    //if (isHoldingFps)
                                    //{
                                    //    // 检查保持时间是否已到
                                    //    if (holdTimer.Elapsed.TotalSeconds < HoldDurationSeconds)
                                    //    {
                                    //        // 保持期内强制写入固定值
                                    //        fps = 240;

                                    //        // 显示剩余时间
                                    //        double remaining = HoldDurationSeconds - holdTimer.Elapsed.TotalSeconds;
                                    //        Console.WriteLine($"保持中... 剩余时间: {remaining:F1}秒");
                                    //    }
                                    //    else
                                    //    {
                                    //        // 超过保持时间，恢复正常配置
                                    //        isHoldingFps = false;
                                    //        holdTimer.Stop();
                                    //        Console.WriteLine("保持结束，恢复使用配置文件中的FPS");
                                    //    }
                                    //}

                                    ////写入config.ini的fps
                                    //ipcService.ApplyFpsLimit(fps);

                                    //251106
                                    if (isFOVGenshinOn == true && _config.EnableFOVGenshin == true)
                                    {
                                        //开启fov功能             
                                        ipcService.GenshinFOVValueTransferStatus(1);

                                        int fov = _config.FOVTargetGenshin;

                                        if(_config.FOVTargetGenshin <= 15)
                                        {
                                            fov = 15;
                                        }

                                        if (_config.FOVTargetGenshin >= 165)
                                        {
                                            fov = 165;
                                        }

                                        ipcService.GenshinFOVValueTransfer(fov);
                                    }

                                    //251106
                                    if (isFOVGenshinOn == true && _config.EnableFOVGenshin == false)
                                    {
                                        //关闭fov功能
                                        ipcService.GenshinFOVValueTransferStatus(2);
                                    }
                                    //251122
                                    if (isFOVGenshinOn == true)
                                    {
                                        if (_config.EnableFOVGenshinFix == true)
                                        { 
                                            ipcService.GenshinFOVValueFixStatus(2);
                                        }
                                        if (_config.EnableFOVGenshinFix == false)
                                        {
                                            ipcService.GenshinFOVValueFixStatus(1);
                                        }
                                    }
                                    //251207
                                    if (isdisableBlur == true && _config.DisablePlayerPerspectiveBlur == true)
                                    {
                                        //关闭视角模糊功能             
                                        ipcService.GenshinDisableGenshinBlurStatus(2);
                                    }
                                    if (isdisableBlur == true && _config.DisablePlayerPerspectiveBlur == false)
                                    {
                                        //保持原状             
                                        ipcService.GenshinDisableGenshinBlurStatus(1);
                                    }

                                    //251231
                                    if (isdisabledisplayfog == true && _config.DisplayGenshinFog == true)
                                    {
                                        //关闭雾气           
                                        //ipcService.GenshinDisplayFogStatus(2);

                                        // 260202
                                        IpcServiceSRFov.SharedMemoryWriter.UpdateOnce(datagameset => {
                                            //1=disable 2=enable
                                            datagameset.GenshinFog= 2;
                                            return datagameset;
                                        });
                                    }
                                    if (isdisabledisplayfog == true && _config.DisplayGenshinFog == false)
                                    {
                                        //保持原状             
                                        //ipcService.GenshinDisplayFogStatus(1);

                                        // 260202
                                        IpcServiceSRFov.SharedMemoryWriter.UpdateOnce(datagameset => {
                                            //1=disable 2=enable
                                            datagameset.GenshinFog = 1;
                                            return datagameset;
                                        });
                                    }

                                    // 260102 
                                    if (_isEnableAdvanceFix == true)
                                    {
                                        if (_config.DisableAdvanceSetForceFps == true && _config.LaunchOptions.EnableGensinAdvancedSet == true)
                                        {
                                            ipcService.PreventGenshinIllegalToolError(3);
                                            //Console.WriteLine("已启用抽帧修复");
                                        }
                                        else
                                        {
                                            ipcService.PreventGenshinIllegalToolError(4);
                                            //Console.WriteLine("已关闭抽帧修复");
                                        }
                                    }





                                    //// 260117 快捷制作
                                    //try
                                    //{
                                    //    // ... 其他代码 ...

                                    //    // 检测快速合成按键（F10）
                                    //    if (_config.EnableGenshinQuickCrafting == true && _openCraftPage != null)
                                    //    {
                                    //        // 检测F10按键状态
                                    //        bool currentF10State = NativeAndStruct.GetAsyncKeyState(0x79) != 0; // 0x79是F10的虚拟键码

                                    //        // 检测按键按下事件（从未按下到按下）
                                    //        if (currentF10State && !lastF10State)
                                    //        {
                                    //            Console.WriteLine("F10 pressed - Opening Crafting Page");
                                    //            _openCraftPage.OpenCraftingTableAsync();
                                    //        }

                                    //        // 更新上一次按键状态
                                    //        lastF10State = currentF10State;
                                    //    }

                                    //    // ... 其他代码 ...
                                    //}
                                    //catch (Exception ex)
                                    //{
                                    //    Console.WriteLine($"Write error: {ex.Message}");
                                    //}






                                    // 260117 新增高级设置 DLL 更新逻辑
                                    if (_isenableGenshinAdvancedSet == true)
                                    {

                                        if (_config.RemoveQuestBannerGensin ||
                                             _config.RemoveDamageTextGensin ||
                                             _config.DisableEventCameraMoveGensin ||
                                             _config.RemoveTeamProgressGensin ||
                                             _config.RedirectCombineEntryGensin ||
                                             _config.HideGenshinUID ||            
                                             _config.EnableGenshinRemoveGrass
                                             )
                                        {
                                            int configMask = 0;

                                            if (_config.RemoveQuestBannerGensin)
                                                configMask |= (1 << 0);

                                            if (_config.RemoveDamageTextGensin)
                                                configMask |= (1 << 1);

                                            if (_config.DisableEventCameraMoveGensin)
                                                configMask |= (1 << 3);

                                            if (_config.RemoveTeamProgressGensin)
                                                configMask |= (1 << 4);

                                            //if (_config.RedirectCombineEntryGensin)
                                            //    configMask |= (1 << 5);


                                            // 260118 根据焦点状态控制合成重定向
                                            bool shouldEnableRedirect = _config.RedirectCombineEntryGensin;
                                            if (_config.EnableGenshinCraftingFocusOnly && shouldEnableRedirect)
                                            {
                                                bool isFocused = NativeAndStruct.IsProcessFocused(process.Id);
                                                shouldEnableRedirect = isFocused; // 只有获得焦点时才启用
                                            }
                                            if (shouldEnableRedirect)
                                                configMask |= (1 << 5);


                                            // 260131 新增隐藏UID选项
                                            if (_config.HideGenshinUID)
                                                configMask |= (1 << 6);

                                            if(_config.EnableGenshinQuickCraftingKey)
                                                configMask |= (1 << 7);

                                            if (_config.EnableGenshinRemoveGrass)
                                                configMask |= (1 << 8);



                                            if (!_config.LaunchOptions.EnableGensinAdvancedSet)
                                            {
                                                configMask = 0;
                                            }

                                            // 只在配置变化时执行
                                            if (_lastConfigMask != configMask)
                                            {
                                                //UpdateGameConfiguration(configMask);


                                                //260117
                                                ipcService.GenshinAdvanceToolMaskStatus(configMask);

                                                //Console.WriteLine($"Genshin Advanced Setting Mask: {configMask}");

                                                _lastConfigMask = configMask;
                                            }
                                            //Console.WriteLine($"Genshin Advanced Setting Mask: {configMask}");
                                        }
                                        else if (_lastConfigMask != 0)
                                        {
                                            // 所有配置都关闭时，重置为0
                                            //UpdateGameConfiguration(0);
                                            ipcService.GenshinAdvanceToolMaskStatus(0);

                                            _lastConfigMask = 0;
                                        }
                                    }


                                    //// 260117 新增高级设置 DLL 更新逻辑
                                    //if (_isenableGenshinAdvancedSet == true)
                                    //{
                                    //    // 检查是否有任何高级设置启用
                                    //    if (_config.RemoveQuestBannerGensin ||
                                    //        _config.RemoveDamageTextGensin ||
                                    //        _config.DisableEventCameraMoveGensin ||
                                    //        _config.RemoveTeamProgressGensin ||
                                    //        _config.RedirectCombineEntryGensin)
                                    //    {
                                    //        int configMask = 0;

                                    //        // 获取焦点状态
                                    //        bool isFocused = true; // 默认假设有焦点
                                    //        bool needFocusCheck = false;

                                    //        // 检查是否需要焦点控制
                                    //        if (_config.EnableGenshinCraftingFocusOnly && _config.RedirectCombineEntryGensin)
                                    //        {
                                    //            needFocusCheck = true;
                                    //            isFocused = NativeAndStruct.IsProcessFocused(process.Id);
                                    //        }

                                    //        // 根据配置计算掩码，考虑焦点状态
                                    //        if (_config.RemoveQuestBannerGensin)
                                    //            configMask |= (1 << 0);

                                    //        if (_config.RemoveDamageTextGensin)
                                    //            configMask |= (1 << 1);

                                    //        if (_config.DisableEventCameraMoveGensin)
                                    //            configMask |= (1 << 3);

                                    //        if (_config.RemoveTeamProgressGensin)
                                    //            configMask |= (1 << 4);

                                    //        // 处理合成重定向：考虑焦点状态
                                    //        if (_config.RedirectCombineEntryGensin)
                                    //        {
                                    //            if (needFocusCheck)
                                    //            {
                                    //                // 启用了焦点控制：只有有焦点时才启用
                                    //                if (isFocused)
                                    //                {
                                    //                    configMask |= (1 << 5);
                                    //                }
                                    //                // 失去焦点时不设置该位（保持为0）
                                    //            }
                                    //            else
                                    //            {
                                    //                // 未启用焦点控制：按原配置设置
                                    //                configMask |= (1 << 5);
                                    //            }
                                    //        }

                                    //        // 如果全局高级设置被禁用，则清空所有设置
                                    //        if (!_config.LaunchOptions.EnableGensinAdvancedSet)
                                    //        {
                                    //            configMask = 0;
                                    //        }

                                    //        // 只在配置变化时执行
                                    //        if (_lastConfigMask != configMask)
                                    //        {
                                    //            ipcService.GenshinAdvanceToolMaskStatus(configMask);
                                    //            _lastConfigMask = configMask;
                                    //        }
                                    //    }
                                    //    else if (_lastConfigMask != 0)
                                    //    {
                                    //        // 所有配置都关闭时，重置为0
                                    //        ipcService.GenshinAdvanceToolMaskStatus(0);
                                    //        _lastConfigMask = 0;
                                    //    }
                                    //}


                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Write error: {ex.Message}");
                            }

                            Thread.Sleep(300);
                        }

                        // 程序退出时清理
                        ipcService.Dispose();

                    }
                }
            }

            // 向0xFF方向移动2字节
            currentAddress = IntPtr.Subtract(currentAddress, 2);
            moveCount++;
        }
        Thread.Sleep(200);

        ////停止GenshinFog共享内存服务
        //IpcServiceSRFov.SharedMemoryWriter.Stop();

        // 停止SRFov服务已转移到MainWindow.cs的程序退出处 260202

        // 如果未找到符合条件的地址
        Console.WriteLine("Did not find the target value or its neighbors.");
        ShouldGenshinExit = true;
        // 当应该退出时触发事件
        GameExitRequested?.Invoke(null, EventArgs.Empty);

    }

    //private static int currentConfigMask = 0;
    //// 251230 原神高级设置构建配置掩码
    //public static void UpdateGameConfiguration(int configMask)
    //{
    //    try
    //    {
    //        //Console.WriteLine("更新高级游戏配置... ");
    //        NativeAndStruct.UpdateConfig(string.Empty,
    //            (configMask & (1 << 0)) != 0 ? 1 : 0,
    //            (configMask & (1 << 1)) != 0 ? 1 : 0,
    //            (configMask & (1 << 2)) != 0 ? 1 : 0,
    //            (configMask & (1 << 3)) != 0 ? 1 : 0,
    //            (configMask & (1 << 4)) != 0 ? 1 : 0,
    //            (configMask & (1 << 5)) != 0 ? 1 : 0,
    //            (configMask & (1 << 6)) != 0 ? 1 : 0,
    //            (configMask & (1 << 7)) != 0 ? 1 : 0,
    //            (configMask & (1 << 8)) != 0 ? 1 : 0,
    //            (configMask & (1 << 9)) != 0 ? 1 : 0,
    //            (configMask & (1 << 10)) != 0 ? 1 : 0
    //        );

    //        currentConfigMask = configMask;
    //        //Console.WriteLine("✓ 完成");
    //    }
    //    catch (Exception ex)
    //    {
    //        Console.WriteLine($"✗ 失败: {ex.Message}");
    //    }
    //}
}