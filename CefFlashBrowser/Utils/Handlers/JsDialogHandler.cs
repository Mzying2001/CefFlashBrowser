using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Views.Dialogs.JsDialogs;
using CefFlashBrowser.WinformCefSharp4WPF;
using CefSharp;
using System.Windows;

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

            Application.Current.Dispatcher.Invoke(() =>
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

            switch (dialogType)
            {
                case CefJsDialogType.Alert:
                    {
                        wpfWebBrowser.Dispatcher.Invoke(() =>
                        {
                            JsAlertDialog.ShowDialog(messageText, wpfWebBrowser.Title);
                        });
                        suppressMessage = true;
                        return false;
                    }

                case CefJsDialogType.Confirm:
                    {
                        wpfWebBrowser.Dispatcher.Invoke(() =>
                        {
                            JsConfirmDialog.ShowDialog(messageText, wpfWebBrowser.Title, result =>
                            {
                                callback.Continue(result == true);
                            });
                        });
                        suppressMessage = false;
                        return true;
                    }

                case CefJsDialogType.Prompt:
                    {
                        wpfWebBrowser.Dispatcher.Invoke(() =>
                        {
                            JsPromptDialog.ShowDialog(messageText, wpfWebBrowser.Title, defaultPromptText, result =>
                            {
                                callback.Continue(result != null, result);
                            });
                        });
                        suppressMessage = false;
                        return true;
                    }
            }
            return false;
        }
    }
}
