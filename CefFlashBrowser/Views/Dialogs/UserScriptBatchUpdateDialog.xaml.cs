using CefFlashBrowser.Services;
using CefFlashBrowser.ViewModels;
using Mzying2001.MonkeySharp.Core.Domain;
using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace CefFlashBrowser.Views.Dialogs
{
    public partial class UserScriptBatchUpdateDialog : Window
    {
        private UserScriptBatchUpdateViewModel _model;
        private bool _closeRequested;

        public UserScriptBatchUpdateDialog()
        {
            InitializeComponent();
        }

        internal void SetModel(UserScriptBatchUpdateViewModel model)
        {
            _model = model;
            DataContext = model;
            _model.ConfirmRow = ConfirmRow;
            _model.ConfirmAll = (entries, token) => WindowManager.ConfirmUserScriptBatchUpdate(entries, this, token);
            _model.PropertyChanged += ModelPropertyChanged;
        }

        private bool ConfirmRow(UserScriptMetadata metadata, string origin, string warning, CancellationToken token)
        {
            return WindowManager.ConfirmUserScriptInstallation(metadata, origin, this, update: true, token: token, warning: warning);
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape || (e.Key == Key.W && Keyboard.Modifiers == ModifierKeys.Control))
            {
                e.Handled = true;
                Close();
            }
            base.OnPreviewKeyDown(e);
        }

        private void CloseClicked(object sender, RoutedEventArgs e) => Close();

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            if (!e.Cancel && _model?.IsBusy == true)
            {
                e.Cancel = true;
                _closeRequested = true;
                _model.Cancel();
            }
        }

        private void ModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_closeRequested && e.PropertyName == nameof(_model.IsBusy) && !_model.IsBusy)
            {
                Dispatcher.InvokeAsync(() => { if (IsVisible) Close(); });
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_model != null)
            {
                _model.PropertyChanged -= ModelPropertyChanged;
                _model.Dispose();
            }
            base.OnClosed(e);
        }
    }
}
