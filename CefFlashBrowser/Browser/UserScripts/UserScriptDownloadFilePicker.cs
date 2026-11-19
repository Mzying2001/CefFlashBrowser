using CefFlashBrowser.Utils;
using Microsoft.Win32;
using System;
using System.Diagnostics;
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
            Win32.EnumWindows((window, parameter) =>
            {
                Win32.GetWindowThreadProcessId(window, out var process);

                if (process != (uint)Process.GetCurrentProcess().Id || Win32.GetWindow(window, Win32.GW_OWNER) != owner)
                    return true;

                var text = new StringBuilder(256);
                var className = new StringBuilder(64);

                Win32.GetWindowText(window, text, text.Capacity);
                Win32.GetClassName(window, className, className.Capacity);

                if (text.ToString() != title || className.ToString() != "#32770")
                    return true;

                Win32.PostMessage(window, Win32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                return false;
            }, IntPtr.Zero);
        }
    }
}
