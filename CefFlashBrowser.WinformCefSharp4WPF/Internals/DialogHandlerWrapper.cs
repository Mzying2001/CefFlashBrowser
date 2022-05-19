using CefSharp;
using System.Collections.Generic;

namespace CefFlashBrowser.WinformCefSharp4WPF.Internals
{
    internal class DialogHandlerWrapper : IDialogHandler, IHandlerWrapper<IDialogHandler>
    {
        public IDialogHandler Handler { get; }
        public IWebBrowser TargetBrowser { get; }

        public DialogHandlerWrapper(IDialogHandler handler, IWebBrowser targetBrowser)
        {
            Handler = handler;
            TargetBrowser = targetBrowser;
        }

        public bool OnFileDialog(IWebBrowser chromiumWebBrowser, IBrowser browser, CefFileDialogMode mode, CefFileDialogFlags flags, string title, string defaultFilePath, List<string> acceptFilters, int selectedAcceptFilter, IFileDialogCallback callback)
        {
            return Handler.OnFileDialog(TargetBrowser, browser, mode, flags, title, defaultFilePath, acceptFilters, selectedAcceptFilter, callback);
        }
    }
}
