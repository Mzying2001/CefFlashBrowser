using Mzying2001.MonkeySharp.Core.Repository;
using SimpleMvvm;
using SimpleMvvm.Command;
using System;
using System.Collections.ObjectModel;
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
        private IUserScriptRepository _repository;
        private CancellationTokenSource _lifetime;
        private Task _pendingOperation = Task.CompletedTask;
        private int _openGeneration;

        public ObservableCollection<UserScriptItemViewModel> Scripts { get; } = new ObservableCollection<UserScriptItemViewModel>();

        public DelegateCommand NewCommand { get; }
        public DelegateCommand SaveCommand { get; }
        public DelegateCommand ToggleCommand { get; }
        public DelegateCommand DeleteCommand { get; }
        public DelegateCommand ReloadCommand { get; }

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

        public UserScriptManagerViewModel(Func<Task<IUserScriptRepository>> initialize,
            Func<string, string> text, Action<Action> dispatch)
        {
            _initialize = initialize;
            _text = text;
            _dispatch = dispatch;

            NewCommand = new DelegateCommand(NewScript) { CanExecuteFunc = _ => !IsBusy };
            SaveCommand = new DelegateCommand(async () => await SaveAsync()) { CanExecuteFunc = _ => CanEdit() };
            ToggleCommand = new DelegateCommand<UserScriptItemViewModel>(async item => await ToggleAsync(item))
            { CanExecuteFunc = item => !IsBusy && _repository != null && (item as UserScriptItemViewModel)?.IsInstalled == true };
            DeleteCommand = new DelegateCommand(async () => await DeleteAsync()) { CanExecuteFunc = _ => CanEdit() };
            ReloadCommand = new DelegateCommand(async () => await ReloadAsync())
            { CanExecuteFunc = _ => CanEdit() && SelectedScript.IsInstalled };
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
                    RemoveItem(removed);

                foreach (var installation in snapshot)
                    Synchronize(installation, false);

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
                _pendingOperation.ContinueWith(_ => lifetime.Dispose(), TaskScheduler.Default);
        }

        public void NewScript()
        {
            var name = _text("userScripts_newScript");
            var source = "// ==UserScript==\r\n// @name         " + name
                + "\r\n// @namespace    CefFlashBrowser\r\n// @version      1.0.0"
                + "\r\n// @match        https://example.com/*\r\n// @run-at       document-idle"
                + "\r\n// @sandbox      DOM\r\n// @grant        none\r\n// ==/UserScript==\r\n\r\n(function() {\r\n    'use strict';\r\n\r\n})();\r\n";

            var item = new UserScriptItemViewModel(source, null, name, false);
            Scripts.Add(item);
            SelectedScript = item;
        }

        public Task SaveAsync()
        {
            var item = SelectedScript;
            if (item == null || item.RequiresConfirmation && !item.IsInstalled) return Task.CompletedTask;

            return RunAsync(async token =>
            {
                var source = item.Source;
                var installation = item.IsInstalled
                    ? await _repository.UpdateIfUnchangedAsync(item.Installation.ScriptKey, item.EditorRevision, source, item.Origin, token)
                    : await _repository.InstallAsync(source, item.Origin, true, token);

                AcceptSave(item, installation, source);
                Status = _text("userScripts_saved");
            });
        }

        public Task ToggleAsync(UserScriptItemViewModel item)
        {
            if (item?.IsInstalled != true) return Task.CompletedTask;

            return RunAsync(async token =>
            {
                var installation = await _repository.SetEnabledAsync(item.Installation.ScriptKey, !item.IsEnabled, token);
                item.Synchronize(installation, true);
                Status = _text("userScripts_refreshPage");
            });
        }

        public Task DeleteAsync()
        {
            var item = SelectedScript;
            if (item == null) return Task.CompletedTask;

            return RunAsync(async token =>
            {
                if (item.IsInstalled)
                    await _repository.RemoveAsync(item.Installation.ScriptKey, token);

                RemoveItem(item);
                Status = _text("userScripts_deleted");
            });
        }

        public Task ReloadAsync()
        {
            var item = SelectedScript;
            if (item?.IsInstalled != true) return Task.CompletedTask;

            return RunAsync(async token =>
            {
                if (item.IsDirty)
                    Scripts.Add(new UserScriptItemViewModel(item.Source, item.Origin, item.Name, false));

                var installation = await _repository.ReloadAsync(item.Installation.ScriptKey, token);
                item.AcceptSave(installation, installation.Definition.Source);
                item.Source = installation.Definition.Source;
                Status = _text("userScripts_refreshPage");
            });
        }

        private void AcceptSave(UserScriptItemViewModel item, Mzying2001.MonkeySharp.Core.Domain.UserScriptInstallation installation, string source)
        {
            var duplicate = Scripts.FirstOrDefault(value => value != item && value.Installation?.ScriptKey == installation.ScriptKey);
            if (duplicate != null) Scripts.Remove(duplicate);

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
                    if (item != null) RemoveItem(item);
                }
                else
                {
                    Synchronize(e.Installation, e.Kind == RepositoryChangeKind.Enabled || e.Kind == RepositoryChangeKind.Disabled);
                }
            });
        }

        private void Synchronize(Mzying2001.MonkeySharp.Core.Domain.UserScriptInstallation installation, bool enabledChanged)
        {
            var item = Scripts.FirstOrDefault(value => value.Installation?.ScriptKey == installation.ScriptKey);
            if (item == null) Scripts.Add(new UserScriptItemViewModel(installation));
            else item.Synchronize(installation, enabledChanged);
        }

        private void RemoveItem(UserScriptItemViewModel item)
        {
            var index = Scripts.IndexOf(item);
            Scripts.Remove(item);
            if (SelectedScript == item)
                SelectedScript = Scripts.Count == 0 ? null : Scripts[Math.Max(0, Math.Min(index, Scripts.Count - 1))];
        }

        private Task RunAsync(Func<CancellationToken, Task> operation)
        {
            if (IsBusy || _lifetime == null) return Task.CompletedTask;

            return _pendingOperation = RunCoreAsync(operation);
        }

        private async Task RunCoreAsync(Func<CancellationToken, Task> operation)
        {

            var lifetime = _lifetime;
            IsBusy = true;
            Status = _text("userScripts_working");

            try
            {
                await operation(lifetime.Token);
                if (Status == _text("userScripts_working")) Status = string.Empty;
            }
            catch (OperationCanceledException)
            {
                Status = _text("userScripts_cancelled");
            }
            catch (Exception e)
            {
                Status = e is RepositoryRevisionMismatchException || e is UserScriptSourceConflictException
                    ? _text("userScripts_conflict") : e.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanEdit() => !IsBusy && _repository != null && SelectedScript != null;

        private void UpdateCommands()
        {
            NewCommand?.RaiseCanExecuteChanged();
            SaveCommand?.RaiseCanExecuteChanged();
            ToggleCommand?.RaiseCanExecuteChanged();
            DeleteCommand?.RaiseCanExecuteChanged();
            ReloadCommand?.RaiseCanExecuteChanged();
        }
    }
}
