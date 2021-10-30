using System;

namespace CefFlashBrowser.Models.FlashBrowser
{
    public class ChromiumFlashBrowser : FlashBrowserBase
    {
        public event EventHandler<NewWindowEventArgs> OnCreatedNewWindow;

        public override void BeginInit()
        {
            base.BeginInit();

            MenuHandler = new ContextMenuHandler();
            LifeSpanHandler = new FlashBrowserLifeSpanHandler((s, e) =>
            {
                OnCreatedNewWindow?.Invoke(s, e);
            });
        }
    }
}
