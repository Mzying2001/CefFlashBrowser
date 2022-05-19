using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
using CefFlashBrowser.Views.Dialogs.JsDialogs;
using CefFlashBrowser.WinformCefSharp4WPF;
using CefSharp;

namespace CefFlashBrowser.FlashBrowser.Handlers
{
    public class JsDialogHandler : IJsDialogHandler
    {
        public bool OnBeforeUnloadDialog(IWebBrowser chromiumWebBrowser, IBrowser browser, string messageText, bool isReload, IJsDialogCallback callback)
        {
            if (GlobalData.Settings.DisableOnBeforeUnloadDialog)
            {
                callback.Continue(true);
                return true;
            }

            ChromiumWebBrowser webBrowser = (ChromiumWebBrowser)chromiumWebBrowser;

            webBrowser.Dispatcher.Invoke(() =>
            {
                var title = LanguageManager.GetString(isReload ? "title_askWhetherToReload" : "title_askWhetherToLeave");
                JsConfirmDialog.ShowDialog(messageText, title, result =>
                {
                    callback.Continue(result == true);
                });
            });
            return true;
        }

        public void OnDialogClosed(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
            //throw new NotImplementedException();
        }

        public bool OnJSDialog(IWebBrowser chromiumWebBrowser, IBrowser browser, string originUrl, CefJsDialogType dialogType, string messageText, string defaultPromptText, IJsDialogCallback callback, ref bool suppressMessage)
        {
            ChromiumWebBrowser webBrowser = (ChromiumWebBrowser)chromiumWebBrowser;

            switch (dialogType)
            {
                case CefJsDialogType.Alert:
                    {
                        webBrowser.Dispatcher.Invoke(() =>
                        {
                            JsAlertDialog.ShowDialog(messageText, webBrowser.Title);
                        });
                        suppressMessage = true;
                        return false;
                    }

                case CefJsDialogType.Confirm:
                    {
                        webBrowser.Dispatcher.Invoke(() =>
                        {
                            JsConfirmDialog.ShowDialog(messageText, webBrowser.Title, result =>
                            {
                                callback.Continue(result == true);
                            });
                        });
                        suppressMessage = false;
                        return true;
                    }

                case CefJsDialogType.Prompt:
                    {
                        webBrowser.Dispatcher.Invoke(() =>
                        {
                            JsPromptDialog.ShowDialog(messageText, webBrowser.Title, defaultPromptText, result =>
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

        public void OnResetDialogState(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
            //throw new NotImplementedException();
        }
    }
}
