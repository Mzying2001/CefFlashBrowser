using CefSharp;
using System.Collections.Generic;

namespace CefFlashBrowser.FlashBrowser.Handlers
{
    public class DialogHandler : IDialogHandler
    {
        public virtual bool OnFileDialog(IWebBrowser chromiumWebBrowser, IBrowser browser, CefFileDialogMode mode, CefFileDialogFlags flags, string title, string defaultFilePath, List<string> acceptFilters, int selectedAcceptFilter, IFileDialogCallback callback)
        {
            return false;
        }
    }
}
