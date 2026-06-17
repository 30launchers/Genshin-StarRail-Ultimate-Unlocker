using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static UnlockFps.Utils.Native;

namespace UnlockFps.Services
{
    //自定义结构体和导入本地方法250901
    internal class NativeAndStruct
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        [DllImport("kernel32.dll")]
        public static extern bool Module32First(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

        [DllImport("kernel32.dll")]
        public static extern bool Module32Next(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr VirtualAlloc(IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool VirtualFree(IntPtr lpAddress, uint dwSize, uint dwFreeType);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, IntPtr lpBuffer, uint nSize, out uint lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out int lpNumberOfBytesWritten);

        // Native方法声明
        [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true)]
        public static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, IntPtr lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetExitCodeProcess(IntPtr handle, out uint exitCode);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool CreateProcess(
        string lpApplicationName,
        string lpCommandLine,
        ref SECURITY_ATTRIBUTES lpProcessAttributes,
        ref SECURITY_ATTRIBUTES lpThreadAttributes,
        bool bInheritHandles,
        CreationFlags dwCreationFlags,
        IntPtr lpEnvironment,
        string lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.U4)]
        public static extern uint ResumeThread(IntPtr hThread); // 或者 nint hThread

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        //[DllImport("user32.dll", SetLastError = true)]
        //private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [StructLayout(LayoutKind.Sequential)]
        public struct MODULEENTRY32
        {
            public uint dwSize;
            public uint th32ModuleID;
            public uint th32ProcessID;
            public uint GlblcntUsage;
            public uint ProccntUsage;
            public IntPtr modBaseAddr;
            public uint modBaseSize;
            public IntPtr hModule;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szModule;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExePath;
        };

        // Define structures
        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGE_NT_HEADERS64
        {
            public uint Signature;
            public IMAGE_FILE_HEADER FileHeader;
            public IMAGE_OPTIONAL_HEADER64 OptionalHeader;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGE_FILE_HEADER
        {
            public ushort Machine;
            public ushort NumberOfSections;
            public uint TimeDateStamp;
            public uint PointerToSymbolTable;
            public uint NumberOfSymbols;
            public ushort SizeOfOptionalHeader;
            public ushort Characteristics;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGE_OPTIONAL_HEADER64
        {
            public ushort Magic;
            public byte MajorLinkerVersion;
            public byte MinorLinkerVersion;
            public uint SizeOfCode;
            public uint SizeOfInitializedData;
            public uint SizeOfUninitializedData;
            public uint AddressOfEntryPoint;
            public uint BaseOfCode;
            public ulong ImageBase;
            public uint SectionAlignment;
            public uint FileAlignment;
            public ushort MajorOperatingSystemVersion;
            public ushort MinorOperatingSystemVersion;
            public ushort MajorImageVersion;
            public ushort MinorImageVersion;
            public ushort MajorSubsystemVersion;
            public ushort MinorSubsystemVersion;
            public uint Win32VersionValue;
            public uint SizeOfImage;
            public uint SizeOfHeaders;
            public uint CheckSum;
            public ushort Subsystem;
            public ushort DllCharacteristics;
            public ulong SizeOfStackReserve;
            public ulong SizeOfStackCommit;
            public ulong SizeOfHeapReserve;
            public ulong SizeOfHeapCommit;
            public uint LoaderFlags;
            public uint NumberOfRvaAndSizes;
            public IMAGE_DATA_DIRECTORY DataDirectory;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGE_DATA_DIRECTORY
        {
            public uint VirtualAddress;
            public uint Size;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct IMAGE_SECTION_HEADER
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] Name;
            public MiscUnion Misc;
            public uint VirtualAddress;
            public uint SizeOfRawData;
            public uint PointerToRawData;
            public uint PointerToRelocations;
            public uint PointerToLinenumbers;
            public ushort NumberOfRelocations;
            public ushort NumberOfLinenumbers;
            public uint Characteristics;

            // Misc联合体
            [StructLayout(LayoutKind.Explicit)]
            public struct MiscUnion
            {
                [FieldOffset(0)]
                public uint VirtualSize;
                [FieldOffset(0)]
                public uint PhysicalAddress;
            }
        }

        // CreateProcess 函数标志
        [Flags]
        public enum CreationFlags : uint
        {
            CREATE_SUSPENDED = 0x00000004,
            DETACHED_PROCESS = 0x00000008,
            CREATE_NEW_CONSOLE = 0x00000010,
            // 可以根据需要添加更多标志
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public bool bInheritHandle;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public uint dwX;
            public uint dwY;
            public uint dwXSize;
            public uint dwYSize;
            public uint dwXCountChars;
            public uint dwYCountChars;
            public uint dwFillAttribute;
            public uint dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }

        // 定义内存分配和释放的常量
        public const uint MEM_COMMIT = 0x1000;
        public const uint MEM_RESERVE = 0x2000;
        public const uint MEM_RELEASE = 0x8000;
        public const uint PAGE_READWRITE = 0x04;

        // Priority classes
        public static readonly int[] PriorityClasses = {
            (int)ProcessPriorityClass.RealTime,
            (int)ProcessPriorityClass.High,
            (int)ProcessPriorityClass.AboveNormal,
            (int)ProcessPriorityClass.Normal,
            (int)ProcessPriorityClass.BelowNormal,
            (int)ProcessPriorityClass.Idle
        };

        public static unsafe IntPtr PatternScan_Region(IntPtr startAddress, long regionSize, string signature)
        {
            List<byte> pattern_to_byte(string pattern)
            {
                List<byte> bytes = new List<byte>();
                string[] tokens = pattern.Split(' ');
                foreach (string token in tokens)
                {
                    if (token == "??")
                    {
                        bytes.Add(0x00); // 使用0x00作为通配符的占位符
                    }
                    else
                    {
                        bytes.Add(byte.Parse(token, System.Globalization.NumberStyles.HexNumber));
                    }
                }
                return bytes;
            }

            List<byte> patternBytes = pattern_to_byte(signature);
            byte* scanBytes = (byte*)startAddress.ToPointer();

            for (long i = 0; i < regionSize - patternBytes.Count; ++i)
            {
                bool found = true;
                for (int j = 0; j < patternBytes.Count; ++j)
                {
                    if (scanBytes[i + j] != patternBytes[j] && patternBytes[j] != 0x00)
                    {
                        found = false;
                        break;
                    }
                }
                if (found)
                {
                    return new IntPtr(&scanBytes[i]);
                }
            }
            return IntPtr.Zero;
        }

        public static IntPtr GetWindowFromProcessId(int processId)
        {
            IntPtr mainWindow = IntPtr.Zero;
            uint targetPid = (uint)processId;

            const string unityWindowClassName = "UnityWndClass"; // Unity主窗口的类名

            EnumWindows(delegate (IntPtr hWnd, IntPtr param)
            {
                GetWindowThreadProcessId(hWnd, out uint windowPid);

                if (windowPid == targetPid)
                {
                    //// 检查窗口是否可见且有标题（更可能是主GUI线程）
                    //if (IsWindowVisible(hWnd))
                    //{
                    //    int length = GetWindowTextLength(hWnd);
                    //    if (length > 0) // 有标题的窗口
                    //    {
                    //        mainWindow = hWnd;
                    //        return false; // 找到主窗口，停止枚举
                    //    }
                    //}

                    // 检查窗口是否可见且有标题（更可能是主GUI线程）
                    //添加对窗口类名检测250903
                    if (IsWindowVisible(hWnd))
                    {
                        int length = GetWindowTextLength(hWnd);
                        if (length > 0) // 有标题的窗口
                        {
                            // --- 新增部分：检测类名 ---
                            // 1. 创建一个StringBuilder来接收类名
                            StringBuilder className = new StringBuilder(256);

                            // 2. 调用GetClassName获取窗口类名
                            GetClassName(hWnd, className, className.Capacity);

                            // 3. 检查类名是否为 "UnityWndClass"
                            if (className.ToString() == unityWindowClassName)
                            {
                                // 找到了完美的匹配：可见、有标题、且是Unity主窗口类
                                mainWindow = hWnd;
                                return false; // 找到目标，立即停止枚举
                            }
                        }
                    }
                }
                return true;
            }, IntPtr.Zero);

            return mainWindow;
        }

        const uint STILL_ACTIVE = 0x103; // 259 的十六进制表示

        public static bool IsGameRunning(IntPtr gameHandle)
        {
            if (gameHandle == IntPtr.Zero)
                return false;

            if (!GetExitCodeProcess(gameHandle, out uint exitCode))
            {
                Console.WriteLine($"[错误] 获取退出代码失败: {Marshal.GetLastWin32Error()}");
                return false;
            }

            return exitCode == STILL_ACTIVE;
        }

        public static bool IsProcessFocused(int processId)
        {
            IntPtr foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero)
            {
                // 没有前景窗口
                return false;
            }

            uint windowProcessId;
            GetWindowThreadProcessId(foregroundWindow, out windowProcessId);

            return windowProcessId == processId;
        }

        // 251230
        // ==================== LauncherPro.dll 函数导入 ====================
        //private const string LauncherDll = @"ulk_ysr_tools\advantol.dll";

        //[DllImport(LauncherDll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        //private static extern int LaunchGameAndInject(
        //    [MarshalAs(UnmanagedType.LPWStr)] string gamePath,
        //    [MarshalAs(UnmanagedType.LPWStr)] string dllPath,
        //    [MarshalAs(UnmanagedType.LPWStr)] string commandLineArgs,
        //    [MarshalAs(UnmanagedType.LPWStr)] StringBuilder errorMessage,
        //    int errorMessageSize);

        //[DllImport(LauncherDll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        //public static extern void UpdateConfig(
        //    [MarshalAs(UnmanagedType.LPWStr)] string gamePath,
        //    int hideQuestBanner,
        //    int disableDamageText,
        //    int useTouchScreen,
        //    int disableEventCameraMove,
        //    int removeTeamProgress,
        //    int redirectCombineEntry,
        //    int resin106,
        //    int resin201,
        //    int resin107009,
        //    int resin107012,
        //    int resin220007);

        //[DllImport(LauncherDll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        //private static extern int GetDefaultDllPath([MarshalAs(UnmanagedType.LPWStr)] StringBuilder dllPath,int dllPathSize);

        //// 260117
        //[DllImport("user32.dll")]
        //public static extern short GetAsyncKeyState(int vKey);


        // 260215 读取ini文件
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetPrivateProfileString(
            string lpAppName,
            string lpKeyName,
            string lpDefault,
            StringBuilder lpReturnedString,
            int nSize,
            string lpFileName);

        public static string ReadIniValue(string iniFilePath, string section, string key)
        {
            var sb = new StringBuilder(255);
            GetPrivateProfileString(section, key, "", sb, sb.Capacity, iniFilePath);
            return sb.ToString();
        }
    }
}
