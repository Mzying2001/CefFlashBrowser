namespace CefFlashBrowser.Models
{
    public sealed class WebsiteIconRecord
    {
        public string PageUrl { get; }
        public string IconUrl { get; }
        public long UpdatedAt { get; }

        public WebsiteIconRecord(string pageUrl, string iconUrl, long updatedAt)
        {
            PageUrl = pageUrl;
            IconUrl = iconUrl;
            UpdatedAt = updatedAt;
        }
    }
}
