using CefFlashBrowser.FlashBrowser.Handlers;

namespace CefFlashBrowser.FlashBrowser
{
    public class ChromiumFlashBrowser : FlashBrowserBase
    {
        public override void BeginInit()
        {
            base.BeginInit();
            OnCreateNewWindow += ChromiumFlashBrowser_OnCreateNewWindow;
        }

        private void ChromiumFlashBrowser_OnCreateNewWindow(object sender, LifeSpanHandler.NewWindowEventArgs e)
        {
            e.CancelPopup = true;
            Dispatcher.Invoke(() => Address = e.TargetUrl);
        }
    }
}
