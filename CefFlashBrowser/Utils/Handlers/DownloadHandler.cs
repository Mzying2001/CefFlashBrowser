using CefSharp;
using System;

namespace CefFlashBrowser.Utils.Handlers
{
    public class DownloadHandler : FlashBrowser.Handlers.DownloadHandler
    {
        public override void OnBeforeDownload(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem, IBeforeDownloadCallback callback)
        {
            if (!callback.IsDisposed)
                callback.Dispose();

            try
            {
                Win32.DoFileDownload(downloadItem.Url);
            }
            catch (Exception e)
            {
                LogHelper.LogError($"Failed to start download: {downloadItem.Url}", e);
            }
        }
    }
}
