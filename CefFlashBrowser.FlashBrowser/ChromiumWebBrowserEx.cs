using CefFlashBrowser.WinformCefSharp4WPF;
using CefSharp;
using SimpleMvvm.Command;
using System.Windows;
using System.Windows.Input;

namespace CefFlashBrowser.FlashBrowser
{
    public class ChromiumWebBrowserEx : ChromiumWebBrowser
    {
        private class NotifyWpfFocusHandler : Handlers.FocusHandler
        {
            public override void OnGotFocus(IWebBrowser chromiumWebBrowser, IBrowser browser)
            {
                if (browser.IsPopup)
                {
                    return; //ignore if method is called by devtools
                }

                (chromiumWebBrowser as UIElement)?.Dispatcher.Invoke(delegate
                {
                    var element = (UIElement)chromiumWebBrowser;
                    element.Focus();
                });
            }
        }

        public ICommand LoadUrlCommand { get; }

        public ChromiumWebBrowserEx()
        {
            FocusHandler = new NotifyWpfFocusHandler();
            LoadUrlCommand = new DelegateCommand<string>(Load);
        }
    }
}
