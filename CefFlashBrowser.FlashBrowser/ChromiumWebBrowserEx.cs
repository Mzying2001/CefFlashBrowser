using CefFlashBrowser.WinformCefSharp4WPF;
using CefSharp;
using SimpleMvvm.Command;
using System.Windows;
using System.Windows.Input;

namespace CefFlashBrowser.FlashBrowser
{
    public class ChromiumWebBrowserEx : ChromiumWebBrowser
    {
        private class WpfFocusHandler : Handlers.FocusHandler
        {
            public override void OnGotFocus(IWebBrowser chromiumWebBrowser, IBrowser browser)
            {
                if (browser.IsPopup)
                {
                    return; //ignore if method is called by devtools
                }

                (chromiumWebBrowser as UIElement)?.Dispatcher.Invoke(delegate
                {
                    //var element = (UIElement)chromiumWebBrowser;
                    //element.Focus();

                    DependencyObject d = (DependencyObject)chromiumWebBrowser;
                    while (LogicalTreeHelper.GetParent(d) is DependencyObject parent)
                    {
                        d = parent;
                    }
                    if (!ReferenceEquals(d, chromiumWebBrowser))
                    {
                        FocusManager.SetFocusedElement(d, (IInputElement)chromiumWebBrowser);
                    }
                });
            }
        }

        private class WpfKeyboardHandler : Handlers.KeyboardHandler
        {
            public override bool OnPreKeyEvent(IWebBrowser chromiumWebBrowser, IBrowser browser, KeyType type, int windowsKeyCode, int nativeKeyCode, CefEventFlags modifiers, bool isSystemKey, ref bool isKeyboardShortcut)
            {
                if (type != KeyType.RawKeyDown)
                    return false;

                bool result = false;

                (chromiumWebBrowser as UIElement)?.Dispatcher.Invoke(delegate
                {
                    UIElement element = (UIElement)chromiumWebBrowser;
                    PresentationSource source = PresentationSource.FromVisual(element);

                    Key key = KeyInterop.KeyFromVirtualKey(windowsKeyCode);
                    KeyEventArgs args = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.KeyDownEvent, };
                    element.RaiseEvent(args);

                    result = args.Handled;
                });

                return result;
            }
        }

        public ICommand LoadUrlCommand { get; }

        public ChromiumWebBrowserEx()
        {
            FocusHandler = new WpfFocusHandler();
            KeyboardHandler = new WpfKeyboardHandler();
            LoadUrlCommand = new DelegateCommand<string>(Load);
        }
    }
}
