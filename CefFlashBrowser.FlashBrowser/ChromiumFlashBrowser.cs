using CefSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;

namespace CefFlashBrowser.FlashBrowser
{
    public class ChromiumFlashBrowser : ChromiumWebBrowserEx
    {
        private readonly ObservableCollection<SwfResourceRecord> _pageSwfs = new ObservableCollection<SwfResourceRecord>();
        private readonly HashSet<string> _pageSwfUrls = new HashSet<string>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<long, long> _swfFrameVersions = new ConcurrentDictionary<long, long>();
        private long _swfPageVersion;
        private long _nextSwfFrameVersion;

        public ReadOnlyObservableCollection<SwfResourceRecord> PageSwfs { get; }

        public ChromiumFlashBrowser() : this(false)
        {
        }

        public ChromiumFlashBrowser(bool deferNativeCreation) : base(deferNativeCreation)
        {
            PageSwfs = new ReadOnlyObservableCollection<SwfResourceRecord>(_pageSwfs);
        }

        protected override void OnFrameLoadStart(FrameLoadStartEventArgs e)
        {
            if (!IsDisposed)
            {
                if (e.Frame.IsMain)
                {
                    Interlocked.Increment(ref _swfPageVersion);
                    _swfFrameVersions.Clear();
                    _pageSwfUrls.Clear();
                    _pageSwfs.Clear();
                }

                _swfFrameVersions[e.Frame.Identifier] = ++_nextSwfFrameVersion;
            }

            base.OnFrameLoadStart(e);
        }

        protected override Action<string, string> CreateResourceRequestObserver(
            IBrowser browser, IFrame frame, IRequest request, string requestInitiator)
        {
            var pageVersion = Interlocked.Read(ref _swfPageVersion);

            if (IsDisposed || browser == null || browser.IsPopup || frame?.IsValid != true
                || request.ResourceType == ResourceType.MainFrame
                || !Uri.TryCreate(request.Url, UriKind.Absolute, out var url))
                return null;

            if (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps
                && (!url.IsFile || !url.AbsolutePath.EndsWith(".swf", StringComparison.OrdinalIgnoreCase)))
                return null;

            var frameId = frame.Identifier;
            var frameUrl = frame.Url;
            _swfFrameVersions.TryGetValue(frameId, out var frameVersion);
            var requestedUrl = request.Url;
            var referrerUrl = request.ReferrerUrl;
            var referrerPolicy = request.ReferrerPolicy;
            var hasOpaqueInitiator = requestInitiator == "null";

            return (address, mimeType) =>
            {
                if (!TryGetSwfUrl(pageVersion, address, mimeType, out var swfUrl))
                    return;

                var swf = new SwfResourceRecord(address, swfUrl, requestedUrl, frameId,
                    frameUrl, frameVersion, referrerUrl, referrerPolicy, hasOpaqueInitiator);

                Dispatcher.InvokeAsync(() =>
                {
                    if (IsDisposed || pageVersion != Interlocked.Read(ref _swfPageVersion) || !_pageSwfUrls.Add(address))
                        return;

                    var index = _pageSwfs.Count;

                    while (index > 0 && StringComparer.OrdinalIgnoreCase.Compare(_pageSwfs[index - 1].FileName, swf.FileName) > 0)
                        index--;
                    _pageSwfs.Insert(index, swf);
                });
            };
        }

        public bool IsSwfSourceCurrent(SwfResourceRecord swf)
        {
            return !IsDisposed && _pageSwfs.Contains(swf)
                && _swfFrameVersions.TryGetValue(swf.FrameId, out var version) && version == swf.FrameVersion;
        }

        private bool TryGetSwfUrl(long pageVersion, string address, string mimeType, out Uri url)
        {
            url = null;
            if (pageVersion != Interlocked.Read(ref _swfPageVersion) || IsDisposed
                || !Uri.TryCreate(address, UriKind.Absolute, out url))
                return false;

            if (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps && !url.IsFile)
                return false;

            if (!url.AbsolutePath.EndsWith(".swf", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(mimeType, "application/x-shockwave-flash", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(mimeType, "application/vnd.adobe.flash.movie", StringComparison.OrdinalIgnoreCase))
                return false;

            return !Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished;
        }

        protected override void OnIsBrowserInitializedChanged(EventArgs e)
        {
            base.OnIsBrowserInitializedChanged(e);

            if (!IsBrowserInitialized)
            {
                return;
            }

            Cef.UIThreadTaskFactory.StartNew(() =>
            { // enable flash contents automatically
                var browser = GetBrowser();
                if (browser == null || browser.IsDisposed)
                {
                    return;
                }

                var host = browser.GetHost();
                if (host == null)
                {
                    return;
                }

                var ok = host.RequestContext.SetPreference(
                    "profile.default_content_setting_values.plugins", 1, out var error);
                if (!ok && !string.IsNullOrEmpty(error))
                {
                    Debug.WriteLine($"[ChromiumFlashBrowser] Failed to enable plugins preference: {error}");
                }
            });
        }
    }
}
