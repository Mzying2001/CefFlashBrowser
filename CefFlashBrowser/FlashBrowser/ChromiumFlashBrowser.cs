using CefFlashBrowser.FlashBrowser.Handlers;
using CefSharp;
using SimpleMvvm.Command;
using System;
using System.Windows.Input;

namespace CefFlashBrowser.FlashBrowser
{
    public class ChromiumFlashBrowser : FlashBrowserBase
    {
        public event EventHandler<LifeSpanHandler.NewBrowserEventArgs> OnCreateNewBrowser;

        public event EventHandler<EventArgs> OnClose;

        public ICommand LoadUrlCommand { get; }

        public new ILifeSpanHandler LifeSpanHandler
        {
            get => base.LifeSpanHandler;
            protected set => base.LifeSpanHandler = value;
        }

        public ChromiumFlashBrowser()
        {
            LoadUrlCommand = new DelegateCommand<string>(Load);

            MenuHandler = new ContextMenuHandler(this);
            DownloadHandler = new IEDownloadHandler();
            JsDialogHandler = new JsDialogHandler(this);

            LifeSpanHandler = new LifeSpanHandler(
                onCreateNewBrowser: (s, e) => Dispatcher.Invoke(() =>
                {
                    OnCreateNewBrowser?.Invoke(this, e);
                }),
                onClose: (s, e) => Dispatcher.Invoke(() =>
                {
                    OnClose?.Invoke(this, e);
                })
            );
        }
    }
}
