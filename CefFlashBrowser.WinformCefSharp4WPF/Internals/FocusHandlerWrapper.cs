using CefSharp;

namespace CefFlashBrowser.WinformCefSharp4WPF.Internals
{
    internal class FocusHandlerWrapper : IFocusHandler, IHandlerWrapper<IFocusHandler>
    {
        public IFocusHandler Handler { get; }

        public IWebBrowser TargetBrowser { get; }

        public FocusHandlerWrapper(IFocusHandler handler, IWebBrowser targetBrowser)
        {
            Handler = handler;
            TargetBrowser = targetBrowser;
        }

        public void OnGotFocus(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
            Handler.OnGotFocus(TargetBrowser, browser);
        }

        public bool OnSetFocus(IWebBrowser chromiumWebBrowser, IBrowser browser, CefFocusSource source)
        {
            return Handler.OnSetFocus(TargetBrowser, browser, source);
        }

        public void OnTakeFocus(IWebBrowser chromiumWebBrowser, IBrowser browser, bool next)
        {
            Handler.OnTakeFocus(TargetBrowser, browser, next);
        }
    }
}
