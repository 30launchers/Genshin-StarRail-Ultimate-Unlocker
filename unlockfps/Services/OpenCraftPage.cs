using System;
using System.Threading;
using System.Threading.Tasks;

namespace UnlockFps.Services
{
    public class OpenCraftPage
    {
        private readonly SemaphoreSlim _craftingSemaphore = new(1, 1);
        private bool _isCraftingEnabled = true;
        private Network? _network;
        private readonly EventManager? _eventManager;

        public OpenCraftPage()
        {
            //// 如果需要EventManager，可以在这里初始化
            //_eventManager = new EventManager();
            //_network = new Network(_eventManager);
        }

        // 可选的构造函数，允许从外部注入依赖
        public OpenCraftPage(Network network, EventManager eventManager = null)
        {
            _network = network;
            _eventManager = eventManager;
        }

        public async Task<bool> OpenCraftingTableAsync()
        {
            Console.WriteLine("Opening Crafting Table...");

            try
            {
                // 尝试获取信号量，如果已有请求在处理中则等待
                if (!await _craftingSemaphore.WaitAsync(0)) // 0表示不等待，直接返回false
                {
                    Console.WriteLine("Crafting table is already being opened, please wait...");
                    return false;
                }

                try
                {
                    // 检查是否允许处理
                    if (!_isCraftingEnabled) return false;

                    // 可选：临时禁用处理
                    _isCraftingEnabled = false;

                    if (_network == null)
                    {
                        _network = new Network();
                        //Console.WriteLine("Creating Network Service...");
                    }

                    await _network.OpenCrafting();

                    // 可选：延迟重新启用，防止连续点击
                    await Task.Delay(200); // 200ms冷却时间

                    return true;
                }
                finally
                {
                    _craftingSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to open Crafting Table: {ex.Message}");
                return false;
            }
            finally
            {
                _isCraftingEnabled = true; // 重新启用
            }
        }

        // 提供重置状态的方法（如果需要）
        public void ResetCraftingState()
        {
            _isCraftingEnabled = true;
        }
    }
}