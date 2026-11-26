using System;
using System.Diagnostics;
using System.IO;
using CefSharp;
using CefSharp.RenderProcess;

namespace CefFlashBrowser.BrowserSubprocess
{
    internal sealed class PolyfillRenderProcessHandler : IRenderProcessHandler
    {
        private const string ResourceName = "CefFlashBrowser.BrowserSubprocess.core-js.min.js";
        private const string ScriptUrl = "cef-flash-browser://polyfills/core-js.js";
        private static readonly Lazy<string> Script = new Lazy<string>(ReadScript);

        public void OnWebKitInitialized()
        {
        }

        public void OnContextCreated(IBrowser browser, IFrame frame, IV8Context context)
        {
            if (!ShouldInstall(frame))
                return;

            var script = Script.Value;
            if (script == null)
                return;

            // CefSharp's renderer-side frame executes synchronously in the main world.
            // The callback can also fire for isolated worlds, so the script guards against reinstallation.
            frame.ExecuteJavaScriptAsync(script, ScriptUrl);
        }

        public void OnContextReleased(IBrowser browser, IFrame frame, IV8Context context)
        {
        }

        private static bool ShouldInstall(IFrame frame)
        {
            var url = frame.Url;
            var schemeEnd = url?.IndexOf(':') ?? -1;
            if (schemeEnd <= 0)
                return false;

            switch (url.Substring(0, schemeEnd))
            {
                case "http":
                case "https":
                case "file":
                case "data":
                case "blob":
                    return true;
                case "about":
                    return !frame.IsMain && Uri.TryCreate(url, UriKind.Absolute, out var about)
                        && (about.AbsolutePath == "blank" || about.AbsolutePath == "srcdoc");
                default:
                    return false;
            }
        }

        private static string ReadScript()
        {
            try
            {
                using (var stream = typeof(PolyfillRenderProcessHandler).Assembly.GetManifestResourceStream(ResourceName))
                {
                    if (stream == null)
                    {
                        Trace.TraceWarning("The embedded core-js bundle was not found.");
                        return null;
                    }

                    using (var reader = new StreamReader(stream))
                        return reader.ReadToEnd();
                }
            }
            catch (IOException e)
            {
                Trace.TraceWarning("Could not read the embedded core-js bundle: {0}", e.Message);
                return null;
            }
        }
    }
}
