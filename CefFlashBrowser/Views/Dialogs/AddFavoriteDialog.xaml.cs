using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace CefFlashBrowser.Views.Dialogs
{
    /// <summary>
    /// AddFavoriteDialog.xaml 的交互逻辑
    /// </summary>
    public partial class AddFavoriteDialog : Window
    {
        private bool _supressClose = true; // Prevent close on first binding

        public AddFavoriteDialog()
        {
            InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _supressClose = false;
            FocusManager.SetFocusedElement(this, nameTextBox);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_supressClose)
                e.Cancel = true;
            base.OnClosing(e);
        }
    }
}
