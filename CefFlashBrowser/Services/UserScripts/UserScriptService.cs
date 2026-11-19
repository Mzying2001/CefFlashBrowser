using CefFlashBrowser.Models.UserScripts;
using CefFlashBrowser.Services.UserScripts.Persistence;
using Mzying2001.MonkeySharp.Core.Domain;
using Mzying2001.MonkeySharp.Core.Matching;
using Mzying2001.MonkeySharp.Core.Permissions;
using Mzying2001.MonkeySharp.Core.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Services.UserScripts
{
    public sealed class UserScriptService
    {
        private readonly object _lock = new object();
        private readonly UserScriptStorage _storage;
        private readonly SqliteUserScriptRepositoryPersistence _persistence;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly Dictionary<string, Func<Task>> _sessions = new Dictionary<string, Func<Task>>();
        private readonly Action<string, Exception> _report;
        private Task _initializeTask;
        private Task _stopTask;
        private IUserScriptRepository _repository;

        public UserScriptContentService Content { get; }

        public SqliteUserScriptValueStore ValueStore { get; }

        public SqliteUserScriptPermissionDecisionStore Decisions { get; }

        public InteractiveUserScriptPermissionPolicy PermissionPolicy { get; }

        public Exception InitializationError { get; private set; }

        public IUserScriptRepository Repository
        {
            get
            {
                lock (_lock)
                    return _repository ?? throw new InvalidOperationException("The userscript repository is not initialized.");
            }
        }

        public UserScriptService(UserScriptStorage storage, string sourceRoot, string cacheRoot, UserScriptContentService content,
            IUserScriptPermissionPrompt prompt, Action<string, Exception> report = null)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            Content = content ?? throw new ArgumentNullException(nameof(content));
            _report = report;
            _persistence = new SqliteUserScriptRepositoryPersistence(storage, sourceRoot, cacheRoot, report);
            ValueStore = new SqliteUserScriptValueStore(storage, report: report);
            Decisions = new SqliteUserScriptPermissionDecisionStore(storage);
            PermissionPolicy = new InteractiveUserScriptPermissionPolicy(new SensitiveUserScriptPermissionsPolicy(), Decisions, prompt);
        }

        public SqliteUserScriptTabStateService CreateTabStateService(string browserId)
        {
            lock (_lock)
                ThrowIfStopping();

            return new SqliteUserScriptTabStateService(_storage, browserId);
        }

        public async Task<IReadOnlyList<UserScriptPagePermission>> GetPagePermissionsAsync(Uri page, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (page == null || !page.IsAbsoluteUri)
                return Array.Empty<UserScriptPagePermission>();

            await InitializeAsync().ConfigureAwait(false);

            var scripts = await Repository.GetSnapshotAsync(token).ConfigureAwait(false);
            var matchingScripts = scripts.Where(script => MatchesPermissionPage(script.Definition.Metadata, page))
                .ToDictionary(script => script.ScriptKey);

            if (matchingScripts.Count == 0)
                return Array.Empty<UserScriptPagePermission>();

            var records = await Decisions.GetForScriptsAsync(matchingScripts.Keys, token).ConfigureAwait(false);

            UserScriptPagePermission selector(UserScriptPermissionRecord record)
            {
                var script = matchingScripts[ScriptKey.Parse(record.Scope.ScriptKey)];
                return new UserScriptPagePermission(script.Definition.Metadata.Name, !script.IsEnabled, record);
            }

            return records
                .Select(selector)
                .OrderBy(item => item.ScriptName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.Record.Scope.ScriptKey, StringComparer.Ordinal)
                .ThenBy(item => item.Record.Scope.Kind, StringComparer.Ordinal)
                .ThenBy(item => item.Record.Scope.Target, StringComparer.Ordinal)
                .ToList().AsReadOnly();
        }

        private static bool MatchesPermissionPage(UserScriptMetadata metadata, Uri page)
        {
            if (metadata == null)
                return false;

            try
            {
                return UserScriptUrlRules.Compile(metadata).IsMatch(page, metadata.RunAt == UserScriptRunAt.ContextMenu);
            }
            catch (Exception error) when (error is FormatException || error is ArgumentException || error is RegexMatchTimeoutException)
            {
                return false;
            }
        }

        public Task InitializeAsync()
        {
            lock (_lock)
            {
                ThrowIfStopping();

                if (_initializeTask == null || _initializeTask.IsFaulted || _initializeTask.IsCanceled)
                    _initializeTask = Task.Run(() => InitializeCoreAsync(_lifetime.Token));

                return _initializeTask;
            }
        }

        private async Task InitializeCoreAsync(CancellationToken token)
        {
            // Do not execute initialization or completion callbacks under the service lock.
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime.Token))
            {
                try
                {
                    await _storage.RunAsync(() => _storage.Database.DeleteUserScriptTabStatesAsync(
                        null, linked.Token), linked.Token).ConfigureAwait(false);

                    var repository = new UserScriptRepository(_persistence,
                        preparer: new UserScriptPreparer(Content, Content));

                    await repository.InitializeAsync(linked.Token).ConfigureAwait(false);
                    await _persistence.CollectAsync(linked.Token).ConfigureAwait(false);
                    linked.Token.ThrowIfCancellationRequested();

                    lock (_lock)
                    {
                        ThrowIfStopping();
                        _repository = new OrderedUserScriptRepository(repository, _storage);
                        InitializationError = null;
                    }
                }
                catch (Exception e)
                {
                    InitializationError = e;

                    try
                    {
                        _report?.Invoke("Userscript initialization failed.", e);
                    }
                    catch { }

                    throw;
                }
            }
        }

        public IDisposable RegisterSession(string browserId, Func<Task> stop)
        {
            if (string.IsNullOrEmpty(browserId))
                throw new ArgumentException("A stable browser ID is required.", nameof(browserId));

            if (stop == null)
                throw new ArgumentNullException(nameof(stop));

            lock (_lock)
            {
                ThrowIfStopping();
                _sessions.Add(browserId, stop);
            }

            return new SessionRegistration(this, browserId);
        }

        public Task StopAsync()
        {
            lock (_lock)
                return _stopTask ?? (_stopTask = Task.Run(StopCoreAsync));
        }

        private async Task StopCoreAsync()
        {
            _lifetime.Cancel();

            Func<Task>[] sessions;
            Task initialization;

            lock (_lock)
            {
                sessions = _sessions.Values.ToArray();
                initialization = _initializeTask;
            }

            var failures = new List<Exception>();

            foreach (var stop in sessions)
            {
                try
                {
                    await stop().ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    failures.Add(e);
                }
            }

            if (initialization != null)
            {
                try
                {
                    await initialization.ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // The initialization failure was already reported.
                }
            }

            PermissionPolicy.Dispose();
            await _storage.StopAsync().ConfigureAwait(false);
            Content.Dispose();
            _lifetime.Dispose();

            if (failures.Count != 0)
                throw new AggregateException("Some userscript sessions could not stop.", failures);
        }

        private void ThrowIfStopping()
        {
            if (_stopTask != null)
                throw new ObjectDisposedException(nameof(UserScriptService));
        }

        private sealed class SessionRegistration : IDisposable
        {
            private readonly UserScriptService _owner;
            private readonly string _id;

            public SessionRegistration(UserScriptService owner, string id)
            {
                _owner = owner;
                _id = id;
            }

            public void Dispose()
            {
                lock (_owner._lock)
                    _owner._sessions.Remove(_id);
            }
        }
    }
}
