using System;
using System.Runtime.InteropServices;

namespace CefFlashBrowser.Utils
{
    public static class Win32
    {
        public const int WM_MOVE = 0x0003;
        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;


        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        [DllImport("kernel32.dll")]
        public static extern bool SetDllDirectory(string lpPathName);
    }
}
