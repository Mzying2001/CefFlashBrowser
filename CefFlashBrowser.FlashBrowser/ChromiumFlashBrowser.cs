using CefSharp;
using System;
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
        private long _swfPageVersion;

        public ReadOnlyObservableCollection<SwfResourceRecord> PageSwfs { get; }
        public long SwfPageVersion => Interlocked.Read(ref _swfPageVersion);

        public ChromiumFlashBrowser() : this(false)
        {
        }

        public ChromiumFlashBrowser(bool deferNativeCreation) : base(deferNativeCreation)
        {
            PageSwfs = new ReadOnlyObservableCollection<SwfResourceRecord>(_pageSwfs);
        }

        protected override void OnFrameLoadStart(FrameLoadStartEventArgs e)
        {
            if (!IsDisposed && e.Frame.IsMain)
            {
                Interlocked.Increment(ref _swfPageVersion);
                _pageSwfUrls.Clear();
                _pageSwfs.Clear();
            }

            base.OnFrameLoadStart(e);
        }

        public void ObserveSwfRequest(long pageVersion, string address, string mimeType = null)
        {
            if (pageVersion != SwfPageVersion || IsDisposed || !Uri.TryCreate(address, UriKind.Absolute, out var url))
                return;

            if (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps && !url.IsFile)
                return;

            if (!url.AbsolutePath.EndsWith(".swf", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(mimeType, "application/x-shockwave-flash", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(mimeType, "application/vnd.adobe.flash.movie", StringComparison.OrdinalIgnoreCase))
                return;

            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
                return;

            Dispatcher.InvokeAsync(() =>
            {
                if (!IsDisposed && pageVersion == SwfPageVersion && _pageSwfUrls.Add(address))
                    _pageSwfs.Add(new SwfResourceRecord(address, url));
            });
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
