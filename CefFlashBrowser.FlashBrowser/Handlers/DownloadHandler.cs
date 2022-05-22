using CefSharp;

namespace CefFlashBrowser.FlashBrowser.Handlers
{
    public class DownloadHandler : IDownloadHandler
    {
        public virtual void OnBeforeDownload(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem, IBeforeDownloadCallback callback)
        {
        }

        public virtual void OnDownloadUpdated(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem, IDownloadItemCallback callback)
        {
        }
    }
}
