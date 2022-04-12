using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
using CefFlashBrowser.Views.Dialogs.JsDialogs;
using CefSharp;
using CefSharp.WinForms;

namespace CefFlashBrowser.FlashBrowser.Handlers
{
    public class JsDialogHandler : IJsDialogHandler
    {
        private readonly WfChromiumWebBrowser wfChromiumWebBrowser;

        public JsDialogHandler(WfChromiumWebBrowser wfChromiumWebBrowser)
        {
            this.wfChromiumWebBrowser = wfChromiumWebBrowser;
        }

        public bool OnBeforeUnloadDialog(IWebBrowser chromiumWebBrowser, IBrowser browser, string messageText, bool isReload, IJsDialogCallback callback)
        {
            if (GlobalData.Settings.DisableOnBeforeUnloadDialog)
            {
                callback.Continue(true);
                return true;
            }

            wfChromiumWebBrowser.Dispatcher.Invoke(() =>
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
            var targetBrowser = (ChromiumWebBrowser)chromiumWebBrowser;

            switch (dialogType)
            {
                case CefJsDialogType.Alert:
                    {
                        wfChromiumWebBrowser.Dispatcher.Invoke(() =>
                        {
                            JsAlertDialog.ShowDialog(messageText, wfChromiumWebBrowser.Title);
                        });
                        suppressMessage = true;
                        return false;
                    }

                case CefJsDialogType.Confirm:
                    {
                        wfChromiumWebBrowser.Dispatcher.Invoke(() =>
                        {
                            JsConfirmDialog.ShowDialog(messageText, wfChromiumWebBrowser.Title, result =>
                            {
                                callback.Continue(result == true);
                            });
                        });
                        suppressMessage = false;
                        return true;
                    }

                case CefJsDialogType.Prompt:
                    {
                        wfChromiumWebBrowser.Dispatcher.Invoke(() =>
                        {
                            JsPromptDialog.ShowDialog(messageText, wfChromiumWebBrowser.Title, defaultPromptText, result =>
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
