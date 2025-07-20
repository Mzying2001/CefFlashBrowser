using CefFlashBrowser.Utils;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CefFlashBrowser.Views.Custom
{
    public class HwndContainer : HwndHost
    {
        private HandleRef _hSelf;


        public IntPtr ContentHandle
        {
            get { return (IntPtr)GetValue(ContentHandleProperty); }
            set { SetValue(ContentHandleProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ContentHandle.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ContentHandleProperty =
            DependencyProperty.Register("ContentHandle", typeof(IntPtr), typeof(HwndContainer), new PropertyMetadata(IntPtr.Zero, ContentHandlePropertyChangedHandler));

        private static void ContentHandlePropertyChangedHandler(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HwndContainer container
                && e.OldValue is IntPtr hOld
                && e.NewValue is IntPtr hNew)
            {
                container.OnContentHandleChanged(hOld, hNew);
            }
        }


        public HwndContainer()
        {
        }

        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            return _hSelf = new HandleRef(this, Win32.CreateWindowEx(
                0, "STATIC", "", Win32.WS_CHILD | Win32.WS_VISIBLE, 0, 0, 0, 0,
                hwndParent.Handle, IntPtr.Zero, Win32.GetModuleHandle(null), IntPtr.Zero));
        }

        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            Win32.DestroyWindow(hwnd.Handle);
            _hSelf = new HandleRef();
        }

        protected virtual void OnContentHandleChanged(IntPtr hOld, IntPtr hNew)
        {
            Win32.SetParent(hOld, IntPtr.Zero);
            Win32.SetParent(hNew, _hSelf.Handle);
            FillContentHandle();
        }

        protected override void OnWindowPositionChanged(Rect rcBoundingBox)
        {
            base.OnWindowPositionChanged(rcBoundingBox);
            FillContentHandle();
        }

        private void FillContentHandle()
        {
            var contentHandle = ContentHandle;
            if (contentHandle != IntPtr.Zero)
            {
                Win32.GetClientRect(_hSelf.Handle, out Win32.RECT rect);
                Win32.MoveWindow(contentHandle, rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top, true);
            }
        }
    }
}
