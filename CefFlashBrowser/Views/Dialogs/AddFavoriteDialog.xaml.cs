using CefFlashBrowser.Models.Data;
using CefFlashBrowser.ViewModels.DialogViewModels;
using SimpleMvvm.Messaging;
using System;
using System.Windows;

namespace CefFlashBrowser.Views.Dialogs
{
    /// <summary>
    /// AddFavoriteDialog.xaml 的交互逻辑
    /// </summary>
    public partial class AddFavoriteDialog : Window
    {
        private Action<bool> callback;

        public AddFavoriteDialog()
        {
            InitializeComponent();

            Messenger.Global.Register(MessageTokens.EXIT_ADDFAVORITES, CloseWindow);
            Closing += (s, e) => Messenger.Global.Unregister(MessageTokens.EXIT_ADDFAVORITES, CloseWindow);
        }

        private void CloseWindow(object obj)
        {
            callback?.Invoke((bool)obj);
            Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            NameTextBox.Focus();
            NameTextBox.SelectAll();
        }

        public static void Show(string name, string url, Action<bool> callback = null)
        {
            var dialog = new AddFavoriteDialog { callback = callback };
            var vmodel = (AddFavoriteDialogViewModel)dialog.DataContext;

            vmodel.Name = name;
            vmodel.Url = url;
            dialog.ShowDialog();
        }
    }
}
