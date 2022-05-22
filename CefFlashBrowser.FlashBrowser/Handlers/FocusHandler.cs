using CefSharp;

namespace CefFlashBrowser.FlashBrowser.Handlers
{
    public class FocusHandler : IFocusHandler
    {
        public virtual void OnGotFocus(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
        }

        public virtual bool OnSetFocus(IWebBrowser chromiumWebBrowser, IBrowser browser, CefFocusSource source)
        {
            return false;
        }

        public virtual void OnTakeFocus(IWebBrowser chromiumWebBrowser, IBrowser browser, bool next)
        {
        }
    }
}
