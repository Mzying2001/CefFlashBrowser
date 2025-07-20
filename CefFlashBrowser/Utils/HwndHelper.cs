using System;
using System.Text;

namespace CefFlashBrowser.Utils
{
    public static class HwndHelper
    {
        public static int GetWindowStyle(IntPtr hwnd)
        {
            return (int)Win32.GetWindowLongPtr(hwnd, Win32.GWL_STYLE);
        }

        public static int SetWindowStyle(IntPtr hwnd, int style)
        {
            return (int)Win32.SetWindowLongPtr(hwnd, Win32.GWL_STYLE, (IntPtr)style);
        }

        public static IntPtr GetOwnerWindow(IntPtr hwnd)
        {
            return Win32.GetWindowLongPtr(hwnd, Win32.GWLP_HWNDPARENT);
        }

        public static IntPtr SetOwnerWindow(IntPtr hwnd, IntPtr hOwner)
        {
            return Win32.SetWindowLongPtr(hwnd, Win32.GWLP_HWNDPARENT, hOwner);
        }

        public static bool IsDevToolsWindow(IntPtr hwnd)
        {
            var clsname = new StringBuilder(256);
            Win32.GetClassName(hwnd, clsname, clsname.Capacity);

            var wndName = new StringBuilder(256);
            Win32.GetWindowText(hwnd, wndName, wndName.Capacity);

            return clsname.ToString().Equals("CefBrowserWindow", StringComparison.OrdinalIgnoreCase)
                && wndName.ToString().StartsWith("devtools", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Find the devtools window that has the specified owner
        /// </summary>
        public static IntPtr FindDevTools(IntPtr pid, IntPtr hOwner)
        {
            IntPtr hDevTools = IntPtr.Zero;

            Win32.EnumWindows((hWnd, lParam) =>
            {
                Win32.GetWindowThreadProcessId(hWnd, out IntPtr pidWnd);

                if (pidWnd == pid
                    && IsDevToolsWindow(hWnd)
                    && GetOwnerWindow(hWnd) == hOwner)
                {
                    hDevTools = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            return hDevTools;
        }

        /// <summary>
        /// Find the devtools window that has no owner
        /// </summary>
        public static IntPtr FindDevTools(IntPtr pid)
        {
            return FindDevTools(pid, IntPtr.Zero);
        }
    }
}
