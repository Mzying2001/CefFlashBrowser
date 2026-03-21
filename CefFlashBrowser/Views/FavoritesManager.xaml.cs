using CefFlashBrowser.Data;
using SimpleMvvm.Command;
using SimpleMvvm.Messaging;
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
            Messenger.Global.Send(MessageTokens.SAVE_FAVORITES, null);
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
            itemEditingGroup.CommitEdit();
        }
    }
}
