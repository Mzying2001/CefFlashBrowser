using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using SimpleMvvm.Command;
using System;
using System.Windows;
using System.Windows.Input;

namespace CefFlashBrowser.Views.Dialogs
{
    /// <summary>
    /// AddFavoriteDialog.xaml 的交互逻辑
    /// </summary>
    public partial class AddFavoriteDialog : Window
    {


        public string ItemName
        {
            get { return (string)GetValue(ItemNameProperty); }
            set { SetValue(ItemNameProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ItemName.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ItemNameProperty =
            DependencyProperty.Register("ItemName", typeof(string), typeof(AddFavoriteDialog), new PropertyMetadata(string.Empty));


        public string ItemUrl
        {
            get { return (string)GetValue(ItemUrlProperty); }
            set { SetValue(ItemUrlProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ItemUrl.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ItemUrlProperty =
            DependencyProperty.Register("ItemUrl", typeof(string), typeof(AddFavoriteDialog), new PropertyMetadata(string.Empty));


        private bool? _result = null;
        private Action<bool?> _callback;

        public ICommand OkCommand { get; }
        public ICommand CancelCommand { get; }

        public AddFavoriteDialog()
        {
            OkCommand = new DelegateCommand(() =>
            {
                if (string.IsNullOrWhiteSpace(ItemName) || string.IsNullOrWhiteSpace(ItemUrl))
                    return;

                var website = new Website(ItemName.Trim(), ItemUrl.Trim());
                GlobalData.Favorites.Add(website);
                _result = true;
                Close();
            });

            CancelCommand = new DelegateCommand(() =>
            {
                _result = false;
                Close();
            });

            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            NameTextBox.Focus();
            NameTextBox.SelectAll();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _callback?.Invoke(_result);
        }

        public static void Show(string name, string url, Action<bool?> callback = null)
        {
            new AddFavoriteDialog
            {
                ItemName = name,
                ItemUrl = url,
                _callback = callback
            }.ShowDialog();
        }
    }
}
