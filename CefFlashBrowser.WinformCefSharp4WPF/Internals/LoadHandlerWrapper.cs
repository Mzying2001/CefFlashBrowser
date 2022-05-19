using CefSharp;

namespace CefFlashBrowser.WinformCefSharp4WPF.Internals
{
    internal class LoadHandlerWrapper : ILoadHandler, IHandlerWrapper<ILoadHandler>
    {
        public ILoadHandler Handler { get; }

        public IWebBrowser TargetBrowser { get; }

        public LoadHandlerWrapper(ILoadHandler handler, IWebBrowser targetBrowser)
        {
            Handler = handler;
            TargetBrowser = targetBrowser;
        }

        public void OnFrameLoadEnd(IWebBrowser chromiumWebBrowser, FrameLoadEndEventArgs frameLoadEndArgs)
        {
            Handler.OnFrameLoadEnd(TargetBrowser, frameLoadEndArgs);
        }

        public void OnFrameLoadStart(IWebBrowser chromiumWebBrowser, FrameLoadStartEventArgs frameLoadStartArgs)
        {
            Handler.OnFrameLoadStart(TargetBrowser, frameLoadStartArgs);
        }

        public void OnLoadError(IWebBrowser chromiumWebBrowser, LoadErrorEventArgs loadErrorArgs)
        {
            Handler.OnLoadError(TargetBrowser, loadErrorArgs);
        }

        public void OnLoadingStateChange(IWebBrowser chromiumWebBrowser, LoadingStateChangedEventArgs loadingStateChangedArgs)
        {
            Handler.OnLoadingStateChange(TargetBrowser, loadingStateChangedArgs);
        }
    }
}
