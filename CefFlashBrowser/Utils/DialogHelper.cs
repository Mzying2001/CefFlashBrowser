using System;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace CefFlashBrowser.Utils
{
    public static class DialogHelper
    {
        public static readonly DependencyProperty DialogResultProperty =
            DependencyProperty.RegisterAttached("DialogResult", typeof(bool?), typeof(DialogHelper), new PropertyMetadata(null, DialogResultPropertyChanged));

        public static bool? GetDialogResult(DependencyObject obj)
        {
            return (bool?)obj.GetValue(DialogResultProperty);
        }

        public static void SetDialogResult(DependencyObject obj, bool? value)
        {
            obj.SetValue(DialogResultProperty, value);
        }

        private static void DialogResultPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Window w)
            {
                try
                {
                    w.DialogResult = e.NewValue as bool?;
                }
                catch (InvalidOperationException)
                {
                    w.Close();
                }
            }
        }

        public static bool? ShowModal(Window window, Window owner = null)
        {
            if (owner == null)
            {
                owner = Application.Current.Windows
                    .OfType<Window>().FirstOrDefault(w => w.IsActive);
            }

            if (owner == null || owner == window)
            {
                window.ShowDialog();
                return GetDialogResult(window);
            }

            var frame = new DispatcherFrame();
            var hOwner = new WindowInteropHelper(owner).Handle;
            bool storeEnabled = Win32.IsWindowEnabled(hOwner);

            window.Closed += (s, e) =>
            {
                Win32.EnableWindow(hOwner, storeEnabled);
                owner.Activate();
                frame.Continue = false;
            };

            window.Owner = owner;
            window.Show();
            Win32.EnableWindow(hOwner, false);
            Dispatcher.PushFrame(frame);
            return GetDialogResult(window);
        }
    }
}
