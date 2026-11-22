using CefSharp;
using System;

namespace CefFlashBrowser.FlashBrowser
{
    public sealed class SwfResourceRecord
    {
        public string Url { get; }
        public string FileName { get; }
        public string RequestedUrl { get; }
        public long FrameId { get; }
        public string FrameUrl { get; }
        public string ReferrerUrl { get; }
        public ReferrerPolicy ReferrerPolicy { get; }
        public bool HasOpaqueInitiator { get; }

        internal long FrameVersion { get; }

        internal SwfResourceRecord(string address, Uri url, string requestedUrl, long frameId,
            string frameUrl, long frameVersion, string referrerUrl, ReferrerPolicy referrerPolicy, bool hasOpaqueInitiator)
        {
            Url = address;
            RequestedUrl = requestedUrl;
            FrameId = frameId;
            FrameUrl = frameUrl;
            FrameVersion = frameVersion;
            ReferrerUrl = referrerUrl;
            ReferrerPolicy = referrerPolicy;
            HasOpaqueInitiator = hasOpaqueInitiator;
            var path = url.AbsolutePath;
            var name = path.Substring(path.LastIndexOf('/') + 1);
            FileName = name.Length > 0 ? Uri.UnescapeDataString(name) : address;
        }
    }
}
