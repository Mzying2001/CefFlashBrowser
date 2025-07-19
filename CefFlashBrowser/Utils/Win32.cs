using System;
using System.Runtime.InteropServices;
using System.Text;

namespace CefFlashBrowser.Utils
{
    public static class Win32
    {
        public const int WM_MOVE = 0x0003;

        public const int GWLP_HWNDPARENT = -8;

        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);


        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        [DllImport("kernel32.dll")]
        public static extern bool SetDllDirectory(string lpPathName);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out IntPtr lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    }
}
