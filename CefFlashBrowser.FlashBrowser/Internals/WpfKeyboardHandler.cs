using CefSharp;
using System.Windows;
using System.Windows.Input;

namespace CefFlashBrowser.FlashBrowser.Internals
{
    internal class WpfKeyboardHandler : IKeyboardHandler
    {
        public IKeyboardHandler InnerHandler { get; }

        public WpfKeyboardHandler(IKeyboardHandler innerHandler)
        {
            InnerHandler = innerHandler;
        }

        public bool OnKeyEvent(IWebBrowser chromiumWebBrowser, IBrowser browser, KeyType type, int windowsKeyCode, int nativeKeyCode, CefEventFlags modifiers, bool isSystemKey)
        {
            return InnerHandler?.OnKeyEvent(chromiumWebBrowser, browser, type, windowsKeyCode, nativeKeyCode, modifiers, isSystemKey) ?? false;
        }

        public bool OnPreKeyEvent(IWebBrowser chromiumWebBrowser, IBrowser browser, KeyType type, int windowsKeyCode, int nativeKeyCode, CefEventFlags modifiers, bool isSystemKey, ref bool isKeyboardShortcut)
        {
            bool handled = InnerHandler?.OnPreKeyEvent(chromiumWebBrowser, browser, type, windowsKeyCode, nativeKeyCode, modifiers, isSystemKey, ref isKeyboardShortcut) ?? false;
            if (!handled && type == KeyType.RawKeyDown && chromiumWebBrowser is UIElement element)
            {
                element.Dispatcher.Invoke(() =>
                {
                    PresentationSource source = PresentationSource.FromVisual(element);

                    Key key = KeyInterop.KeyFromVirtualKey(windowsKeyCode);
                    KeyEventArgs args = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.KeyDownEvent, };
                    element.RaiseEvent(args);

                    handled = args.Handled;
                });
            }
            return handled;
        }
    }
}
