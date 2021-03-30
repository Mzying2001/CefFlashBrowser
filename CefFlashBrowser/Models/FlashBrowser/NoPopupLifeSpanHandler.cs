using CefSharp;
using CefSharp.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CefFlashBrowser.Models.FlashBrowser
{
    public class NoPopupLifeSpanHandler : ILifeSpanHandler
    {
        private readonly EventHandler<NewPageEventArgs> OnOpenNewPage;

        public NoPopupLifeSpanHandler(EventHandler<NewPageEventArgs> OnOpenNewPage = null)
        {
            this.OnOpenNewPage = OnOpenNewPage;
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
            if (chromiumWebBrowser is ChromiumFlashBrowser fbrowser)
            {
                fbrowser.Dispatcher.Invoke(() =>
                {
                    fbrowser.Address = targetUrl;
                });

                OnOpenNewPage?.Invoke(fbrowser, new NewPageEventArgs(targetUrl));
            }

            newBrowser = null;
            return true;
        }
    }
}
