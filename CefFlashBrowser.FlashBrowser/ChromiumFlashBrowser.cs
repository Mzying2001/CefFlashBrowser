using CefSharp;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;

namespace CefFlashBrowser.FlashBrowser
{
    public class ChromiumFlashBrowser : ChromiumWebBrowserEx
    {
        public static readonly DependencyProperty BlockedSwfsProperty;
        public static readonly DependencyProperty HasBlockedSwfsProperty;

        private const string FlashAccelerationScript = @"
(function() {
    var nodes = Array.prototype.slice.call(document.querySelectorAll('embed, object'));
    nodes.forEach(function(node) {
        try {
            node.style.transform = 'translateZ(0)';
            node.style.backfaceVisibility = 'hidden';
            node.style.willChange = 'transform';
            node.style.webkitTransform = 'translateZ(0)';

            var tag = (node.tagName || '').toLowerCase();
            if (tag === 'embed') {
                node.setAttribute('wmode', 'direct');
            } else if (tag === 'object') {
                var hasWmode = false;
                Array.prototype.slice.call(node.querySelectorAll('param')).forEach(function(param) {
                    if ((param.getAttribute('name') || '').toLowerCase() === 'wmode') {
                        param.setAttribute('value', 'direct');
                        hasWmode = true;
                    }
                });
                if (!hasWmode) {
                    var wmode = document.createElement('param');
                    wmode.setAttribute('name', 'wmode');
                    wmode.setAttribute('value', 'direct');
                    node.appendChild(wmode);
                }
            }
        } catch (e) {}
    });
})();";

        static ChromiumFlashBrowser()
        {
            BlockedSwfsProperty = DependencyProperty.Register(
                nameof(BlockedSwfs), typeof(ObservableCollection<string>), typeof(ChromiumFlashBrowser), new PropertyMetadata(null));

            HasBlockedSwfsProperty = DependencyProperty.Register(
                nameof(HasBlockedSwfs), typeof(bool), typeof(ChromiumFlashBrowser), new PropertyMetadata(false));
        }

        public ChromiumFlashBrowser()
        {
            SetValue(BlockedSwfsProperty, new ObservableCollection<string>());
        }


        public ObservableCollection<string> BlockedSwfs
        {
            get => (ObservableCollection<string>)GetValue(BlockedSwfsProperty);
        }

        public bool HasBlockedSwfs
        {
            get => (bool)GetValue(HasBlockedSwfsProperty);
        }

        public void EnableFlashAcceleration()
        {
            if (CanExecuteJavascriptInMainFrame)
            {
                ExecuteScriptAsync(FlashAccelerationScript);
            }
        }


        protected override void OnFrameLoadStart(FrameLoadStartEventArgs e)
        {
            base.OnFrameLoadStart(e);

            if (e.Frame.IsMain)
            {
                BlockedSwfs.Clear();
                SetCurrentValue(HasBlockedSwfsProperty, false);
            }
        }

        protected override void OnFrameLoadEnd(FrameLoadEndEventArgs e)
        {
            base.OnFrameLoadEnd(e);

            if (e.Frame.IsMain)
            {
                e.Frame.ExecuteJavaScriptAsync(FlashAccelerationScript);
            }
        }

        protected override void OnConsoleMessage(ConsoleMessageEventArgs e)
        {
            base.OnConsoleMessage(e);

            if (e.Level != LogSeverity.Info)
            {
                return;
            }

            var msg = e.Message;
            if (msg == null || !msg.StartsWith("Cross-origin plugin content from", StringComparison.Ordinal))
            {
                return;
            }

            var parts = msg.Split(' ');
            if (parts.Length <= 4)
            {
                return;
            }

            var url = parts[4];
            if (string.IsNullOrWhiteSpace(url) || BlockedSwfs.Contains(url))
            {
                return;
            }

            BlockedSwfs.Add(url);
            SetCurrentValue(HasBlockedSwfsProperty, true);
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
