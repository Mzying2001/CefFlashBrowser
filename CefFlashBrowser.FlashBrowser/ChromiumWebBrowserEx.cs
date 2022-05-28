using CefFlashBrowser.WinformCefSharp4WPF;
using CefSharp;
using SimpleMvvm.Command;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace CefFlashBrowser.FlashBrowser
{
    public class ChromiumWebBrowserEx : ChromiumWebBrowser
    {
        private class NotifyWpfFocusHandler : Handlers.FocusHandler
        {
            public override void OnGotFocus(IWebBrowser chromiumWebBrowser, IBrowser browser)
            {
                ((IWpfWebBrowser)chromiumWebBrowser).Dispatcher.Invoke(delegate
                {
                    DependencyObject d = (DependencyObject)chromiumWebBrowser;
                    while (VisualTreeHelper.GetParent(d) is DependencyObject parent)
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

        public ICommand LoadUrlCommand { get; }

        public ChromiumWebBrowserEx()
        {
            FocusHandler = new NotifyWpfFocusHandler();
            LoadUrlCommand = new DelegateCommand<string>(Load);
        }
    }
}
