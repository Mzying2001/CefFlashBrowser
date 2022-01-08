using CefSharp;
using System;

namespace CefFlashBrowser.FlashBrowser
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
}
