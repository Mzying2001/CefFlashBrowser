using CefSharp;

namespace CefFlashBrowser.WinformCefSharp4WPF.Internals
{
    internal class RenderProcessMessageHandlerWrapper : IRenderProcessMessageHandler, IHandlerWrapper<IRenderProcessMessageHandler>
    {
        public IRenderProcessMessageHandler Handler { get; }

        public IWebBrowser TargetBrowser { get; }

        public RenderProcessMessageHandlerWrapper(IRenderProcessMessageHandler handler, IWebBrowser targetBrowser)
        {
            Handler = handler;
            TargetBrowser = targetBrowser;
        }

        public void OnContextCreated(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame)
        {
            Handler.OnContextCreated(TargetBrowser, browser, frame);
        }

        public void OnContextReleased(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame)
        {
            Handler.OnContextReleased(TargetBrowser, browser, frame);
        }

        public void OnFocusedNodeChanged(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IDomNode node)
        {
            Handler.OnFocusedNodeChanged(TargetBrowser, browser, frame, node);
        }

        public void OnUncaughtException(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, JavascriptException exception)
        {
            Handler.OnUncaughtException(TargetBrowser, browser, frame, exception);
        }
    }
}
