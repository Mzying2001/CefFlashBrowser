using CefSharp;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;

namespace CefFlashBrowser.FlashBrowser
{
    public class ChromiumFlashBrowser : ChromiumWebBrowserEx
    {
        public static readonly DependencyProperty BlockedSwfsProperty = DependencyProperty.Register(
            nameof(BlockedSwfs), typeof(ObservableCollection<string>), typeof(ChromiumFlashBrowser), new PropertyMetadata(null));

        public static readonly DependencyProperty IsMutedProperty = DependencyProperty.Register(
            nameof(IsMuted), typeof(bool), typeof(ChromiumFlashBrowser), new PropertyMetadata(false));

        public ObservableCollection<string> BlockedSwfs
        {
            get => (ObservableCollection<string>)GetValue(BlockedSwfsProperty);
        }

        public bool IsMuted
        {
            get { return (bool)GetValue(IsMutedProperty); }
        }

        public ChromiumFlashBrowser()
        {
            SetValue(BlockedSwfsProperty, new ObservableCollection<string>());
        }

        public void SetAudioMuted(bool mute)
        {
            Cef.UIThreadTaskFactory.StartNew(() =>
            { // mute or unmute the browser audio on the CEF UI thread
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

                host.SetAudioMuted(mute);
                var isMuted = host.IsAudioMuted;
                Dispatcher.BeginInvoke(new Action(() => SetCurrentValue(IsMutedProperty, isMuted)));
            });
        }

        public void ToggleMute()
        {
            SetAudioMuted(!IsMuted);
        }

        protected override void OnFrameLoadStart(FrameLoadStartEventArgs e)
        {
            base.OnFrameLoadStart(e);

            if (e.Frame.IsMain)
            {
                BlockedSwfs.Clear();
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
