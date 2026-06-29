using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
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
                new PropertyMetadata(null, null, DialogResultCoerceValue));

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

        public static bool? ShowModal(Window window, Window owner = null)
        {
            if (owner == null)
            {
                owner = Application.Current.Windows
                    .OfType<Window>().FirstOrDefault(w => w.IsActive);
            }

            if (owner == null)
            {
                if (Keyboard.FocusedElement is DependencyObject obj)
                {
                    owner = Window.GetWindow(obj);
                }
            }

            if (owner == null || owner == window)
            {
                window.ShowDialog();
                return GetDialogResult(window);
            }

            var frame = new DispatcherFrame();
            var hOwner = new WindowInteropHelper(owner).Handle;
            bool storeEnabled = Win32.IsWindowEnabled(hOwner);

            // Re-enable the owner from GetBeforeDestroyHandlers BEFORE WM_DESTROY
            // tears down the HWND, so Windows activates the owner (not another window) on close.
            window.SourceInitialized += (s, e) =>
            {
                GetBeforeDestroyHandlers(window).Add(delegate
                {
                    Win32.EnableWindow(hOwner, storeEnabled);
                });
            };

            window.Closed += (s, e) =>
            {
                owner.Activate();
                frame.Continue = false;
            };

            window.Owner = owner;
            window.Show();
            Win32.EnableWindow(hOwner, false);

            try
            {
                Dispatcher.PushFrame(frame);
            }
            finally
            {
                frame.Continue = false;
                Win32.EnableWindow(hOwner, storeEnabled);
            }

            return GetDialogResult(window);
        }

        /// <summary>
        /// Gets the handlers that run just before the window HWND is destroyed.
        /// </summary>
        /// <remarks>
        /// The hook is attached through the window's HwndSource, so the window must
        /// already be initialized before calling this method.
        /// </remarks>
        /// <param name="window">The window to attach the before-destroy hook to.</param>
        /// <returns>The list of handlers invoked from WM_DESTROY.</returns>
        public static List<EventHandler> GetBeforeDestroyHandlers(Window window)
        {
            if (window == null)
                throw new ArgumentNullException(nameof(window));

            const string key = "__BeforeDestroyHandlers";

            List<EventHandler> getHandlers(Window wnd)
            {
                return wnd.Resources.Contains(key) ? wnd.Resources[key] as List<EventHandler> : null;
            }

            if (getHandlers(window) is List<EventHandler> handlers)
            {
                return handlers;
            }
            else
            {
                handlers = new List<EventHandler>();

                IntPtr wndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
                {
                    if (msg == Win32.WM_DESTROY
                        && getHandlers(window) is List<EventHandler> list)
                    {
                        foreach (var h in list.ToArray())
                            h?.Invoke(window, EventArgs.Empty);
                    }
                    return IntPtr.Zero;
                }

                var source = (HwndSource)PresentationSource.FromVisual(window)
                    ?? throw new InvalidOperationException("Window must be initialized before calling GetBeforeDestroyHandlers.");

                window.Resources[key] = handlers;
                source.AddHook(wndProc);
                return handlers;
            }
        }
    }
}
