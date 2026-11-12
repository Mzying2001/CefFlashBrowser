using System;
using System.Diagnostics;

namespace CefFlashBrowser.Utils
{
    // Supply logging for linked helpers without initializing the application's global services.
    internal static class LogHelper
    {
        public static void LogInfo(string message)
        {
            Trace.TraceInformation(message);
        }

        public static void LogError(string message, Exception exception = null)
        {
            Trace.TraceError("{0}{1}{2}", message, Environment.NewLine, exception);
        }
    }
}
