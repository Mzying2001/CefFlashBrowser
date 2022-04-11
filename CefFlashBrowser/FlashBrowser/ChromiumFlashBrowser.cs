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

        public ICommand LoadUrlCommand { get; private set; }

        private void LoadUrl(string url)
        {
            Load(url);
        }

        public ChromiumFlashBrowser()
        {
            LoadUrlCommand = new DelegateCommand<string>(LoadUrl);

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
