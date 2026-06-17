using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnlockFps.Gui.Utils;
using UnlockFps.Gui.Views;
//251102
using System.Linq;

namespace UnlockFps.Gui;

internal sealed class Program
{
    private static readonly string MutexName = "E9FD8B73-CD15-690D-8BF0-8D74D06F50D4";
    //private static readonly string MutexName = "GenshinFPSUnlocker";
    private static readonly string EventName = "DE80CB0E-E18C-AE09-8149-9BFA85107BE2";

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        //using (new Mutex(true, @"GenshinFPSUnlocker", out var createdNew))
        //{
        //    DuplicatedInstance = !createdNew;
        //    BuildAvaloniaApp()
        //        .StartWithClassicDesktopLifetime(args);
        //}

        //251102
        using (new Mutex(true, MutexName, out var createdNew))
        {
            if (!createdNew)
            {
                //second instance
                try
                {
                    using var evt = EventWaitHandle.OpenExisting(EventName);
                    evt.Set();
                }
                catch { }

                Console.WriteLine("重复的实例，已停止");
                // 已有实例，直接退出进程（不启动 Avalonia）
                return;
            }

            using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
            _ = Task.Run(() => {
                while (showEvent.WaitOne())
                {

                    //var form = Application.OpenForms
                    //    .OfType<MainWindow>()
                    //    .FirstOrDefault();

                    //if (form is { IsHandleCreated: true })
                    //{
                    //    form.RestoreFromTray();
                    //}


                    // 使用 ApplicationLifetime 获取窗口集合（适用于经典桌面生命周期）
                    if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        var form = desktop.Windows
                            .OfType<MainWindow>()
                            .FirstOrDefault();

                        if (form is not null)
                        {
                            Console.WriteLine("重复的实例，已恢复原实例");
                            // 在 UI 线程上恢复窗口
                            Dispatcher.UIThread.Post(() => form.RestoreFromTray());
                        }
                    }
                }
            });


            DuplicatedInstance = !createdNew;
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
    }

    public static bool DuplicatedInstance { get; private set; }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        var appBuilder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithNativeFonts()
            .LogToTrace()
            .UseReactiveUI();
        if (WineHelper.DetectWine(out _, out _))
        {
            return appBuilder
                .With(new Win32PlatformOptions
                {
                    CompositionMode = [Win32CompositionMode.RedirectionSurface],
                    RenderingMode = [Win32RenderingMode.Software],
                    OverlayPopups = true
                });
        }
        else
        {
            return appBuilder;
        }
    }
}