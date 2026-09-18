using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CefFlashBrowser.WinformCefSharp4WPF.Internals
{
    /// <summary>
    /// 触屏增强的运行时开关。由主程序根据设置读取/写入：
    ///  TranslateToMouse = true  -> 把触摸翻译成鼠标事件（v2），让 SWF 里鼠标型的内容可被手指拖动
    ///  TranslateToMouse = false -> 保持原生触摸（v1，依赖页面 touch-action:none）
    /// 之所以用 static，是为了让用户在设置里切换后立即生效，无需重建浏览器控件。
    /// </summary>
    public static class TouchEmulationSettings
    {
        /// <summary>是否把单指触摸翻译成鼠标拖动。默认关闭（由实验室开关控制）。</summary>
        public static bool TranslateToMouse = false;
    }


    /// <summary>
    /// CefSharp 浏览器控件的子类：在 Win32 层把 WM_TOUCH 翻译成
    /// WM_LBUTTONDOWN / WM_MOUSEMOVE / WM_LBUTTONUP 序列，直接喂给 CEF 控件句柄。
    /// 这样 Pepper Flash（SWF 只认鼠标）在触屏上也能收到连续拖动，
    /// 解决鼠标型交互（如滑块）在触屏上滑不动的问题。
    /// </summary>
    internal class TouchEmulationChromiumWebBrowser : CefSharp.WinForms.ChromiumWebBrowser
    {
        // --- 窗口消息 ---
        private const int WM_TOUCH = 0x0240;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;

        // --- TOUCH 标志 ---
        private const int TOUCHEVENTF_DOWN = 0x0001;
        private const int TOUCHEVENTF_MOVE = 0x0002;
        private const int TOUCHEVENTF_UP = 0x0004;

        private const int MK_LBUTTON = 0x0001;

        private bool _registeredTouch;
        private int _trackedTouchId = -1;
        private bool _buttonDown;

        public TouchEmulationChromiumWebBrowser()
        {
            HandleCreated += (s, e) => RegisterTouch();
        }

        private void RegisterTouch()
        {
            if (_registeredTouch || IsDisposed || Handle == IntPtr.Zero)
            {
                return;
            }
            // 请求把触摸以 WM_TOUCH 形式发送到本控件句柄
            // （CEF 自身也有触摸处理；即使它先处理了 WM_TOUCH，这里仍是纯增量的鼠标注入，不会更糟）
            try
            {
                if (RegisterTouchWindow(Handle, 0))
                {
                    _registeredTouch = true;
                }
            }
            catch
            {
                _registeredTouch = false;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_TOUCH && TouchEmulationSettings.TranslateToMouse)
            {
                if (TranslateTouchToMouse(m))
                {
                    // 已注入鼠标事件；不再交给 CefSharp/Chromium 继续当作触摸手势处理
                    return;
                }
            }
            base.WndProc(ref m);
        }

        private bool TranslateTouchToMouse(Message m)
        {
            int count = m.WParam.ToInt32();
            IntPtr touchHandle = m.LParam;
            if (count <= 0 || touchHandle == IntPtr.Zero)
            {
                return false;
            }

            var inputs = new TOUCHINPUT[count];
            IntPtr size = (IntPtr)Marshal.SizeOf(typeof(TOUCHINPUT));
            if (!GetTouchInputInfo(touchHandle, (uint)count, inputs, size))
            {
                return false;
            }

            try
            {
                var screen = System.Windows.Forms.Screen.FromControl(this);
                if (screen == null)
                {
                    return false;
                }

                foreach (var t in inputs)
                {
                    if (t.dwID >= 0 && (t.dwID == _trackedTouchId || _trackedTouchId < 0))
                    {
                        // 坐标是“屏幕物理坐标 ×100”，先还原成屏幕像素，再转到控件客户区
                        int sx = t.x / 100;
                        int sy = t.y / 100;
                        System.Drawing.Point client = this.PointToClient(new System.Drawing.Point(sx, sy));
                        int lp = MakeLParam(client.X, client.Y);

                        if ((t.dwFlags & TOUCHEVENTF_DOWN) != 0)
                        {
                            _trackedTouchId = (int)t.dwID;
                            _buttonDown = true;
                            SendMessage(Handle, WM_LBUTTONDOWN, (IntPtr)MK_LBUTTON, (IntPtr)lp);
                        }
                        else if ((t.dwFlags & TOUCHEVENTF_MOVE) != 0 && _buttonDown)
                        {
                            SendMessage(Handle, WM_MOUSEMOVE, (IntPtr)MK_LBUTTON, (IntPtr)lp);
                        }
                        else if ((t.dwFlags & TOUCHEVENTF_UP) != 0 && _buttonDown)
                        {
                            SendMessage(Handle, WM_MOUSEMOVE, (IntPtr)MK_LBUTTON, (IntPtr)lp);
                            SendMessage(Handle, WM_LBUTTONUP, IntPtr.Zero, (IntPtr)lp);
                            _buttonDown = false;
                            _trackedTouchId = -1;
                        }
                    }
                }
                return true;
            }
            finally
            {
                CloseTouchInputHandle(touchHandle);
            }
        }

        private static int MakeLParam(int x, int y)
        {
            return (y & 0xFFFF) << 16 | (x & 0xFFFF);
        }


        [StructLayout(LayoutKind.Sequential)]
        internal struct TOUCHINPUT
        {
            public int x;
            public int y;
            public IntPtr hSource;
            public int dwID;
            public int dwFlags;
            public int dwMask;
            public int dwTime;
            public IntPtr dwExtraInfo;
            public int cxContact;
            public int cyContact;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RegisterTouchWindow(IntPtr hWnd, int flags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetTouchInputInfo(IntPtr hTouchInput, uint cInputs,
            [Out] TOUCHINPUT[] pInputs, IntPtr cbSize);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseTouchInputHandle(IntPtr hTouchInput);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
    }
}