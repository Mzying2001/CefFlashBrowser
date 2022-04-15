using CefFlashBrowser.FlashBrowser.Handlers;
using CefSharp;
using SimpleMvvm.Command;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
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

        public ICollection<string> BlockedSwfs
        {
            get => (ICollection<string>)GetValue(BlockedSwfsProperty);
        }

        public bool HasBlockedSwfs
        {
            get => (bool)GetValue(HasBlockedSwfsProperty);
        }

        public static readonly DependencyProperty BlockedSwfsProperty;

        public static readonly DependencyProperty HasBlockedSwfsProperty;

        static ChromiumFlashBrowser()
        {
            BlockedSwfsProperty = DependencyProperty.Register(nameof(BlockedSwfs), typeof(ICollection<string>), typeof(ChromiumFlashBrowser), new PropertyMetadata(null));
            HasBlockedSwfsProperty = DependencyProperty.Register(nameof(HasBlockedSwfs), typeof(bool), typeof(ChromiumFlashBrowser), new PropertyMetadata(false));
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

            SetValue(BlockedSwfsProperty, new ObservableCollection<string>());
            ConsoleMessage += OnConsoleMessage;
            AddressChanged += OnAddressChanged;
        }

        private void OnAddressChanged(object sender, AddressChangedEventArgs e)
        {
            BlockedSwfs.Clear();
            SetValue(HasBlockedSwfsProperty, false);
        }

        private void OnConsoleMessage(object sender, ConsoleMessageEventArgs e)
        {
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
