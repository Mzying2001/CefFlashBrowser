using CefSharp;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;

namespace CefFlashBrowser.FlashBrowser
{
    public class ChromiumFlashBrowser : ChromiumWebBrowserEx
    {
        public static readonly DependencyProperty BlockedSwfsProperty;
        public static readonly DependencyProperty HasBlockedSwfsProperty;

        private const string SpeedGearBootstrapScript = @"
(function() {
    if (window.__cefSpeedGear) return;

    var NativeDate = Date;
    var nativeDateNow = Date.now.bind(Date);
    var nativeSetTimeout = window.setTimeout.bind(window);
    var nativeSetInterval = window.setInterval.bind(window);
    var nativeClearTimeout = window.clearTimeout.bind(window);
    var nativeClearInterval = window.clearInterval.bind(window);
    var nativeRequestAnimationFrame = window.requestAnimationFrame ? window.requestAnimationFrame.bind(window) : null;
    var nativeCancelAnimationFrame = window.cancelAnimationFrame ? window.cancelAnimationFrame.bind(window) : null;
    var nativePerformanceNow = window.performance && window.performance.now
        ? window.performance.now.bind(window.performance)
        : null;

    var maxDelay = 2147483647;
    var speed = 1.0;
    var realAnchor = nativeDateNow();
    var virtualAnchor = realAnchor;
    var perfRealAnchor = nativePerformanceNow ? nativePerformanceNow() : 0;
    var perfVirtualAnchor = perfRealAnchor;

    function clampSpeed(value) {
        value = Number(value);
        if (!isFinite(value) || value < 0) return 1.0;
        if (value > 16) return 16;
        return value;
    }

    function virtualDateNow() {
        if (speed === 0) {
            return virtualAnchor;
        }
        return virtualAnchor + (nativeDateNow() - realAnchor) * speed;
    }

    function virtualPerformanceNow() {
        if (!nativePerformanceNow) {
            return virtualDateNow();
        }
        if (speed === 0) {
            return perfVirtualAnchor;
        }
        return perfVirtualAnchor + (nativePerformanceNow() - perfRealAnchor) * speed;
    }

    function rescaleDelay(delay) {
        delay = Number(delay) || 0;
        if (delay <= 0) return 0;
        if (speed === 0) return maxDelay;
        return Math.max(0, delay / speed);
    }

    function CefSpeedGearDate() {
        if (!(this instanceof CefSpeedGearDate)) {
            return new NativeDate(virtualDateNow()).toString();
        }

        if (arguments.length === 0) {
            return new NativeDate(virtualDateNow());
        }
        if (arguments.length === 1) {
            return new NativeDate(arguments[0]);
        }
        if (arguments.length === 2) {
            return new NativeDate(arguments[0], arguments[1]);
        }
        if (arguments.length === 3) {
            return new NativeDate(arguments[0], arguments[1], arguments[2]);
        }
        if (arguments.length === 4) {
            return new NativeDate(arguments[0], arguments[1], arguments[2], arguments[3]);
        }
        if (arguments.length === 5) {
            return new NativeDate(arguments[0], arguments[1], arguments[2], arguments[3], arguments[4]);
        }
        if (arguments.length === 6) {
            return new NativeDate(arguments[0], arguments[1], arguments[2], arguments[3], arguments[4], arguments[5]);
        }
        return new NativeDate(arguments[0], arguments[1], arguments[2], arguments[3], arguments[4], arguments[5], arguments[6]);
    }

    CefSpeedGearDate.prototype = NativeDate.prototype;
    CefSpeedGearDate.parse = NativeDate.parse;
    CefSpeedGearDate.UTC = NativeDate.UTC;
    CefSpeedGearDate.now = virtualDateNow;

    try {
        Object.defineProperty(window, 'Date', {
            configurable: true,
            writable: true,
            value: CefSpeedGearDate
        });
    } catch (e) {
        window.Date = CefSpeedGearDate;
    }

    if (window.performance && nativePerformanceNow) {
        try {
            Object.defineProperty(window.performance, 'now', {
                configurable: true,
                value: virtualPerformanceNow
            });
        } catch (e) {}
    }

    window.setTimeout = function(callback, delay) {
        var args = Array.prototype.slice.call(arguments, 2);
        return nativeSetTimeout(function() {
            if (typeof callback === 'function') {
                callback.apply(window, args);
            } else {
                (0, eval)(callback);
            }
        }, rescaleDelay(delay));
    };

    window.setInterval = function(callback, delay) {
        var args = Array.prototype.slice.call(arguments, 2);
        return nativeSetInterval(function() {
            if (typeof callback === 'function') {
                callback.apply(window, args);
            } else {
                (0, eval)(callback);
            }
        }, rescaleDelay(delay));
    };

    window.clearTimeout = nativeClearTimeout;
    window.clearInterval = nativeClearInterval;

    window.requestAnimationFrame = function(callback) {
        if (speed === 1 && nativeRequestAnimationFrame) {
            return nativeRequestAnimationFrame(callback);
        }

        return nativeSetTimeout(function() {
            callback(virtualPerformanceNow());
        }, speed === 0 ? maxDelay : Math.max(1, 16 / speed));
    };

    window.cancelAnimationFrame = function(handle) {
        if (nativeCancelAnimationFrame) {
            nativeCancelAnimationFrame(handle);
        }
        nativeClearTimeout(handle);
    };

    window.__cefSpeedGear = {
        setSpeed: function(value) {
            virtualAnchor = virtualDateNow();
            realAnchor = nativeDateNow();

            if (nativePerformanceNow) {
                perfVirtualAnchor = virtualPerformanceNow();
                perfRealAnchor = nativePerformanceNow();
            }

            speed = clampSpeed(value);
            return speed;
        },
        getSpeed: function() {
            return speed;
        },
        reset: function() {
            return this.setSpeed(1.0);
        }
    };
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

        public double SpeedGearFactor { get; private set; } = 1.0;

        public void SetSpeedGearFactor(double factor)
        {
            if (double.IsNaN(factor) || double.IsInfinity(factor) || factor < 0)
            {
                factor = 1.0;
            }

            if (factor > 16.0)
            {
                factor = 16.0;
            }

            SpeedGearFactor = factor;

            if (CanExecuteJavascriptInMainFrame)
            {
                ExecuteScriptAsync(GetSetSpeedGearScript(factor));
            }
        }

        public void ResetSpeedGear()
        {
            SetSpeedGearFactor(1.0);
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
                e.Frame.ExecuteJavaScriptAsync(GetSetSpeedGearScript(SpeedGearFactor));
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

        private static string GetSetSpeedGearScript(double factor)
        {
            var value = factor.ToString("0.###", CultureInfo.InvariantCulture);
            return SpeedGearBootstrapScript + "\nwindow.__cefSpeedGear.setSpeed(" + value + ");";
        }
    }
}
