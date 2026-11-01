using SimpleMvvm.Command;
using CefFlashBrowser.Services;
using CefFlashBrowser.ViewModels;
using SimpleMvvm.Ioc;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CefFlashBrowser.Views
{
    /// <summary>
    /// FavoriteManager.xaml 的交互逻辑
    /// </summary>
    public partial class FavoritesManager : Window
    {
        public ICommand CommitEditCommand { get; }

        public FavoritesManager()
        {
            CommitEditCommand = new DelegateCommand(CommitEdit) { CanExecuteFunc = CanCommit };
            InitializeComponent();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            SimpleIoc.Global.GetInstance<FavoritesService>().Save();
        }

        private void TextChangedHandler(object sender, TextChangedEventArgs e)
        {
            var cmd = CommitEditCommand as DelegateCommand;
            cmd?.RaiseCanExecuteChanged();
        }

        private bool CanCommit(object _)
        {
            return !string.IsNullOrWhiteSpace(urlTextBox.Text)
                && !string.IsNullOrWhiteSpace(nameTextBox.Text);
        }

        private void TrimTextBoxText(TextBox textBox)
        {
            textBox.SetCurrentValue(
                TextBox.TextProperty, textBox.Text.Trim());
        }

        private void CommitEdit()
        {
            TrimTextBoxText(urlTextBox);
            TrimTextBoxText(nameTextBox);

            var viewModel = DataContext as FavoritesManagerViewModel;
            var website = viewModel?.SelectedWebsite;

            if (website == null)
                return;

            string oldName = website.Name;
            string oldUrl = website.Url;

            if (!itemEditingGroup.CommitEdit() || (website.Name == oldName && website.Url == oldUrl))
                return;

            int index = viewModel.Favorites.IndexOf(website);
            if (index >= 0)
            {
                var replacement = website.Clone();
                viewModel.Favorites[index] = replacement;
                viewModel.SelectedWebsite = replacement;
            }
        }
    }
}
