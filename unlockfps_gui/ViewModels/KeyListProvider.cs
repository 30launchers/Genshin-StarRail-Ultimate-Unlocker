//using System;
//using System.Collections.Generic;
//using System.Linq;
//using Avalonia.Input;

//namespace UnlockFps.Gui.ViewModels;

///// <summary>
///// 提供"全键盘"可选列表，等价于 GetAvailableKeys()。
///// 通过反射 Avalonia.Input.Key 枚举一次性拿到所有按键。
///// </summary>
//public static class KeyListProvider
//{
//    private static readonly IReadOnlyList<KeyOption> _allKeys = BuildAllKeys();

//    /// <summary>获取所有可用按键（已去重、按键码升序）。</summary>
//    public static IReadOnlyList<KeyOption> AllKeys => _allKeys;

//    private static IReadOnlyList<KeyOption> BuildAllKeys()
//    {
//        var list = new List<KeyOption>();
//        foreach (Key key in Enum.GetValues(typeof(Key)))
//        {
//            if (key == Key.None) continue;
//            list.Add(new KeyOption { KeyCode = (int)key, KeyName = key.ToString() });
//        }
//        return list
//            .GroupBy(k => k.KeyCode)
//            .Select(g => g.First())
//            .OrderBy(k => k.KeyCode)
//            .ToList();
//    }

//    /// <summary>按 Avalonia 键码查找对应的 KeyOption（找不到返回 null）。</summary>
//    public static KeyOption? FindByKeyCode(int keyCode) =>
//        _allKeys.FirstOrDefault(k => k.KeyCode == keyCode);
//}


using System;
using System.Collections.Generic;
using System.Linq;

namespace UnlockFps.Gui.ViewModels;

/// <summary>
/// 提供"全键盘"可选列表（等价于 fufu 的 GetAvailableKeys）。
/// 通过反射 <see cref="ConsoleKey"/> 枚举动态生成所有按键。
/// ConsoleKey 的整数值与 Win32 VK 码一致（如 F12 = 123），
/// 可直接传给 DLL 的 GetAsyncKeyState，无需任何转换。
/// </summary>
public static class KeyListProvider
{
    private static readonly IReadOnlyList<KeyOption> _allKeys = BuildAllKeys();

    /// <summary>获取所有可用按键（按 VK 码升序）。</summary>
    public static IReadOnlyList<KeyOption> AllKeys => _allKeys;

    /// <summary>按 VK 码查找对应的 KeyOption（找不到返回 null）。</summary>
    public static KeyOption? FindByKeyCode(int keyCode) =>
        _allKeys.FirstOrDefault(k => k.KeyCode == keyCode);

    private static IReadOnlyList<KeyOption> BuildAllKeys()
    {
        // 等价于 GetAvailableKeys()：
        // 反射枚举拿全部按键，跳过 None/0，去重，按键码升序。
        // 区别：其他用的是 Windows.System.VirtualKey，
        // 这里用 System.ConsoleKey（同样基于 Win32 VK 码体系）。
        var list = new List<KeyOption>();
        foreach (ConsoleKey key in Enum.GetValues(typeof(ConsoleKey)))
        {
            if (key == ConsoleKey.None) continue;
            list.Add(new KeyOption { KeyCode = (int)key, KeyName = key.ToString() });
        }
        return list
            .GroupBy(k => k.KeyCode)
            .Select(g => g.First())
            .OrderBy(k => k.KeyCode)
            .ToList();
    }
}
