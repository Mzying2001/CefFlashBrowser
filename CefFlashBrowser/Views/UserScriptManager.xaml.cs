using CefFlashBrowser.Services;
using CefFlashBrowser.ViewModels;
using Microsoft.Win32;
using SimpleMvvm.Command;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace CefFlashBrowser.Views
{
    public partial class UserScriptManager : Window
    {
        public DelegateCommand ImportFileCommand { get; }
        public DelegateCommand ExportCommand { get; }

        private UserScriptManagerViewModel ViewModel
        {
            get => (UserScriptManagerViewModel)DataContext;
        }

        public UserScriptManager()
        {
            ImportFileCommand = new DelegateCommand(ImportFile) { CanExecuteFunc = _ => !ViewModel.IsBusy };
            ExportCommand = new DelegateCommand(Export) { CanExecuteFunc = _ => !ViewModel.IsBusy && ViewModel.SelectedScript != null };

            InitializeComponent();
            Loaded += WindowLoaded;
        }

        private async void WindowLoaded(object sender, RoutedEventArgs e)
        {
            ViewModel.ConfirmInstallation = ConfirmInstallation;
            ViewModel.PropertyChanged += ViewModelPropertyChanged;
            await ViewModel.OpenAsync();
        }

        private void ViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            ImportFileCommand.RaiseCanExecuteChanged();
            ExportCommand.RaiseCanExecuteChanged();
        }

        private bool ConfirmInstallation(string message)
        {
            bool accepted = false;
            WindowManager.Confirm(message, Title, result => accepted = result == true);
            return accepted;
        }

        private async void ImportFile()
        {
            var dialog = new OpenFileDialog { Filter = "JavaScript (*.user.js;*.js)|*.user.js;*.js|All files (*.*)|*.*" };

            if (dialog.ShowDialog(this) == true)
            {
                await ViewModel.ImportFileAsync(dialog.FileName);
            }
        }

        private async void Export()
        {
            var dialog = new SaveFileDialog { Filter = "Userscript (*.user.js)|*.user.js", DefaultExt = ".user.js", FileName = "script.user.js" };

            if (dialog.ShowDialog(this) == true)
            {
                await ViewModel.ExportAsync(dialog.FileName);
            }
        }

        private void SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // TextBox undo history belongs to the selected script, never the previous item.
            if (sourceEditor == null) return;

            sourceEditor.IsUndoEnabled = false;
            sourceEditor.IsUndoEnabled = true;
        }

        protected override void OnClosed(EventArgs e)
        {
            ViewModel.PropertyChanged -= ViewModelPropertyChanged;
            ViewModel.ConfirmInstallation = null;
            ViewModel.Close();
            base.OnClosed(e);
        }
    }
}
