using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace CefFlashBrowser.Browser.UserScripts
{
    internal static class UserScriptDownloadFilePicker
    {
        internal static string Select(Window owner, string title, string name, string directory, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var handle = new WindowInteropHelper(owner).Handle;

            // Cancellation can arrive before the native dialog creates its HWND.
            // Its modal loop pumps this timer as well as the queued close attempt.
            var timer = new DispatcherTimer(DispatcherPriority.Normal, owner.Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };

            EventHandler tick = (sender, e) =>
            {
                if (token.IsCancellationRequested)
                    Close(handle, title);
            };

            timer.Tick += tick;
            timer.Start();

            try
            {
                using (token.Register(() => owner.Dispatcher.BeginInvoke(new Action(() => Close(handle, title)))))
                {
                    token.ThrowIfCancellationRequested();

                    var dialog = new SaveFileDialog
                    {
                        Title = title,
                        FileName = name,
                        InitialDirectory = directory,
                        OverwritePrompt = true,
                        AddExtension = false
                    };

                    return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
                }
            }
            finally
            {
                timer.Stop();
                timer.Tick -= tick;
            }
        }

        private static void Close(IntPtr owner, string title)
        {
            EnumWindows((window, parameter) =>
            {
                GetWindowThreadProcessId(window, out var process);

                if (process != (uint)Process.GetCurrentProcess().Id || GetWindow(window, 4) != owner)
                    return true;

                var text = new StringBuilder(256);
                var className = new StringBuilder(64);

                GetWindowText(window, text, text.Capacity);
                GetClassName(window, className, className.Capacity);

                if (text.ToString() != title || className.ToString() != "#32770")
                    return true;

                PostMessage(window, 0x10, IntPtr.Zero, IntPtr.Zero);
                return false;
            }, IntPtr.Zero);
        }

        private delegate bool WindowCallback(IntPtr window, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(WindowCallback callback, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr window, uint command);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr window, StringBuilder text, int count);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr window, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    }
}
