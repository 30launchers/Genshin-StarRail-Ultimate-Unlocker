using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace UnlockFps.Gui;

public partial class LoadingGame : Window
{
    public LoadingGame()
    {
        InitializeComponent();
    }

    // 通过名字查找控件并设置文本，安全简单
    public string LoadingMessage
    {
        get
        {
            var tb = this.FindControl<TextBlock>("LoadingTextBlock");
            return tb?.Text ?? string.Empty;
        }
        set
        {
            var tb = this.FindControl<TextBlock>("LoadingTextBlock");
            if (tb != null)
                tb.Text = value;
        }
    }

}