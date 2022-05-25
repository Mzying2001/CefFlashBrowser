using CefFlashBrowser.WinformCefSharp4WPF;
using SimpleMvvm.Command;
using System.Windows.Input;

namespace CefFlashBrowser.FlashBrowser
{
    public class ChromiumWebBrowserEx : ChromiumWebBrowser
    {
        public ICommand LoadUrlCommand { get; }

        public ChromiumWebBrowserEx()
        {
            LoadUrlCommand = new DelegateCommand<string>(Load);
        }
    }
}
