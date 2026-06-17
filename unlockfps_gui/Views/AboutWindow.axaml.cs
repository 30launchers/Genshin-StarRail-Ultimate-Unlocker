using Avalonia.Controls;
using Avalonia.Input;
using System;
using System.Diagnostics;
using UnlockFps.Gui.Utils;

namespace UnlockFps.Gui.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        //this.SetSystemChrome();
        InitializeComponent();
        //Run_Version.Text = "v" + ReflectionUtil.GetInformationalVersion();
    }

    private void HyperLink_OnTapped(object? sender, TappedEventArgs e)
    {
        if (sender is TextBlock { Text: { } text })
        {
            Process.Start(new ProcessStartInfo(text) { UseShellExecute = true });
        }
    }

    private async void HyperLink_OnTapped_Authur(object sender, TappedEventArgs e)
    {
        if (sender is TextBlock textBlock && textBlock.Tag is string url)
        {
            // 使用 Process.Start 打开链接
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
    }
}