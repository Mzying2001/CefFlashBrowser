using CefFlashBrowser.Data;
using CefFlashBrowser.Models;
using CefFlashBrowser.Services;
using CefFlashBrowser.ViewModels;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using Microsoft.Win32;
using Mzying2001.MonkeySharp.Core.Domain;
using SimpleMvvm.Command;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace CefFlashBrowser.Views
{
    public partial class UserScriptManager : Window
    {
        private const double MaxScriptListWidth = 450;

        private readonly ConditionalWeakTable<UserScriptItemViewModel, ScriptEditorState> _editorStates;
        private UserScriptItemViewModel _displayedScript;

        public DelegateCommand ImportFileCommand { get; }
        public DelegateCommand ExportCommand { get; }
        public DelegateCommand OpenInDefaultBrowserCommand { get; }

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

            InitializeComponent();

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
            ViewModel.ShowDetails = script => WindowManager.ShowUserScriptDetails(script, this);
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

        private void ScriptContextMenuOpened(object sender, RoutedEventArgs e)
        {
            // MenuItem keeps its last CanExecute result when the popup binds the row's CommandParameter.
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
            ViewModel.ShowDetails = null;
            ViewModel.Close();
            base.OnClosed(e);
        }
    }
}
