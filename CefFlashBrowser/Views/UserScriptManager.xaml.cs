using CefFlashBrowser.Data;
using CefFlashBrowser.Models;
using CefFlashBrowser.Services;
using CefFlashBrowser.ViewModels;
using Microsoft.Win32;
using Mzying2001.MonkeySharp.Core.Domain;
using SimpleMvvm.Command;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CefFlashBrowser.Views
{
    public partial class UserScriptManager : Window
    {
        private const double MaxScriptListWidth = 450;

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
            sourceEditor.TextChanged += (_, __) => UpdateSearchControls();
            sourceEditor.TextArea.SelectionChanged += (_, __) => UpdateSearchControls();

            WindowSizeInfo.Apply(GlobalData.Settings.UserScriptManagerSizeInfo, this);
        }

        private async void WindowLoaded(object sender, RoutedEventArgs e)
        {
            ViewModel.ConfirmInstallation = ConfirmInstallation;
            ViewModel.ConfirmDeletion = ConfirmOperation;
            ViewModel.PropertyChanged += ViewModelPropertyChanged;
            await ViewModel.OpenAsync();
        }

        private void ViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            ImportFileCommand.RaiseCanExecuteChanged();
            ExportCommand.RaiseCanExecuteChanged();
        }

        private bool ConfirmInstallation(UserScriptMetadata metadata, string origin, bool update)
        {
            return WindowManager.ConfirmUserScriptInstallation(metadata, origin, this, update: update);
        }

        private bool ConfirmOperation(string message)
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

        private void ScriptPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var item = (ListBoxItem)sender;
            item.IsSelected = true;
            item.Focus();
        }

        private void SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Undo history belongs to the selected script, even when two scripts have identical text.
            if (sourceEditor == null)
                return;

            sourceEditor.Document.UndoStack.ClearAll();
            OnSearchScriptChanged();
        }

        private void ContentSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!e.WidthChanged)
                return;

            double availableWidth = e.NewSize.Width - scriptLayout.Margin.Left - scriptLayout.Margin.Right
                - scriptSplitterColumn.Width.Value - scriptEditorColumn.MinWidth;
            scriptListColumn.MaxWidth = Math.Max(scriptListColumn.MinWidth, Math.Min(MaxScriptListWidth, availableWidth));
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            if (!e.Cancel)
            {
                GlobalData.Settings.UserScriptManagerSizeInfo = WindowSizeInfo.GetSizeInfo(this);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            ViewModel.PropertyChanged -= ViewModelPropertyChanged;
            ViewModel.ConfirmInstallation = null;
            ViewModel.ConfirmDeletion = null;
            ViewModel.Close();
            base.OnClosed(e);
        }
    }
}
