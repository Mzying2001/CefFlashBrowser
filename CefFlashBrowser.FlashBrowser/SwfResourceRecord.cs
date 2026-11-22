using System;

namespace CefFlashBrowser.FlashBrowser
{
    public sealed class SwfResourceRecord
    {
        public string Url { get; }
        public string FileName { get; }

        internal SwfResourceRecord(string address, Uri url)
        {
            Url = address;
            var path = url.AbsolutePath;
            var name = path.Substring(path.LastIndexOf('/') + 1);
            FileName = name.Length > 0 ? Uri.UnescapeDataString(name) : address;
        }
    }
}
