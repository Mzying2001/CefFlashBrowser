using System;

namespace CefFlashBrowser.FlashBrowser
{
    public class FaviconDiscoveredEventArgs : EventArgs
    {
        public string PageUrl { get; }
        public string IconUrl { get; }

        public FaviconDiscoveredEventArgs(string pageUrl, string iconUrl)
        {
            PageUrl = pageUrl;
            IconUrl = iconUrl;
        }
    }
}
