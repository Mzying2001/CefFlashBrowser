using CefFlashBrowser.Services.UserScripts;
using Mzying2001.MonkeySharp.Core.Domain;
using Mzying2001.MonkeySharp.Core.Repository;
using Mzying2001.MonkeySharp.Core.Updates;
using SimpleMvvm;
using SimpleMvvm.Command;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;

namespace CefFlashBrowser.ViewModels
{
    internal sealed class UserScriptBatchUpdateViewModel : ViewModelBase, IDisposable
    {
        private readonly ObservableCollection<UserScriptItemViewModel> _scripts;
        private readonly ObservableCollection<UserScriptBatchUpdateRow> _rows = new ObservableCollection<UserScriptBatchUpdateRow>();
        private readonly Func<UserScriptUpdateOperation> _createOperation;
        private readonly Func<string, string> _text;
        private readonly Action<UserScriptItemViewModel, Guid, UserScriptInstallation> _acceptUpdate;
        private readonly CancellationTokenSource _lifetime;
        private bool _disposed;

        private ListCollectionView _rowsView;
        public ListCollectionView RowsView => _rowsView ?? (_rowsView = new ListCollectionView(_rows)
        {
            Filter = item => MatchesFilter((UserScriptBatchUpdateRow)item)
        });

        public DelegateCommand CheckAllCommand { get; }
        public DelegateCommand UpdateAllCommand { get; }
        public DelegateCommand<UserScriptBatchUpdateRow> CheckRowCommand { get; }
        public DelegateCommand<UserScriptBatchUpdateRow> UpdateRowCommand { get; }

        public Func<UserScriptMetadata, string, string, CancellationToken, bool> ConfirmRow { private get; set; }
        public Func<string, CancellationToken, bool> ConfirmAll { private get; set; }

        private string _filterText = string.Empty;
        private string _filterTerm = string.Empty;
        public string FilterText
        {
            get => _filterText;
            set
            {
                if (!UpdateValue(ref _filterText, value ?? string.Empty)) return;
                _filterTerm = _filterText.Trim();
                _rowsView?.Refresh();
                UpdateCommands();
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (UpdateValue(ref _isBusy, value)) UpdateCommands();
            }
        }

        private string _summary;
        public string Summary
        {
            get => _summary;
            private set => UpdateValue(ref _summary, value);
        }

        public UserScriptBatchUpdateViewModel(ObservableCollection<UserScriptItemViewModel> scripts,
            IUserScriptRepository repository, Func<string, CancellationToken, Task<string>> fetchSource,
            Func<string, string> text, Action<UserScriptItemViewModel, Guid, UserScriptInstallation> acceptUpdate,
            CancellationToken token)
        {
            _scripts = scripts;
            _createOperation = () => new UserScriptUpdateOperation(repository, fetchSource);
            _text = text;
            _acceptUpdate = acceptUpdate;
            _lifetime = CancellationTokenSource.CreateLinkedTokenSource(token);

            CheckAllCommand = new DelegateCommand(async () => await CheckAllAsync())
            {
                CanExecuteFunc = _ => CanRun && _rows.Any(MatchesFilter)
            };
            UpdateAllCommand = new DelegateCommand(async () => await UpdateAllAsync())
            {
                CanExecuteFunc = _ => CanRun && _rows.Any(row => MatchesFilter(row) && row.HasUpdate)
            };
            CheckRowCommand = new DelegateCommand<UserScriptBatchUpdateRow>(async row => await CheckRowAsync(row))
            {
                CanExecuteFunc = item => CanRun && item is UserScriptBatchUpdateRow row && _rows.Contains(row)
            };
            UpdateRowCommand = new DelegateCommand<UserScriptBatchUpdateRow>(async row => await UpdateRowAsync(row))
            {
                CanExecuteFunc = item => CanRun && item is UserScriptBatchUpdateRow row && row.HasUpdate && _rows.Contains(row)
            };

            _scripts.CollectionChanged += ScriptsChanged;
            SynchronizeRows();
        }

        private bool CanRun => !_disposed && !IsBusy && !_lifetime.IsCancellationRequested;

        private bool MatchesFilter(UserScriptBatchUpdateRow row)
            => row.Name.IndexOf(_filterTerm, StringComparison.OrdinalIgnoreCase) >= 0;

        public Task CheckAllAsync() => RunAsync(async token =>
        {
            var rows = _rows.Where(MatchesFilter).ToArray();

            foreach (var row in rows)
            {
                token.ThrowIfCancellationRequested();
                if (_rows.Contains(row)) await CheckAsync(row, token);
            }
        });

        public Task CheckRowAsync(UserScriptBatchUpdateRow row)
            => _rows.Contains(row) ? RunAsync(token => CheckAsync(row, token)) : Task.CompletedTask;

        private async Task CheckAsync(UserScriptBatchUpdateRow row, CancellationToken token)
        {
            var installation = row.Item.Installation;
            row.SetResult(null, null, null);
            row.IsChecking = true;

            try
            {
                var operation = _createOperation();
                var result = await operation.CheckAsync(installation.ScriptKey, token);

                token.ThrowIfCancellationRequested();

                if (!_rows.Contains(row))
                    return;

                if (result.ExpectedRevisionId != row.Item.Installation.RevisionId)
                {
                    MarkStale(row);
                    return;
                }

                string message = null;

                if (result.Status == UserScriptUpdateStatus.NotConfigured)
                    message = _text("userScriptBatchUpdate_notConfigured");

                else if (result.Status == UserScriptUpdateStatus.Disabled)
                    message = _text("userScriptBatchUpdate_disabled");

                row.SetResult(result, operation.GetCheckedVersion(result), message);
            }
            catch (Exception error) when (!token.IsCancellationRequested)
            {
                if (UserScriptUpdateOperation.IsStale(error))
                {
                    MarkStale(row);
                }
                else
                {
                    row.SetResult(null, null, _text("userScriptBatchUpdate_checkFailed"), error.Message);
                }
            }
            finally
            {
                row.IsChecking = false;
            }
        }

        public Task UpdateRowAsync(UserScriptBatchUpdateRow row)
        {
            if (row?.HasUpdate != true || !_rows.Contains(row))
                return Task.CompletedTask;

            return RunAsync(async token =>
            {
                var check = row.Check;

                if (!EnsureCurrent(row, check))
                    return;

                var operation = _createOperation();

                try
                {
                    var metadata = await operation.PrepareAsync(row.Item.Installation, check, token);
                    token.ThrowIfCancellationRequested();

                    if (!EnsureCurrent(row, check))
                        return;

                    var warning = row.Item.IsDirty ? _text("userScriptBatchUpdate_overwriteEdits") : null;

                    if (ConfirmRow?.Invoke(metadata, check.DownloadUrl, warning, token) != true)
                        return;

                    await ApplyAsync(row, check, operation, token);
                }
                catch (Exception error) when (!token.IsCancellationRequested)
                {
                    MarkApplyFailure(row, check, error);
                }
            });
        }

        public Task UpdateAllAsync() => RunAsync(async token =>
        {
            var pending = _rows.Where(row => MatchesFilter(row) && row.HasUpdate && EnsureCurrent(row, row.Check))
                .Select(row => new { Row = row, Check = row.Check }).ToArray();

            if (pending.Length == 0)
                return;

            var entries = pending.Select(entry =>
            {
                var row = entry.Row;
                var versions = $"{row.Name} ({row.CurrentVersion} → {row.LatestVersion})";
                return row.Item.IsDirty ? versions + "\n" + _text("userScriptBatchUpdate_overwriteEdits") : versions;
            });

            var message = _text("userScriptBatchUpdate_confirmAll") + "\n\n" + string.Join("\n\n", entries);

            if (ConfirmAll?.Invoke(message, token) != true)
                return;

            int succeeded = 0;

            foreach (var entry in pending)
            {
                if (await ApplyAsync(entry.Row, entry.Check, _createOperation(), token))
                    succeeded++;
            }

            Summary = string.Format(_text("userScriptBatchUpdate_summary"), succeeded, pending.Length - succeeded);
        });

        private async Task<bool> ApplyAsync(UserScriptBatchUpdateRow row, UserScriptUpdateCheckResult check,
            UserScriptUpdateOperation operation, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (!EnsureCurrent(row, check))
                return false;

            row.SetResult(check, check.AvailableVersion, _text("userScriptManager_working"));

            UserScriptInstallation installation;

            try
            {
                installation = await operation.ApplyAsync(check, token);
            }
            catch (Exception error) when (!token.IsCancellationRequested)
            {
                MarkApplyFailure(row, check, error);
                return false;
            }

            _acceptUpdate(row.Item, check.ExpectedRevisionId, installation);

            if (!_disposed && row.Item.Installation.RevisionId == installation.RevisionId)
                row.SetResult(null, row.CurrentVersion, _text("userScriptBatchUpdate_updated"));

            return true;
        }

        private bool EnsureCurrent(UserScriptBatchUpdateRow row, UserScriptUpdateCheckResult check)
        {
            if (!_rows.Contains(row))
                return false;

            if (row.Item.Installation.RevisionId == check.ExpectedRevisionId
                && DateTimeOffset.UtcNow - check.CheckedAt <= UserScriptUpdateService.CheckResultLifetime) return true;

            MarkStale(row);
            return false;
        }

        private void MarkStale(UserScriptBatchUpdateRow row)
            => row.SetResult(null, null, _text("userScriptBatchUpdate_recheck"));

        private void MarkApplyFailure(UserScriptBatchUpdateRow row, UserScriptUpdateCheckResult check, Exception error)
        {
            if (UserScriptUpdateOperation.IsStale(error))
            {
                MarkStale(row);
            }
            else if (EnsureCurrent(row, check))
            {
                row.SetResult(check, check.AvailableVersion, _text("userScriptBatchUpdate_applyFailed"), error.Message);
            }
        }

        private async Task RunAsync(Func<CancellationToken, Task> operation)
        {
            if (!CanRun)
                return;

            var token = _lifetime.Token;
            IsBusy = true;
            Summary = string.Empty;

            try
            {
                await operation(token);
            }
            catch (Exception) when (token.IsCancellationRequested)
            {
            }
            finally
            {
                IsBusy = false;
                if (_disposed) _lifetime.Dispose();
            }
        }

        private void ScriptsChanged(object sender, NotifyCollectionChangedEventArgs e)
            => SynchronizeRows();

        private void SynchronizeRows()
        {
            var installed = _scripts.Where(item => item.IsInstalled).ToArray();

            foreach (var removed in _rows.Where(row => !installed.Contains(row.Item)).ToArray())
            {
                removed.Item.PropertyChanged -= InstallationChanged;
                _rows.Remove(removed);
            }

            for (int index = 0; index < installed.Length; index++)
            {
                var item = installed[index];
                var existing = _rows.FirstOrDefault(row => row.Item == item);

                if (existing == null)
                {
                    item.PropertyChanged += InstallationChanged;
                    _rows.Insert(index, new UserScriptBatchUpdateRow(item, _text("userScriptBatchUpdate_check")));
                }
                else if (_rows[index] != existing)
                {
                    _rows.Move(_rows.IndexOf(existing), index);
                }
            }

            UpdateCommands();
        }

        private void InstallationChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(UserScriptItemViewModel.IsInstalled))
                return;

            var row = _rows.FirstOrDefault(value => value.Item == sender);

            if (row == null || !row.RefreshInstallation())
                return;

            if (row.Check != null)
            {
                MarkStale(row);
            }
            else
            {
                row.SetResult(null, null, null);
            }

            _rowsView?.Refresh();
            UpdateCommands();
        }

        private void UpdateCommands()
        {
            CheckAllCommand.RaiseCanExecuteChanged();
            UpdateAllCommand.RaiseCanExecuteChanged();
            CheckRowCommand.RaiseCanExecuteChanged();
            UpdateRowCommand.RaiseCanExecuteChanged();
        }

        public void Cancel() => _lifetime.Cancel();

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _scripts.CollectionChanged -= ScriptsChanged;

            foreach (var row in _rows)
                row.Item.PropertyChanged -= InstallationChanged;

            _lifetime.Cancel();
            if (!IsBusy) _lifetime.Dispose();
        }
    }

    internal sealed class UserScriptBatchUpdateRow : ViewModelBase
    {
        private readonly string _checkLabel;
        private UserScriptInstallation _installation;

        internal UserScriptItemViewModel Item { get; }
        internal UserScriptUpdateCheckResult Check { get; private set; }
        internal bool HasUpdate => Check?.Status == UserScriptUpdateStatus.Available;

        public string Name => Item.Name;
        public string CurrentVersion => Item.Version;

        private bool _isChecking;
        public bool IsChecking
        {
            get => _isChecking;
            internal set => UpdateValue(ref _isChecking, value);
        }

        private string _latestVersion;
        public string LatestVersion => _latestVersion ?? _checkLabel;

        private string _status;
        public string Status => _status;
        public bool HasStatus => !string.IsNullOrEmpty(_status);

        private string _statusDetail;
        public string StatusDetail => _statusDetail;

        public UserScriptBatchUpdateRow(UserScriptItemViewModel item, string checkLabel)
        {
            Item = item;
            _installation = item.Installation;
            _checkLabel = checkLabel;
        }

        internal bool RefreshInstallation()
        {
            if (_installation.RevisionId == Item.Installation.RevisionId)
                return false;

            _installation = Item.Installation;
            RaisePropertyChanged(nameof(Name));
            RaisePropertyChanged(nameof(CurrentVersion));
            return true;
        }

        internal void SetResult(UserScriptUpdateCheckResult check, string latestVersion, string status, string detail = null)
        {
            Check = check;
            UpdateValue(ref _latestVersion, latestVersion, nameof(LatestVersion));
            UpdateValue(ref _statusDetail, detail, nameof(StatusDetail));

            if (UpdateValue(ref _status, status, nameof(Status)))
            {
                RaisePropertyChanged(nameof(HasStatus));
            }
        }
    }
}
