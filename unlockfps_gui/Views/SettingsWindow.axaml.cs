using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using UnlockFps.Gui.Utils;
using UnlockFps.Gui.ViewModels;
using UnlockFps.Gui.Views;
using UnlockFps.Services;
using UnlockFps.Utils;

// 260116
using static UnlockFps.Gui.Utils.Native2;

namespace UnlockFps.Gui.ViewModels
{
    public class SettingsWindowViewModel : ViewModelBase
    {
        private ICommand? _addDllCommand;
        private ICommand? _removeDllCommand;

        public required SettingsWindow Window { get; init; }
        public required Config Config { get; init; }

        public string? SelectedDll { get; set; }

        public ICommand AddDllCommand => _addDllCommand ??= ReactiveCommand.CreateFromTask(async () =>
        {
            var selectedFiles = await Window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                FileTypeFilter =
                [
                    new FilePickerFileType("DLL (*.dll)") { Patterns = ["*.dll"] },
                    new FilePickerFileType("All files (*.*)") { Patterns = ["*.*"] },
                ],
                AllowMultiple = true
            });

            foreach (var selectedFile in selectedFiles)
            {
                var localPath = selectedFile.Path.LocalPath;
                if (!VerifyDll(localPath))
                {
                    var alertWindow = App.DefaultServices.GetRequiredService<AlertWindow>();
                    alertWindow.Text =
                        $"""
                         Invalid File: 
                         {localPath}
                         
                         Only native x64 dlls are supported.
                         """;
                    await alertWindow.ShowDialog(Window);
                }
                else
                {
                    Config.LaunchOptions.DllList.Add(localPath);
                }
            }
        });

        public ICommand RemoveDllCommand => _removeDllCommand ??= ReactiveCommand.Create(() =>
        {
            if (SelectedDll != null)
            {
                Config.LaunchOptions.DllList.Remove(SelectedDll);
            }
        });

        private static bool VerifyDll(string fullPath)
        {
            if (!File.Exists(fullPath))
                return false;

            using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
            using var peReader = new PEReader(fs);
            if (peReader.HasMetadata)
                return false;

            return peReader.PEHeaders.CoffHeader.Machine == Machine.Amd64;
        }
    }
}

namespace UnlockFps.Gui.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly ConfigService? _configService;
        private readonly SettingsWindowViewModel _viewModel;


        // 260116
        private IntPtr _oldWndProc = IntPtr.Zero;
        //private Delegate _wndProcDelegate; // 防止垃圾回收
        // 定义委托签名
        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);


        // 260116
        private readonly OpenCraftPage _openCraftPage;

        ////251226
        ////private readonly Network _network;  // 添加这行
        //private Network? _network; // 改为可为空
        //// 添加 EventManager
        //private EventManager? _eventManager;
        //// 添加服务状态检测定时器
        //private Timer? _statusCheckTimer;
        //private bool _lastOnlineStatus = false;


        //FOV状态251106
        private static bool _allowsetFOVstate = true;
        private static bool _allowsetSRFOVstate = true;

        //251106定义一个静态事件，用于通知UI更新FOV状态
        public static event Action<bool> OnFOVStateChanged;
        public static event Action<bool> OnSRFOVStateChanged;

        public SettingsWindow(ConfigService configService)
        {
            this.SetSystemChrome();
            _configService = configService;
            _viewModel = new SettingsWindowViewModel
            {
                Config = _configService.Config,
                Window = this,
            };
            DataContext = _viewModel;

            ////251106订阅FOVStateChanged事件
            //OnFOVStateChanged += HandleFOVStateChanged;
            //OnSRFOVStateChanged += HandleSRFOVStateChanged;


            InitializeComponent();

            //确保窗口初始化后再设置FOV状态251106
            //isloadsettingwindow = true;


            //// 添加 F12 按键检测
            //this.KeyDown += (sender, e) =>
            //{
            //    if (e.Key == Avalonia.Input.Key.F10)
            //    {
            //        OpenCraftingTableHandler(null, null);
            //    }
            //};


            //// 260116
            //this.Opened += (s, e) =>
            //{
            //    var handle = this.TryGetPlatformHandle()?.Handle;
            //    if (handle != null && handle != IntPtr.Zero)
            //    {
            //        // 1. 注册热键
            //        RegisterHotKey(handle.Value, HOTKEY_ID, 0, VK_F10);

            //        // 2. 注入 WndProc 钩子来监听消息
            //        _wndProcDelegate = new WndProcDelegate(WndProc);
            //        _oldWndProc = SetWindowLongPtr(handle.Value, GWL_WNDPROC, Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));
            //    }
            //};

            //this.Closed += (s, e) =>
            //{
            //    var handle = this.TryGetPlatformHandle()?.Handle;
            //    if (handle != null && handle != IntPtr.Zero)
            //    {
            //        // 还原 WndProc 并注销热键
            //        UnregisterHotKey(handle.Value, HOTKEY_ID);
            //        if (_oldWndProc != IntPtr.Zero)
            //        {
            //            SetWindowLongPtr(handle.Value, GWL_WNDPROC, _oldWndProc);
            //        }
            //    }
            //};

            // 260117
            if (_openCraftPage == null)
            {
                //Console.WriteLine("OpenCraftPage initialized in SettingsWindow.");
                // 初始化 OpenCraftPage 260116
                _openCraftPage = new OpenCraftPage();
            }
        }

#if DEBUG
        public SettingsWindow()
        {
            if (!Design.IsDesignMode) throw new InvalidOperationException();
            InitializeComponent();
        }
#endif

        private void Control_OnLoaded(object? sender, RoutedEventArgs e)
        {
            if (_configService != null)
            {
                _configService.Config.PropertyChanged += Config_PropertyChanged;
                _configService.Config.LaunchOptions.PropertyChanged += Config_PropertyChanged;
                _configService.Config.LaunchOptions.DllList.CollectionChanged += DllList_CollectionChanged;
            }

            // 订阅 static 事件（在 Loaded 中订阅，确保在 Unloaded 中取消订阅）
            OnFOVStateChanged += HandleFOVStateChanged;
            OnSRFOVStateChanged += HandleSRFOVStateChanged;

            //设置FOV开关状态251106
            if (_allowsetFOVstate == false && FOVGenshinSwitch.IsChecked == false)
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    FOVGenshinSwitch.IsEnabled = false;
                });
            }
            //同上
            if (_allowsetFOVstate == true)
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    FOVGenshinSwitch.IsEnabled = true;
                });
            }

            //设置SRFOV开关状态251205
            if (_allowsetSRFOVstate == false && FOVStarRailSwitch.IsChecked == false)
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    FOVStarRailSwitch.IsEnabled = false;
                });
            }
            //同上
            if (_allowsetSRFOVstate == true)
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    FOVStarRailSwitch.IsEnabled = true;
                });
            }

            // 在控件完全加载后允许处理相关变更事件 260215 已转移到control_onloaded中最后，确保在UI元素状态正确后才允许事件处理，避免在加载过程中因事件触发导致的状态不一致问题
            isloadsettingwindow = true;

            //Console.WriteLine("订阅数量"+OnSRFOVStateChanged?.GetInvocationList()?.Length);
        }

        private void Control_OnUnloaded(object? sender, RoutedEventArgs e)
        {
            if (_configService != null)
            {
                _configService.Config.PropertyChanged -= Config_PropertyChanged;
                _configService.Config.LaunchOptions.PropertyChanged -= Config_PropertyChanged;
                _configService.Config.LaunchOptions.DllList.CollectionChanged -= DllList_CollectionChanged;
            }

            // 取消订阅，防止静态事件保留对已关闭窗口实例的引用
            OnFOVStateChanged -= HandleFOVStateChanged;
            OnSRFOVStateChanged -= HandleSRFOVStateChanged;

            // 260215写入StarRail分辨率设置到注册表，确保在窗口关闭时保存用户的分辨率选择
            if (_viewModel.Config.EnableSRGraphicOptionSet == true)
            {
                int _currentgamever = 0;
                _currentgamever = Detect_SR_GameVersion();

                if (_viewModel.Config.EnableSRCustomResolutionSet == true)
                {
                    // 自定义分辨率启用：直接写入完整配置（宽、高、全屏）
                    if (_currentgamever == 1)
                    {
                        RegistryHelper.WriteResolution(_viewModel.Config.SRCustomResolutionWidth,_viewModel.Config.SRCustomResolutionHeight,_viewModel.Config.SRCustomFullScreen ,1);
                    }
                    if (_currentgamever == 2)
                    {
                        RegistryHelper.WriteResolution(_viewModel.Config.SRCustomResolutionWidth,_viewModel.Config.SRCustomResolutionHeight,_viewModel.Config.SRCustomFullScreen ,2);
                    }
                }
                else
                {
                    if (_currentgamever == 1)
                    {
                        // 仅更新全屏状态（保持当前分辨率）
                        RegistryHelper.UpdateFullScreenOnly(_viewModel.Config.SRCustomFullScreen ,1);
                    }
                    if (_currentgamever == 2)
                    {
                        // 仅更新全屏状态（保持当前分辨率）
                        RegistryHelper.UpdateFullScreenOnly(_viewModel.Config.SRCustomFullScreen ,2);
                    }
                }
            }
        }

        private void Config_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            _configService?.Save();
        }

        private void DllList_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            _configService?.Save();
        }

        // 主题切换事件处理
        public void ApplyCustomAccent(bool useCustomAccent)
        {
            if (App.FluentTheme != null)
            {
                if (useCustomAccent)
                {
                    if (_viewModel.Config.GameSelection == false)
                    {
                        //App.FluentTheme.CustomAccentColor = Colors.Yellow;
                        //Genshin Impact
                        //App.FluentTheme.CustomAccentColor = Color.Parse("#FFA500");
                        App.FluentTheme.CustomAccentColor = Colors.Gold;
                    }
                    else
                    {
                        //App.FluentTheme.CustomAccentColor = Colors.Brown;
                        //Honkai Star Rail
                        //App.FluentTheme.CustomAccentColor = Color.Parse("#FF69B4");
                        App.FluentTheme.CustomAccentColor = Colors.DodgerBlue;
                    }
                }
                else
                {
                    App.FluentTheme.CustomAccentColor = null;
                }
            }
        }

        private void OnCustomAccentToggled(object sender, RoutedEventArgs e)
        {
            var toggleSwitch = sender as ToggleSwitch;
            ApplyCustomAccent(toggleSwitch?.IsChecked == false);
        }

        // 这是XAML中 Click="ConfirmButton_Click" 对应的事件处理器
        private async void ConfirmSRCustomParam(object sender, RoutedEventArgs e)
        {

            _viewModel.Config.SRCustomParam = SRCustomParamTextBox.Text;
            // 获取触发事件的按钮
            var button = sender as Button;

            // 步骤 A: 立即改变按钮状态
            button.Content = "Saved!";
            //button.IsEnabled = false; // 禁用按钮，防止在等待期间被再次点击

            // 步骤 B: 异步等待2秒
            // 关键点1: 使用 await Task.Delay() 而不是 Thread.Sleep()
            // Thread.Sleep() 会阻塞UI线程，导致界面卡死
            // Task.Delay() 不会阻塞，UI会保持响应
            await Task.Delay(2000);

            // 步骤 C: 2秒后，恢复按钮的原始状态
            button.Content = "Confirm Param";
            //button.IsEnabled = true;
        }

        // 获取FOV状态251106
        public static void allowsetFOVstate(bool state)
        {
            _allowsetFOVstate = state;

            SettingsWindow.OnFOVStateChanged?.Invoke(_allowsetFOVstate);
        }

        // FOV状态变化处理251106
        private void HandleFOVStateChanged(bool newState)
        {
            if (this != null)
            {
                try
                {
                    if (newState)
                    {
                        Dispatcher.UIThread.Invoke(() =>
                        {
                            FOVGenshinSwitch.IsEnabled = true;
                        });
                    }
                    else
                    {
                        Dispatcher.UIThread.Invoke(() =>
                        {
                            FOVGenshinSwitch.IsEnabled = false;
                        });
                    }
                }
                catch (Exception)
                {
                    //忽略异常
                }
            }
        }

        //251106从json文件加载到UI
        private bool isloadsettingwindow = false;

        private void FOV_ValueChanged(object? sender, Avalonia.Controls.NumericUpDownValueChangedEventArgs e)
        {
            if (!isloadsettingwindow)
            {
                return;
            }

            //_viewModel.Config.FOVTargetGenshin = (int)FOVGenshinValue.Value;
            try
            {
                _viewModel.Config.FOVTargetGenshin = (int)e.NewValue;
            }
            catch (Exception)
            {
                //忽略异常
            }
        }

        private void FOVGenshinValue_LostFocus(object? sender, RoutedEventArgs e)
        {
            if (!isloadsettingwindow)
            {
                return;
            }
            //FOVGenshinValue.Value = 123;
        }


        // 获取FOV状态251205
        public static void allowsetSRFOVstate(bool state)
        {
            _allowsetSRFOVstate = state;

            SettingsWindow.OnSRFOVStateChanged?.Invoke(_allowsetSRFOVstate);
        }

        // FOV状态变化处理251205
        private void HandleSRFOVStateChanged(bool newState)
        {
            if (this != null)
            {
                try
                {
                    if (newState)
                    {
                        Dispatcher.UIThread.Invoke(() =>
                        {
                            FOVStarRailSwitch.IsEnabled = true;
                        });
                    }
                    else
                    {
                        Dispatcher.UIThread.Invoke(() =>
                        {
                            FOVStarRailSwitch.IsEnabled = false;
                        });
                    }
                }
                catch (Exception)
                {
                    //忽略异常
                }
            }
        }

        private void FOV_SR_ValueChanged(object? sender, Avalonia.Controls.NumericUpDownValueChangedEventArgs e)
        {
            if (!isloadsettingwindow)
            {
                return;
            }

            //_viewModel.Config.FOVTargetGenshin = (int)FOVGenshinValue.Value;
            try
            {
                _viewModel.Config.FOVTargetStarRail = (int)e.NewValue;
            }
            catch (Exception)
            {
                //忽略异常
            }
        }

        private void FOVSRValue_LostFocus(object? sender, RoutedEventArgs e)
        {
            if (!isloadsettingwindow)
            {
                return;
            }
        }



        //private readonly Network _network;
        //251226
        //public async void OpenCraftingTableHandler(object sender, RoutedEventArgs args)
        //{
        //    Console.WriteLine("Opening Crafting Table...");

        //    if (_network == null)
        //    {
        //        // 延迟创建或显示错误
        //        _network = new Network(); // 或者通过其他方式获取
        //        Console.WriteLine("Creating Network Service...");
        //    }

        //    try
        //    {
        //        await this._network.OpenCrafting();
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine("Failed to open Crafting Table: " + ex.Message);
        //    }

        //}


        //// 初始化服务监控
        //private void InitializeServiceMonitoring()
        //{
        //    try
        //    {
        //        // 创建 EventManager
        //        _eventManager = new EventManager();

        //        // 订阅服务状态变化事件
        //        _eventManager.OnServiceStatusChanged += OnServiceStatusChanged;

        //        // 创建 Network 实例（如果有需要）
        //        _network = new Network(_eventManager);

        //        // 启动服务状态检测定时器
        //        StartStatusCheckTimer();

        //        Console.WriteLine("Service monitoring started.");
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Failed to initialize service monitoring: {ex.Message}");
        //    }
        //}

        //// 服务状态变化事件处理器
        //private void OnServiceStatusChanged(object sender, bool isOnline)
        //{
        //    // 输出服务状态到控制台
        //    Console.WriteLine(isOnline ? "服务在线" : "服务离线");
        //}

        //// 启动状态检测定时器
        //private void StartStatusCheckTimer()
        //{
        //    // 每5秒检查一次服务状态
        //    _statusCheckTimer = new Timer(async state =>
        //    {
        //        try
        //        {
        //            if (_network == null) return;

        //            // 尝试发送一个简单请求检查服务是否在线
        //            var response = await _network.GetUserUidAsync();
        //            bool isOnline = !string.IsNullOrEmpty(response);

        //            // 如果状态发生变化，触发事件
        //            if (isOnline != _lastOnlineStatus)
        //            {
        //                _lastOnlineStatus = isOnline;
        //                _eventManager?.FireEvent(EventId.EvtServiceStatusChanged, isOnline);
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            // 连接失败，服务离线
        //            if (_lastOnlineStatus)
        //            {
        //                _lastOnlineStatus = false;
        //                _eventManager?.FireEvent(EventId.EvtServiceStatusChanged, false);
        //            }
        //        }
        //    }, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        //}

        //private readonly SemaphoreSlim _craftingSemaphore = new SemaphoreSlim(1, 1); // 最多一个并发
        //private bool _isCraftingEnabled = true; // 额外控制开关

        //public async void OpenCraftingTableHandler(object sender, RoutedEventArgs args)
        //{
        //    if (sender is Button button)
        //    {
        //        // 可选：更新UI状态
        //        button.IsEnabled = false;
        //        button.Content = "Opening...";
        //    }

        //    Console.WriteLine("Opening Crafting Table...");

        //    try
        //    {
        //        // 尝试获取信号量，如果已有请求在处理中则等待
        //        if (!await _craftingSemaphore.WaitAsync(0)) // 0表示不等待，直接返回false
        //        {
        //            Console.WriteLine("Crafting table is already being opened, please wait...");
        //            return;
        //        }

        //        try
        //        {
        //            // 检查是否允许处理
        //            if (!_isCraftingEnabled) return;

        //            // 可选：临时禁用处理
        //            _isCraftingEnabled = false;

        //            if (_network == null)
        //            {
        //                _network = new Network();
        //                Console.WriteLine("Creating Network Service...");
        //            }

        //            await _network.OpenCrafting();

        //            // 可选：延迟重新启用，防止连续点击
        //            await Task.Delay(200); // 200ms秒冷却时间
        //        }
        //        finally
        //        {
        //            _craftingSemaphore.Release();
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Failed to open Crafting Table: {ex.Message}");
        //    }
        //    finally
        //    {
        //        _isCraftingEnabled = true; // 重新启用

        //        if (sender is Button buttonToEnable)
        //        {
        //            // 恢复按钮状态
        //            Dispatcher.UIThread.Invoke(() =>
        //            {
        //                buttonToEnable.IsEnabled = true;
        //                buttonToEnable.Content = "打开合成台";
        //            });
        //        }
        //    }
        //}


        private void Genshin_Advanced_Implementation_Tapped(object? sender, Avalonia.Input.TappedEventArgs e)
        {
            var url = "https://github.com/CodeCubist/FufuLauncher";
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }

        //// 260116
        //// 处理原生 Windows 消息
        //private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        //{
        //    if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        //    {
        //        // 即使在后台也会触发
        //        Dispatcher.UIThread.Post(() =>
        //        {
        //            // 执行你的逻辑
        //            OpenCraftingTableHandler(null, null);
        //        });
        //    }
        //    // 将消息传递回 Avalonia 原来的处理函数
        //    return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
        //}

        // 修改 OpenCraftingTableHandler 方法
        public async void OpenCraftingTableHandler(object sender, RoutedEventArgs args)
        {
            Button? button = null;

            if (sender is Button btn)
            {
                button = btn;
                // 可选：更新UI状态
                button.IsEnabled = false;
                button.Content = "Opening...";
            }

            try
            {
                var success = await _openCraftPage.OpenCraftingTableAsync();

                if (success)
                {
                    Console.WriteLine("Crafting table opened successfully.");
                }
                else
                {
                    //Console.WriteLine("Failed to open crafting table or request was skipped.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in OpenCraftingTableHandler: {ex.Message}");
            }
            finally
            {
                if (button != null)
                {
                    // 恢复按钮状态
                    Dispatcher.UIThread.Invoke(() =>
                    {

                        //if(_viewModel.Config.EnableGenshinQuickCrafting == true)
                        //{
                        //    button.IsEnabled = true;
                        //}
                        button.IsEnabled = true;
                        button.Content = "打开合成台";
                    });
                }
            }
        }

        // 修改 WndProc 方法中的热键处理部分
        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                // 即使在后台也会触发
                Dispatcher.UIThread.Post(async () =>
                {
                    // 执行你的逻辑
                    await _openCraftPage.OpenCraftingTableAsync();
                });
            }
            // 将消息传递回 Avalonia 原来的处理函数
            return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
        }


        // 260215
        private void SRCustomResolutionX_ValueChanged(object? sender, Avalonia.Controls.NumericUpDownValueChangedEventArgs e)
        {
            if (!isloadsettingwindow)
            {
                return;
            }

            try
            {
                _viewModel.Config.SRCustomResolutionWidth = (int)e.NewValue;
                //Console.WriteLine($"SRCustomResolutionWidth updated to: {_viewModel.Config.SRCustomResolutionWidth}");
            }
            catch (Exception)
            {
                //忽略异常
            }
        }

        // 260215
        private void SRCustomResolutionY_ValueChanged(object? sender, Avalonia.Controls.NumericUpDownValueChangedEventArgs e)
        {
            if (!isloadsettingwindow)
            {
                return;
            }

            try
            {
                _viewModel.Config.SRCustomResolutionHeight = (int)e.NewValue;
            }
            catch (Exception)
            {
                //忽略异常
            }
        }



        // 当开关用于“Use Custom Resolution (SR Only)”时触发（Checked 事件）
        private void ToggleSwitch_Checked1y(object sender, RoutedEventArgs e)
        {
            if (!isloadsettingwindow) return;

            if (sender is not ToggleSwitch ts) return;

            // 如果控件当前状态与模型一致，通常是绑定引起的回写，忽略以避免重复处理
            // 注意：Config.EnableSRCustomResolutionSet 由 XAML 双向绑定驱动
            try
            {
                bool controlChecked = ts.IsChecked == true;
                bool modelChecked = _viewModel.Config.EnableSRCustomResolutionSet;

                if (controlChecked == modelChecked)
                {
                    // 不执行重复逻辑，仅更新去抖时间戳以防短时重复
                    return;
                }

                // 这里放置仅在“用户确实改变值且与模型不同”时执行的业务逻辑
                // 如果你需要修改模型，可以直接设置；但避免在此处重复触发绑定循环
                // 例如：_viewModel.Config.EnableSRCustomResolutionSet = controlChecked;

                // 解析SR游戏路径并读取版本是CN还是国际服 260215
                //Detect_SR_GameVersion();

                Console.WriteLine("Enabled");
            }
            finally
            {

            }
        }

        // 解析SR游戏路径并读取版本是CN还是国际服 260215
        private int Detect_SR_GameVersion()
        {
           
            int _gameversion = 0;
            if (_viewModel.Config.DetectSRCustomResolutionMode == 0)
            {
                string srgamepath = _viewModel.Config.LaunchOptions.SRGamePath;
                string gameDir = Path.GetDirectoryName(srgamepath);          // 获取目录

                //string iniFilePath = Path.Combine(gameDir, "config.ini");    // 拼接 INI 文件路径
                // 260218：添加对游戏路径和目录的检查，避免因路径问题导致的异常
                if (string.IsNullOrEmpty(gameDir))
                {
                    // 处理错误：可以抛出更明确的异常，或设置默认路径
                    Console.WriteLine("Star Rail:Invalid game path,unable to detect game version.");
                    return 0; // 或者抛出异常：throw new InvalidOperationException("Invalid game path");
                }
                string iniFilePath = Path.Combine(gameDir, "config.ini");

                string uapc = Native2.ReadIniValue(iniFilePath, "General", "uapc");

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

            if (_viewModel.Config.DetectSRCustomResolutionMode == 1)
            {
                _gameversion = 1;
            }
            if (_viewModel.Config.DetectSRCustomResolutionMode == 2)
            {
                _gameversion = 2;
            }

            return _gameversion;
        }
    }
}