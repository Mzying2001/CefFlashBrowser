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

        private const string InputMemoryBootstrapScript = @"
(function() {
    if (window.__cefInputMemory) return;
    var memory = {
        recording: false,
        events: [],
        startTime: 0,
        startedAt: null,
        start: function() {
            this.events = [];
            this.startTime = Date.now();
            this.startedAt = document.activeElement;
            this.recording = true;
        },
        stop: function() {
            this.recording = false;
        },
        clear: function() {
            this.events = [];
            this.recording = false;
        },
        state: function() {
            return { recording: this.recording, count: this.events.length };
        },
        record: function(e) {
            if (!this.recording || !e || !e.type) return;
            this.events.push({
                type: e.type,
                time: Date.now() - this.startTime,
                x: typeof e.clientX === 'number' ? e.clientX : null,
                y: typeof e.clientY === 'number' ? e.clientY : null,
                button: typeof e.button === 'number' ? e.button : 0,
                buttons: typeof e.buttons === 'number' ? e.buttons : 0,
                deltaX: typeof e.deltaX === 'number' ? e.deltaX : 0,
                deltaY: typeof e.deltaY === 'number' ? e.deltaY : 0,
                key: e.key || '',
                code: e.code || '',
                keyCode: e.keyCode || 0,
                ctrlKey: !!e.ctrlKey,
                shiftKey: !!e.shiftKey,
                altKey: !!e.altKey,
                metaKey: !!e.metaKey
            });
        },
        targetFor: function(item) {
            if (item.x !== null && item.y !== null) {
                return document.elementFromPoint(item.x, item.y) || document.body || document.documentElement;
            }
            return this.startedAt || document.activeElement || document.body || document.documentElement;
        },
        dispatch: function(item) {
            var target = this.targetFor(item);
            var init = {
                bubbles: true,
                cancelable: true,
                composed: true,
                clientX: item.x || 0,
                clientY: item.y || 0,
                button: item.button || 0,
                buttons: item.buttons || 0,
                deltaX: item.deltaX || 0,
                deltaY: item.deltaY || 0,
                key: item.key || '',
                code: item.code || '',
                keyCode: item.keyCode || 0,
                which: item.keyCode || 0,
                ctrlKey: !!item.ctrlKey,
                shiftKey: !!item.shiftKey,
                altKey: !!item.altKey,
                metaKey: !!item.metaKey
            };
            var evt;
            if (item.type.indexOf('key') === 0) {
                evt = new KeyboardEvent(item.type, init);
            } else if (item.type === 'wheel') {
                evt = new WheelEvent(item.type, init);
            } else {
                evt = new MouseEvent(item.type, init);
            }
            target.dispatchEvent(evt);
        },
        play: function() {
            var list = this.events.slice(0);
            if (!list.length) return;
            this.recording = false;
            list.forEach(function(item) {
                window.setTimeout(function() { memory.dispatch(item); }, Math.max(0, item.time || 0));
            });
        }
    };

    ['mousedown','mouseup','mousemove','click','dblclick','wheel','keydown','keyup'].forEach(function(type) {
        document.addEventListener(type, function(e) { memory.record(e); }, true);
    });
    window.__cefInputMemory = memory;
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

        public bool IsInputMemoryRecording { get; private set; }

        public void EnableFlashAcceleration()
        {
            if (CanExecuteJavascriptInMainFrame)
            {
                ExecuteScriptAsync(FlashAccelerationScript);
            }
        }

        public void StartInputMemoryRecording()
        {
            IsInputMemoryRecording = true;
            if (CanExecuteJavascriptInMainFrame)
            {
                ExecuteScriptAsync(InputMemoryBootstrapScript + "\nwindow.__cefInputMemory.start();");
            }
        }

        public void StopInputMemoryRecording()
        {
            IsInputMemoryRecording = false;
            if (CanExecuteJavascriptInMainFrame)
            {
                ExecuteScriptAsync(InputMemoryBootstrapScript + "\nwindow.__cefInputMemory.stop();");
            }
        }

        public void ReplayInputMemory()
        {
            IsInputMemoryRecording = false;
            if (CanExecuteJavascriptInMainFrame)
            {
                ExecuteScriptAsync(InputMemoryBootstrapScript + "\nwindow.__cefInputMemory.play();");
            }
        }

        public void ClearInputMemory()
        {
            IsInputMemoryRecording = false;
            if (CanExecuteJavascriptInMainFrame)
            {
                ExecuteScriptAsync(InputMemoryBootstrapScript + "\nwindow.__cefInputMemory.clear();");
            }
        }


        protected override void OnFrameLoadStart(FrameLoadStartEventArgs e)
        {
            base.OnFrameLoadStart(e);

            if (e.Frame.IsMain)
            {
                IsInputMemoryRecording = false;
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
                e.Frame.ExecuteJavaScriptAsync(InputMemoryBootstrapScript);
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
