using CefFlashBrowser.Data;
using CefFlashBrowser.Models;
using CefFlashBrowser.Services;
using CefFlashBrowser.Utils;
using CefFlashBrowser.ViewModels;
using CefFlashBrowser.Views.Dialogs;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using Microsoft.Win32;
using Mzying2001.MonkeySharp.Core.Domain;
using SimpleMvvm.Command;
using SimpleMvvm.Ioc;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace CefFlashBrowser.Views
{
    public partial class UserScriptManager : Window
    {
        private const double MaxScriptListWidth = 450;
        private const string JavaScriptFileExtension = ".js";
        private const string UserScriptFileExtension = ".user.js";

        private readonly ConditionalWeakTable<UserScriptItemViewModel, ScriptEditorState> _editorStates;
        private UserScriptItemViewModel _displayedScript;

        public DelegateCommand ImportFileCommand { get; }
        public DelegateCommand ExportCommand { get; }
        public DelegateCommand OpenInDefaultBrowserCommand { get; }
        public DelegateCommand OpenBatchUpdateCommand { get; }

        private UserScriptManagerViewModel ViewModel
        {
            get => (UserScriptManagerViewModel)DataContext;
        }

        public UserScriptManager()
        {
            _editorStates = new ConditionalWeakTable<UserScriptItemViewModel, ScriptEditorState>();

            ImportFileCommand = new DelegateCommand(ImportFile) { CanExecuteFunc = _ => !ViewModel.IsBusy };
            ExportCommand = new DelegateCommand(Export) { CanExecuteFunc = _ => !ViewModel.IsBusy && ViewModel.SelectedScript != null };
            OpenInDefaultBrowserCommand = new DelegateCommand<string>(WindowManager.OpenInDefaultBrowser);
            OpenBatchUpdateCommand = new DelegateCommand(OpenBatchUpdate) { CanExecuteFunc = _ => ViewModel.CanOpenBatchUpdate };

            InitializeComponent();
            sourceEditor.ApplySettings(GlobalData.Settings.ScriptEditorSettings);

            Loaded += WindowLoaded;
            sourceEditor.TextChanged += (_, __) => UpdateSearchControls();
            sourceEditor.TextArea.SelectionChanged += (_, __) => UpdateSearchControls();
            scriptList.SelectionChanged += (_, __) => ShowSelectedScript();

            ShowSelectedScript();
            WindowSizeInfo.Apply(GlobalData.Settings.UserScriptManagerSizeInfo, this);
        }

        private async void WindowLoaded(object sender, RoutedEventArgs e)
        {
            ViewModel.ConfirmInstallation = ConfirmInstallation;
            ViewModel.ConfirmDeletion = ConfirmOperation;
            ViewModel.ShowUpdateResult = (message, token) => WindowManager.ShowAlertDialog(message, Title, this, token);
            ViewModel.ShowDetails = script => WindowManager.ShowUserScriptDetails(script, this);
            ViewModel.PropertyChanged += ViewModelPropertyChanged;
            await ViewModel.OpenAsync();
        }

        private void ViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            ImportFileCommand.RaiseCanExecuteChanged();
            ExportCommand.RaiseCanExecuteChanged();
            OpenBatchUpdateCommand.RaiseCanExecuteChanged();
        }

        private void OpenEditorSettings(object sender, RoutedEventArgs e)
        {
            var settings = GlobalData.Settings;
            var previous = settings.ScriptEditorSettings;
            var dialog = WindowManager.CreateWindow<EditorSettingsDialog>();

            dialog.Initialize(previous, sourceEditor.FontFamily.Source);

            if (DialogHelper.ShowModal(dialog, this) != true)
                return;

            settings.ScriptEditorSettings = dialog.Result;

            if (!GlobalData.SaveSettings())
            {
                settings.ScriptEditorSettings = previous;

                var language = SimpleIoc.Global.GetInstance<LanguageService>();

                WindowManager.ShowAlertDialog(
                    language.GetString("scriptEditorSettings_saveFailed"),
                    language.GetString("common_error"), this);
                return;
            }

            sourceEditor.ApplySettings(dialog.Result);
        }

        private void OpenBatchUpdate()
        {
            using (var model = ViewModel.CreateBatchUpdateViewModel())
            {
                if (model == null)
                    return;

                var dialog = WindowManager.CreateWindow<UserScriptBatchUpdateDialog>();
                dialog.SetModel(model);
                DialogHelper.ShowModal(dialog, this);
            }
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
            var language = SimpleIoc.Global.GetInstance<LanguageService>();

            var dialog = new OpenFileDialog
            {
                Filter = $"{language.GetString("userScriptManager_javaScriptFile")}|*{UserScriptFileExtension};*{JavaScriptFileExtension}|" +
                         $"{language.GetString("common_allFiles")}|*.*"
            };

            if (dialog.ShowDialog(this) == true)
            {
                await ViewModel.ImportFileAsync(dialog.FileName);
            }
        }

        private void ImportFileDragOver(object sender, DragEventArgs e)
        {
            GetDroppedScriptPath(e);
        }

        private async void ImportFileDrop(object sender, DragEventArgs e)
        {
            var path = GetDroppedScriptPath(e);
            if (path == null)
                return;

            // Finish the drag-and-drop operation before import can open an update dialog.
            await Dispatcher.Yield(DispatcherPriority.Background);
            await ViewModel.ImportFileAsync(path);
        }

        private string GetDroppedScriptPath(DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop, autoConvert: false))
                return null;

            e.Handled = true;
            e.Effects = DragDropEffects.None;

            ICommand import = ImportFileCommand;

            if (!import.CanExecute(null) || (e.AllowedEffects & DragDropEffects.Copy) == 0
                || !(e.Data.GetData(DataFormats.FileDrop, autoConvert: false) is string[] files) || files.Length != 1)
                return null;

            var path = files[0];

            if (path?.EndsWith(JavaScriptFileExtension, StringComparison.OrdinalIgnoreCase) != true || Directory.Exists(path))
                return null;

            e.Effects = DragDropEffects.Copy;
            return path;
        }

        private async void Export()
        {
            var language = SimpleIoc.Global.GetInstance<LanguageService>();

            var dialog = new SaveFileDialog
            {
                Filter = $"{language.GetString("userScriptManager_userScriptFile")}|*{UserScriptFileExtension}",
                DefaultExt = UserScriptFileExtension,
                FileName = GetExportFileName(ViewModel.SelectedScript?.Name)
            };

            if (dialog.ShowDialog(this) == true)
            {
                await ViewModel.ExportAsync(dialog.FileName);
            }
        }

        private static string GetExportFileName(string scriptName)
        {
            var name = scriptName?.Trim().TrimEnd('.', ' ');

            if (name?.EndsWith(UserScriptFileExtension, StringComparison.OrdinalIgnoreCase) == true)
                name = name.Substring(0, name.Length - UserScriptFileExtension.Length).TrimEnd('.', ' ');

            if (string.IsNullOrWhiteSpace(name?.Replace(".", string.Empty)))
                name = "script";

            return UserScriptDownloadPaths.SafeFileName(name.Replace('/', '_').Replace('\\', '_')) + UserScriptFileExtension;
        }

        private void ScriptPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var item = (ListBoxItem)sender;
            item.IsSelected = true;
            item.Focus();
        }

        private void ScriptContextMenuOpened(object sender, RoutedEventArgs e)
        {
            // MenuItem keeps its last CanExecute result when the popup binds the row's CommandParameter.
            ViewModel.UpdateCommand.RaiseCanExecuteChanged();
            ViewModel.ShowDetailsCommand.RaiseCanExecuteChanged();
        }

        private void ShowSelectedScript()
        {
            var script = (UserScriptItemViewModel)scriptList.SelectedItem;

            if (ReferenceEquals(script, _displayedScript))
                return;

            var textArea = sourceEditor.TextArea;

            if (_displayedScript != null && _editorStates.TryGetValue(_displayedScript, out var previousState))
            {
                previousState.CaretPosition = textArea.Caret.Position;
                previousState.Selection = textArea.Selection;
                previousState.ScrollOffset = textArea.TextView.ScrollOffset;
            }

            _displayedScript = script;

            var state = script == null ? null : GetEditorState(script);
            var binding = script == null ? null : new Binding(nameof(UserScriptItemViewModel.Source))
            {
                Source = script,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                TargetNullValue = string.Empty
            };

            sourceEditor.SetSourceDocument(state?.Document ?? new TextDocument(), binding);

            if (state?.Selection != null)
            {
                textArea.Caret.Position = state.CaretPosition;
                textArea.Selection = state.Selection;

                // Apply to the text view immediately so another selection change can capture the restored offsets.
                IScrollInfo scrollInfo = textArea.TextView;
                scrollInfo.SetHorizontalOffset(state.ScrollOffset.X);
                scrollInfo.SetVerticalOffset(state.ScrollOffset.Y);
                scrollInfo.ScrollOwner?.InvalidateScrollInfo();
            }

            OnSearchScriptChanged();
        }

        private ScriptEditorState GetEditorState(UserScriptItemViewModel script)
        {
            var source = script.Source ?? string.Empty;

            if (_editorStates.TryGetValue(script, out var state))
            {
                if (state.Document.Text == source)
                    return state;

                _editorStates.Remove(script);
            }

            state = new ScriptEditorState(source);
            _editorStates.Add(script, state);
            return state;
        }

        private sealed class ScriptEditorState
        {
            public TextDocument Document { get; }
            public TextViewPosition CaretPosition { get; set; }
            public Selection Selection { get; set; }
            public Vector ScrollOffset { get; set; }

            public ScriptEditorState(string source)
            {
                Document = new TextDocument(source);
            }
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
            ViewModel.ShowUpdateResult = null;
            ViewModel.ShowDetails = null;
            ViewModel.Close();
            base.OnClosed(e);
        }
    }
}
