using CefSharp;
using System.Collections.ObjectModel;
using System.Windows;

namespace CefFlashBrowser.FlashBrowser
{
    public class ChromiumFlashBrowser : ChromiumFlashBrowserBase
    {
        public static readonly DependencyProperty BlockedSwfsProperty;

        public static readonly DependencyProperty HasBlockedSwfsProperty;

        static ChromiumFlashBrowser()
        {
            BlockedSwfsProperty = DependencyProperty.Register(nameof(BlockedSwfs), typeof(ObservableCollection<string>), typeof(ChromiumFlashBrowser), new PropertyMetadata(null));
            HasBlockedSwfsProperty = DependencyProperty.Register(nameof(HasBlockedSwfs), typeof(bool), typeof(ChromiumFlashBrowser), new PropertyMetadata(false));
        }

        public ChromiumFlashBrowser()
        {
            SetValue(BlockedSwfsProperty, new ObservableCollection<string>());
        }





        public ObservableCollection<string> BlockedSwfs
        {
            get => (ObservableCollection<string>)GetValue(BlockedSwfsProperty);
        }

        public bool HasBlockedSwfs
        {
            get => (bool)GetValue(HasBlockedSwfsProperty);
        }

        protected override void OnAddressChanged(AddressChangedEventArgs e)
        {
            base.OnAddressChanged(e);

            BlockedSwfs.Clear();
            SetValue(HasBlockedSwfsProperty, false);
        }

        protected override void OnConsoleMessage(ConsoleMessageEventArgs e)
        {
            base.OnConsoleMessage(e);

            if (e.Level != LogSeverity.Info)
            {
                return;
            }

            var msg = e.Message;
            if (msg.StartsWith("Cross-origin plugin content from"))
            {
                var url = msg.Split(' ')?[4];
                if (!string.IsNullOrWhiteSpace(url) && !BlockedSwfs.Contains(url))
                {
                    BlockedSwfs.Add(url);
                    SetValue(HasBlockedSwfsProperty, true);
                }
            }
        }
    }
}
