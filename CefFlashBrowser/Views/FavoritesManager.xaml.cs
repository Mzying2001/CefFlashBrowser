using CefFlashBrowser.Models.Data;
using SimpleMvvm.Messaging;
using System;
using System.Windows;

namespace CefFlashBrowser.Views
{
    /// <summary>
    /// FavoriteManager.xaml 的交互逻辑
    /// </summary>
    public partial class FavoritesManager : Window
    {
        public FavoritesManager()
        {
            InitializeComponent();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Messenger.Global.Send(MessageTokens.SAVE_FAVORITES, null);
        }
    }
}
