using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Tmds.DBus.Protocol;
using UnlockFps.Gui.Utils;
using UnlockFps.Gui.ViewModels;
using UnlockFps.Gui.Views;
using UnlockFps.Services;

namespace UnlockFps.Gui.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public required ProcessService ProcessService { get; init; }
        public required Config Config { get; init; }
        public required GameInstanceService GameInstanceService { get; init; }

        public int MinimumFps { get; set; } = 20;
        public int MaximumFps { get; set; } = 720;

        public ICommand OpenInitializationWindowCommand { get; } =
            ReactiveCommand.CreateFromTask(ShowWindow<InitializationWindow>);

        public ICommand OpenSettingsWindowCommand { get; } = ReactiveCommand.CreateFromTask(ShowWindow<SettingsWindow>);
        public ICommand OpenAboutWindowCommand { get; } = ReactiveCommand.CreateFromTask(ShowWindow<AboutWindow>);

        public static async Task ShowWindow<T>() where T : Window
        {
            var window = App.DefaultServices.GetRequiredService<T>();
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            await window.ShowDialog(MainWindow);
        }

        private static Window MainWindow =>
            ((IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!).MainWindow!;


        //private int _fpsValue = 150; // 添加 FPS 值字段
        //// 添加 FPS 值属性
        //public int FpsValue
        //{
        //    get => _fpsValue;
        //    set => this.RaiseAndSetIfChanged(ref _fpsValue, value);
        //}
        //public MainWindowViewModel() // 添加构造函数
        //{
        //    // 订阅FpsValue属性的变更通知
        //    this.WhenAnyValue(x => x.FpsValue)
        //        .Subscribe(newValue =>
        //        {
        //            Console.WriteLine($"FPS值已更新为: {newValue}");
        //        });
        //}
    }
}

namespace UnlockFps.Gui.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _viewModel;
        private readonly ConfigService _configService;
        private readonly ProcessService _processService;
        private ProcessServiceSR _processServiceSR;
        private readonly TrayIcon _trayIcon;

#if DEBUG
        public MainWindow()
        {
            if (!Design.IsDesignMode) throw new InvalidOperationException();
            InitializeComponent();
        }
#endif

        private readonly string[]? _startupArgs; // 新增：保存启动参数251104

        private string arg1 = string.Empty;

        //public MainWindow(ConfigService configService, ProcessService processService, GameInstanceService gameInstanceService)

        //251104新增启动参数startupArgs
        public MainWindow(ConfigService configService, ProcessService processService, GameInstanceService gameInstanceService, string[]? startupArgs = null)
        {
            _startupArgs = startupArgs ?? Environment.GetCommandLineArgs().Skip(1).ToArray(); // 优先使用注入的参数，否则读取命令行（去掉可执行文件路径）

            this.SetSystemChrome();
            //DataContext = _viewModel = new MainWindowViewModel()
            //{
            //    Config = configService.Config,
            //    ProcessService = processService,
            //    GameInstanceService = gameInstanceService
            //};
            _configService = configService;
            _processService = processService;
            //InitializeComponent();

            //调整顺序以确保 slider_fps 已初始化，通常建议先调用 InitializeComponent() 再设置 DataContext
            DataContext = _viewModel = new MainWindowViewModel()
            {
                Config = configService.Config,
                ProcessService = processService,
                GameInstanceService = gameInstanceService
            };

            InitializeComponent();

            // 获取 ViewModel
            if (DataContext is MainWindowViewModel viewModel)
            {
                // 订阅 Config 的 PropertyChanged 事件
                viewModel.Config.PropertyChanged += ConfigOnPropertyChanged;
                // 订阅事件后，手动触发一次FpsTarget的属性变更
                ConfigOnPropertyChanged(viewModel.Config, new PropertyChangedEventArgs(nameof(viewModel.Config.FpsTarget)));
            }

            if(_viewModel.Config.UmlimitedFpsGenshin==true)
            {
                GenshinUnlimitedFpsMenuItem_Click(null, new RoutedEventArgs());
            }

            if (_viewModel.Config.UmlimitedFpsStarRail == true)
            {
                StarRailUnlimitedFpsMenuItem_Click(null, new RoutedEventArgs());
            }

            // 注册ToggleSwitch状态改变事件
            Game_Selected_Item.IsCheckedChanged += OnGameSelectionChanged;
            //解除启动游戏解锁监控250831
            gameInstanceService.Start();


            if (WineHelper.DetectWine(out var version, out var buildId))
            {
                Title += $" (Wine {version})";
            }
            else if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Title += $" ({Environment.OSVersion})";
            }

            _trayIcon = TrayIcon.GetIcons(Application.Current!)![0];
            _trayIcon.Clicked += (_, _) =>
            {
                if (WindowState == WindowState.Minimized)
                {
                    Show();
                    WindowState = WindowState.Normal;
                }
            };
            if (_trayIcon.Menu is { } menu)
            {
                var items = menu.Items
                    .Where(k => k is not NativeMenuItemSeparator)
                    .OfType<NativeMenuItem>()
                    .ToArray();
                items[0].Click += (_, _) =>
                {
                    Show();
                    WindowState = WindowState.Normal;
                };
                items[1].Click += (_, _) => Close();
            }
        }


        public void OnGameSelectionChanged(object? sender, RoutedEventArgs e)
        {
            //if (sender is ToggleSwitch toggleSwitch)
            //{
            //    bool isChecked = toggleSwitch.IsChecked ?? false;
            //    // 根据 isChecked 的值执行相应的操作
            //    if (isChecked)
            //    {
            //        _viewModel.Config.FpsTargetSR = _viewModel.Config.FpsTargetTemp;
            //    }
            //    else
            //    {
            //        _viewModel.Config.FpsTarget = _viewModel.Config.FpsTargetTemp;
            //    }
            //}
        }

        private void ConfigOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_viewModel.Config.FpsTargetTemp))
            {
                // 打印 FpsTarget 的值
                //Console.WriteLine($"FpsTarget changed to: {_viewModel.Config.FpsTargetTemp}");
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property != WindowStateProperty) return;
            if (WindowState == WindowState.Minimized)
            {
                this.Hide();
                _trayIcon.IsVisible = true;
                //_trayIcon.ToolTipText = $"{Title} (FPS: {_viewModel.Config.FpsTarget})";
                _trayIcon.ToolTipText = $"{Title}{Environment.NewLine}Genshin FPS: {_viewModel.Config.FpsTarget}{Environment.NewLine}StarRail FPS: {_viewModel.Config.FpsTargetSR}";
            }
            else
            {
                _trayIcon.IsVisible = false;
            }
        }
        private bool isloadwin = false;

        public string _stubDllPath = string.Empty;
        private string _srFOVDllPath = string.Empty;
        private string _yslaucnherproDllPath = string.Empty;
        private string _ysnvhelperDllPath = string.Empty;
        // 260117
        private string _ysadvaddon = string.Empty;

        //251102字段：用 PixelPoint 保存像素位置，用 Avalonia.Size 保存窗口大小
        private PixelPoint _windowLocation;
        private Size _windowSize;

        //251104
        private string decryptedText = string.Empty;
        //251205新增共享内存对象
        private ShareMemTol _memoryManager;

        private async void Window_OnLoaded(object? sender, RoutedEventArgs e)
        {
            //251104新增：打印启动参数
            // 打印：如果构造函数通过参数注入了启动参数，则使用它；否则使用 Environment.GetCommandLineArgs()
            try
            {
                var argsToPrint = _startupArgs ?? Environment.GetCommandLineArgs().Skip(1).ToArray();

                if (argsToPrint.Length == 0)
                {
                    //Console.WriteLine("No startup arguments.");
                }
                else
                {
                    //Console.WriteLine("Startup arguments:");
                    for (int i = 0; i < argsToPrint.Length; i++)
                    {
                        //Console.WriteLine($"  [{i}] = {argsToPrint[i]}");
                    }

                    // 单独处理第一个参数
                    arg1 = argsToPrint[0];
                    //Console.WriteLine("参数1: " + arg1);
                }

                //// 可选：也可从 App.Current.Properties 获取启动时放入的任意对象（如果你的 App 在启动时放入了）
                //if (Application.Current?.Properties != null && Application.Current.Properties.ContainsKey("StartupArgsFromApp"))
                //{
                //    var obj = Application.Current.Properties["StartupArgsFromApp"];
                //    Console.WriteLine($"App.Properties[\"StartupArgsFromApp\"] = {obj}");
                //}
            }
            catch (Exception ex)
            {
                Console.WriteLine($"打印启动参数时出错: {ex.Message}");
            }

            //解密加密后的参数251104
            string password = "d666e326dc97b"; // 必须与加密时使用的密码相同

            try
            {
                // 解密
                decryptedText = DecryptString(arg1, password);
                //Console.WriteLine($"解密后的字符串: {decryptedText}");
            }
            catch (Exception ex)
            {
                //Console.WriteLine($"解密失败: {ex.Message}");
            }


            //251102保存初始位置与大小
            try
            {
                //_windowLocation = Location;
                //_windowSize = Size;

                // Window_OnLoaded 中保存位置/大小（替换原来的 Location/Size 用法）
                _windowLocation = this.Position;
                _windowSize = this.Bounds.Size;

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving initial window state: {ex.Message}");
            }

            //检查自定义强调色是否启用并应用
            // 创建 SettingsWindow 实例并调用方法
            var settingsWindow = new SettingsWindow(_configService);
            settingsWindow.ApplyCustomAccent(_viewModel.Config.UseSystemTheme == false);
            isloadwin = true;


            // 检查配置加载错误
            if (!string.IsNullOrEmpty(_configService.JsonLoadError))
            {
                var alertWindow = new AlertWindow();
                alertWindow.Text = _configService.JsonLoadError;
                alertWindow.Title = "Notification";
                alertWindow.IsError = true; // ⭐ 关键设置：启用第二个图标
                await alertWindow.ShowDialog(this);
                //await ShowErrorMessage(_configService.JsonLoadError);
            }

            if (Design.IsDesignMode) return;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ConsoleManager.BindExitAction(() =>
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("User manually closes debug window. Program will now exit.");
                    Console.ResetColor();
                    Thread.Sleep(1000);
                    Dispatcher.UIThread.Invoke(Close);
                });
            }

            // 初始化共享内存管理器251206
            _memoryManager = new ShareMemTol("00E06258-8504-01C4-4AC1-30EE4209A90D");
            _memoryManager.Initialize(false);
            _memoryManager.WriteValue(true);

            ////加载GameDLL
            _stubDllPath = LoadGameDLL();
            //加载SRFOVDLL
            _srFOVDllPath = LoadSRFOVDLL();

            //加载MobileUiUnlocker
            LoadMoubileUiUnlocker();

            //加载解密后的DLL 251104
            //_stubDllPath = "unlocker_"+ decryptedText+".dll";
            // 把解密后的文件名与程序运行目录组合成完整路径
            //_stubDllPath = Path.Combine(GetAppDirectory(), $"unlocker_{decryptedText}.dll");

            //Task.Run(() =>
            //{
            //    while (true)
            //    {
            //        Thread.Sleep(2115);
            //        Console.WriteLine($"StarRail fps:{_viewModel.Config.FpsTargetSR} Genshin Impact fps:{_viewModel.Config.FpsTarget}");
            //    }
            //});

            // 加载LauncherPro6.2 DLL
            //_yslaucnherproDllPath = LoadGenshinToolsDLL("Launcher_no_lg_62.dll", "advantol.dll");
            //_ysnvhelperDllPath = LoadGenshinToolsDLL("nvhelper_62.dll", "advantol_1.dll");

            // 260117 加载ysAdvAddon DLL
            //_ysadvaddon = LoadGenshinToolsDLL("ys_adv_addon.dll", "adv_addon.dll");
            // 260117 修改为：
            _ysadvaddon = LoadGenshinToolsDLL("ys_adv_addon.dll", "genshin_advan_tol.dll");

            IpcService.getStubPath(_stubDllPath);

            if (_viewModel.Config.AutoLaunch && !_viewModel.Config.GameSelection)
            {
                await LaunchGame(true);
            }

            if (_viewModel.Config.AutoLaunch && _viewModel.Config.GameSelection)
            {
                await LaunchGameSR(true);
            }
        }

        //获取应用程序目录的辅助方法251106
        private string GetAppDirectory()
        {
            // 首选 AppContext.BaseDirectory（适用于大多数运行/发布场景）
            if (!string.IsNullOrEmpty(AppContext.BaseDirectory))
                return AppContext.BaseDirectory;

            // 回退到程序集位置（在某些单文件/特殊场景可能不同）
            try
            {
                var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (!string.IsNullOrEmpty(asmDir))
                    return asmDir;
            }
            catch
            {
                // 忽略并回退到当前工作目录
            }

            return Environment.CurrentDirectory;
        }

        public static string DecryptString(string encryptedText, string password)
        {
            byte[] cipherBytes = Convert.FromBase64String(encryptedText);

            using (Aes aes = Aes.Create())
            {
                // 从密码生成密钥和IV（必须与加密时相同）
                byte[] salt = Encoding.UTF8.GetBytes("mesboxsslta");
                using (var deriveBytes = new Rfc2898DeriveBytes(password, salt, 1000))
                {
                    aes.Key = deriveBytes.GetBytes(32); // 256位密钥
                    aes.IV = deriveBytes.GetBytes(16);  // 128位IV
                }

                using (var memoryStream = new MemoryStream(cipherBytes))
                {
                    using (var cryptoStream = new CryptoStream(memoryStream, aes.CreateDecryptor(), CryptoStreamMode.Read))
                    {
                        using (var streamReader = new StreamReader(cryptoStream))
                        {
                            return streamReader.ReadToEnd();
                        }
                    }
                }
            }
        }

        private string LoadGameDLL() 
        {
            //string defaultNamespace = Assembly.GetExecutingAssembly().GetName().Name;
            //string resourceName = $"{defaultNamespace}.Resources.Unlocker-YSR.dll";
            string resourceName = $"UnlockFps.Gui.Resources.Unlocker-YSR.dll";
            string filePath = Path.Combine(AppContext.BaseDirectory, "Unlocker-YSR.dll");

            //// 检查是否已存在且最新（可选）
            //if (File.Exists(filePath))
            //    return filePath;

            var assembly = Assembly.GetExecutingAssembly();

            // 打印所有嵌入资源名称
            //Console.WriteLine("可用的嵌入资源:");
            foreach (var name in assembly.GetManifestResourceNames())
            {
                //Console.WriteLine(name);
            }

            // 验证资源是否存在
            if (!assembly.GetManifestResourceNames().Contains(resourceName))
                throw new Exception($"资源 '{resourceName}' 未找到");

            // 使用 using 确保流正确关闭
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new Exception($"无法加载资源 '{resourceName}'");

                try
                {
                    using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                    {
                        stream.CopyTo(fileStream);
                    }
                }
                catch (Exception ex)
                {
                    //throw new Exception($"写入文件 '{filePath}' 时出错: {ex.Message}");
                    //ShowErrorMessage(ex.Message);
                    string message = ex.Message;
                    const int maxLength = 500;
                    if (message.Length > maxLength)
                    {
                        message = message.Substring(0, maxLength) + "...";
                    }
                    var alertWindow = new AlertWindow();
                    alertWindow.Text = message;
                    alertWindow.Title = "Error";
                    alertWindow.IsError = true; // ⭐ 关键设置：启用第二个图标
                    alertWindow.ShowDialog(this);
                }
                //using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                //{
                //    stream.CopyTo(fileStream);
                //}
            }

            // 检查是否已存在且最新（可选）
            if (File.Exists(filePath))
                return filePath;

            return filePath;
        }

        private string LoadSRFOVDLL()
        {
            //string defaultNamespace = Assembly.GetExecutingAssembly().GetName().Name;
            //string resourceName = $"{defaultNamespace}.Resources.Unlocker-YSR.dll";
            string resourceName = $"UnlockFps.Gui.Resources.Sr_Fov_Changer.dll";
            string filePath = Path.Combine(AppContext.BaseDirectory, "UtilityUlk1.dll");

            //// 检查是否已存在且最新（可选）
            //if (File.Exists(filePath))
            //    return filePath;

            var assembly = Assembly.GetExecutingAssembly();

            // 打印所有嵌入资源名称
            //Console.WriteLine("可用的嵌入资源:");
            foreach (var name in assembly.GetManifestResourceNames())
            {
                //Console.WriteLine(name);
            }

            // 验证资源是否存在
            if (!assembly.GetManifestResourceNames().Contains(resourceName))
                throw new Exception($"资源 '{resourceName}' 未找到");

            // 使用 using 确保流正确关闭
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new Exception($"无法加载资源 '{resourceName}'");

                try
                {
                    using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                    {
                        stream.CopyTo(fileStream);
                    }
                }
                catch (Exception ex)
                {
                    //throw new Exception($"写入文件 '{filePath}' 时出错: {ex.Message}");
                    //ShowErrorMessage(ex.Message);
                    string message = ex.Message;
                    const int maxLength = 500;
                    if (message.Length > maxLength)
                    {
                        message = message.Substring(0, maxLength) + "...";
                    }
                    //var alertWindow = new AlertWindow();
                    //alertWindow.Text = message;
                    //alertWindow.Title = "Error";
                    //alertWindow.IsError = true; // ⭐ 关键设置：启用第二个图标
                    //alertWindow.ShowDialog(this);
                    throw new Exception(message);
                }
                //using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                //{
                //    stream.CopyTo(fileStream);
                //}
            }

            // 检查是否已存在且最新（可选）
            if (File.Exists(filePath))
                return filePath;

            return filePath;
        }

        private string LoadMoubileUiUnlocker()
        {
            //string defaultNamespace = Assembly.GetExecutingAssembly().GetName().Name;
            //string resourceName = $"{defaultNamespace}.Resources.Unlocker-YSR.dll";
            string resourceName = $"UnlockFps.Gui.Resources.wTE_ysr251210b.exe";
            string filePath = Path.Combine(AppContext.BaseDirectory, "wTE_ysr251210b.exe");

            //// 检查是否已存在且最新（可选）
            //if (File.Exists(filePath))
            //    return filePath;

            var assembly = Assembly.GetExecutingAssembly();

            // 打印所有嵌入资源名称
            //Console.WriteLine("可用的嵌入资源:");
            foreach (var name in assembly.GetManifestResourceNames())
            {
                //Console.WriteLine(name);
            }

            // 验证资源是否存在
            if (!assembly.GetManifestResourceNames().Contains(resourceName))
                throw new Exception($"资源 '{resourceName}' 未找到");

            // 使用 using 确保流正确关闭
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new Exception($"无法加载资源 '{resourceName}'");

                try
                {
                    using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                    {
                        stream.CopyTo(fileStream);
                    }
                }
                catch (Exception ex)
                {
                    //throw new Exception($"写入文件 '{filePath}' 时出错: {ex.Message}");
                    //ShowErrorMessage(ex.Message);
                    string message = ex.Message;
                    const int maxLength = 500;
                    if (message.Length > maxLength)
                    {
                        message = message.Substring(0, maxLength) + "...";
                    }
                    //var alertWindow = new AlertWindow();
                    //alertWindow.Text = message;
                    //alertWindow.Title = "Error";
                    //alertWindow.IsError = true; // ⭐ 关键设置：启用第二个图标
                    //alertWindow.ShowDialog(this);
                    throw new Exception(message);
                }
                //using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                //{
                //    stream.CopyTo(fileStream);
                //}
            }

            // 检查是否已存在且最新（可选）
            if (File.Exists(filePath))
                return filePath;

            return filePath;
        }

        /// <summary>
        /// 通用函数：从嵌入资源加载DLL文件
        /// </summary>
        /// <param name="resourceFileName">资源文件名称（如：Launcher_no_lg_62.dll）</param>
        /// <param name="outputFileName">输出文件名称（如：advantol.dll）</param>
        /// <returns>DLL文件的完整路径</returns>
        private string LoadGenshinToolsDLL(string resourceFileName, string outputFileName)
        {
            // 构建资源完整名称和输出文件路径
            string resourceName = $"UnlockFps.Gui.Resources.{resourceFileName}";
            string filePath = Path.Combine(AppContext.BaseDirectory, "ulk_ysr_tools", outputFileName);
            //string filePath = Path.Combine(AppContext.BaseDirectory, "ulk_ysr_tools\\advantol.dll");

            // 检查文件是否已存在 取消此检查以确保每次都从嵌入资源提取最新版本260704
            //if (File.Exists(filePath))
            //    return filePath;

            var assembly = Assembly.GetExecutingAssembly();

            // 验证资源是否存在（调试时可启用打印）
            // Console.WriteLine("可用的嵌入资源:");
            // foreach (var name in assembly.GetManifestResourceNames())
            // {
            //     Console.WriteLine(name);
            // }

            if (!assembly.GetManifestResourceNames().Contains(resourceName))
                throw new Exception($"资源 '{resourceName}' 未找到");

            // 确保输出目录存在
            string outputDir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // 从嵌入资源提取并保存文件
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new Exception($"无法加载资源 '{resourceName}'");

                try
                {
                    using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                    {
                        stream.CopyTo(fileStream);
                    }
                }
                catch (Exception ex)
                {
                    // 截断异常信息以避免过长
                    string message = ex.Message;
                    const int maxLength = 500;
                    if (message.Length > maxLength)
                    {
                        message = message.Substring(0, maxLength) + "...";
                    }
                    throw new Exception($"保存DLL文件失败: {message}");
                }
            }

            return filePath;
        }

        private void Window_OnClosing(object? sender, WindowClosingEventArgs e)
        {
            _configService.Save();
        }

        private void Window_OnClosed(object? sender, EventArgs e)
        {
            try
            {
                ConsoleManager.Hide();
            }
            catch
            {
                // ignored
            }
        }

        private async void BtnLaunchGame_OnClick(object? sender, RoutedEventArgs e)
        {
            //当GameSelection为false时，启动GenshinImpact，反之为Honkai:Star Rail
            if (!_viewModel.Config.GameSelection)
            {
                await LaunchGame(false);
            }
            else
            {
                await LaunchGameSR(false);
            }
        }

        private async Task LaunchGame(bool isAutoStart)
        {
            if (_viewModel.Config.EnableFOVGenshin == false)
            {
                //设置FOV状态251106
                SettingsWindow.allowsetFOVstate(false);
            }

            if (!File.Exists(_viewModel.Config.LaunchOptions.GamePath))
            {
                if (isAutoStart) return;
                await MainWindowViewModel.ShowWindow<InitializationWindow>();
            }

            if (!File.Exists(_viewModel.Config.LaunchOptions.GamePath)) return;

            try
            {
                _processService.Start(false);
                _viewModel.GameInstanceService.PropertyChanged += ProcessServiceOnPropertyChanged;

                // 订阅游戏退出事件250901
                ProcessService.GameExitRequested += OnGenshinSRGameExitRequested;

                WindowState = WindowState.Minimized;
            }
            catch (Exception ex)
            {
                await ShowErrorMessage(ex.Message);
            }

            //Console.WriteLine("Game process has exited.11");

            //Task.Run(() =>
            //{
            //    while (true)
            //    {
            //        if (_viewModel.Config.AutoClose)
            //        {
            //            if (ProcessService.shouldgameexit == true)
            //            {
            //                // 通过Dispatcher在UI线程执行关闭操作
            //                Dispatcher.UIThread.InvokeAsync(() => Close());
            //                break; // 退出循环，结束任务
            //            }
            //        }
            //        Thread.Sleep(1000);
            //    }
            //});

            return;

            void ProcessServiceOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName != nameof(GameInstanceService.IsRunning)) return;
                if (!_viewModel.GameInstanceService.IsRunning)
                {
                    _viewModel.GameInstanceService.PropertyChanged -= ProcessServiceOnPropertyChanged;
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        if (_viewModel.Config.AutoClose)
                        {
                            Close();
                        }
                        else
                        {
                            Show();
                            WindowState = WindowState.Normal;
                        }
                    });
                }
            }

            //// 事件处理程序250901
            //void OnGameExitRequested(object sender, EventArgs e)
            //{
            //    if (_viewModel.Config.AutoClose)
            //    {
            //        Dispatcher.UIThread.InvokeAsync(() => Close());
            //    }
            //}
        }

        private static async Task ShowErrorMessage(string infoWindowText)
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var infoWindow = App.DefaultServices.GetRequiredService<AlertWindow>();
                infoWindow.Text = infoWindowText;
                await infoWindow.ShowDialog(App.CurrentMainWindow!);
            });
        }

        private void NumericUpDown_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            double slider_temp_value = slider_fps.Value;
            if (sender is NumericUpDown numericUpDown)
            {

                // 直接转换为字符串并打印
                //Console.WriteLine($"NumericUpDown 的值: {numericUpDown.Value?.ToString() ?? "空"}");
                if (numericUpDown.Value?.ToString() == null)
                {
                    Console.WriteLine("empty value null");
                    numericUpDown.Value = (decimal)slider_fps.Value;
                }

            }
        }

        private void NumericUpDown_ValueChanged(object? sender, Avalonia.Controls.NumericUpDownValueChangedEventArgs e)
        {
            //if (sender is NumericUpDown slider_fps_input)
            //{
            //    // 直接转换为字符串并打印
            //    Console.WriteLine($"NumericUpDown 的值1: {slider_fps_input.Value?.ToString() ?? "空"}");
            //}

            if (sender is NumericUpDown numericUpDown && numericUpDown.Value.HasValue)
            {
                if(isloadwin == false)
                {
                    return;
                }
                // 直接设置 Slider 的值
                slider_fps.Value = (double)numericUpDown.Value.Value;

                // 打印值（可选）
                //Console.WriteLine($"NumericUpDown 的值: {numericUpDown.Value.Value}");
            }
        }


        //private void Slider_fps_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        //{
        //    int fps_minvalue = 20;
        //    if (e.NewValue < fps_minvalue)
        //    {
        //        // 如果新值小于 30，立即将 Slider 的值重置为 30
        //        // 这将再次触发 ValueChanged 事件，但传入的 NewValue 将是 30
        //        ((Slider)sender).Value = fps_minvalue;

        //        // 注意：如果你在 ViewModel 中也做了限制，这里设置后，
        //        // 由于是双向绑定，也会更新 ViewModel 中的 FpsValue 为 30。
        //    }

        //    //更新SR与Genshin FPS临时变量
        //    if (_viewModel.Config.GameSelection==true)
        //    {
        //        _viewModel.Config.FpsTargetSR = _viewModel.Config.FpsTargetTemp;
        //    }
        //    else
        //    {
        //        _viewModel.Config.FpsTarget = _viewModel.Config.FpsTargetTemp;
        //    }
        //}



        //private void Slider_fps_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        //{
        //    int fps_minvalue = 20;
        //    if (e.NewValue < fps_minvalue)
        //    {
        //        ((Slider)sender).Value = fps_minvalue;
        //    }

        //    // 四舍五入处理 FpsTargetTemp
        //    int roundedFps = (int)Math.Round((double)_viewModel.Config.FpsTargetTemp);

        //    // 更新 SR 与 Genshin FPS 临时变量
        //    if (_viewModel.Config.GameSelection)
        //    {
        //        _viewModel.Config.FpsTargetSR = roundedFps;
        //    }
        //    else
        //    {
        //        _viewModel.Config.FpsTarget = roundedFps;
        //    }
        //}

        //private void Game_Selected_Item_Unchecked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        //{
        //    slider_fps.Value = _viewModel.Config.FpsTarget;
        //}

        //private void Game_Selected_Item_Checked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        //{
        //    Console.WriteLine("ToggleSwitch 状态已改变");

        //    slider_fps.Value = _viewModel.Config.FpsTargetSR;

        //}


        //// 增加一个标志，表示当前正在切换 ToggleSwitch，不处理 Slider 的 ValueChanged
        //private bool _isSwitchingGameSelection = false;

        //private void Game_Selected_Item_Unchecked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        //{
        //    _isSwitchingGameSelection = true;
        //    slider_fps.Value = _viewModel.Config.FpsTarget;
        //    _isSwitchingGameSelection = false;
        //}

        //private void Game_Selected_Item_Checked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        //{
        //    //Console.WriteLine("ToggleSwitch 状态已改变");
        //    _isSwitchingGameSelection = true;
        //    slider_fps.Value = _viewModel.Config.FpsTargetSR;
        //    _isSwitchingGameSelection = false;
        //}

        //private void Slider_fps_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        //{
        //    if (_isSwitchingGameSelection) return; // 切换时不处理

        //    int fps_minvalue = 20;
        //    if (e.NewValue < fps_minvalue)
        //    {
        //        ((Slider)sender).Value = fps_minvalue;
        //    }

        //    int roundedFps = (int)Math.Round((double)_viewModel.Config.FpsTargetTemp);

        //    if (_viewModel.Config.GameSelection)
        //    {
        //        if (_viewModel.Config.FpsTargetSR != roundedFps)
        //        {
        //            _viewModel.Config.FpsTargetSR = roundedFps;
        //        }
        //    }
        //    else
        //    {
        //        if (_viewModel.Config.FpsTarget != roundedFps)
        //        {
        //            _viewModel.Config.FpsTarget = roundedFps;
        //        }
        //    }
        //}




        private bool _isSwitchingGameSelection = false;
        private int _lastGenshinFps = 0;
        private int _lastStarRailFps = 0;

        private void Game_Selected_Item_Unchecked(object? sender, RoutedEventArgs e)
        {
            _isSwitchingGameSelection = true;

            // 保存当前Star Rail的FPS值
            _lastStarRailFps = _viewModel.Config.FpsTargetSR;

            // 恢复Genshin的FPS值
            slider_fps.Value = _viewModel.Config.FpsTarget;

            _isSwitchingGameSelection = false;
        }

        private void Game_Selected_Item_Checked(object? sender, RoutedEventArgs e)
        {
            _isSwitchingGameSelection = true;

            // 保存当前Genshin的FPS值
            _lastGenshinFps = _viewModel.Config.FpsTarget;

            // 恢复Star Rail的FPS值
            slider_fps.Value = _viewModel.Config.FpsTargetSR;

            _isSwitchingGameSelection = false;
        }

        private void Slider_fps_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_isSwitchingGameSelection) return;

            int fps_minvalue = 20;
            if (e.NewValue < fps_minvalue)
            {
                ((Slider)sender).Value = fps_minvalue;
                return;
            }

            // 四舍五入到整数
            int roundedFps = (int)Math.Round(e.NewValue);

            if (_viewModel.Config.GameSelection)
            {
                // 只有当值真正改变时才更新
                if (_viewModel.Config.FpsTargetSR != roundedFps)
                {
                    _viewModel.Config.FpsTargetSR = roundedFps;
                    _lastStarRailFps = roundedFps; // 更新最后的值
                }
            }
            else
            {
                // 只有当值真正改变时才更新
                if (_viewModel.Config.FpsTarget != roundedFps)
                {
                    _viewModel.Config.FpsTarget = roundedFps;
                    _lastGenshinFps = roundedFps; // 更新最后的值
                }
            }
        }










        private bool _isUnlimitedFpsOnGenshin = false; // 初始状态为 Off

        private bool _isUnlimitedFpsOnStarRail = false; // 初始状态为 Off

        private void GenshinUnlimitedFpsMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            // 切换状态
            _isUnlimitedFpsOnGenshin = !_isUnlimitedFpsOnGenshin;

            // 根据新状态更新 MenuItem 的 Header，显示明确的开关状态
            UnlimitedFpsMenuItemGenshin.Header = _isUnlimitedFpsOnGenshin ?
                "_Genshin Impact (On)" : "_Genshin Impact (Off)";

            //// 根据状态启用/禁用 FPS 控制控件
            //slider_fps_input.IsEnabled = !_isUnlimitedFpsOn;
            //slider_fps.IsEnabled = !_isUnlimitedFpsOn;

            // 这里可以添加实际切换 FPS 限制的逻辑
            Console.WriteLine($"Unlimited FPS Genshin Impact is now: {_isUnlimitedFpsOnGenshin}");

            //if(_isUnlimitedFpsOn == true)
            //{
            //    _viewModel.Config.UmlimitedFps = true;
            //}
            //else
            //{
            //    _viewModel.Config.UmlimitedFps = false;
            //}

            //直接将要赋值的布尔变量 _isUnlimitedFpsOn 赋值给目标属性，因为条件判断本身已经返回布尔值，无需显式比较（== true）或使用条件语句。
            _viewModel.Config.UmlimitedFpsGenshin = _isUnlimitedFpsOnGenshin;
        }

        private void StarRailUnlimitedFpsMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            // 切换状态
            _isUnlimitedFpsOnStarRail = !_isUnlimitedFpsOnStarRail;

            // 根据新状态更新 MenuItem 的 Header，显示明确的开关状态
            UnlimitedFpsMenuItemStarRail.Header = _isUnlimitedFpsOnStarRail ?
                "_Star Rail (On)" : "_Star Rail (Off)";

            //// 根据状态启用/禁用 FPS 控制控件
            //slider_fps_input.IsEnabled = !_isUnlimitedFpsOn;
            //slider_fps.IsEnabled = !_isUnlimitedFpsOn;

            // 这里可以添加实际切换 FPS 限制的逻辑
            Console.WriteLine($"Unlimited FPS Star Rail is now: {_isUnlimitedFpsOnStarRail}");

            //if(_isUnlimitedFpsOn == true)
            //{
            //    _viewModel.Config.UmlimitedFps = true;
            //}
            //else
            //{
            //    _viewModel.Config.UmlimitedFps = false;
            //}

            //直接将要赋值的布尔变量 _isUnlimitedFpsOn 赋值给目标属性，因为条件判断本身已经返回布尔值，无需显式比较（== true）或使用条件语句。
            _viewModel.Config.UmlimitedFpsStarRail = _isUnlimitedFpsOnStarRail;
        }

        // 主题切换事件处理
        private void ApplyCustomAccentGame(object? sender, RoutedEventArgs e)
        {
            if (App.FluentTheme == null) return;

            if (_viewModel.Config.UseSystemTheme)
            {
                App.FluentTheme.CustomAccentColor = null;
                return;
            }

            if (sender is not ToggleSwitch { IsChecked: bool isChecked }) return;

            //App.FluentTheme.CustomAccentColor = isChecked ? Colors.Brown : Colors.Yellow;

            //App.FluentTheme.CustomAccentColor = isChecked ? Color.Parse("#FF69B4") : Color.Parse("#FFA500");

            App.FluentTheme.CustomAccentColor = isChecked ? Colors.DodgerBlue : Colors.Gold;
        }

        //private async Task<bool> CheckIfGameIsRunningAndAlert(List<string> processNamesToCheck, string gameDisplayName,bool shouldDisplayMsg)
        //{
        //    try
        //    {
        //        foreach (var processName in processNamesToCheck)
        //        {
        //            var processes = Process.GetProcessesByName(processName);
        //            if (processes.Length > 0)
        //            {
        //                string processIds = string.Join(", ", processes.Select(p => p.Id.ToString()));
        //                Console.WriteLine($"Game '{processName}' is already running. Process IDs: {processIds}");

        //                if (shouldDisplayMsg == true)
        //                {
        //                    var alertWindow = new AlertWindow();
        //                    alertWindow.Text = $"游戏 '{gameDisplayName}' (进程: {processName}) 已在运行中！进程ID: {processIds}";
        //                    alertWindow.Title = "Error";
        //                    alertWindow.IsError = false;
        //                    await alertWindow.ShowDialog(this);
        //                }

        //                return true; // 发现游戏正在运行
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"An error occurred while checking processes: {ex.Message}");
        //        // 可以根据需要决定是否在这里也返回 true，或者继续
        //    }

        //    return false; // 所有指定进程都未运行
        //}


        private async Task<int?> CheckIfGameIsRunningAndAlertQuick(List<string> processNamesToCheck, string gameDisplayName, bool shouldDisplayMsg)
        {
            try
            {
                foreach (var processName in processNamesToCheck)
                {
                    var processes = Process.GetProcessesByName(processName);
                    if (processes.Length > 0)
                    {
                        string processIds = string.Join(", ", processes.Select(p => p.Id.ToString()));
                        Console.WriteLine($"Game '{processName}' is already running. Process IDs: {processIds}");

                        if (shouldDisplayMsg == true)
                        {
                            var alertWindow = new AlertWindow();
                            alertWindow.Text = $"游戏 '{gameDisplayName}' (进程: {processName}) 已在运行中！进程ID: {processIds}";
                            alertWindow.Title = "Error";
                            alertWindow.IsError = false;
                            await alertWindow.ShowDialog(this);
                        }

                        // 返回第一个找到的 PID（调用方可以选择使用或忽略）
                        return processes[0].Id;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while checking processes: {ex.Message}");
            }

            return null; // 未找到任何目标进程
        }





        //private async Task<int?> CheckIfGameIsRunningAndAlert(List<string> processNamesToCheck, string gameDisplayName, bool shouldDisplayMsg, int maxRetries = 3, int delayMs = 1000)
        //{
        //    try
        //    {
        //        for (int attempt = 0; attempt < maxRetries; attempt++)
        //        {
        //            foreach (var processName in processNamesToCheck)
        //            {
        //                var processes = Process.GetProcessesByName(processName);

        //                // 过滤掉可能挂起的进程
        //                var activeProcesses = processes.Where(p =>
        //                {
        //                    try
        //                    {
        //                        // 尝试访问进程属性来检查是否真正运行
        //                        return !p.HasExited && p.StartTime != DateTime.MinValue;
        //                    }
        //                    catch
        //                    {
        //                        // 如果无法访问，可能是权限不足或进程未完全启动
        //                        return false;
        //                    }
        //                }).ToArray();


        //                //if (activeProcesses.Length > 0)
        //                if (activeProcesses.Length > 0)
        //                {
        //                    string processIds = string.Join(", ", activeProcesses.Select(p => p.Id.ToString()));
        //                    Console.WriteLine($"Game '{processName}' is already running. Process IDs: {processIds}");

        //                    if (shouldDisplayMsg)
        //                    {
        //                        var alertWindow = new AlertWindow();
        //                        alertWindow.Text = $"游戏 '{gameDisplayName}' (进程: {processName}) 已在运行中！进程ID: {processIds}";
        //                        alertWindow.Title = "Error";
        //                        alertWindow.IsError = false;
        //                        await alertWindow.ShowDialog(this);
        //                    }

        //                    return activeProcesses[0].Id;
        //                }
        //            }

        //            // 如果没有找到活跃进程，等待后重试
        //            if (attempt < maxRetries - 1)
        //            {
        //                await Task.Delay(delayMs);
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"An error occurred while checking processes: {ex.Message}");
        //    }

        //    return null;
        //}


        private async Task<int?> CheckIfGameIsRunningAndAlert(List<string> processNamesToCheck, string gameDisplayName, bool shouldDisplayMsg, int maxRetries = 3, int delayMs = 51)
        {
            try
            {
                for (int attempt = 0; attempt < maxRetries; attempt++)
                {
                    foreach (var processName in processNamesToCheck)
                    {
                        var processes = Process.GetProcessesByName(processName);

                        // 遍历找到的每个进程，检查其主线程状态
                        foreach (var process in processes)
                        {
                            bool isActuallyRunning = false;
                            try
                            {
                                // 尝试获取主线程。通常，进程的第一个线程是主线程。
                                // 注意：访问 Threads 属性可能会抛出异常，例如权限不足。
                                //ProcessThread mainThread = process.Threads[0];

                                // 检查线程状态。Running 是最理想的状态，表示线程正在CPU上执行。
                                // 我们排除 Wait 状态，因为它通常对应于等待UAC提升、I/O操作或其他系统资源，此时进程可能并未真正“运行”游戏逻辑。
                                //if (mainThread.ThreadState == System.Diagnostics.ThreadState.Running)
                                //{
                                //    isActuallyRunning = true;
                                //}


                                //// 我们找到了名为 "yuanshen" 的进程，现在需要检查它是不是“真”的
                                //Process genshinProcess = processes[0];
                                //ProcessThread mainThread = process.Threads[0];

                                //// 检查进程的内存工作集是否超过我们设定的阈值
                                ////if (genshinProcess.WorkingSet64 > memoryThreshold && genshinProcess.Threads.Count>=2)
                                //if (genshinProcess.Threads.Count >= 2 && mainThread.ThreadState == System.Diagnostics.ThreadState.Running)
                                //{
                                //    Console.WriteLine("Genshin Impact is truly running.");
                                //    Console.WriteLine($"Process ID: {genshinProcess.Id}");
                                //    Console.WriteLine($"Memory Usage: {genshinProcess.WorkingSet64 / 1024 / 1024} MB");
                                //    isActuallyRunning = true;
                                //    //break; // 找到了，退出循环
                                //}
                                //else
                                //{
                                //    // 这是“伪进程”，内存太小，忽略它
                                //    //Console.WriteLine($"Found a 'yuanshen' process (ID: {genshinProcess.Id}), but it's likely the UAC stub. Memory: {genshinProcess.WorkingSet64 / 1024} KB. Ignoring.");
                                //}


                                // 检查主模块是否已加载
                                if (process.MainModule != null && !string.IsNullOrEmpty(process.MainModule.FileName))
                                {
                                    // 检查进程是否响应
                                    if (!process.HasExited && process.Responding)
                                    {
                                        isActuallyRunning = true;
                                        Console.WriteLine($"Game '{processName}' is fully running.");
                                        Console.WriteLine($"Process ID: {process.Id}");
                                        Console.WriteLine($"Process File: {process.MainModule.FileName}");
                                        //break;
                                    }
                                }

                            }
                            catch (Exception ex)
                            {
                                // 如果无法访问进程或其线程（例如，进程正在启动或退出，或权限不足），
                                // 我们将其视为“未真正运行”，并继续检查下一个进程。
                                // Console.WriteLine($"Could not check thread for process {process.Id}: {ex.Message}");
                                isActuallyRunning = false;
                            }

                            // 如果通过线程状态确认进程正在运行
                            if (isActuallyRunning)
                            {
                                // 由于我们是在循环内部找到的，这里直接使用当前 process 的信息
                                string processId = process.Id.ToString();
                                Console.WriteLine($"Game '{processName}' is confirmed running (main thread is active). Process ID: {processId}");

                                if (shouldDisplayMsg)
                                {
                                    var alertWindow = new AlertWindow();
                                    alertWindow.Text = $"游戏 '{gameDisplayName}' (进程: {processName}) 已在运行中！进程ID: {processId}";
                                    alertWindow.Title = "Error";
                                    alertWindow.IsError = false;
                                    await alertWindow.ShowDialog(this);
                                }

                                // 找到一个就足够了，返回其ID
                                return process.Id;
                            }
                            else
                            {
                                // 进程存在，但可能处于UAC等待状态或其他非活动状态
                                Console.WriteLine($"Game '{processName}' process found, but it's not active yet (e.g., waiting for UAC).");
                            }
                        }

                        //// 释放进程对象资源，这是一个好习惯
                        //foreach (var p in processes)
                        //{
                        //    p.Dispose();
                        //}
                    }

                    // 如果没有找到活跃进程，等待后重试
                    if (attempt < maxRetries - 1)
                    {
                        await Task.Delay(delayMs);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while checking processes: {ex.Message}");
            }

            return null;
        }




        private CancellationTokenSource _cts; // 声明取消令牌源

        private async void MenuItem_Manual_Mode_Click(object? sender, RoutedEventArgs e)
        {
            string textToShow = null;
            List<string> processNamesToCheck;
            string gameDisplayName;
            int gameLaunchSection = 0;

            if (_viewModel.Config.GameSelection == false) // Genshin Impact
            {
                textToShow = "等待Genshin Impact启动...";
                processNamesToCheck = new List<string> { "YuanShen", "GenshinImpact" };
                gameDisplayName = "Genshin Impact";
                gameLaunchSection = 1;
            }
            else
            {
                textToShow = "等待Honkai: Star Rail启动...";
                processNamesToCheck = new List<string> { "StarRail", "SRSTTEST" };
                gameDisplayName = "Honkai: Star Rail";
                gameLaunchSection = 2;
            }

            //// 调用提取的方法，如果游戏正在运行则直接返回
            //if (await CheckIfGameIsRunningAndAlert(processNamesToCheck, gameDisplayName, true))
            //{
            //    return;
            //}

            // 在 MenuItem_Manual_Mode_Click 中使用新返回值
            var existingPid = await CheckIfGameIsRunningAndAlertQuick(processNamesToCheck, gameDisplayName, true);
            if (existingPid != null)
            {
                return;
            }


            // 如果代码执行到这里，说明游戏没有运行，可以继续后续逻辑
            Console.WriteLine("None of the target games are running.");
            // ... 在这里继续你的游戏启动逻辑 ...



            // 这里我们假设这段代码是在我们当前的 Window 类中执行的，"this" 对象是一个 Window。
            var ownerWindow = this;
            var loadwindow = new LoadingGame();
            ////阻止窗口关闭
            //window.Closing += (s, e) =>
            //{
            //    e.Cancel = true;
            //};
            loadwindow.Title= "Manual Mode";
            loadwindow.Height = 200;
            loadwindow.Width = 320;
            loadwindow.Topmost = true;

            // 关键：在显示前设置要传递的文本
            loadwindow.LoadingMessage = textToShow;

            // 关键：把主窗口的 DataContext（含 Config）传给 LoadingGame
            loadwindow.DataContext = this.DataContext;


            _cts = new CancellationTokenSource(); // 初始化取消令牌源
            // 启动监控任务时传递取消令牌
            //Task.Run(() => MonitorProcess(_cts.Token));
            // 将显示的 loadwindow 实例传入 MonitorProcess
            Task.Run(() => MonitorProcess(_cts.Token, loadwindow,gameLaunchSection));
            loadwindow.Closed += (s, args) =>
            {
                // 窗口关闭时触发取消操作
                //Console.WriteLine("Monitoring stopped.123");
                _cts.Cancel();
            };

            await loadwindow.ShowDialog(ownerWindow);
        }

        private async Task MonitorProcess(CancellationToken token, LoadingGame loadwindow,int LaunchSection)
        {
            //const string processNametest = "yuanshen";
            List<string> processNamesToCheck = null;
            //Console.WriteLine("Current section: " + LaunchSection);
            string gameDisplayName = "none";
            // 订阅游戏退出事件250901
            ProcessService.GameExitRequested += OnGenshinSRGameExitRequested;
            // 订阅游戏退出事件250901
            ProcessServiceSR.GameExitRequested += OnGenshinSRGameExitRequested;

            try
            {
                if (LaunchSection == 1) // Genshin Impact
                {
                    Console.WriteLine("等待Genshin Impact启动...");
                }

                if (LaunchSection == 2) // Honkai: Star Rail
                {
                    Console.WriteLine("等待Honkai: Star Rail启动...");
                }

                while (true)
                {
                    token.ThrowIfCancellationRequested(); // 检查是否被取消

                    if (LaunchSection == 1) // Genshin Impact
                    {
                        //Console.WriteLine("等待Genshin Impact启动...");
                        processNamesToCheck = new List<string> { "YuanShen", "GenshinImpact" };
                    }

                    if (LaunchSection == 2) // Honkai: Star Rail
                    {
                        //Console.WriteLine("等待Honkai: Star Rail启动...");
                        processNamesToCheck = new List<string> { "StarRail", "SRSTTEST1111122" };
                    }

                    // 在 MonitorProcess 中使用返回的 PID，并传给 ProcessServiceSR.Start
                    var detectedPid = await CheckIfGameIsRunningAndAlert(processNamesToCheck, gameDisplayName, false);

                    //if (await CheckIfGameIsRunningAndAlert(processNamesToCheck, gameDisplayName, false))
                    if (detectedPid != null)
                    {
                        //await Task.Delay(315);
                        if (LaunchSection == 1)
                        {
                            await Dispatcher.UIThread.InvokeAsync(() => loadwindow.LoadingMessage = "Genshin Impact detected!");
                            Console.WriteLine("Genshin Impact detected!");
                            _processService.Start(true,detectedPid);
                        }

                        if (LaunchSection == 2)
                        {
                            await Dispatcher.UIThread.InvokeAsync(() => loadwindow.LoadingMessage = "Honkai: Star Rail detected!");
                            Console.WriteLine("Honkai: Star Rail detected!");
                            _processServiceSR = new ProcessServiceSR(_configService);
                            _processServiceSR.Start(_viewModel.Config.LaunchOptions.SRGamePath, null, true, detectedPid);
                        }

                        await Task.Delay(1531);

                        try
                        {
                            await Dispatcher.UIThread.InvokeAsync(() => loadwindow.Close());
                        }
                        catch
                        {
                            // 忽略可能的关闭异常
                        }

                        return;
                    }

                    // 使用可取消的异步延迟
                    await Task.Delay(15, token);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Monitoring stopped.");
                // 取消订阅游戏退出事件
                ProcessService.GameExitRequested -= OnGenshinSRGameExitRequested;
            }
        }

        private async Task LaunchGameSR(bool isAutoStart)
        {
            if (_viewModel.Config.EnableFOVStarRail == false)
            {
                //设置FOV状态251106
                SettingsWindow.allowsetSRFOVstate(false);
            }

            if (!File.Exists(_viewModel.Config.LaunchOptions.SRGamePath))
            {
                if (isAutoStart) return;
                await MainWindowViewModel.ShowWindow<InitializationWindow>();
            }

            if (!File.Exists(_viewModel.Config.LaunchOptions.SRGamePath)) return;


            try
            {
                string customParam = _viewModel.Config.SRCustomParam ?? string.Empty;
                // 在这里使用 new 关键字创建 ProcessServiceSR 的实例
                //_processServiceSR = new ProcessServiceSR();
                _processServiceSR = new ProcessServiceSR(_configService);
                //在这里添加启动游戏
                _processServiceSR.Start(_viewModel.Config.LaunchOptions.SRGamePath,customParam,false);
                // 订阅游戏退出事件250901
                ProcessServiceSR.GameExitRequested += OnGenshinSRGameExitRequested;

                WindowState = WindowState.Minimized;
            }
            catch (Exception ex)
            {
                await ShowErrorMessage(ex.Message);
            }


        }

        // 事件处理程序250901
        private void OnGenshinSRGameExitRequested(object sender, EventArgs e)
        {
            if (_viewModel.Config.AutoClose && ProcessService.ShouldGenshinExit == true && ProcessServiceSR.ShouldSRExit == true)
            {
                Dispatcher.UIThread.InvokeAsync(() => Close());

                // 260202 停止GenshinFog或StarRailFOV共享内存写入
                IpcServiceSRFov.SharedMemoryWriter.Stop();
            }
            //else
            //{
            //    Dispatcher.UIThread.Invoke(() =>
            //    {
            //        Show();
            //        WindowState = WindowState.Normal;
            //    });
            //}

            if (!_viewModel.Config.AutoClose && ProcessService.ShouldGenshinExit == true && ProcessServiceSR.ShouldSRExit == true)
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    Show();
                    WindowState = WindowState.Normal;
                });

                // 260202 停止GenshinFog或StarRailFOV共享内存写入
                IpcServiceSRFov.SharedMemoryWriter.Stop();
            }

            if (ProcessService.ShouldGenshinExit == true)
            {
                //设置FOV开关状态251106
                SettingsWindow.allowsetFOVstate(true);
                //Console.WriteLine("Genshin Impact has exited.");
            }

            if (ProcessServiceSR.ShouldSRExit == true)
            {
                //设置FOV开关状态251106
                SettingsWindow.allowsetSRFOVstate(true);
                //Console.WriteLine("Genshin Impact has exited.");
            }

            // 当启用GenshinMbUIWt或StarRailMbUIWt时，关闭游戏后杀掉wTE_ysr251207a进程
            if (_viewModel.Config.EnableGenshinMbUIWt == true || _viewModel.Config.EnableStarRailMbUIWt == true)
            {
                try
                {
                    var processes = Process.GetProcessesByName("wTE_ysr251210b");
                    foreach (var process in processes)
                    {
                        process.Kill();
                        //process.WaitForExit();
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error: {ex.Message}");
                }
            }
        }

        private async void MenuItem_About_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var mainWindow = this;
            var aboutWindow = new AboutWindow();
            //setWindow.Title = "Loading...";
            aboutWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            await aboutWindow.ShowDialog(mainWindow);
        }

        //251102
        public void RestoreFromTray()
        {
            //if (InvokeRequired)
            //{
            //    Invoke(RestoreFromTray);
            //    return;
            //}

            //WindowState = FormWindowState.Normal;
            //ShowInTaskbar = true;
            //TopMost = true;
            //Show();
            //Activate();
            //TopMost = false;

            //Location = _windowLocation;
            //Size = _windowSize;


            // 如果当前不是 UI 线程，就在 UI 线程上异步调用自身
            if (!Dispatcher.UIThread.CheckAccess())
            {
                // 使用 InvokeAsync 确保在 UI 线程执行
                Dispatcher.UIThread.InvokeAsync(RestoreFromTray);
                return;
            }

            WindowState = WindowState.Normal;
            ShowInTaskbar = true;
            Topmost = true;
            Show();
            Activate();
            Topmost = false;

            // RestoreFromTray 中恢复位置/大小（替换原来的 Location/Size 用法）
            Position = _windowLocation;
            Width = _windowSize.Width;
            Height = _windowSize.Height;
        }
    }
}
