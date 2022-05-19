using CefSharp;

namespace CefFlashBrowser.WinformCefSharp4WPF.Internals
{
    internal class JsDialogHandlerWrapper : IJsDialogHandler, IHandlerWrapper<IJsDialogHandler>
    {
        public IJsDialogHandler Handler { get; }

        public IWebBrowser TargetBrowser { get; }

        public JsDialogHandlerWrapper(IJsDialogHandler handler, IWebBrowser targetBrowser)
        {
            Handler = handler;
            TargetBrowser = targetBrowser;
        }

        public bool OnBeforeUnloadDialog(IWebBrowser chromiumWebBrowser, IBrowser browser, string messageText, bool isReload, IJsDialogCallback callback)
        {
            return Handler.OnBeforeUnloadDialog(TargetBrowser, browser, messageText, isReload, callback);
        }

        public void OnDialogClosed(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
            Handler.OnDialogClosed(TargetBrowser, browser);
        }

        public bool OnJSDialog(IWebBrowser chromiumWebBrowser, IBrowser browser, string originUrl, CefJsDialogType dialogType, string messageText, string defaultPromptText, IJsDialogCallback callback, ref bool suppressMessage)
        {
            return Handler.OnJSDialog(TargetBrowser, browser, originUrl, dialogType, messageText, defaultPromptText, callback, ref suppressMessage);
        }

        public void OnResetDialogState(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
            Handler.OnResetDialogState(TargetBrowser, browser);
        }
    }
}
