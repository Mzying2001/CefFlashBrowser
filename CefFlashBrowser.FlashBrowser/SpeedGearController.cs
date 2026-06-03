using System;
using System.Diagnostics;

namespace CefFlashBrowser.FlashBrowser
{
    /// <summary>
    /// Coordinates the browser-side speed gear state.
    ///
    /// Real Flash speed gear cannot be implemented by patching page JavaScript timers: SWF logic runs
    /// inside the Flash/CEF runtime and reads native process time sources. The native backend should
    /// be limited to app-controlled CEF/Flash subprocesses and scale process-local timing APIs such as
    /// QueryPerformanceCounter, GetTickCount/GetTickCount64, and timeGetTime.
    /// </summary>
    public static class SpeedGearController
    {
        public const double DefaultFactor = 1.0;
        public const double MinFactor = 0.5;
        public const double MaxFactor = 100.0;

        private static readonly object SyncRoot = new object();
        private static double _factor = DefaultFactor;

        public static double CurrentFactor
        {
            get
            {
                lock (SyncRoot)
                {
                    return _factor;
                }
            }
        }

        public static double SetFactor(double factor)
        {
            factor = NormalizeFactor(factor);

            lock (SyncRoot)
            {
                _factor = factor;
            }

            // Intentionally do not inject page JavaScript here. That path was removed because it does
            // not reliably affect Pepper Flash. The next implementation step is a native, subprocess-
            // scoped timing backend that applies this factor inside the controlled Flash/CEF process.
            Debug.WriteLine($"[SpeedGear] Requested factor: {factor:0.###}x. Native backend is not wired yet.");
            return factor;
        }

        public static double NormalizeFactor(double factor)
        {
            if (double.IsNaN(factor) || double.IsInfinity(factor))
            {
                return DefaultFactor;
            }

            if (factor < MinFactor)
            {
                return MinFactor;
            }

            if (factor > MaxFactor)
            {
                return MaxFactor;
            }

            return factor;
        }
    }
}
