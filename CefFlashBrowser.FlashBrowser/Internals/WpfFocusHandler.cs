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
                element.Dispatcher.Invoke(() =>
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
            if (InnerHandler != null)
            {
                return InnerHandler.OnSetFocus(chromiumWebBrowser, browser, source);
            }
            else
            {
                // Do not let the browser take focus when a Load method has been called
                return source == CefFocusSource.FocusSourceNavigation;
            }
        }

        public void OnTakeFocus(IWebBrowser chromiumWebBrowser, IBrowser browser, bool next)
        {
            if (!browser.IsPopup && chromiumWebBrowser is UIElement element)
            {
                element.Dispatcher.Invoke(() =>
                {
                    // Move focus to the next or previous WPF element
                    element.MoveFocus(new TraversalRequest(
                        next ? FocusNavigationDirection.Next : FocusNavigationDirection.Previous));
                });
            }
            InnerHandler?.OnTakeFocus(chromiumWebBrowser, browser, next);
        }
    }
}
