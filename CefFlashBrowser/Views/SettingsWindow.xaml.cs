using CefFlashBrowser.Models.Data;
using SimpleMvvm.Messaging;
using System;
using System.Windows;

namespace CefFlashBrowser.Views
{
    /// <summary>
    /// SettingsWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Messenger.Global.Send(MessageTokens.SAVE_SETTINGS, null);
        }
    }
}
