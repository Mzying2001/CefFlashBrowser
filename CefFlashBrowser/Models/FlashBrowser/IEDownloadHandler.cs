using CefSharp;
using System.Windows.Controls;

namespace CefFlashBrowser.Models.FlashBrowser
{
    public class IEDownloadHandler : IDownloadHandler
    {
        public void OnBeforeDownload(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem, IBeforeDownloadCallback callback)
        {
            new WebBrowser().Navigate(downloadItem.Url);
        }

        public void OnDownloadUpdated(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem, IDownloadItemCallback callback)
        {
        }
    }
}
