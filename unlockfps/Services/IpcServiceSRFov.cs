using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace UnlockFps.Services
{
    public class IpcServiceSRFov
    {
        private static MemoryMappedFile _sharedMemory;
        private static EventWaitHandle _dataReadyEvent;
        private static EventWaitHandle _dataConsumedEvent;

        public enum IPCStatus : int
        {
            Idle,
            Running,
            Error
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
        public struct SharedData
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string currentstring;
            public IPCStatus Status;
            public int SRFovValue;
            public int SRFovChangeEnabled;
            public int SRFovChageFix;
            public int SRFovHookSpeed;
            public int SRFovHookDepth;
            public int SRFovHookRunTime;
            // 260202
            public int GenshinFog;
        }

        public class SharedMemoryWriter
        {
            private static readonly int _structureSize = Marshal.SizeOf<SharedData>();

            // 定义带 ref 参数的委托
            public delegate void UpdateSharedData(ref SharedData data);

            private static bool _isStarted = false;

            private static bool _isClosedSharedMemory = false;

            public static void Start()
            {
                if (_isStarted)
                {
                    return;
                }

                // 在注入DLL之前创建共享内存
                _sharedMemory = MemoryMappedFile.CreateOrOpen(
                    "3FDA00E1-3F3D-A33B-706C-3A11FEBFA687",
                    4096,
                    MemoryMappedFileAccess.ReadWrite);

                // 创建事件（用于同步）
                _dataReadyEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "89597B6A-FA92-21BD-1E7D-07AAAF1DCDE8");

                _dataConsumedEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "C1B99146-EB12-08F4-A972-561CAFE52C78");

                // 初始化初始化共享内存数据，但不等待
                InitializeSharedData();

                _isStarted = true;
            }

            public static void Stop()
            {
                if (!_isStarted)
                {
                    // 如果已经停止或从未启动，则直接返回
                    return;
                }

                try
                {
                    if (_sharedMemory != null)
                    {
                        _isClosedSharedMemory = true;
                        _sharedMemory.Dispose(); // 释放 MemoryMappedFile 资源
                        _dataReadyEvent?.Dispose();
                        _dataConsumedEvent?.Dispose();

                        _dataReadyEvent = null;
                        _dataConsumedEvent = null;
                        _sharedMemory = null;    // 将引用置空，帮助GC并防止误用
                        //Console.WriteLine("[IPC] 共享内存已成功释放。");
                    }
                }
                catch (Exception ex)
                {
                    // 即使释放时出错，也要尝试将状态重置
                    //Console.WriteLine($"[IPC] 释放共享内存时发生错误: {ex.Message}");
                }
                finally
                {
                    // 无论成功与否，都将状态标志重置
                    _isStarted = false;
                }
            }

            private static byte[] StructureToByteArray(SharedData structure)
            {
                byte[] arr = new byte[_structureSize];
                IntPtr ptr = Marshal.AllocHGlobal(_structureSize);

                try
                {
                    Marshal.StructureToPtr(structure, ptr, false);
                    Marshal.Copy(ptr, arr, 0, _structureSize);
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }
                return arr;
            }

            // 添加静态读取方法
            public static SharedData ReadDataStatic()
            {
                try
                {
                    if (_sharedMemory == null)
                    {
                        //Console.WriteLine("ReadDataStatic 错误: 共享内存对象未初始化。");
                        return new SharedData();
                    }

                    using (var accessor = _sharedMemory.CreateViewAccessor())
                    {
                        byte[] buffer = new byte[_structureSize];
                        accessor.ReadArray(0, buffer, 0, buffer.Length);

                        // 使用静态方法反序列化
                        IntPtr ptr = Marshal.AllocHGlobal(_structureSize);
                        try
                        {
                            Marshal.Copy(buffer, 0, ptr, _structureSize);
                            return (SharedData)Marshal.PtrToStructure(ptr, typeof(SharedData));
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(ptr);
                        }
                    }
                }
                catch (Exception ex)
                {
                    //Console.WriteLine($"静态读取共享内存时发生错误: {ex.Message}");
                    return new SharedData();
                }
            }

            public static void UpdateOnce(Func<SharedData, SharedData> updateAction)
            {
                if (updateAction == null)
                    throw new ArgumentNullException(nameof(updateAction));

                try
                {
                    // 读取当前共享内存中的数据
                    SharedData currentData = ReadDataStatic();

                    // 调用委托，获取修改后的数据
                    currentData = updateAction(currentData);

                    // 使用正确的序列化方法
                    byte[] buffer = StructureToByteArray(currentData);

                    // 写入共享内存
                    using (var accessor = _sharedMemory.CreateViewAccessor())
                    {
                        accessor.WriteArray(0, buffer, 0, buffer.Length);
                    }

                    //Console.WriteLine($"C#: 写入数据 - SRFov: {currentData.SRFovValue}, SRFovEnabled: {currentData.SRFovChangeEnabled}");

                    // 通知 C++ DLL 数据已准备好
                    _dataReadyEvent.Set();

                    if (_isClosedSharedMemory)
                    {
                        //Console.WriteLine("C#: 共享内存已关闭，跳过等待事件");
                        return;
                    }

                    //// 等待 C++ DLL 处理完成
                    //if (_dataConsumedEvent.WaitOne(3000))
                    //{
                    //    Console.WriteLine("C#: C++ DLL 已确认处理数据");
                    //}
                    //else
                    //{
                    //    Console.WriteLine("C#: 等待 C++ DLL 响应超时");
                    //}

                    // 使用超时循环代替单次等待，可以中途检查关闭状态
                    int totalWaitTime = 0;
                    int waitInterval = 51; // 每次等待51ms，数值越小越灵敏
                    while (totalWaitTime < 2117)
                    {
                        if (_isClosedSharedMemory)
                        {
                            //Console.WriteLine("C#: 共享内存已关闭，中断等待");
                            return;
                        }

                        if (_dataConsumedEvent.WaitOne(waitInterval))
                        {
                            //Console.WriteLine("C#: C++ DLL 已确认处理数据");
                            return;
                        }

                        totalWaitTime += waitInterval;
                    }

                    //Console.WriteLine("C#: 等待 C++ DLL 响应超时");

                }
                catch (Exception ex)
                {
                    //Console.WriteLine($"数据写入错误: {ex.Message}");
                }
            }

            // 添加一个只写入数据但不等待事件的方法
            public static void InitializeSharedData()
            {
                try
                {
                    SharedData initialData = new SharedData
                    {
                        Status = IPCStatus.Idle,
                        SRFovValue = -1,
                        SRFovChangeEnabled = -1,
                        SRFovChageFix = -1,
                        SRFovHookSpeed = -1,
                        SRFovHookDepth = -1,
                        // 260202
                        GenshinFog = -1,
                    };

                    byte[] buffer = StructureToByteArray(initialData);

                    using (var accessor = _sharedMemory.CreateViewAccessor())
                    {
                        accessor.WriteArray(0, buffer, 0, buffer.Length);
                    }

                    //Console.WriteLine($"C#: 初始化共享内存数据 - SRFov: {initialData.SRFovValue}, SRFovEnabled: {initialData.SRFovChangeEnabled}, Status: {initialData.Status}");
                }
                catch (Exception ex)
                {
                    //Console.WriteLine($"初始化共享内存数据错误: {ex.Message}");
                }
            }
        }
    }
}
