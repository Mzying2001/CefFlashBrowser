using CefFlashBrowser.Services.UserScripts;
using Mzying2001.MonkeySharp.Core.Domain;
using Mzying2001.MonkeySharp.Core.Parsing;
using Mzying2001.MonkeySharp.Core.Repository;
using SimpleMvvm;
using SimpleMvvm.Command;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

        public ObservableCollection<UserScriptItemViewModel> Scripts { get; } = new ObservableCollection<UserScriptItemViewModel>();

        public DelegateCommand NewCommand { get; }
        public DelegateCommand SaveCommand { get; }
        public DelegateCommand InstallCommand { get; }
        public DelegateCommand ToggleCommand { get; }
        public DelegateCommand DeleteCommand { get; }
        public DelegateCommand ReloadCommand { get; }
        public DelegateCommand LoadUrlCommand { get; }
        public DelegateCommand ShowUrlCommand { get; }
        public DelegateCommand HideUrlCommand { get; }

        public Func<string, bool> ConfirmInstallation { private get; set; }
        public Func<string, bool> ConfirmDeletion { private get; set; }

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
            private set => UpdateValue(ref _status, value);
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

            LoadUrlCommand = new DelegateCommand(async () => await ImportUrlAsync(InstallUrl)) { CanExecuteFunc = _ => !IsBusy && _repository != null };
            ShowUrlCommand = new DelegateCommand(() => ShowUrlInput = true) { CanExecuteFunc = _ => !IsBusy };
            HideUrlCommand = new DelegateCommand(() => ShowUrlInput = false);
            NewCommand = new DelegateCommand(NewScript) { CanExecuteFunc = _ => !IsBusy };
            InstallCommand = new DelegateCommand(async () => await SaveAsync()) { CanExecuteFunc = _ => CanEdit() && !SelectedScript.IsInstalled };
            SaveCommand = new DelegateCommand(async () => await SaveAsync()) { CanExecuteFunc = _ => CanEdit() };
            ToggleCommand = new DelegateCommand<UserScriptItemViewModel>(async item => await ToggleAsync(item)) { CanExecuteFunc = item => !IsBusy && _repository != null && (item as UserScriptItemViewModel)?.IsInstalled == true };
            DeleteCommand = new DelegateCommand(async () => await DeleteAsync()) { CanExecuteFunc = _ => CanEdit() };
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

                SelectedScript = SelectedScript ?? Scripts.FirstOrDefault();
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

                if (!item.IsInstalled && item.RequiresConfirmation
                    && ConfirmInstallation?.Invoke(ConfirmationText(parsed.Metadata, item.Origin)) != true)
                {
                    Status = _text("userScriptManager_cancelled");
                    return;
                }

                token.ThrowIfCancellationRequested();

                var installation = previous == null
                    ? await _repository.InstallAsync(source, origin, true, token)
                    : await _repository.UpdateIfUnchangedAsync(previous.ScriptKey, revision, source, origin, token);

                AcceptSave(item, installation, source);

                Status = _text("userScriptManager_saved");
                UpdateCommands();
            });
        }

        public Task ImportFileAsync(string path)
        {
            return RunAsync(async token =>
            {
                var source = await UserScriptSourceText.ReadAsync(path, token);
                token.ThrowIfCancellationRequested();
                AddImportDraft(source, Path.GetFullPath(path));
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
                AddImportDraft(source, uri.AbsoluteUri);
                ShowUrlInput = false;
            });
        }

        private void AddImportDraft(string source, string origin)
        {
            var parsed = new UserScriptMetadataParser().Parse(source);
            var item = new UserScriptItemViewModel(source, origin, parsed.Metadata?.Name ?? origin, true);

            Scripts.Add(item);
            SelectedScript = item;
            Status = _text("userScriptManager_reviewInstall");
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

        private string ConfirmationText(UserScriptMetadata metadata, string origin)
        {
            return _text("userScriptManager_confirmInstall") + Environment.NewLine + Environment.NewLine
                + metadata.Name + " " + metadata.Version + Environment.NewLine + origin + Environment.NewLine
                + _text("userScriptManager_match") + ": " + string.Join(", ", metadata.Matches.Concat(metadata.Includes)) + Environment.NewLine
                + _text("userScriptManager_world") + ": " + metadata.Sandbox + " / " + metadata.InjectInto + Environment.NewLine
                + _text("userScriptManager_grants") + ": " + string.Join(", ", metadata.DeclaredGrants) + Environment.NewLine
                + "@connect: " + string.Join(", ", metadata.Connects) + Environment.NewLine
                + "@require: " + string.Join(", ", metadata.Requires.Select(item => item.Url)) + Environment.NewLine
                + "@resource: " + string.Join(", ", metadata.Resources.Select(item => item.Name + " " + item.Url));
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
                Scripts.Remove(duplicate);

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
                Scripts.Add(new UserScriptItemViewModel(installation));
            }
            else
            {
                item.Synchronize(installation, enabledChanged);
            }
        }

        private void KeepDraft(UserScriptItemViewModel item)
        {
            Scripts.Add(new UserScriptItemViewModel(item.Source, item.Origin, item.Name, false));
        }

        private void RemoveItem(UserScriptItemViewModel item)
        {
            var index = Scripts.IndexOf(item);
            Scripts.Remove(item);

            if (SelectedScript == item)
            {
                SelectedScript = Scripts.Count == 0 ? null : Scripts[Math.Max(0, Math.Min(index, Scripts.Count - 1))];
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

            try
            {
                await operation(lifetime.Token);
                if (Status == _text("userScriptManager_working")) Status = string.Empty;
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

        private void UpdateCommands()
        {
            NewCommand?.RaiseCanExecuteChanged();
            SaveCommand?.RaiseCanExecuteChanged();
            InstallCommand?.RaiseCanExecuteChanged();
            ToggleCommand?.RaiseCanExecuteChanged();
            DeleteCommand?.RaiseCanExecuteChanged();
            ReloadCommand?.RaiseCanExecuteChanged();
            LoadUrlCommand?.RaiseCanExecuteChanged();
            ShowUrlCommand?.RaiseCanExecuteChanged();
        }
    }
}
