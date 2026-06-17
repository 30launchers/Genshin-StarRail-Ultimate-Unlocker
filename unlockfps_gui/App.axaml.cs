using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using FluentAvalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using UnlockFps.Gui.Utils;
using UnlockFps.Gui.Views;
using UnlockFps.Services;

namespace UnlockFps.Gui;

public partial class App : Application
{
    public static ServiceProvider DefaultServices { get; private set; } = null!;

    // 公开FluentAvaloniaTheme实例以便其他窗口访问，用于切换控件颜色主题
    public static FluentAvaloniaTheme FluentTheme { get; private set; } = null!;

    public static Window? CurrentMainWindow =>
        (App.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    public override void Initialize() 
    {
        AvaloniaXamlLoader.Load(this);

        // 获取FluentAvaloniaTheme实例，用于切换控件颜色主题
        FluentTheme = this.Styles[0] as FluentAvaloniaTheme;
    }

    public override void RegisterServices()
    {
        base.RegisterServices();

        var services = new ServiceCollection();
        services.AddTransient<AboutWindow>();
        services.AddTransient<AlertWindow>();
        services.AddTransient<InitializationWindow>();
        services.AddTransient<MainWindow>();
        services.AddTransient<SettingsWindow>();
        services.AddSingleton<ConfigService>();
        services.AddSingleton<ProcessService>();
        services.AddSingleton<GameInstanceService>();
        DefaultServices = services.BuildServiceProvider();
    }

    private  Config _config;

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;
            var configService = DefaultServices.GetRequiredService<ConfigService>();
            configService.Config.PropertyChanged += Config_PropertyChanged;
            ToggleConsole(configService.Config.ShowDebugConsole);
            if (!Program.DuplicatedInstance)
            {
                desktop.MainWindow = DefaultServices.GetRequiredService<MainWindow>();
                //var mainWindow = DefaultServices.GetRequiredService<MainWindow>();
                //desktop.MainWindow = mainWindow;
                //_config = configService.Config;
                //bool autolau = _config.AutoLaunch;
                //if (autolau==true)
                //{
                //    Console.WriteLine("11111");
                    
                //    mainWindow.Hide();
                //    mainWindow.Width = 320;
                //    // 使用 Dispatcher 来确保窗口完全初始化后再最小化
                //    Dispatcher.UIThread.Post(() =>
                //    {
                //        mainWindow.WindowState = WindowState.Minimized;
                //    }, DispatcherPriority.Background);
                //}
            }
            else
            {
                var alertWindow = DefaultServices.GetRequiredService<AlertWindow>();
                alertWindow.Text = "Another unlocker is already running.";
                desktop.MainWindow = alertWindow;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void Config_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is Config config && e.PropertyName == nameof(Config.ShowDebugConsole))
        {
            ToggleConsole(config.ShowDebugConsole);
        }
    }

    private static void TaskSchedulerOnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        if (e.Exception.InnerExceptions.Count == 1)
        {
            Console.WriteLine("Unobserved task exception: " + e.Exception.InnerException);
        }
        else
        {
            Console.WriteLine("Unobserved task exception: " + e.Exception);
        }
    }

    private static void ToggleConsole(bool show)
    {
        try
        {
            if (show)
            {
                ConsoleManager.Show();
            }
            else
            {
                ConsoleManager.Hide();
            }
        }
        catch
        {
            // ignored
        }
    }
}