using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnlockFps.Gui.Utils
{
    public class ShareMemTol : IDisposable
    {
        private MemoryMappedFile _mmf;
        private readonly string _mapName;
        private bool _disposed = false;

        // 构造函数
        public ShareMemTol(string mapName)
        {
            _mapName = mapName ?? throw new ArgumentNullException(nameof(mapName));
        }

        // 初始化并写入数据
        public void Initialize(bool initialValue)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ShareMemTol));

            try
            {
                // 创建或打开内存映射文件
                _mmf = MemoryMappedFile.CreateOrOpen(_mapName, 1);

                // 写入初始值
                WriteValue(initialValue);

                //Console.WriteLine($"共享内存 '{_mapName}' 初始化成功，初始值: {initialValue}");
            }
            catch (Exception ex)
            {
                //Console.WriteLine($"初始化错误: {ex.Message}");
                throw;
            }
        }

        // 写入数据
        public void WriteValue(bool value)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ShareMemTol));

            if (_mmf == null)
                //throw new InvalidOperationException("共享内存未初始化，请先调用 Initialize 方法");
                throw new InvalidOperationException("ShareMem Error");

            try
            {
                using (MemoryMappedViewAccessor accessor = _mmf.CreateViewAccessor())
                {
                    accessor.Write(0, value);
                    //Console.WriteLine($"写入成功: {value}");
                }
            }
            catch (Exception ex)
            {
                //Console.WriteLine($"写入错误: {ex.Message}");
                throw;
            }
        }

        // 读取数据（可选）
        public bool ReadValue()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ShareMemTol));

            if (_mmf == null)
                throw new InvalidOperationException("共享内存未初始化");

            try
            {
                using (MemoryMappedViewAccessor accessor = _mmf.CreateViewAccessor())
                {
                    bool value = accessor.ReadBoolean(0);
                    Console.WriteLine($"读取成功: {value}");
                    return value;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取错误: {ex.Message}");
                throw;
            }
        }

        // 实现 IDisposable 接口
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 释放托管资源
                    _mmf?.Dispose();
                    //Console.WriteLine($"共享内存 '{_mapName}' 已释放");
                }

                _disposed = true;
            }
        }

        // 终结器（可选）
        ~ShareMemTol()
        {
            Dispose(false);
        }
    }
}
