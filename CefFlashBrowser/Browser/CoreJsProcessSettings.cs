using System;

namespace CefFlashBrowser.Browser
{
    internal static class CoreJsProcessSettings
    {
        private const string VariableName = "CEF_FLASH_BROWSER_ENABLE_CORE_JS_POLYFILLS";

        // Renderer processes inherit this value from the browser process when CEF launches them.
        internal static bool Enabled
        {
            get => Environment.GetEnvironmentVariable(VariableName) != "0";
            set => Environment.SetEnvironmentVariable(VariableName, value ? "1" : "0");
        }
    }
}
