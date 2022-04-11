using CefFlashBrowser.FlashBrowser.Handlers;
using SimpleMvvm.Command;
using System;
using System.Windows.Input;

namespace CefFlashBrowser.FlashBrowser
{
    public class ChromiumFlashBrowser : FlashBrowserBase
    {
        public event EventHandler<LifeSpanHandler.NewWindowEventArgs> OnCreateNewWindow;

        public event EventHandler<EventArgs> OnClose;

        public ICommand LoadUrlCommand { get; }

        public ChromiumFlashBrowser()
        {
            LoadUrlCommand = new DelegateCommand<string>(Load);

            MenuHandler = new ContextMenuHandler();
            DownloadHandler = new IEDownloadHandler();
            JsDialogHandler = new JsDialogHandler();
            LifeSpanHandler = new LifeSpanHandler(
                onCreateNewWindow: (s, e) =>
                {
                    OnCreateNewWindow?.Invoke(this, e);
                },
                onClose: (s, e) =>
                {
                    OnClose?.Invoke(this, e);
                }
            );
        }
    }
}
