using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace UnlockFps.Gui.Utils
{
    internal class Native2
    {
        // 260116
        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")]
        public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        [DllImport("user32.dll")]
        public static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        public const int GWL_WNDPROC = -4;
        public const int WM_HOTKEY = 0x0312;
        public const int HOTKEY_ID = 9000;
        public const uint VK_F12 = 0x78;
        public const uint VK_F10 = 0x79;

        // 260215 读取ini文件
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetPrivateProfileString(
            string lpAppName,
            string lpKeyName,
            string lpDefault,
            StringBuilder lpReturnedString,
            int nSize,
            string lpFileName);

        public static string ReadIniValue(string iniFilePath, string section, string key)
        {
            var sb = new StringBuilder(255);
            GetPrivateProfileString(section, key, "", sb, sb.Capacity, iniFilePath);
            return sb.ToString();
        }
    }
}
