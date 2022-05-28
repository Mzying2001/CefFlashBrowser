using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Views.Dialogs.JsDialogs;
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

            ((IWpfWebBrowser)chromiumWebBrowser).Dispatcher.Invoke(delegate
            {
                var title = LanguageManager.GetString(isReload ? "title_askWhetherToReload" : "title_askWhetherToLeave");
                JsConfirmDialog.ShowDialog(messageText, title, result =>
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
                            JsAlertDialog.ShowDialog(messageText, wpfWebBrowser.Title);
                            callback.Continue(true);
                            break;
                        }
                    case CefJsDialogType.Confirm:
                        {
                            JsConfirmDialog.ShowDialog(messageText, wpfWebBrowser.Title, result =>
                            {
                                callback.Continue(result == true);
                            });
                            break;
                        }
                    case CefJsDialogType.Prompt:
                        {
                            JsPromptDialog.ShowDialog(messageText, wpfWebBrowser.Title, defaultPromptText, result =>
                            {
                                callback.Continue(result != null, result);
                            });
                            break;
                        }
                }
            });
            return true;
        }
    }
}
