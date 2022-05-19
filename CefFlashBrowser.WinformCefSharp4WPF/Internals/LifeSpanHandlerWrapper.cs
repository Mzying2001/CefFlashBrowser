using CefSharp;

namespace CefFlashBrowser.WinformCefSharp4WPF.Internals
{
    internal class LifeSpanHandlerWrapper : ILifeSpanHandler, IHandlerWrapper<ILifeSpanHandler>
    {
        public ILifeSpanHandler Handler { get; }

        public IWebBrowser TargetBrowser { get; }

        public LifeSpanHandlerWrapper(ILifeSpanHandler handler, IWebBrowser targetBrowser)
        {
            Handler = handler;
            TargetBrowser = targetBrowser;
        }

        public bool DoClose(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
            return Handler.DoClose(TargetBrowser, browser);
        }

        public void OnAfterCreated(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
            Handler.OnAfterCreated(TargetBrowser, browser);
        }

        public void OnBeforeClose(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
            Handler.OnBeforeClose(TargetBrowser, browser);
        }

        public bool OnBeforePopup(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, string targetUrl, string targetFrameName, WindowOpenDisposition targetDisposition, bool userGesture, IPopupFeatures popupFeatures, IWindowInfo windowInfo, IBrowserSettings browserSettings, ref bool noJavascriptAccess, out IWebBrowser newBrowser)
        {
            return Handler.OnBeforePopup(TargetBrowser, browser, frame, targetUrl, targetFrameName, targetDisposition, userGesture, popupFeatures, windowInfo, browserSettings, ref noJavascriptAccess, out newBrowser);
        }
    }
}
