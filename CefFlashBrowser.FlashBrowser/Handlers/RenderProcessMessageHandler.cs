using CefSharp;

namespace CefFlashBrowser.FlashBrowser.Handlers
{
    public class RenderProcessMessageHandler : IRenderProcessMessageHandler
    {
        public virtual void OnContextCreated(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame)
        {
        }

        public virtual void OnContextReleased(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame)
        {
        }

        public virtual void OnFocusedNodeChanged(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IDomNode node)
        {
        }

        public virtual void OnUncaughtException(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, JavascriptException exception)
        {
        }
    }
}
