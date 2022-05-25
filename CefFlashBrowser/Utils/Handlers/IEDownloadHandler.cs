using CefFlashBrowser.FlashBrowser.Handlers;
using CefSharp;
using System.Windows.Controls;

namespace CefFlashBrowser.Utils.Handlers
{
    public class IEDownloadHandler : DownloadHandler
    {
        public override void OnBeforeDownload(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem, IBeforeDownloadCallback callback)
        {
            new WebBrowser().Navigate(downloadItem.Url);
        }
    }
}
