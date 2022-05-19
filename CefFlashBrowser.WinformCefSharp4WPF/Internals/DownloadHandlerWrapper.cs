using CefSharp;

namespace CefFlashBrowser.WinformCefSharp4WPF.Internals
{
    internal class DownloadHandlerWrapper : IDownloadHandler, IHandlerWrapper<IDownloadHandler>
    {
        public IDownloadHandler Handler { get; }

        public IWebBrowser TargetBrowser { get; }

        public DownloadHandlerWrapper(IDownloadHandler handler, IWebBrowser targetBrowser)
        {
            Handler = handler;
            TargetBrowser = targetBrowser;
        }

        public void OnBeforeDownload(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem, IBeforeDownloadCallback callback)
        {
            Handler.OnBeforeDownload(TargetBrowser, browser, downloadItem, callback);
        }

        public void OnDownloadUpdated(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem, IDownloadItemCallback callback)
        {
            Handler.OnDownloadUpdated(TargetBrowser, browser, downloadItem, callback);
        }
    }
}
