using CefSharp;
using CefSharp.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CefFlashBrowser.Models.FlashBrowser
{
    public class FlashBrowserLifeSpanHandler : ILifeSpanHandler
    {
        private readonly EventHandler<NewWindowEventArgs> OnCreateNewWindow;

        public FlashBrowserLifeSpanHandler(EventHandler<NewWindowEventArgs> OnCreateNewWindow = null)
        {
            this.OnCreateNewWindow = OnCreateNewWindow;
        }

        public bool DoClose(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
            return !(browser.IsDisposed || browser.IsPopup);
        }

        public void OnAfterCreated(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
        }

        public void OnBeforeClose(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
        }

        public bool OnBeforePopup(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, string targetUrl, string targetFrameName, WindowOpenDisposition targetDisposition, bool userGesture, IPopupFeatures popupFeatures, IWindowInfo windowInfo, IBrowserSettings browserSettings, ref bool noJavascriptAccess, out IWebBrowser newBrowser)
        {
            newBrowser = null;

            if (chromiumWebBrowser is ChromiumFlashBrowser fbrowser)
            {
                var args = new NewWindowEventArgs(targetUrl, true);
                OnCreateNewWindow?.Invoke(fbrowser, args);

                if (args.CancelPopup)
                {
                    fbrowser.Dispatcher.Invoke(() =>
                    {
                        fbrowser.Address = targetUrl;
                    });
                    return true;
                }
            }
            return false;
        }
    }
}
