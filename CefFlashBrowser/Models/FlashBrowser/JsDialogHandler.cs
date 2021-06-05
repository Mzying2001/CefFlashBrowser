using CefFlashBrowser.Views.Dialogs.JsDialogs;
using CefSharp;
using CefSharp.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CefFlashBrowser.Models.FlashBrowser
{
    class JsDialogHandler : IJsDialogHandler
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
                            var dr = JsConfirmDialog.Show(messageText, originUrl);
                            callback.Continue(dr);
                        });
                        suppressMessage = false;
                        return true;
                    }

                case CefJsDialogType.Prompt:
                    {
                        targetBrowser.Dispatcher.Invoke(() =>
                        {
                            System.Windows.MessageBox.Show("Comming...");
                            callback.Continue(true, defaultPromptText);
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
