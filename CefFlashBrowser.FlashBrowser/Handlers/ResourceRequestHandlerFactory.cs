using CefSharp;

namespace CefFlashBrowser.FlashBrowser.Handlers
{
    public class ResourceRequestHandlerFactory : IResourceRequestHandlerFactory
    {
        public virtual bool HasHandlers
        {
            get { return false; }
        }

        public virtual IResourceRequestHandler GetResourceRequestHandler(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool isNavigation, bool isDownload, string requestInitiator, ref bool disableDefaultHandling)
        {
            return null;
        }
    }
}
