using CefSharp;
using System;

namespace CefFlashBrowser.FlashBrowser.Handlers
{
    public class LifeSpanHandler : ILifeSpanHandler
    {



        public class NewBrowserEventArgs : EventArgs
        {
            public bool Handled { get; set; }
            public string TargetUrl { get; set; }
            public IWebBrowser Browser { get; set; }
            public IWebBrowser NewBrowser { get; set; }
            public IPopupFeatures PopupFeatures { get; set; }
            public WindowOpenDisposition OpenDisposition { get; set; }
        }



        public event EventHandler<NewBrowserEventArgs> OnCreateNewBrowser;

        public event EventHandler<EventArgs> OnClose;

        public LifeSpanHandler() { }

        public LifeSpanHandler(EventHandler<NewBrowserEventArgs> onCreateNewBrowser = null, EventHandler<EventArgs> onClose = null)
        {
            OnCreateNewBrowser += onCreateNewBrowser;
            OnClose += onClose;
        }



        public bool DoClose(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
            if (!browser.IsPopup)
                OnClose?.Invoke(this, new EventArgs());
            return false;
        }

        public void OnAfterCreated(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
        }

        public void OnBeforeClose(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
        }

        public bool OnBeforePopup(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, string targetUrl, string targetFrameName, WindowOpenDisposition targetDisposition, bool userGesture, IPopupFeatures popupFeatures, IWindowInfo windowInfo, IBrowserSettings browserSettings, ref bool noJavascriptAccess, out IWebBrowser newBrowser)
        {
            var args = new NewBrowserEventArgs
            {
                Handled = false,
                NewBrowser = null,
                TargetUrl = targetUrl,
                Browser = chromiumWebBrowser,
                PopupFeatures = popupFeatures,
                OpenDisposition = targetDisposition
            };

            OnCreateNewBrowser?.Invoke(this, args);
            newBrowser = args.NewBrowser;
            return args.Handled;
        }
    }
}
