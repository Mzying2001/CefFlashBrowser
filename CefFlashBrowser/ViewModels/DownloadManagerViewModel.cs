using CefFlashBrowser.Models.Downloads;
using CefFlashBrowser.Services;
using CefFlashBrowser.Services.Downloads;
using SimpleMvvm;
using SimpleMvvm.Command;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;

namespace CefFlashBrowser.ViewModels
{
    public sealed class DownloadManagerViewModel : ViewModelBase, IDisposable
    {
        private readonly DownloadManagerService _service;
        private DownloadItemViewModel _selected;
        private string _filterText = string.Empty;
        private string _filterTerm = string.Empty;

        public ObservableCollection<DownloadItemViewModel> Items => _service.Items;
        public ListCollectionView ItemsView { get; }

        public string FilterText
        {
            get => _filterText;
            set
            {
                if (!UpdateValue(ref _filterText, value ?? string.Empty))
                    return;

                _filterTerm = _filterText.Trim();
                RefreshItemsView();
            }
        }

        public DownloadItemViewModel SelectedItem
        {
            get => _selected;
            set
            {
                if (UpdateValue(ref _selected, value))
                    RefreshCommands();
            }
        }

        public DelegateCommand NewCommand { get; }
        public DelegateCommand PauseCommand { get; }
        public DelegateCommand ResumeCommand { get; }
        public DelegateCommand CancelCommand { get; }
        public DelegateCommand OpenCommand { get; }
        public DelegateCommand FolderCommand { get; }
        public DelegateCommand RemoveCommand { get; }
        public DelegateCommand DeleteFileCommand { get; }
        public DelegateCommand ClearCommand { get; }

        public DownloadManagerViewModel(DownloadManagerService service)
        {
            _service = service;
            ItemsView = new ListCollectionView(Items) { Filter = item => MatchesFilter((DownloadItemViewModel)item) };

            NewCommand = new DelegateCommand(() => RunAsync(service.NewAsync));

            PauseCommand = new DelegateCommand(() => RunAsync(() => service.PauseAsync(_selected.Record)))
            {
                CanExecuteFunc = _ => _selected != null && service.Find(_selected.Record)?.CanPause == true
            };

            ResumeCommand = new DelegateCommand(() => RunAsync(() => service.ResumeAsync(_selected.Record)))
            {
                CanExecuteFunc = _ => _selected != null && !_selected.CanOpen
                    && _selected.Record.State != DownloadState.Downloading
                    && _selected.Record.State != DownloadState.Waiting
                    && (service.Find(_selected.Record)?.CanResume == true || service.CanRestore(_selected.Record))
            };

            CancelCommand = new DelegateCommand(() => RunAsync(() => service.CancelAsync(_selected.Record)))
            {
                CanExecuteFunc = _ => _selected?.IsActive == true || _selected?.Record.State == DownloadState.Interrupted
            };

            OpenCommand = new DelegateCommand(Open)
            {
                CanExecuteFunc = _ => _selected?.CanOpen == true
            };

            FolderCommand = new DelegateCommand(OpenFolder)
            {
                CanExecuteFunc = _ => !string.IsNullOrEmpty(_selected?.FilePath)
            };

            RemoveCommand = new DelegateCommand(() => RunAsync(() => service.RemoveAsync(_selected)))
            {
                CanExecuteFunc = _ => _selected != null
            };

            DeleteFileCommand = new DelegateCommand(() => RunAsync(() => service.DeleteFileAsync(_selected.Record)))
            {
                CanExecuteFunc = _ => _selected?.CanOpen == true
            };

            ClearCommand = new DelegateCommand(() => RunAsync(service.ClearFinishedAsync));

            service.Refreshed += ServiceRefreshed;
        }

        private async void RunAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                WindowManager.ShowError(error.Message);
            }

            _service.Refresh();
        }

        private void Open()
        {
            _service.Refresh();

            if (_selected?.CanOpen != true)
                return;

            try
            {
                Process.Start(new ProcessStartInfo(_selected.FilePath) { UseShellExecute = true })?.Dispose();
            }
            catch (Exception error)
            {
                _service.Refresh();
                WindowManager.ShowError(error.Message);
            }
        }

        private void OpenFolder()
        {
            try
            {
                if (File.Exists(_selected.FilePath))
                {
                    Process.Start("explorer.exe", "/select,\"" + _selected.FilePath + "\"")?.Dispose();
                }
                else
                {
                    Process.Start(new ProcessStartInfo(Path.GetDirectoryName(_selected.FilePath)) { UseShellExecute = true })?.Dispose();
                }
            }
            catch (Exception error)
            {
                WindowManager.ShowError(error.Message);
            }
        }

        private bool MatchesFilter(DownloadItemViewModel item) => _filterTerm.Length == 0
            || ContainsFilterTerm(item.FileName) || ContainsFilterTerm(item.FilePath) || ContainsFilterTerm(item.Url);

        private bool ContainsFilterTerm(string value) => value != null
            && value.IndexOf(_filterTerm, StringComparison.OrdinalIgnoreCase) >= 0;

        private void RefreshItemsView()
        {
            if (_selected != null && !MatchesFilter(_selected))
                SelectedItem = null;

            ItemsView.Refresh();
        }

        private void ServiceRefreshed(object sender, EventArgs e)
        {
            // Redirects and destination conflicts can change which downloads match while the filter is active.
            if (_filterTerm.Length != 0 && !Items.Where(MatchesFilter).SequenceEqual(ItemsView.Cast<DownloadItemViewModel>()))
                RefreshItemsView();

            RefreshCommands();
        }

        private void RefreshCommands()
        {
            PauseCommand.RaiseCanExecuteChanged();
            ResumeCommand.RaiseCanExecuteChanged();
            CancelCommand.RaiseCanExecuteChanged();
            OpenCommand.RaiseCanExecuteChanged();
            FolderCommand.RaiseCanExecuteChanged();
            RemoveCommand.RaiseCanExecuteChanged();
            DeleteFileCommand.RaiseCanExecuteChanged();
        }

        public void Dispose()
        {
            _service.Refreshed -= ServiceRefreshed;
            ItemsView.DetachFromSourceCollection();
        }
    }
}
