using CefSharp;
using System;

namespace CefFlashBrowser.FlashBrowser.Handlers
{
    public class LifeSpanHandler : ILifeSpanHandler
    {



        public class NewWindowEventArgs : EventArgs
        {
            public IWebBrowser Browser { get; set; }
            public string TargetUrl { get; set; }
            public bool CancelPopup { get; set; }
            public NewWindowEventArgs() { }
            public NewWindowEventArgs(IWebBrowser browser, string targetUrl, bool cancelPopup)
            {
                Browser = browser;
                TargetUrl = targetUrl;
                CancelPopup = cancelPopup;
            }
        }



        public event EventHandler<NewWindowEventArgs> OnCreateNewWindow;

        public event EventHandler<EventArgs> OnClose;

        public LifeSpanHandler() { }

        public LifeSpanHandler(EventHandler<NewWindowEventArgs> onCreateNewWindow = null, EventHandler<EventArgs> onClose = null)
        {
            OnCreateNewWindow += onCreateNewWindow;
            OnClose += onClose;
        }

        public bool DoClose(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
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
            newBrowser = null;

            NewWindowEventArgs args = new NewWindowEventArgs(chromiumWebBrowser, targetUrl, false);
            OnCreateNewWindow?.Invoke(this, args);
            return args.CancelPopup;
        }
    }
}
