using CefFlashBrowser.Services.UserScripts;
using CefFlashBrowser.Infrastructure.Collections;
using Mzying2001.MonkeySharp.Core.Domain;
using Mzying2001.MonkeySharp.Core.Parsing;
using Mzying2001.MonkeySharp.Core.Repository;
using SimpleMvvm;
using SimpleMvvm.Command;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;

namespace CefFlashBrowser.ViewModels
{
    public class UserScriptManagerViewModel : ViewModelBase
    {
        private readonly Func<Task<IUserScriptRepository>> _initialize;
        private readonly Func<string, string> _text;
        private readonly Action<Action> _dispatch;
        private readonly Func<string, CancellationToken, Task<string>> _fetchSource;

        private IUserScriptRepository _repository;
        private CancellationTokenSource _lifetime;
        private Task _pendingOperation = Task.CompletedTask;
        private int _openGeneration;
        private UserScriptItemViewModel _deletingItem;

        public ObservableCollection<UserScriptItemViewModel> Scripts { get; }

        private ListCollectionView _scriptsView;
        public ListCollectionView ScriptsView
        {
            get
            {
                if (_scriptsView == null)
                {
                    _scriptsView = new ListCollectionView(Scripts);
                    _scriptsView.LiveFilteringProperties.Add(nameof(UserScriptItemViewModel.Name));
                    RefreshScriptsView();
                }

                return _scriptsView;
            }
        }

        public DelegateCommand NewCommand { get; }
        public DelegateCommand SaveCommand { get; }
        public DelegateCommand InstallCommand { get; }
        public DelegateCommand ToggleCommand { get; }
        public DelegateCommand MoveUpCommand { get; }
        public DelegateCommand MoveDownCommand { get; }
        public DelegateCommand MoveToFirstCommand { get; }
        public DelegateCommand MoveToLastCommand { get; }
        public DelegateCommand ReorderCommand { get; }
        public Predicate<object> CanReorderItem { get; }
        public DelegateCommand DeleteCommand { get; }
        public DelegateCommand<UserScriptItemViewModel> ShowDetailsCommand { get; }
        public DelegateCommand ReloadCommand { get; }
        public DelegateCommand LoadUrlCommand { get; }
        public DelegateCommand ShowUrlCommand { get; }
        public DelegateCommand HideUrlCommand { get; }

        public Func<UserScriptMetadata, string, bool, bool> ConfirmInstallation { private get; set; }
        public Func<string, bool> ConfirmDeletion { private get; set; }
        public Action<UserScriptItemViewModel> ShowDetails { private get; set; }

        private string _filterText = string.Empty;
        private string _filterTerm = string.Empty;
        public string FilterText
        {
            get => _filterText;
            set
            {
                if (!UpdateValue(ref _filterText, value ?? string.Empty))
                    return;

                var term = _filterText.Trim();

                if (string.Equals(_filterTerm, term, StringComparison.OrdinalIgnoreCase))
                    return;

                var wasFiltering = IsFiltering;
                _filterTerm = term;

                if (wasFiltering != IsFiltering)
                {
                    RaisePropertyChanged(nameof(IsReorderingEnabled));
                    UpdateMoveCommands();
                }

                if (SelectedScript != null && !MatchesFilter(SelectedScript))
                    SelectedScript = null;

                RefreshScriptsView();
            }
        }

        private bool IsFiltering => _filterTerm.Length > 0;
        public bool IsReorderingEnabled => !IsFiltering;

        private string _installUrl;
        public string InstallUrl
        {
            get => _installUrl;
            set => UpdateValue(ref _installUrl, value);
        }

        private bool _showUrlInput;
        public bool ShowUrlInput
        {
            get => _showUrlInput;
            set => UpdateValue(ref _showUrlInput, value);
        }

        private UserScriptItemViewModel _selectedScript;
        public UserScriptItemViewModel SelectedScript
        {
            get => _selectedScript;
            set
            {
                UpdateValue(ref _selectedScript, value);
                UpdateCommands();
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                UpdateValue(ref _isBusy, value);
                UpdateCommands();
            }
        }

        private string _status;
        public string Status
        {
            get => _status;
            private set => UpdateValue(ref _status,
                value == null ? null : Regex.Replace(value, @"[\r\n\v\f\u0085\u2028\u2029]+", " "));
        }

        public UserScriptManagerViewModel(
            Func<Task<IUserScriptRepository>> initialize,
            Func<string, string> text,
            Action<Action> dispatch,
            Func<string, CancellationToken, Task<string>> fetchSource = null)
        {
            _initialize = initialize;
            _text = text;
            _dispatch = dispatch;
            _fetchSource = fetchSource;

            Scripts = new ObservableCollection<UserScriptItemViewModel>();
            Scripts.CollectionChanged += (_, __) => UpdateMoveCommands();

            LoadUrlCommand = new DelegateCommand(async () => await ImportUrlAsync(InstallUrl)) { CanExecuteFunc = _ => !IsBusy && _repository != null };
            ShowUrlCommand = new DelegateCommand(() => ShowUrlInput = true) { CanExecuteFunc = _ => !IsBusy };
            HideUrlCommand = new DelegateCommand(() => ShowUrlInput = false);
            NewCommand = new DelegateCommand(NewScript) { CanExecuteFunc = _ => !IsBusy };
            InstallCommand = new DelegateCommand(async () => await SaveAsync()) { CanExecuteFunc = _ => CanEdit() && !SelectedScript.IsInstalled };
            SaveCommand = new DelegateCommand(async () => await SaveAsync()) { CanExecuteFunc = _ => CanEdit() };
            ToggleCommand = new DelegateCommand<UserScriptItemViewModel>(async item => await ToggleAsync(item)) { CanExecuteFunc = item => !IsBusy && _repository != null && (item as UserScriptItemViewModel)?.IsInstalled == true };
            DeleteCommand = new DelegateCommand(async () => await DeleteAsync()) { CanExecuteFunc = _ => CanEdit() };
            ShowDetailsCommand = new DelegateCommand<UserScriptItemViewModel>(item => ShowDetails?.Invoke(item)) { CanExecuteFunc = item => !IsBusy && item is UserScriptItemViewModel };
            MoveUpCommand = CreateMoveCommand(UserScriptMove.Up);
            MoveDownCommand = CreateMoveCommand(UserScriptMove.Down);
            MoveToFirstCommand = CreateMoveCommand(UserScriptMove.First);
            MoveToLastCommand = CreateMoveCommand(UserScriptMove.Last);
            CanReorderItem = item => item is UserScriptItemViewModel script && CanDragScript(script);
            ReorderCommand = new DelegateCommand<ListReorderRequest>(ExecuteReorder) { CanExecuteFunc = CanExecuteReorder };
            ReloadCommand = new DelegateCommand(async () => await ReloadAsync()) { CanExecuteFunc = _ => CanEdit() && SelectedScript.IsInstalled };
        }

        public async Task OpenAsync()
        {
            if (_lifetime != null) return;

            var generation = ++_openGeneration;

            await _pendingOperation;
            if (generation != _openGeneration) return;

            _lifetime = new CancellationTokenSource();

            await RunAsync(async token =>
            {
                var repository = await _initialize();
                token.ThrowIfCancellationRequested();

                _repository = repository;
                _repository.Changed += RepositoryChanged;

                var snapshot = await _repository.GetSnapshotAsync(token);

                foreach (var removed in Scripts.Where(item => item.IsInstalled
                    && !snapshot.Any(value => value.ScriptKey == item.Installation.ScriptKey)).ToArray())
                {
                    if (removed.IsDirty)
                        KeepDraft(removed);
                    RemoveItem(removed);
                }

                foreach (var installation in snapshot)
                {
                    Synchronize(installation, false);
                }

                ApplyInstalledOrder(snapshot);
                SelectedScript = SelectedScript ?? Scripts.FirstOrDefault(MatchesFilter);
            });
        }

        public void Close()
        {
            if (_repository != null)
                _repository.Changed -= RepositoryChanged;

            ++_openGeneration;

            var lifetime = _lifetime;
            _lifetime = null;
            lifetime?.Cancel();

            FilterText = string.Empty;

            if (lifetime != null)
            {
                _pendingOperation.ContinueWith(_ => lifetime.Dispose(), TaskScheduler.Default);
            }
        }

        public void NewScript()
        {
            var name = _text("userScriptManager_newScript");

            var source =
                "// ==UserScript==\r\n" +
                "// @name         " + name + "\r\n" +
                "// @namespace    CefFlashBrowser\r\n" +
                "// @version      1.0.0\r\n" +
                "// @match        https://example.com/*\r\n" +
                "// @run-at       document-idle\r\n" +
                "// @sandbox      DOM\r\n" +
                "// @grant        none\r\n" +
                "// ==/UserScript==\r\n" +
                "\r\n" +
                "(function() {\r\n" +
                "  'use strict';\r\n" +
                "  \r\n" +
                "})();\r\n";

            var item = new UserScriptItemViewModel(source, null, name, false);
            Scripts.Add(item);
            SelectScriptForEditing(item);
        }

        private bool MatchesFilter(UserScriptItemViewModel item)
            => !IsFiltering || item.Name?.IndexOf(_filterTerm, StringComparison.OrdinalIgnoreCase) >= 0;

        private void RefreshScriptsView()
        {
            if (_scriptsView == null)
                return;

            using (_scriptsView.DeferRefresh())
            {
                _scriptsView.Filter = IsFiltering
                    ? new Predicate<object>(item => MatchesFilter((UserScriptItemViewModel)item))
                    : null;
                _scriptsView.IsLiveFiltering = IsFiltering;
            }
        }

        private void SelectScriptForEditing(UserScriptItemViewModel item)
        {
            if (!MatchesFilter(item))
            {
                FilterText = string.Empty;
            }
            else if (_scriptsView != null && !_scriptsView.Contains(item))
            {
                // An import can rename a hidden script before live filtering runs.
                _scriptsView.Refresh();
            }

            SelectedScript = item;
        }

        public Task SaveAsync()
        {
            var item = SelectedScript;
            if (item == null) return Task.CompletedTask;

            return RunAsync(async token =>
            {
                var source = item.Source;
                var previous = item.Installation;
                var revision = item.EditorRevision;
                var origin = item.Origin;

                UserScriptSourceText.Validate(source);

                var parsed = new UserScriptMetadataParser().Parse(source);

                if (!parsed.CanEnable)
                {
                    Status = _text("userScriptManager_invalidMetadata");
                    return;
                }

                token.ThrowIfCancellationRequested();

                UserScriptInstallation installation;

                if (previous == null && item.RequiresConfirmation)
                {
                    installation = await SaveImportAsync(parsed.Metadata, source, origin, token);
                    if (installation == null) return;
                }
                else
                {
                    installation = previous == null
                        ? await _repository.InstallAsync(source, origin, true, token)
                        : await _repository.UpdateIfUnchangedAsync(previous.ScriptKey, revision, source, origin, token);
                }

                AcceptSave(item, installation, source);

                if (previous == null)
                    await RefreshInstalledOrderAsync(token);

                Status = _text("userScriptManager_saved");
                UpdateCommands();
            });
        }

        private async Task<UserScriptInstallation> SaveImportAsync(UserScriptMetadata metadata, string source, string origin,
            CancellationToken token)
        {
            var matches = await UserScriptSourceInstaller.FindMatchesAsync(_repository, origin, token);
            token.ThrowIfCancellationRequested();

            if (matches.Length > 1)
            {
                Status = _text("userScriptInstall_conflict");
                return null;
            }

            return await ConfirmAndCommitImportAsync(metadata, matches.SingleOrDefault(), source, origin, token);
        }

        private async Task<UserScriptInstallation> ConfirmAndCommitImportAsync(UserScriptMetadata metadata,
            UserScriptInstallation previous, string source, string origin, CancellationToken token)
        {
            if (ConfirmInstallation?.Invoke(metadata, origin, previous != null) != true)
            {
                Status = _text("userScriptManager_cancelled");
                return null;
            }

            token.ThrowIfCancellationRequested();
            var installation = await UserScriptSourceInstaller.TryCommitAsync(_repository, previous, source, origin, token);

            if (installation == null)
                Status = _text("userScriptInstall_conflict");

            return installation;
        }

        public Task ImportFileAsync(string path)
        {
            return RunAsync(async token =>
            {
                var origin = Path.GetFullPath(path);
                var source = await UserScriptSourceText.ReadAsync(origin, token);
                await ImportSourceAsync(source, origin, token);
            });
        }

        public Task ImportUrlAsync(string url)
        {
            return RunAsync(async token =>
            {
                if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)
                    || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    throw new InvalidOperationException(_text("userScriptManager_invalidUrl"));
                }

                var source = await _fetchSource(uri.AbsoluteUri, token);
                token.ThrowIfCancellationRequested();

                UserScriptSourceText.Validate(source);
                await ImportSourceAsync(source, uri.AbsoluteUri, token);
                ShowUrlInput = false;
            });
        }

        private async Task ImportSourceAsync(string source, string origin, CancellationToken token)
        {
            var matches = await UserScriptSourceInstaller.FindMatchesAsync(_repository, origin, token);
            token.ThrowIfCancellationRequested();

            if (matches.Length > 1)
            {
                Status = _text("userScriptInstall_conflict");
                return;
            }

            var parsed = new UserScriptMetadataParser().Parse(source);
            var previous = matches.SingleOrDefault();
            if (previous == null)
            {
                var draft = new UserScriptItemViewModel(source, origin, parsed.Metadata?.Name ?? origin, true);
                Scripts.Add(draft);
                SelectScriptForEditing(draft);
                Status = _text("userScriptManager_reviewInstall");
                return;
            }

            if (!parsed.CanEnable)
            {
                Status = _text("userScriptManager_invalidMetadata");
                return;
            }

            var installation = await ConfirmAndCommitImportAsync(parsed.Metadata, previous, source, origin, token);
            if (installation == null) return;

            var item = Scripts.FirstOrDefault(value => value.Installation?.ScriptKey == installation.ScriptKey);
            if (item == null)
            {
                item = new UserScriptItemViewModel(installation);
                InsertInstalledItem(item);
                await RefreshInstalledOrderAsync(token);
            }
            else
            {
                item.AcceptSave(installation, installation.Definition.Source);
                item.Source = installation.Definition.Source;
            }

            SelectScriptForEditing(item);
            Status = _text("userScriptManager_saved");
        }

        public Task ExportAsync(string path)
        {
            var source = SelectedScript?.Source;
            if (source == null) return Task.CompletedTask;

            return RunAsync(async token =>
            {
                await UserScriptSourceText.WriteAsync(path, source, token);
                Status = _text("userScriptManager_exported");
            });
        }

        public Task ToggleAsync(UserScriptItemViewModel item)
        {
            if (item?.IsInstalled != true) return Task.CompletedTask;

            return RunAsync(async token =>
            {
                try
                {
                    var installation = await _repository.SetEnabledAsync(item.Installation.ScriptKey, !item.IsEnabled, token);
                    item.Synchronize(installation, true);
                }
                finally
                {
                    item.RefreshEnabled();
                }
                Status = _text("userScriptManager_refreshPage");
            });
        }

        public Task DeleteAsync()
        {
            var item = SelectedScript;
            if (item == null) return Task.CompletedTask;

            return RunAsync(async token =>
            {
                if (ConfirmDeletion?.Invoke(string.Format(_text("userScriptManager_confirmDelete"), item.Name)) != true)
                {
                    Status = _text("userScriptManager_cancelled");
                    return;
                }

                token.ThrowIfCancellationRequested();
                if (!Scripts.Contains(item)) return;

                _deletingItem = item;

                try
                {
                    if (item.IsInstalled)
                        await _repository.RemoveAsync(item.Installation.ScriptKey, token);

                    RemoveItem(item);
                }
                finally
                {
                    _deletingItem = null;
                }

                Status = _text("userScriptManager_deleted");
            });
        }

        private DelegateCommand CreateMoveCommand(UserScriptMove move)
        {
            return new DelegateCommand(async () => await MoveAsync(SelectedScript, move))
            {
                CanExecuteFunc = _ => CanMove(SelectedScript, move)
            };
        }

        private bool CanMove(UserScriptItemViewModel item, UserScriptMove move)
        {
            var index = GetReorderableIndex(item);
            if (index < 0)
                return false;

            return move == UserScriptMove.Up || move == UserScriptMove.First
                ? index > 0 && Scripts[index - 1].IsInstalled
                : index + 1 < Scripts.Count && Scripts[index + 1].IsInstalled;
        }

        private int GetReorderableIndex(UserScriptItemViewModel item)
            => IsReorderingEnabled && !IsBusy && _lifetime != null && _repository is OrderedUserScriptRepository && item?.IsInstalled == true
                ? Scripts.IndexOf(item) : -1;

        internal bool CanDragScript(UserScriptItemViewModel item)
            => CanMove(item, UserScriptMove.Up) || CanMove(item, UserScriptMove.Down);

        private bool CanExecuteReorder(object value)
        {
            return value is ListReorderRequest request
                && request.Item is UserScriptItemViewModel item
                && request.TargetItem is UserScriptItemViewModel target
                && CanMoveRelative(item, target, request.InsertAfter);
        }

        private async void ExecuteReorder(ListReorderRequest request)
        {
            await MoveRelativeAsync(request?.Item as UserScriptItemViewModel,
                request?.TargetItem as UserScriptItemViewModel, request?.InsertAfter == true);
        }

        internal bool CanMoveRelative(UserScriptItemViewModel item, UserScriptItemViewModel target, bool after)
        {
            var index = GetReorderableIndex(item);
            var destination = OrderedUserScriptRepository.GetRelativeDestination(index, GetReorderableIndex(target), after);
            return destination >= 0 && destination != index;
        }

        internal Task MoveAsync(UserScriptItemViewModel item, UserScriptMove move)
        {
            if (!CanMove(item, move))
                return Task.CompletedTask;

            var repository = (OrderedUserScriptRepository)_repository;
            var key = item.Installation.ScriptKey;
            return CommitMoveAsync(token => repository.MoveAsync(key, move, token));
        }

        internal Task MoveRelativeAsync(UserScriptItemViewModel item, UserScriptItemViewModel target, bool after)
        {
            if (!CanMoveRelative(item, target, after))
                return Task.CompletedTask;

            var repository = (OrderedUserScriptRepository)_repository;
            var key = item.Installation.ScriptKey;
            var targetKey = target.Installation.ScriptKey;
            return CommitMoveAsync(token => repository.MoveRelativeAsync(key, targetKey, after, token));
        }

        private Task CommitMoveAsync(Func<CancellationToken, Task<bool>> move)
        {
            return RunAsync(async token =>
            {
                if (await move(token))
                {
                    await RefreshInstalledOrderAsync(token);
                    Status = _text("userScriptManager_refreshPage");
                }
            });
        }

        public Task ReloadAsync()
        {
            var item = SelectedScript;
            if (item?.IsInstalled != true) return Task.CompletedTask;

            return RunAsync(async token =>
            {
                var source = item.Source;

                if (item.IsDirty)
                    KeepDraft(item);

                var installation = await _repository.ReloadAsync(item.Installation.ScriptKey, token);

                if (item.Source != source && item.IsDirty)
                    KeepDraft(item);

                item.AcceptSave(installation, installation.Definition.Source);
                item.Source = installation.Definition.Source;
                Status = _text("userScriptManager_refreshPage");
            });
        }

        private void AcceptSave(UserScriptItemViewModel item, UserScriptInstallation installation, string source)
        {
            var duplicate = Scripts.FirstOrDefault(value => value != item && value.Installation?.ScriptKey == installation.ScriptKey);

            if (duplicate != null)
            {
                if (duplicate.IsDirty) KeepDraft(duplicate, item.RequiresConfirmation);
                Scripts.Remove(duplicate);
            }

            item.AcceptSave(installation, source);
        }

        private void RepositoryChanged(object sender, UserScriptRepositoryChangedEventArgs e)
        {
            var lifetime = _lifetime;

            _dispatch(() =>
            {
                if (lifetime == null || lifetime != _lifetime) return;

                if (e.Kind == RepositoryChangeKind.Removed)
                {
                    var item = Scripts.FirstOrDefault(value => value.Installation?.ScriptKey == e.Installation.ScriptKey);
                    if (item != null)
                    {
                        if (item.IsDirty && item != _deletingItem) KeepDraft(item);
                        RemoveItem(item);
                    }
                }
                else
                {
                    Synchronize(e.Installation, e.Kind == RepositoryChangeKind.Enabled || e.Kind == RepositoryChangeKind.Disabled);
                }
            });
        }

        private void Synchronize(UserScriptInstallation installation, bool enabledChanged)
        {
            var item = Scripts.FirstOrDefault(value => value.Installation?.ScriptKey == installation.ScriptKey);

            if (item == null)
            {
                InsertInstalledItem(new UserScriptItemViewModel(installation));
            }
            else
            {
                item.Synchronize(installation, enabledChanged);
            }
        }

        private void InsertInstalledItem(UserScriptItemViewModel item)
            => Scripts.Insert(Scripts.TakeWhile(value => value.IsInstalled).Count(), item);

        private async Task RefreshInstalledOrderAsync(CancellationToken token)
        {
            var snapshot = await _repository.GetSnapshotAsync(token);
            token.ThrowIfCancellationRequested();
            ApplyInstalledOrder(snapshot);
        }

        private void ApplyInstalledOrder(IReadOnlyList<UserScriptInstallation> snapshot)
        {
            var ranks = snapshot.Select((installation, index) => new { installation.ScriptKey, Index = index })
                .ToDictionary(entry => entry.ScriptKey, entry => entry.Index);

            var ordered = Scripts.OrderBy(item => !item.IsInstalled)
                .ThenBy(item => item.IsInstalled && ranks.TryGetValue(item.Installation.ScriptKey, out var rank) ? rank : int.MaxValue)
                .ToArray();

            for (var index = 0; index < ordered.Length; index++)
            {
                if (Scripts[index] != ordered[index])
                    Scripts.Move(Scripts.IndexOf(ordered[index]), index);
            }
        }

        private void KeepDraft(UserScriptItemViewModel item, bool requiresConfirmation = false)
        {
            Scripts.Add(new UserScriptItemViewModel(item.Source, item.Origin, item.Name, requiresConfirmation));
        }

        private void RemoveItem(UserScriptItemViewModel item)
        {
            var index = Scripts.IndexOf(item);
            Scripts.Remove(item);

            if (SelectedScript == item)
            {
                SelectedScript = IsFiltering || Scripts.Count == 0
                    ? null : Scripts[Math.Max(0, Math.Min(index, Scripts.Count - 1))];
            }
        }

        private Task RunAsync(Func<CancellationToken, Task> operation)
        {
            if (IsBusy || _lifetime == null)
            {
                return Task.CompletedTask;
            }
            else
            {
                return _pendingOperation = RunCoreAsync(operation);
            }
        }

        private async Task RunCoreAsync(Func<CancellationToken, Task> operation)
        {
            var lifetime = _lifetime;

            IsBusy = true;
            Status = _text("userScriptManager_working");

            var workingStatus = Status;

            try
            {
                await operation(lifetime.Token);
                if (Status == workingStatus) Status = string.Empty;
            }
            catch (OperationCanceledException)
            {
                Status = _text("userScriptManager_cancelled");
            }
            catch (Exception e)
            {
                Status = e is RepositoryRevisionMismatchException || e is UserScriptSourceConflictException
                    ? _text("userScriptManager_conflict") : e.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanEdit()
        {
            return !IsBusy && _repository != null && SelectedScript != null;
        }

        private void UpdateMoveCommands()
        {
            MoveUpCommand.RaiseCanExecuteChanged();
            MoveDownCommand.RaiseCanExecuteChanged();
            MoveToFirstCommand.RaiseCanExecuteChanged();
            MoveToLastCommand.RaiseCanExecuteChanged();
            ReorderCommand.RaiseCanExecuteChanged();
        }

        private void UpdateCommands()
        {
            NewCommand?.RaiseCanExecuteChanged();
            SaveCommand?.RaiseCanExecuteChanged();
            InstallCommand?.RaiseCanExecuteChanged();
            ToggleCommand?.RaiseCanExecuteChanged();
            UpdateMoveCommands();
            DeleteCommand?.RaiseCanExecuteChanged();
            ShowDetailsCommand?.RaiseCanExecuteChanged();
            ReloadCommand?.RaiseCanExecuteChanged();
            LoadUrlCommand?.RaiseCanExecuteChanged();
            ShowUrlCommand?.RaiseCanExecuteChanged();
        }
    }
}
