using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using UnlockFps.Utils;
using static UnlockFps.Services.NativeAndStruct;

namespace UnlockFps.Services
{
    public enum IpcStatus
    {
        Error = -1,
        None = 0,
        HostAwaiting = 1,
        ClientReady = 2,
        ClientExit = 3,
        HostExit = 4
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct IpcData
    {
        public ulong Address;
        public int Value;
        public IpcStatus Status;
        public int PreventIllegalToolStatus;
        public int ProcessPriorityStatus;
        public int FOVgenshinValue1;
        public int FOVgenshinStatus1;
        public int FOVgenshinStatusFix;
        public int DisableGenshinBlurStatus;
        public int DisplayGenshinFogStatus;
        public int GenshinAdvanceToolMask;
    }

    public class IpcService : IDisposable
    {
        private bool _started = false;
        private MemoryMappedFile _sharedMemory;
        private MemoryMappedViewAccessor _sharedMemoryAccessor;
        //private string _stubPath = string.Empty;
        private static string _stubPath = string.Empty;
        private IntPtr _stubModule = IntPtr.Zero;
        private IntPtr _wndHook = IntPtr.Zero;

        public void Start(int processId, IntPtr pFpsValue,string shareguid)
        {
            if (_started) return;

            try
            {
                //// 创建共享内存
                //_sharedMemory = MemoryMappedFile.CreateOrOpen(
                //    "BDA1DCAE-96C9-4B4A-A5A4-A43DC86DB253",
                //    4096,
                //    MemoryMappedFileAccess.ReadWrite);

                // 创建共享内存
                _sharedMemory = MemoryMappedFile.CreateOrOpen(
                    shareguid,
                    4096,
                    MemoryMappedFileAccess.ReadWrite);


                _sharedMemoryAccessor = _sharedMemory.CreateViewAccessor();

                // 初始化IPC数据
                WriteToSharedMemory(pFpsValue, 60, IpcStatus.HostAwaiting);

                // 加载Stub DLL
                //_stubPath = GetUnlockerStubPath();
                //_stubPath = path;
                Console.WriteLine("Current DLL Path: "+_stubPath);

                _stubModule = LoadLibrary(_stubPath);
                if (_stubModule == IntPtr.Zero)
                    throw new Exception("Failed to load stub DLL");

                // 获取窗口线程
                IntPtr targetWindow = GetWindowFromProcessId(processId);
                uint threadId = GetWindowThreadProcessId(targetWindow, out _);

                // 设置钩子
                IntPtr stubWndProc = GetProcAddress(_stubModule, "WndProc");
                _wndHook = SetWindowsHookEx(3, stubWndProc, _stubModule, threadId);
                if (_wndHook == IntPtr.Zero)
                    throw new Exception("Failed to set hook");

                // 触发钩子加载
                if (!PostThreadMessage(threadId, 0, IntPtr.Zero, IntPtr.Zero))
                    throw new Exception("Failed to post message");


                //// 触发钩子加载_修改触发代码
                //const uint WM_NULL = 0x0000;
                //if (targetWindow != IntPtr.Zero)
                //{
                //    if (!PostMessage(targetWindow, WM_NULL, IntPtr.Zero, IntPtr.Zero))
                //    {
                //        Console.WriteLine("Warning: Failed to post window message");
                //    }
                //    else
                //    {
                //        Console.WriteLine("post window message OK");
                //    }
                //}
                //else
                //{
                //    Console.WriteLine("Warning: Target window not found");
                //}

                // 等待客户端就绪
                int retryCount = 0;
                while (true)
                {
                    IpcData ipcData = new IpcData();
                    _sharedMemoryAccessor.Read(0, out ipcData);

                    if (ipcData.Status == IpcStatus.ClientReady) break;
                    if (retryCount++ >= 10)
                        throw new Exception("Client not ready");

                    Thread.Sleep(1000);
                }

                _started = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"IPC Start failed: {ex.Message}");
                Stop();
            }
        }

        public static void getStubPath(string path)
        {
            _stubPath = path;
        }

        //public static IntPtr GetWindowFromProcessId(int processId)
        //{
        //    IntPtr mainWindow = IntPtr.Zero;
        //    uint targetPid = (uint)processId;

        //    EnumWindows(delegate (IntPtr hWnd, IntPtr param)
        //    {
        //        GetWindowThreadProcessId(hWnd, out uint windowPid);

        //        if (windowPid == targetPid)
        //        {
        //            // 检查窗口是否可见且有标题（更可能是主GUI线程）
        //            if (IsWindowVisible(hWnd))
        //            {
        //                int length = GetWindowTextLength(hWnd);
        //                if (length > 0) // 有标题的窗口
        //                {
        //                    mainWindow = hWnd;
        //                    return false; // 找到主窗口，停止枚举
        //                }
        //            }
        //        }
        //        return true;
        //    }, IntPtr.Zero);

        //    return mainWindow;
        //}

        public void ApplyFpsLimit(int fps)
        {
            if (!_started) return;
            //WriteToSharedMemory(IntPtr.Zero, fps, IpcStatus.None);

            // 1. 先从共享内存中读取当前的数据
            IpcData currentIpcData = new IpcData();
            _sharedMemoryAccessor.Read(0, out currentIpcData);

            // 2. 只修改需要更新的字段 (FPS)
            currentIpcData.Value = fps;
            // 注意：这里我们不改变 Status 和 PreventIllegalTool，保留它们原有的值

            // 3. 将修改后的完整数据对象写回共享内存
            _sharedMemoryAccessor.Write(0, ref currentIpcData);
        }

        public void Stop()
        {
            if (_started)
            {
                WriteToSharedMemory(IntPtr.Zero, 0, IpcStatus.HostExit);
                Thread.Sleep(200);
            }

            if (_wndHook != IntPtr.Zero) UnhookWindowsHookEx(_wndHook);
            if (_stubModule != IntPtr.Zero) FreeLibrary(_stubModule);

            _sharedMemoryAccessor?.Dispose();
            _sharedMemory?.Dispose();

            _started = false;
        }

        private void WriteToSharedMemory(IntPtr address, int fps, IpcStatus status)
        {
            IpcData ipcData = new IpcData
            {
                Address = (ulong)address,
                Value = fps,
                Status = status,
                PreventIllegalToolStatus = 0,
                ProcessPriorityStatus = -1,
                FOVgenshinStatus1 = 0,
                FOVgenshinValue1 = 1000,
                FOVgenshinStatusFix = 1000,
                DisableGenshinBlurStatus = 0,
                DisplayGenshinFogStatus = 0,
                GenshinAdvanceToolMask = -1
            };

            _sharedMemoryAccessor?.Write(0, ref ipcData);
        }

        //private string GetUnlockerStubPath()
        //{
        //    //// 从资源或当前目录加载Stub DLL
        //    //string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UnlockerStub-IPC.dll");
        //    //if (!File.Exists(filePath))
        //    //{
        //    //    // 如果不存在则从资源写入
        //    //    //byte[] stubBytes = Properties.Resources.UnlockerStub;
        //    //    //File.WriteAllBytes(filePath, stubBytes);
        //    //}


        //    //var assembly = Assembly.GetExecutingAssembly();
        //    //var stream = assembly.GetManifestResourceStream("ConsoleApp1genshin.Resources.UnlockerStub-IPC.dll");
        //    //var filePath = Path.Combine(AppContext.BaseDirectory, "UnlockerStub-IPC.dll");
        //    //var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        //    //stream.CopyTo(fileStream);


        //    // 确保资源名称完全正确
        //    //string resourceName = "ConsoleApp1genshin.Resources.UnlockerStub-IPC.dll";
        //    string defaultNamespace = Assembly.GetExecutingAssembly().GetName().Name;
        //    string resourceName = $"{defaultNamespace}.Resources.UnlockerStub-IPC.dll";
        //    string filePath = Path.Combine(AppContext.BaseDirectory, "UnlockerStub-IPC.dll");

        //    //// 检查是否已存在且最新（可选）
        //    //if (File.Exists(filePath))
        //    //    return filePath;

        //    var assembly = Assembly.GetExecutingAssembly();

        //    // 打印所有嵌入资源名称
        //    Console.WriteLine("可用的嵌入资源:");
        //    foreach (var name in assembly.GetManifestResourceNames())
        //    {
        //        Console.WriteLine(name);
        //    }

        //    // 验证资源是否存在
        //    if (!assembly.GetManifestResourceNames().Contains(resourceName))
        //        throw new Exception($"资源 '{resourceName}' 未找到");

        //    // 使用 using 确保流正确关闭
        //    using (var stream = assembly.GetManifestResourceStream(resourceName))
        //    {
        //        if (stream == null)
        //            throw new Exception($"无法加载资源 '{resourceName}'");

        //        using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
        //        {
        //            stream.CopyTo(fileStream);
        //        }
        //    }

        //    return filePath;
        //}

        //public void Dispose() => Stop();

        public void Dispose()
        {
            Stop();
            _sharedMemoryAccessor?.Dispose();
            _sharedMemory?.Dispose();
        }


        //public void PreventGenshinIllegalToolError(bool ifwrite)
        //{
        //    if (ifwrite)
        //    {
        //        WriteToSharedMemory(怎么写入PreventIllegalTool = true);
        //    }
        //}

        public void PreventGenshinIllegalToolError(int ifwrite)
        {

            // Read current IPC data first to preserve existing values
            IpcData ipcData = new IpcData();
            _sharedMemoryAccessor.Read(0, out ipcData);

            // Update only the PreventIllegalTool flag
            ipcData.PreventIllegalToolStatus = ifwrite;

            // Write back to shared memory
            _sharedMemoryAccessor.Write(0, ref ipcData);
        }

        public void ProcessPriorityWrite(int ifwrite)
        {

            // Read current IPC data first to preserve existing values
            IpcData ipcData = new IpcData();
            _sharedMemoryAccessor.Read(0, out ipcData);

            // Update only the PreventIllegalTool flag
            ipcData.ProcessPriorityStatus = ifwrite;

            // Write back to shared memory
            _sharedMemoryAccessor.Write(0, ref ipcData);
        }

        //251106
        public void GenshinFOVValueTransfer(int fovvalue)
        {
            // Read current IPC data first to preserve existing values
            IpcData ipcData = new IpcData();
            _sharedMemoryAccessor.Read(0, out ipcData);

            // Update only the PreventIllegalTool flag
            ipcData.FOVgenshinValue1 = fovvalue;

            // Write back to shared memory
            _sharedMemoryAccessor.Write(0, ref ipcData);
        }

        public void GenshinFOVValueTransferStatus(int fovstatus)
        {
            // Read current IPC data first to preserve existing values
            IpcData ipcData = new IpcData();
            _sharedMemoryAccessor.Read(0, out ipcData);

            // Update only the PreventIllegalTool flag
            ipcData.FOVgenshinStatus1 = fovstatus;

            // Write back to shared memory
            _sharedMemoryAccessor.Write(0, ref ipcData);
        }

        public void GenshinFOVValueFixStatus(int fovstatus)
        {
            // Read current IPC data first to preserve existing values
            IpcData ipcData = new IpcData();
            _sharedMemoryAccessor.Read(0, out ipcData);

            // Update only the PreventIllegalTool flag
            ipcData.FOVgenshinStatusFix = fovstatus;

            // Write back to shared memory
            _sharedMemoryAccessor.Write(0, ref ipcData);
        }

        public void GenshinDisableGenshinBlurStatus(int dgblurstatus)
        {
            // Read current IPC data first to preserve existing values
            IpcData ipcData = new IpcData();
            _sharedMemoryAccessor.Read(0, out ipcData);

            // Update only the PreventIllegalTool flag
            ipcData.DisableGenshinBlurStatus = dgblurstatus;

            // Write back to shared memory
            _sharedMemoryAccessor.Write(0, ref ipcData);
        }

        public void GenshinDisplayFogStatus(int disfovtatus)
        {
            // Read current IPC data first to preserve existing values
            IpcData ipcData = new IpcData();
            _sharedMemoryAccessor.Read(0, out ipcData);

            // Update only the PreventIllegalTool flag
            ipcData.DisplayGenshinFogStatus = disfovtatus;

            // Write back to shared memory
            _sharedMemoryAccessor.Write(0, ref ipcData);
        }

        public void GenshinAdvanceToolMaskStatus(int maskstatus)
        {
            // Read current IPC data first to preserve existing values
            IpcData ipcData = new IpcData();
            _sharedMemoryAccessor.Read(0, out ipcData);

            // Update only the PreventIllegalTool flag
            ipcData.GenshinAdvanceToolMask = maskstatus;

            // Write back to shared memory
            _sharedMemoryAccessor.Write(0, ref ipcData);
        }
    }
}
