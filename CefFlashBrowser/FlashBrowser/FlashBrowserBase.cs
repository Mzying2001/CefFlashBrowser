using CefFlashBrowser.Utils;
using CefFlashBrowser.Views.Dialogs.JsDialogs;
using CefFlashBrowser.WinformCefSharp4WPF;
using CefSharp;
using System;

namespace CefFlashBrowser.FlashBrowser
{
    public abstract class FlashBrowserBase : ChromiumWebBrowser
    {
        public FlashBrowserBase()
        {
            IsBrowserInitializedChanged += FlashBrowserIsBrowserInitializedChanged;
        }

        private void FlashBrowserIsBrowserInitializedChanged(object sender, EventArgs e)
        {
            if (IsBrowserInitialized)
            {
                Cef.UIThreadTaskFactory.StartNew(() =>
                {
                    var requestContext = GetBrowser().GetHost().RequestContext;
                    var flag = requestContext.SetPreference("profile.default_content_setting_values.plugins", 1, out string err);

                    if (!flag)
                    {
                        var title = LanguageManager.GetString("title_error");
                        JsAlertDialog.ShowDialog(err, title);
                    }
                });
            }
        }

    }
}
