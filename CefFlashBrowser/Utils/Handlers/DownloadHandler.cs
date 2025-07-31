using CefSharp;
using System;
using System.Diagnostics;

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
                Process.Start(new ProcessStartInfo
                {
                    FileName = downloadItem.Url,
                    UseShellExecute = true,
                });
            }
            catch (Exception e)
            {
                LogHelper.LogError($"Failed to start download: {downloadItem.Url}", e);
            }
        }
    }
}
