namespace CefFlashBrowser.FlashBrowser
{
    public class ChromiumFlashBrowser : FlashBrowserBase
    {
        public override void BeginInit()
        {
            base.BeginInit();

            MenuHandler = new ContextMenuHandler();
            OnCreateNewWindow += ChromiumFlashBrowser_OnCreateNewWindow;
        }

        private void ChromiumFlashBrowser_OnCreateNewWindow(object sender, NewWindowEventArgs e)
        {
            e.CancelPopup = true;
            Dispatcher.Invoke(() => Address = e.TargetUrl);
        }
    }
}
