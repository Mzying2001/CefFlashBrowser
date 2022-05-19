using CefSharp;

namespace CefFlashBrowser.WinformCefSharp4WPF.Internals
{
    internal class ResourceRequestHandlerFactoryWrapper : IResourceRequestHandlerFactory, IHandlerWrapper<IResourceRequestHandlerFactory>
    {
        public IResourceRequestHandlerFactory Handler { get; }

        public IWebBrowser TargetBrowser { get; }

        public bool HasHandlers => Handler.HasHandlers;

        public ResourceRequestHandlerFactoryWrapper(IResourceRequestHandlerFactory handler, IWebBrowser targetBrowser)
        {
            Handler = handler;
            TargetBrowser = targetBrowser;
        }

        public IResourceRequestHandler GetResourceRequestHandler(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool isNavigation, bool isDownload, string requestInitiator, ref bool disableDefaultHandling)
        {
            return Handler.GetResourceRequestHandler(TargetBrowser, browser, frame, request, isNavigation, isDownload, requestInitiator, ref disableDefaultHandling);
        }
    }
}
