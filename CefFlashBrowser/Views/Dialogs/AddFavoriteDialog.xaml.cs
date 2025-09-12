using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
using System;
using System.Windows;

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


        public AddFavoriteDialog()
        {
            InitializeComponent();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (DialogHelper.GetDialogResult(this) == true)
            {
                GlobalData.Favorites.Add(new Models.Website(ItemName, ItemUrl));
            }
        }

        private void OnCancelButtonClick(object sender, RoutedEventArgs e)
        {
            DialogHelper.SetDialogResult(this, false);
        }

        private void OnConfirmButtonClick(object sender, RoutedEventArgs e)
        {
            DialogHelper.SetDialogResult(this, true);
        }
    }
}
