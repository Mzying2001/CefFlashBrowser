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
            var b = chromiumWebBrowser as ChromiumWebBrowser;

            b?.Dispatcher.Invoke(() =>
            {
                b.Address = targetUrl;
            });

            newBrowser = null;
            return true;
        }
    }
}
