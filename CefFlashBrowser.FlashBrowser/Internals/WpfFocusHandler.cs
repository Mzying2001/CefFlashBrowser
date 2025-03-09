using CefSharp;
using System.Windows;
using System.Windows.Input;

namespace CefFlashBrowser.FlashBrowser.Internals
{
    internal class WpfFocusHandler : IFocusHandler
    {
        public IFocusHandler InnerHandler { get; }

        public WpfFocusHandler(IFocusHandler innerHandler)
        {
            InnerHandler = innerHandler;
        }

        public void OnGotFocus(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
            if (!browser.IsPopup && chromiumWebBrowser is UIElement element)
            {
                element.Dispatcher.InvokeAsync(() =>
                {
                    if (!element.Focus() && element.IsFocused)
                    {
                        Keyboard.Focus(element);
                    }
                });
            }
            InnerHandler?.OnGotFocus(chromiumWebBrowser, browser);
        }

        public bool OnSetFocus(IWebBrowser chromiumWebBrowser, IBrowser browser, CefFocusSource source)
        {
            return InnerHandler?.OnSetFocus(chromiumWebBrowser, browser, source) ?? false;
        }

        public void OnTakeFocus(IWebBrowser chromiumWebBrowser, IBrowser browser, bool next)
        {
            InnerHandler?.OnTakeFocus(chromiumWebBrowser, browser, next);
        }
    }
}
