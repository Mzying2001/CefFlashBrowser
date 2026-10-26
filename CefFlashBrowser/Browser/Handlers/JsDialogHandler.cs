using CefFlashBrowser.Data;
using CefFlashBrowser.Services;
using CefFlashBrowser.Utils;
using CefFlashBrowser.Views.Dialogs;
using CefFlashBrowser.WinformCefSharp4WPF;
using CefSharp;
using SimpleMvvm.Messaging;
using System.Threading.Tasks;

namespace CefFlashBrowser.Browser.Handlers
{
    public class JsDialogHandler : FlashBrowser.Handlers.JsDialogHandler
    {
        private readonly object _beforeUnloadLock = new object();
        private IJsDialogCallback _beforeUnloadCallback;
        private IWebBrowser _beforeUnloadBrowser;
        private bool _beforeUnloadIsReload;
        private JsConfirmDialog _beforeUnloadDialog;
        private TaskCompletionSource<bool> _reloadCompletion;

        public override bool OnBeforeUnloadDialog(IWebBrowser chromiumWebBrowser, IBrowser browser, string messageText, bool isReload, IJsDialogCallback callback)
        {
            RegisterBeforeUnloadDialog(chromiumWebBrowser, callback, isReload);
            if (GlobalData.Settings.DisableOnBeforeUnloadDialog)
            {
                CompleteBeforeUnloadDialog(chromiumWebBrowser, callback, isReload, true);
                return true;
            }

            var wpfWebBrowser = (IWpfWebBrowser)chromiumWebBrowser;
            wpfWebBrowser.Dispatcher.InvokeAsync(delegate
            {
                lock (_beforeUnloadLock)
                {
                    if (!ReferenceEquals(_beforeUnloadCallback, callback))
                        return;
                }

                var msg = LanguageManager.GetString(isReload ? "message_askWhetherToReload" : "message_askWhetherToLeave");
                var dialog = WindowManager.ShowWindow<JsConfirmDialog>(modal: true, initializer: window =>
                {
                    window.Title = wpfWebBrowser.Title;
                    window.Message = msg;
                    _beforeUnloadDialog = window;
                });
                if (ReferenceEquals(_beforeUnloadDialog, dialog))
                    _beforeUnloadDialog = null;
                CompleteBeforeUnloadDialog(chromiumWebBrowser, callback, isReload, DialogHelper.GetDialogResult(dialog) == true);
            });
            return true;
        }

        protected void RegisterBeforeUnloadDialog(IWebBrowser browser, IJsDialogCallback callback, bool isReload)
        {
            lock (_beforeUnloadLock)
            {
                _beforeUnloadBrowser = browser;
                _beforeUnloadCallback = callback;
                _beforeUnloadIsReload = isReload;
                _reloadCompletion = isReload ? new TaskCompletionSource<bool>() : null;
            }
        }

        internal Task WaitForPendingReloadAsync()
        {
            lock (_beforeUnloadLock)
            {
                return _reloadCompletion?.Task ?? Task.CompletedTask;
            }
        }

        internal void ForceClosePendingDialog()
        {
            IJsDialogCallback callback;
            IWebBrowser browser;
            bool isReload;
            lock (_beforeUnloadLock)
            {
                callback = _beforeUnloadCallback;
                browser = _beforeUnloadBrowser;
                isReload = _beforeUnloadIsReload;
            }

            if (callback != null)
                CompleteBeforeUnloadDialog(browser, callback, isReload, true);

            if (_beforeUnloadDialog != null)
                DialogHelper.SetDialogResult(_beforeUnloadDialog, true);
        }

        protected void CompleteBeforeUnloadDialog(IWebBrowser browser, IJsDialogCallback callback, bool isReload, bool accepted)
        {
            TaskCompletionSource<bool> reloadCompletion;
            lock (_beforeUnloadLock)
            {
                if (!ReferenceEquals(_beforeUnloadCallback, callback))
                    return;

                _beforeUnloadCallback = null;
                _beforeUnloadBrowser = null;
                reloadCompletion = _reloadCompletion;
                _reloadCompletion = null;
            }

            using (callback)
            {
                callback.Continue(accepted);
            }

            reloadCompletion?.TrySetResult(accepted);

            if (!accepted && !isReload)
                Messenger.Global.Send(MessageTokens.BROWSER_CLOSE_CANCELED, browser);
        }

        public override bool OnJSDialog(IWebBrowser chromiumWebBrowser, IBrowser browser, string originUrl, CefJsDialogType dialogType, string messageText, string defaultPromptText, IJsDialogCallback callback, ref bool suppressMessage)
        {
            var wpfWebBrowser = (IWpfWebBrowser)chromiumWebBrowser;
            wpfWebBrowser.Dispatcher.InvokeAsync(delegate
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
