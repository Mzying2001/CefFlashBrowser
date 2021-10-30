using CefFlashBrowser.Views.Dialogs.JsDialogs;
using CefSharp;
using CefSharp.Wpf;

namespace CefFlashBrowser.Models.FlashBrowser
{
    public class JsDialogHandler : IJsDialogHandler
    {
        public bool OnBeforeUnloadDialog(IWebBrowser chromiumWebBrowser, IBrowser browser, string messageText, bool isReload, IJsDialogCallback callback)
        {
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
                        targetBrowser.Dispatcher.Invoke(() =>
                        {
                            JsAlertDialog.Show(messageText, originUrl);
                        });
                        suppressMessage = true;
                        return false;
                    }

                case CefJsDialogType.Confirm:
                    {
                        targetBrowser.Dispatcher.Invoke(() =>
                        {
                            JsConfirmDialog.Show(messageText, originUrl, result =>
                            {
                                callback.Continue(result == true);
                            });
                        });
                        suppressMessage = false;
                        return true;
                    }

                case CefJsDialogType.Prompt:
                    {
                        targetBrowser.Dispatcher.Invoke(() =>
                        {
                            JsPromptDialog.Show(messageText, originUrl, defaultPromptText, result =>
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
