using CefSharp;
using System;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace CefFlashBrowser.Utils
{
    public static class HwndHelper
    {
        private const int DEVTOOLSFLAG = 0x00000001;
        private const string PROP_DEVTOOLSFLAG = "CefFlashBrowser.DevToolsFlag";

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

        public static void ApplyEmbeddedChildStyle(IntPtr hwnd, bool visible = true)
        {
            SetWindowStyle(hwnd, Win32.WS_CHILD | (visible ? Win32.WS_VISIBLE : 0));
        }

        public static void SetDevToolsFlag(IntPtr hwnd)
        {
            Win32.SetProp(hwnd, PROP_DEVTOOLSFLAG, new IntPtr(DEVTOOLSFLAG));
        }

        public static bool CheckDevToolsFlag(IntPtr hwnd)
        {
            return Win32.GetProp(hwnd, PROP_DEVTOOLSFLAG) == new IntPtr(DEVTOOLSFLAG);
        }

        public static bool IsDevToolsWindow(IntPtr hwnd)
        {
            if (CheckDevToolsFlag(hwnd))
            {
                return true;
            }
            else
            {
                var clsname = new StringBuilder(256);
                Win32.GetClassName(hwnd, clsname, clsname.Capacity);

                var wndName = new StringBuilder(256);
                Win32.GetWindowText(hwnd, wndName, wndName.Capacity);

                return clsname.ToString().Equals("CefBrowserWindow", StringComparison.OrdinalIgnoreCase)
                    && wndName.ToString().StartsWith("devtools", StringComparison.OrdinalIgnoreCase);
            }
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

        /// <summary>
        /// Find the top-level devtools window that owned by the specified browser
        /// </summary>
        public static IntPtr FindNotIntegratedDevTools(IWebBrowser browser)
        {
            var obj = browser as DependencyObject;

            while (obj != null && !(obj is Window))
            {
                obj = LogicalTreeHelper.GetParent(obj);
            }

            if (obj is Window w)
            {
                IntPtr hwnd = new WindowInteropHelper(w).Handle;
                Win32.GetWindowThreadProcessId(hwnd, out IntPtr pidWnd);
                return FindDevTools(pidWnd, hwnd);
            }

            return IntPtr.Zero;
        }
    }
}
