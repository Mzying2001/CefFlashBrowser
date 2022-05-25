using CefSharp;
using System;

namespace CefFlashBrowser.FlashBrowser
{
    public abstract class ChromiumFlashBrowserBase : ChromiumWebBrowserEx
    {
        protected override void OnIsBrowserInitializedChanged(EventArgs e)
        {
            base.OnIsBrowserInitializedChanged(e);

            if (IsBrowserInitialized)
            {
                Cef.UIThreadTaskFactory.StartNew(() =>
                {
                    var requestContext = GetBrowser().GetHost().RequestContext;
                    requestContext.SetPreference("profile.default_content_setting_values.plugins", 1, out _);
                });
            }
        }
    }
}
