using CefFlashBrowser.Models.Data;
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
            CommitEditCommand = new DelegateCommand(CommitEdit);
            InitializeComponent();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Messenger.Global.Send(MessageTokens.SAVE_FAVORITES, null);
        }

        private void TrimTextBoxText(TextBox textBox)
        {
            textBox.SetCurrentValue(
                TextBox.TextProperty, textBox.Text.Trim());
        }

        private void CommitEdit()
        {
            if (!string.IsNullOrWhiteSpace(urlTextBox.Text) &&
                !string.IsNullOrWhiteSpace(nameTextBox.Text))
            {
                TrimTextBoxText(urlTextBox);
                TrimTextBoxText(nameTextBox);
                itemEditingGroup.CommitEdit();
            }
        }
    }
}
