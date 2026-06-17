using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using UnlockFps.Utils;
using static UnlockFps.Services.NativeAndStruct;
//using static Windows.Win32.PInvoke;

namespace UnlockFps.Services
{
    public class ProcessServiceSR
    {
        public static bool ShouldSRExit = true;

        private readonly Config _config;

        public ProcessServiceSR(ConfigService configService)
        {
            _config = configService.Config;
        }

        private string _srFOVDllPath = string.Empty;

        public void Start(string gamePath,string gameParam,bool ifManualLaunch, int? manualModepid = null)
        {
            //if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            //{
            //    throw new PlatformNotSupportedException("Only windows or wine is supported.");
            //}

            //var runningProcess = Process.GetProcesses()
            //    .FirstOrDefault(x => Array.IndexOf(GameConstants.GameNamesSR, x.ProcessName) != -1);

            //if (runningProcess is not null)
            //{
            //    throw new Exception("An instance of the game is already running: " + runningProcess.Id);
            //}




            ////开启StarRail移动UI 251206
            //bool _isreadyStarRailMbUIWt = false;
            //string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            //string mobileuiexe = Path.Combine(appDirectory, "wTE_ysr251207a.exe");

            //if (_config.EnableStarRailMbUIWt)
            //{
            //    //try
            //    //{
            //    //    // 先杀掉旧进程
            //    //    var processes = Process.GetProcessesByName("wTE_ysr251207a");
            //    //    foreach (var process in processes)
            //    //    {
            //    //        process.Kill();
            //    //        process.WaitForExit();
            //    //    }
            //    //}
            //    //catch (Exception ex)
            //    //{
            //    //    throw new Exception($"Kill {mobileuiexe} Error: {ex.Message}");
            //    //}

            //    if (File.Exists(mobileuiexe))
            //    {
            //        // ⭐ 先创建事件（使用 OpenOrCreate 模式）
            //        EventWaitHandle readyEvent = new EventWaitHandle(
            //            false,
            //            EventResetMode.ManualReset,
            //            "Global\\StarRailMobileUI_Ready251206XQX");

            //        // 重置事件状态，确保是未触发状态
            //        readyEvent.Reset();

            //        // 然后启动进程
            //        ProcessStartInfo startInfo = new ProcessStartInfo
            //        {
            //            FileName = mobileuiexe,
            //            Arguments = "-hksr",
            //            UseShellExecute = false
            //        };

            //        Process process = Process.Start(startInfo);

            //        if (!ifManualLaunch) 
            //        { 
            //            // 等待事件信号（最多30秒）
            //            if (readyEvent.WaitOne(TimeSpan.FromSeconds(30)))
            //            {
            //                //Console.WriteLine("StarRail Mobile UI initialized successfully.");
            //                _isreadyStarRailMbUIWt = true;
            //            }
            //            else
            //            {
            //                throw new Exception("Timeout waiting for process to initialize");
            //            }
            //            readyEvent.Dispose();
            //        }         
            //    }
            //}
            //if ( _isreadyStarRailMbUIWt && !ifManualLaunch)
            //{
            //    // 等待，确保移动UI完全准备好
            //    Thread.Sleep(517);
            //}



            // 260215 在启动游戏前，根据用户设置更新注册表中的分辨率配置（如果启用了相关选项）。
            if (!ifManualLaunch)
            {
                // 260215写入StarRail分辨率设置到注册表，确保在窗口关闭时保存用户的分辨率选择
                if (_config.EnableSRGraphicOptionSet == true)
                {
                    int _currentgamever = 0;
                    _currentgamever = Detect_SR_GameVersion();

                    if (_config.EnableSRCustomResolutionSet == true)
                    {
                        // 自定义分辨率启用：直接写入完整配置（宽、高、全屏）
                        if (_currentgamever == 1)
                        {
                            RegistryHelper.WriteResolution(_config.SRCustomResolutionWidth, _config.SRCustomResolutionHeight, _config.SRCustomFullScreen, 1);
                        }
                        if (_currentgamever == 2)
                        {
                            RegistryHelper.WriteResolution(_config.SRCustomResolutionWidth, _config.SRCustomResolutionHeight, _config.SRCustomFullScreen, 2);
                        }
                    }
                    else
                    {
                        if (_currentgamever == 1)
                        {
                            // 仅更新全屏状态（保持当前分辨率）
                            RegistryHelper.UpdateFullScreenOnly(_config.SRCustomFullScreen, 1);
                        }
                        if (_currentgamever == 2)
                        {
                            // 仅更新全屏状态（保持当前分辨率）
                            RegistryHelper.UpdateFullScreenOnly(_config.SRCustomFullScreen, 2);
                        }
                    }
                }
            }



            if (!ifManualLaunch)
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    throw new PlatformNotSupportedException("Only windows or wine is supported.");
                }

                var runningProcess = Process.GetProcesses()
                    .FirstOrDefault(x => Array.IndexOf(GameConstants.GameNamesSR, x.ProcessName) != -1);

                if (runningProcess is not null)
                {
                    throw new Exception("An instance of the game is already running: " + runningProcess.Id);
                }
            }

            LaunchOptions? launchOptions = null;
            NativeAndStruct.PROCESS_INFORMATION lpProcessInformation = default;

            //if (ifManualLaunch)
            //{
            //    // 手动模式：附加到已存在的游戏进程（而不是 CreateProcess）
            //    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            //    {
            //        throw new PlatformNotSupportedException("Only windows or wine is supported.");
            //    }

            //    var runningProcess = Process.GetProcesses()
            //        .FirstOrDefault(x => Array.IndexOf(GameConstants.GameNamesSR, x.ProcessName) != -1);

            //    if (runningProcess is null)
            //    {
            //        throw new Exception("Manual start requested but target game process was not found.");
            //    }

            //    // 填充 PROCESS_INFORMATION，注意 hThread 在附加场景下不可用（置为 IntPtr.Zero）
            //    lpProcessInformation = default;
            //    lpProcessInformation.hProcess = (Windows.Win32.Foundation.HANDLE)runningProcess.Handle;
            //    lpProcessInformation.hThread = (Windows.Win32.Foundation.HANDLE)IntPtr.Zero;
            //    lpProcessInformation.dwProcessId = (uint)runningProcess.Id;
            //    //launchOptions = _config.LaunchOptions;
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
                lpProcessInformation.hProcess = runningProcess.Handle;
                lpProcessInformation.hThread = IntPtr.Zero;
                lpProcessInformation.dwProcessId = (uint)runningProcess.Id;
                lpProcessInformation.dwThreadId = 0;
                //launchOptions = _config.LaunchOptions;
            }


            Task.Run(() =>
            {
                StartGameSR(gamePath, gameParam, ifManualLaunch, lpProcessInformation);
            });
        }

        public static event EventHandler GameExitRequested;
        //private void StartGameSR(string gamePathSR, string gameParam, bool ifManualLaunch, LaunchOptions? launchOptions,NativeAndStruct.PROCESS_INFORMATION lpProcessInformation) 
        private void StartGameSR(string gamePathSR, string gameParam, bool ifManualLaunch, NativeAndStruct.PROCESS_INFORMATION lpProcessInformation)
        {
            //string fullStartCommand = null;
            //bool NeedCustomSR = false;
            //if (string.IsNullOrWhiteSpace(gameParam))
            //{
            //    Console.WriteLine("No custom SR parameters provided.");
            //}
            //else
            //{
            //    fullStartCommand = $"{gamePathSR} {gameParam}";
            //    Console.WriteLine($"Starting game with command: {fullStartCommand}");
            //    NeedCustomSR = true;
            //}

            //Process process = null;
            //// 3. 使用传入的 gamePath 启动新的游戏进程
            //try
            //{
            //    // Process.Start 是 .NET 中用来启动进程的标准方法
            //    if (!NeedCustomSR)
            //    {
            //        process = Process.Start(gamePathSR);
            //    }
            //    else
            //    {
            //        // 1. 创建一个 ProcessStartInfo 对象
            //        var startInfo = new ProcessStartInfo();
            //        // 2. 设置要启动的程序路径
            //        startInfo.FileName = gamePathSR;
            //        startInfo.Arguments = gameParam;
            //        process = Process.Start(startInfo);
            //    }
            //    //process = Process.Start(gamePathSR);
            //    ShouldSRExit = false;
            //}
            //catch (Exception ex)
            //{
            //    // 如果启动失败（例如文件不存在、没有权限等），包装并抛出异常
            //    throw new Exception($"Failed to start the game from path: {gamePathSR}", ex);
            //}

            //var launchOptions = _config.LaunchOptions;
            //if (!ProcessUtils.InjectDlls(lpProcessInformation.hProcess, launchOptions.DllList))
            //{
            //    throw new Win32Exception(Marshal.GetLastWin32Error(),
            //        $"Dll Injection failed. ({Marshal.GetLastPInvokeErrorMessage()})");
            //}

            //if (launchOptions.SuspendLoad)
            //{
            //    var retCode = ResumeThread(lpProcessInformation.hThread);
            //    if (retCode == 0xFFFFFFFF)
            //    {
            //        throw new Win32Exception(Marshal.GetLastWin32Error(),
            //            $"ResumeThread failed. ({Marshal.GetLastPInvokeErrorMessage()})");
            //    }
            //}

            // 在注入SRFov DLL之前创建共享内存
            if (_config.EnableFOVStarRail == true)
            {
                IpcServiceSRFov.SharedMemoryWriter.Start();
            }


            string fullStartCommand = null;
            // 这个变量现在用来决定命令行参数，而不是启动方式
            bool hasCustomParams = !string.IsNullOrWhiteSpace(gameParam);

            if (hasCustomParams)
            {
                // CreateProcess 的第二个参数 lpCommandLine 接收完整的命令行
                // 注意：如果应用程序路径包含空格，最好将其用引号括起来。
                // 一个稳健的做法是总是将路径用引号括起来。
                fullStartCommand = $"\"{gamePathSR}\" {gameParam}";
                Console.WriteLine($"Starting game with command: {fullStartCommand}");
            }
            else
            {
                // 如果没有参数，lpCommandLine 可以只包含应用程序路径（同样建议用引号）
                fullStartCommand = $"\"{gamePathSR}\"";
                Console.WriteLine($"Starting game with default command: {fullStartCommand}");
            }

            // 准备 CreateProcess 需要的结构体
            var pi = new NativeAndStruct.PROCESS_INFORMATION();
            var si = new NativeAndStruct.STARTUPINFO();
            si.cb = Marshal.SizeOf(si); // 必须初始化 cb 字段

            var saProcess = new SECURITY_ATTRIBUTES();
            var saThread = new SECURITY_ATTRIBUTES();
            saProcess.nLength = Marshal.SizeOf(saProcess);
            saThread.nLength = Marshal.SizeOf(saThread);

            var launchOptions = _config.LaunchOptions;

            // 决定创建标志 251209
            CreationFlags creationFlags = 0;

            bool needSuspended = launchOptions.SuspendLoad || _config.EnableFOVStarRail || _config.EnableStarRailMbUIWt;
            if (_config.ApplySRLoadDll || needSuspended)
            {
                if (needSuspended)
                {
                    creationFlags |= CreationFlags.CREATE_SUSPENDED;
                    Console.WriteLine("Process will be created in a suspended state.");
                }
            }

            Process process = null;

            //定义移动UI进程变量251209
            Process procmbui = null;

            try
            {
                Console.WriteLine($"Attempting to create process: {fullStartCommand}");

                if (!ifManualLaunch)
                {
                    // 调用 CreateProcess
                    bool success = CreateProcess(
                        null,                   // lpApplicationName, 可以为 null，因为我们在 lpCommandLine 中指定了
                        fullStartCommand,       // lpCommandLine, 包含程序路径和参数
                        ref saProcess,          // 进程安全属性
                        ref saThread,           // 线程安全属性
                        false,                  // bInheritHandles, 不继承句柄
                        creationFlags,          // dwCreationFlags, 创建标志
                        IntPtr.Zero,            // lpEnvironment, 使用父进程的环境
                        null,                   // lpCurrentDirectory, 使用父进程的当前目录
                        ref si,                 // STARTUPINFO
                        out pi                  // 输出 PROCESS_INFORMATION
                    );

                    if (!success)
                    {
                        // 如果创建失败，抛出包含 Win32 错误信息的异常
                        int errorCode = Marshal.GetLastWin32Error();
                        throw new Win32Exception(errorCode, $"Failed to create process. Error code: {errorCode} ({Marshal.GetPInvokeErrorMessage(errorCode)})");
                    }

                    if (success)
                    {
                        ShouldSRExit = false;
                    }
                }

                if (ifManualLaunch)
                {
                    // 手动附加：将传入的 lpProcessInformation 复制到本地 pi
                    // 检查传入数据的合法性
                    if (lpProcessInformation.hProcess == IntPtr.Zero || lpProcessInformation.dwProcessId == 0)
                    {
                        throw new ArgumentException("lpProcessInformation 未正确初始化：hProcess 或 dwProcessId 为 0。");
                    }

                    pi.hProcess = lpProcessInformation.hProcess;
                    pi.hThread = lpProcessInformation.hThread;
                    // 支持 dwProcessId 类型为 uint/int 两种情况，做一次安全转换
                    try
                    {
                        // 如果定义为 uint/int 都能赋值（编译时类型已确定），这里只直接赋值
                        pi.dwProcessId = lpProcessInformation.dwProcessId;
                        pi.dwThreadId = lpProcessInformation.dwThreadId;
                    }
                    catch
                    {
                        //// 如果类型不匹配（极少见），用显式转换
                        //pi.dwProcessId = (int)Convert.ToUInt32(lpProcessInformation.dwProcessId);
                        //pi.dwThreadId = (int)Convert.ToUInt32(lpProcessInformation.dwThreadId);
                    }


                    Console.WriteLine($"Attached to existing process. PID: {pi.dwProcessId}, TID: {pi.dwThreadId}");
                    ShouldSRExit = false;
                }


                //// 调用 CreateProcess
                //bool success = CreateProcess(
                //    null,                   // lpApplicationName, 可以为 null，因为我们在 lpCommandLine 中指定了
                //    fullStartCommand,       // lpCommandLine, 包含程序路径和参数
                //    ref saProcess,          // 进程安全属性
                //    ref saThread,           // 线程安全属性
                //    false,                  // bInheritHandles, 不继承句柄
                //    creationFlags,          // dwCreationFlags, 创建标志
                //    IntPtr.Zero,            // lpEnvironment, 使用父进程的环境
                //    null,                   // lpCurrentDirectory, 使用父进程的当前目录
                //    ref si,                 // STARTUPINFO
                //    out pi                  // 输出 PROCESS_INFORMATION
                //);

                //if (!success)
                //{
                //    // 如果创建失败，抛出包含 Win32 错误信息的异常
                //    int errorCode = Marshal.GetLastWin32Error();
                //    throw new Win32Exception(errorCode, $"Failed to create process. Error code: {errorCode} ({Marshal.GetPInvokeErrorMessage(errorCode)})");
                //}

                //if (success)
                //{
                //    ShouldSRExit = false;
                //}

                Console.WriteLine($"Process created successfully. PID: {pi.dwProcessId}, TID: {pi.dwThreadId}");


                //开启StarRail移动UI 251209
                bool _isreadyStarRailMbUIWt = false;
                string appDirectorymbui = AppDomain.CurrentDomain.BaseDirectory;
                string mobileuiexe = Path.Combine(appDirectorymbui, "wTE_ysr251210b.exe");

                if (_config.EnableStarRailMbUIWt)
                {
                    if (File.Exists(mobileuiexe))
                    {
                        ProcessStartInfo startInfo = new ProcessStartInfo
                        {
                            FileName = mobileuiexe,
                            Arguments = "-hksr",
                            UseShellExecute = false
                        };

                        procmbui = Process.Start(startInfo);
                    }
                }


                //处理SRFov注入251205
                int _EnableFOVStarRail = 0;
                int _SRFovChageFix = 0;

                if (_config.EnableFOVStarRail == true)
                {
                    _EnableFOVStarRail = 2;
                }
                if(_config.EnableFOVStarRailFix == true)
                {
                    _SRFovChageFix = 2;
                }

                string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string dllPathsrget = Path.Combine(appDirectory, "UtilityUlk1.dll");

                string[] srfovdllPath = new string[]
                {
                    dllPathsrget,
                };

                if (_config.EnableFOVStarRail == true)
                {
                    if (!ProcessUtils.InjectDlls(pi.hProcess, srfovdllPath))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(),
                            $"Dll Injection failed. ({Marshal.GetLastPInvokeErrorMessage()})");
                    }
                    //Console.WriteLine("srfov DLL injected successfully.");

                    if (!ifManualLaunch && !_config.EnableStarRailMbUIWt)
                    {
                        //Console.WriteLine("Resuming the main thread...");
                        // 使用 CreateProcess 返回的线程句柄 pi.hThread
                        uint retCode = ResumeThread(pi.hThread);
                        if (retCode == 0xFFFFFFFF) // -1 表示失败
                        {
                            throw new Win32Exception(Marshal.GetLastWin32Error(),
                                $"Dll Injection failed. ({Marshal.GetLastPInvokeErrorMessage()})");
                        }
                        //Console.WriteLine("Main thread resumed.");
                    }

                    //launchOptions.DllList.Add(@"UtilityUlk1.dll");

                    IpcServiceSRFov.SharedMemoryWriter.UpdateOnce(datagameset =>
                    {
                        datagameset.SRFovValue = _config.FOVTargetStarRail;
                        datagameset.SRFovHookDepth = _config.SRFovDepth;
                        datagameset.SRFovHookSpeed = _config.SRFovSpeed;
                        datagameset.SRFovHookRunTime = _config.SRFovRuntime;
                        //1=disable 2=enable
                        datagameset.SRFovChangeEnabled = _EnableFOVStarRail;
                        datagameset.SRFovChageFix = _SRFovChageFix;
                        return datagameset;
                    });
                }


                // --- 核心操作：DLL 注入 ---
                // 使用 CreateProcess 返回的进程句柄 pi.hProcess
                if (_config.ApplySRLoadDll == true)
                {
                    if (!ProcessUtils.InjectDlls(pi.hProcess, launchOptions.DllList))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(),
                            $"Dll Injection failed. ({Marshal.GetLastPInvokeErrorMessage()})");
                    }
                    Console.WriteLine("DLLs injected successfully.");
                }

                // --- 核心操作：恢复线程 ---
                // 只有在之前挂起了线程的情况下才需要恢复
                if (_config.ApplySRLoadDll == true)
                {
                    if (launchOptions.SuspendLoad && !ifManualLaunch && !_config.EnableStarRailMbUIWt)
                    {
                        Console.WriteLine("Resuming the main thread...");
                        // 使用 CreateProcess 返回的线程句柄 pi.hThread
                        uint retCode = ResumeThread(pi.hThread);
                        if (retCode == 0xFFFFFFFF) // -1 表示失败
                        {
                            throw new Win32Exception(Marshal.GetLastWin32Error(),
                                $"Dll Injection failed. ({Marshal.GetLastPInvokeErrorMessage()})");
                        }
                        Console.WriteLine("Main thread resumed.");
                    }
                }

                ShouldSRExit = false;

                // 如果您仍然需要一个 Process 对象来管理进程（例如，等待退出或监控），
                // 可以通过 PID 获取它。但这不是必须的，因为我们已经有了 hProcess。
                process = Process.GetProcessById((int)pi.dwProcessId);
            }
            finally
            {
                // **极其重要**：必须关闭 CreateProcess 返回的进程和线程句柄
                // 这些句柄是内核对象，不关闭会导致资源泄漏。
                // 关闭句柄不会终止进程，它只是释放了我们当前进程中对这些对象的引用。
                //if (pi.hThread != IntPtr.Zero)
                //{
                //    CloseHandle(pi.hThread);
                //}
                //if (pi.hProcess != IntPtr.Zero)
                //{
                //    CloseHandle(pi.hProcess);
                //}
                //Console.WriteLine("Process and thread handles closed.");

                // 仅当我们通过 CreateProcess 创建新进程时，才关闭 CreateProcess 返回的句柄
                if (!ifManualLaunch)
                {
                    if (pi.hThread != IntPtr.Zero)
                    {
                        CloseHandle(pi.hThread);
                    }
                    if (pi.hProcess != IntPtr.Zero)
                    {
                        CloseHandle(pi.hProcess);
                    }
                    Console.WriteLine("Process and thread handles closed (created by CreateProcess).");
                }
                else
                {
                    // 手动附加时不要关闭传入的句柄（它们属于调用方/Process 对象）
                    Console.WriteLine("Manual launch: external process/thread handles left intact.");
                }

            }


            Console.WriteLine($"PID: {process.Id}");
            System.Threading.Thread.Sleep(200);

            process.PriorityClass = (ProcessPriorityClass)NativeAndStruct.PriorityClasses[1];

            MODULEENTRY32 hUnityPlayer = new MODULEENTRY32();
            hUnityPlayer.dwSize = (uint)Marshal.SizeOf(typeof(MODULEENTRY32));

            bool moduleFound = false;
            int maxAttempts = 100; // 最大尝试次数，例如100次，每次50ms，总超时5秒
            int attempts = 0;

            while (attempts < maxAttempts && !moduleFound)
            {
                IntPtr snapshot = CreateToolhelp32Snapshot(0x8, (uint)process.Id);
                if (snapshot == IntPtr.Zero)
                {
                    Console.WriteLine("CreateToolhelp32Snapshot failed.");
                    attempts++;
                    System.Threading.Thread.Sleep(50);
                    continue;
                }

                hUnityPlayer.dwSize = (uint)Marshal.SizeOf(typeof(MODULEENTRY32));
                if (Module32First(snapshot, ref hUnityPlayer))
                {
                    do
                    {
                        string moduleName = hUnityPlayer.szModule.Trim('\0'); // 去除可能的空字符
                        //if (moduleName.Equals(procName, StringComparison.OrdinalIgnoreCase))
                        if (moduleName.Equals("UnityPlayer.dll", StringComparison.OrdinalIgnoreCase))
                        {
                            moduleFound = true;
                            break;
                        }
                    } while (Module32Next(snapshot, ref hUnityPlayer));
                }
                CloseHandle(snapshot); // 每次循环结束后释放快照

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
                //CloseHandle(process.Handle);
                return;
            }

            Console.WriteLine($"UnityPlayer: {hUnityPlayer.modBaseAddr.ToInt64():X}");

            IntPtr absolute_address = IntPtr.Zero; // 初始化为默认值
            try
            {

                IntPtr mbasePEBuffer = VirtualAlloc(IntPtr.Zero, 0x1000, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
                if (mbasePEBuffer == IntPtr.Zero)
                {
                    Console.WriteLine("VirtualAlloc Failed! (PE_buffer)");
                    //CloseHandle(process.Handle);
                    return;
                }

                if (hUnityPlayer.modBaseAddr == IntPtr.Zero)
                {
                    Console.WriteLine("Module base address is zero!");
                    VirtualFree(mbasePEBuffer, 0, MEM_RELEASE);
                    //CloseHandle(process.Handle);
                    return;
                }

                uint bytesRead;
                if (!ReadProcessMemory(process.Handle, hUnityPlayer.modBaseAddr, mbasePEBuffer, 0x1000, out bytesRead))
                //if (!ReadProcessMemory(hProcess, hUnityPlayer.modBaseAddr, mbasePEBuffer, 0x1000, out bytesRead))
                {
                    Console.WriteLine("Readmem Failed! (PE_buffer)");
                    VirtualFree(mbasePEBuffer, 0, MEM_RELEASE);
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
                IMAGE_NT_HEADERS64 filePE_Nt_Header = Marshal.PtrToStructure<IMAGE_NT_HEADERS64>(peHeaderPtr);

                IMAGE_SECTION_HEADER secTemp;
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
                        secTemp = Marshal.PtrToStructure<IMAGE_SECTION_HEADER>(secHeaderPtr);

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
                        VirtualFree(mbasePEBuffer, 0, MEM_RELEASE);
                        //CloseHandle(process.Handle);
                        return;
                    }


                    // 这里可以添加对找到的节的进一步操作
                }
                else
                {
                    Console.WriteLine("Invalid PE header!");
                    VirtualFree(mbasePEBuffer, 0, MEM_RELEASE);
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
                bool readSuccess = ReadProcessMemory(process.Handle, textRemoteRVA, bufferPtr, textVSize, out bytesRead);

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
                //IntPtr relative_address = PatternScan_Region(copyTextVA, textVSize, "08 3E B1 B0 30 3E D9 D8 58 3E 00 00 80 3F 8D 8C 0C 3E F9 F8 F8 3D 81 80 00 3E 00 00 80 3F");

                IntPtr relative_address = PatternScan_Region(copyTextVA, textVSize, "4D 61 6E 61 67 65 72 73 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 47 55 49 45 76 65 6E 74 4D 61 6E 61 67 65 72");
                if (relative_address == IntPtr.Zero)
                {
                    Console.WriteLine("Pattern not found");

                    VirtualFree(copyTextVA, 0, MEM_RELEASE);
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

            // 崩铁
            IntPtr currentAddress = IntPtr.Add(absolute_address, 0); // 假设absoluteAddress是之前计算得到的绝对地址
            int moveCount = 0;
            const int maxMoves = 200;
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

                if (ReadProcessMemory(process.Handle, currentAddress, bufferPtr1, dwordSize, out bytesRead1))
                {
                    uint value = BitConverter.ToUInt32(buffer1, 0);
                    if (value == targetValue)
                    {
                        // 读取并打印左边地址的值
                        if (ReadProcessMemory(process.Handle, IntPtr.Subtract(currentAddress, dwordSize), bufferPtr1, dwordSize, out bytesRead1))
                        {
                            leftValue = BitConverter.ToUInt32(buffer1, 0);
                            //Console.WriteLine("Value at left address ({0}): {1}", IntPtr.Subtract(currentAddress, dwordSize).ToString("X"), leftValue);
                        }

                        // 读取并打印右边地址的值
                        if (ReadProcessMemory(process.Handle, IntPtr.Add(currentAddress, dwordSize), bufferPtr1, dwordSize, out bytesRead1))
                        {
                            rightValue = BitConverter.ToUInt32(buffer1, 0);
                            //Console.WriteLine("Value at right address ({0}): {1}", IntPtr.Add(currentAddress, dwordSize).ToString("X"), rightValue);
                        }

                        // 检查左边和右边的值是否为0
                        if (leftValue == 0 && rightValue == 0)
                        {
                            // 打印当前地址及其值
                            Console.WriteLine("Current address ({0}): {1}", currentAddress.ToString("X"), value);

                            // 进入无限循环写入150
                            uint writeValue = 1500;
                            byte[] writeBuffer = BitConverter.GetBytes(writeValue);
                            int bytesWritten;

                            //while (true)
                            //{
                            //    WriteProcessMemory(process.Handle, currentAddress, writeBuffer, dwordSize, out bytesWritten);
                            //    // 在这里可以添加一个短暂的休眠，以避免CPU占用过高
                            //    //System.Threading.Thread.Sleep(1); // 休眠100毫秒

                            //    if (process.HasExited)
                            //    //if (pi.dwProcessId.HasExited)
                            //    {
                            //        Console.WriteLine("\nGame Terminated !\n");
                            //        break; // 进程已终止，退出循环

                            //    }

                            //    Thread.Sleep(1000);

                            //}

                            //251205
                            bool isFOVSROn = false;

                            // 假设初始状态是开启省电的，如果不确定，可以先读取一次 _config.UsePowerSaveSR
                            bool wasPowerSaveOn = _config.UsePowerSaveSR;
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

                                            bool processmbuiExited = false;
                                            if (_config.EnableStarRailMbUIWt == true)
                                            {
                                                // 等待进程退出，最多等待60秒
                                                processmbuiExited = SpinWait.SpinUntil(() =>
                                                {
                                                    // 检查进程是否为null或已退出
                                                    if (procmbui == null)
                                                    {
                                                        return false; // 进程未启动，继续等待
                                                    }

                                                    return procmbui.HasExited; // 返回true表示进程已退出，停止等待

                                                }, TimeSpan.FromSeconds(10)); // 最多等待10秒超时

                                                if (processmbuiExited)
                                                {
                                                    //Console.WriteLine("mbui进程已退出");
                                                }
                                                if (!processmbuiExited)
                                                {
                                                    //Console.WriteLine("等待mbui进程退出超时");
                                                }
                                            }

                                            Thread.Sleep(1517);
                                            Console.WriteLine("Switching to IPC mode...");

                                            string StarRailGuid = "741E764D-0EB9-EE23-BC9B-EEE71F0D64C1"; ;
                                            ipcService.Start(process.Id, fpsAddress, StarRailGuid);
                                            //ipcService.Start(process.Id, fpsAddress);
                                            useIpc = true;

                                            ipcService.PreventGenshinIllegalToolError(2);

                                            //251205
                                            if (_config.EnableFOVStarRail == true)
                                            {
                                                isFOVSROn = true;
                                            }

                                            ////先应用FPS限制再应用非法工具错误预防，否则导致无法写入
                                            //if (_config.ForceUmlimitedFps == true)
                                            //{
                                            //    ipcService.PreventGenshinIllegalToolError(1);
                                            //}
                                            //else
                                            //{
                                            //    ipcService.PreventGenshinIllegalToolError(2);
                                            //}

                                            //// 进入IPC模式时启动保持状态
                                            //isHoldingFps = true;
                                            //holdTimer.Restart(); // 开始计时
                                            //Console.WriteLine($"开始保持FPS为{HoldFpsValue}，持续{HoldDurationSeconds}秒");
                                        }
                                    }
                                    else
                                    {
                                        int targetFPS = 1500;
                                        int targetFPSvsr = _config.FpsTargetSR;
                                        if (_config.UmlimitedFpsStarRail == true)
                                        {
                                            targetFPSvsr = -1;
                                        }

                                        if (_config.UsePowerSaveSR == true)
                                        {
                                            bool isFocused = NativeAndStruct.IsProcessFocused(process.Id);

                                            if (isFocused == false)
                                            {
                                                targetFPSvsr = 15;
                                            }
                                        }

                                        // 通过IPC更新FPS
                                        ipcService.ApplyFpsLimit(targetFPSvsr);

                                        int currentPriority = _config.ProcessPrioritySR;
                                        // 仅在值发生变化时（或首次）设置
                                        if (currentPriority != lastProcessPriority)
                                        {
                                            // 确保优先级值在有效范围内（0-5）
                                            if (currentPriority >= 0 && currentPriority <= 5)
                                            {
                                                ipcService.ProcessPriorityWrite(currentPriority);
                                                Console.WriteLine($"StarRail Process Priority: {currentPriority}");
                                                lastProcessPriority = currentPriority; // 更新记录的值
                                            }
                                        }


                                        if (_config.UsePowerSaveSR == true)
                                        {
                                            bool isFocused = NativeAndStruct.IsProcessFocused(process.Id);

                                            if (isFocused == false && powerSaveState == false)
                                            {
                                                ipcService.ProcessPriorityWrite(5);
                                                powerSaveState = true;
                                                //Console.WriteLine($"Is SR focused: {isFocused}");
                                            }

                                            if (isFocused == true && powerSaveState == true)
                                            {
                                                ipcService.ProcessPriorityWrite(currentPriority);
                                                powerSaveState = false;
                                                //Console.WriteLine($"Is SR focused: {isFocused}");
                                            }
                                        }

                                        // 获取当前状态
                                        bool isPowerSaveOnNow = _config.UsePowerSaveSR;

                                        // 核心判断逻辑：检测从 true 到 false 的变化
                                        if (wasPowerSaveOn && !isPowerSaveOnNow)
                                        {
                                            // xxx 代码块
                                            //Console.WriteLine("检测到 UsePowerSaveSR 从 true 变为 false，执行 xxx 操作一次。");
                                            Console.WriteLine("StarRail Power Saving Mode off");
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

                                        //251205 SR FOV功能
                                        if (_config.EnableFOVStarRail == true && isFOVSROn == true)
                                        {
                                            int srfov = _config.FOVTargetStarRail;

                                            if (_config.FOVTargetStarRail <= 15)
                                            {
                                                srfov = 15;
                                            }

                                            if (_config.FOVTargetStarRail >= 165)
                                            {
                                                srfov = 165;
                                            }

                                            //int _EnableFOVStarRail = 0;
                                            int _EnableFOVStarRailFix = 0;
                                            //if (_config.EnableFOVStarRail == true)
                                            //{
                                            //    _EnableFOVStarRail = 2;
                                            //}
                                            //else
                                            //{
                                            //    _EnableFOVStarRail = 1;
                                            //}
                                            if (_config.EnableFOVStarRailFix == true)
                                            {
                                                _EnableFOVStarRailFix = 2;
                                            }
                                            else
                                            {
                                                _EnableFOVStarRailFix = 1;
                                            }

                                            IpcServiceSRFov.SharedMemoryWriter.UpdateOnce(datagameset => {
                                                datagameset.SRFovValue = _config.FOVTargetStarRail;
                                                //1=disable 2=enable
                                                datagameset.SRFovChangeEnabled = 2;
                                                datagameset.SRFovChageFix = _EnableFOVStarRailFix;
                                                return datagameset;
                                            });
                                        }


                                        if (_config.EnableFOVStarRail == false && isFOVSROn == true)
                                        {
                                            IpcServiceSRFov.SharedMemoryWriter.UpdateOnce(datagameset => {
                                                //1=disable 2=enable
                                                datagameset.SRFovChangeEnabled = 1;
                                                return datagameset;
                                            });
                                        }

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

            ////停止SRFoV共享内存服务
            //IpcServiceSRFov.SharedMemoryWriter.Stop();

            // 停止SRFov服务已转移到MainWindow.cs的程序退出处 260202

            // 如果未找到符合条件的地址
            Console.WriteLine("Did not find the target value or its neighbors.");
            ShouldSRExit = true;
            GameExitRequested?.Invoke(null, EventArgs.Empty);
        }


        // 解析SR游戏路径并读取版本是CN还是国际服 260215
        private int Detect_SR_GameVersion()
        {

            int _gameversion = 0;
            if (_config.DetectSRCustomResolutionMode == 0)
            {
                string srgamepath = _config.LaunchOptions.SRGamePath;
                string gameDir = Path.GetDirectoryName(srgamepath);          // 获取目录
                string iniFilePath = Path.Combine(gameDir, "config.ini");    // 拼接 INI 文件路径

                string uapc = ReadIniValue(iniFilePath, "General", "uapc");

                string versionmsg = "";

                if (uapc.Contains("hkrpg_cn"))
                {
                    //Console.WriteLine("CN");
                    versionmsg = "CN";

                    _gameversion = 1;
                }
                else if (uapc.Contains("hkrpg_global"))
                {
                    //Console.WriteLine("GL");
                    versionmsg = "global";

                    _gameversion = 2;
                }
                else
                {
                    Console.WriteLine("Star Rail:Unable to automatically detect the game version; please manually set the DetectSRCustomResolutionMode parameter in the config.ini file, where 1 = CN version and 2 = GLOBAL version");

                    versionmsg = "Unknown";
                }

                var resolution = RegistryHelper.GetCurrentResolution(_gameversion);
                if (resolution.HasValue)
                {
                    string output = $"Star Rail:CURR RESL:{resolution.Value.width}x{resolution.Value.height},SCR STAT:{resolution.Value.isFullScreen},Game Version:{versionmsg}";

                    //Console.WriteLine(output);
                }
            }

            if (_config.DetectSRCustomResolutionMode == 1)
            {
                _gameversion = 1;
            }
            if (_config.DetectSRCustomResolutionMode == 2)
            {
                _gameversion = 2;
            }

            return _gameversion;
        }
    }
}
