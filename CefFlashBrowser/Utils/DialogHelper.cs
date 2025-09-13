using System;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace CefFlashBrowser.Utils
{
    public static class DialogHelper
    {
        private static readonly DependencyProperty IsDialogResultChangingProperty =
            DependencyProperty.RegisterAttached(
                "IsDialogResultChanging",
                typeof(bool),
                typeof(DialogHelper),
                new PropertyMetadata(false));

        public static readonly DependencyProperty DialogResultProperty =
            DependencyProperty.RegisterAttached(
                "DialogResult",
                typeof(bool?),
                typeof(DialogHelper),
                new PropertyMetadata(null, DialogResultPropertyChanged, DialogResultCoerceValue));

        public static bool? GetDialogResult(DependencyObject obj)
        {
            return (bool?)obj.GetValue(DialogResultProperty);
        }

        public static void SetDialogResult(DependencyObject obj, bool? value)
        {
            obj.SetValue(DialogResultProperty, value);
        }

        private static object DialogResultCoerceValue(DependencyObject d, object baseValue)
        {
            // Why use CoerceValueCallback?
            // PropertyChangedCallback is only called when the value actually changes,
            // but we need to handle the case where the same value is set again.
            // For example, if the DialogResult is set to true but the close operation
            // is cancelled, setting it to true again should still attempt to close the window.

            var window = d as Window;
            var result = baseValue;

            if (window != null &&
                !(bool)d.GetValue(IsDialogResultChangingProperty))
            {
                // Update the DialogResult property if the window is successfully closed,
                // otherwise keep the previous value.

                void windowClosedHandler(object s, EventArgs e)
                {
                    result = baseValue as bool?;
                }

                result = GetDialogResult(d);
                window.Closed += windowClosedHandler;
                window.SetValue(IsDialogResultChangingProperty, true);
                window.SetCurrentValue(DialogResultProperty, baseValue);

                try
                {
                    window.DialogResult = baseValue as bool?;
                }
                catch (InvalidOperationException)
                {
                    window.Close();
                }
                finally
                {
                    window.Closed -= windowClosedHandler;
                    window.SetValue(IsDialogResultChangingProperty, false);
                }
            }

            return result;
        }

        private static void DialogResultPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //if (d is Window w)
            //{
            //    try
            //    {
            //        w.DialogResult = e.NewValue as bool?;
            //    }
            //    catch (InvalidOperationException)
            //    {
            //        w.Close();
            //    }
            //}
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
