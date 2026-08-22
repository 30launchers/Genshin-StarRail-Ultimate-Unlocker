namespace UnlockFps.Gui.ViewModels;

/// <summary>
/// 表示一个可选按键，用于 ComboBox 绑定。
/// 对应 项目里的 VirtualKeyOption。
/// </summary>
public sealed class KeyOption
{
    /// <summary>Avalonia.Input.Key 的整数值</summary>
    public int KeyCode { get; set; }

    /// <summary>显示名称（枚举名）</summary>
    public string KeyName { get; set; } = string.Empty;

    public override string ToString() => KeyName;
}
