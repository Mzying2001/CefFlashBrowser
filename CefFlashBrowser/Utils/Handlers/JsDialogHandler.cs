using CefFlashBrowser.Models.Data;
using CefFlashBrowser.WinformCefSharp4WPF;
using CefSharp;

namespace CefFlashBrowser.Utils.Handlers
{
    public class JsDialogHandler : FlashBrowser.Handlers.JsDialogHandler
    {
        public override bool OnBeforeUnloadDialog(IWebBrowser chromiumWebBrowser, IBrowser browser, string messageText, bool isReload, IJsDialogCallback callback)
        {
            if (GlobalData.Settings.DisableOnBeforeUnloadDialog)
            {
                callback.Continue(true);
                return true;
            }

            var wpfWebBrowser = (IWpfWebBrowser)chromiumWebBrowser;
            wpfWebBrowser.Dispatcher.Invoke(delegate
            {
                var msg = LanguageManager.GetString(isReload ? "message_askWhetherToReload" : "message_askWhetherToLeave");
                WindowManager.Confirm(msg, wpfWebBrowser.Title, result =>
                {
                    callback.Continue(result == true);
                });
            });
            return true;
        }

        public override bool OnJSDialog(IWebBrowser chromiumWebBrowser, IBrowser browser, string originUrl, CefJsDialogType dialogType, string messageText, string defaultPromptText, IJsDialogCallback callback, ref bool suppressMessage)
        {
            var wpfWebBrowser = (IWpfWebBrowser)chromiumWebBrowser;
            wpfWebBrowser.Dispatcher.Invoke(delegate
            {
                switch (dialogType)
                {
                    case CefJsDialogType.Alert:
                        {
                            WindowManager.Alert(messageText, wpfWebBrowser.Title);
                            callback.Continue(true);
                            break;
                        }
                    case CefJsDialogType.Confirm:
                        {
                            WindowManager.Confirm(messageText, wpfWebBrowser.Title, result =>
                            {
                                callback.Continue(result == true);
                            });
                            break;
                        }
                    case CefJsDialogType.Prompt:
                        {
                            WindowManager.Prompt(messageText, wpfWebBrowser.Title, defaultPromptText, (success, input) =>
                            {
                                callback.Continue(success == true, input);
                            });
                            break;
                        }
                }
            });
            return true;
        }
    }
}
