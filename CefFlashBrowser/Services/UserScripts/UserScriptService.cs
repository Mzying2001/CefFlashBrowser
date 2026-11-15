using CefFlashBrowser.Models.UserScripts;
using CefFlashBrowser.Services.UserScripts.Persistence;
using Mzying2001.MonkeySharp.Core.Domain;
using Mzying2001.MonkeySharp.Core.Parsing;
using Mzying2001.MonkeySharp.Core.Permissions;
using Mzying2001.MonkeySharp.Core.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private PersistentUserScriptRepository _repository;

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

        public UserScriptService(UserScriptStorage storage, string root, UserScriptContentService content,
            IUserScriptPermissionPrompt prompt, Action<string, Exception> report = null)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            Content = content ?? throw new ArgumentNullException(nameof(content));
            _report = report;
            _persistence = new SqliteUserScriptRepositoryPersistence(storage, root, report);
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

        public Task InitializeAsync()
        {
            lock (_lock)
            {
                ThrowIfStopping();
                if (_initializeTask == null || _initializeTask.IsFaulted || _initializeTask.IsCanceled)
                    _initializeTask = Task.Run(() => InitializeCoreAsync(null, null, _lifetime.Token));
                return _initializeTask;
            }
        }

        public Task<IReadOnlyList<UserScriptMaintenanceRecord>> ReadMaintenanceAsync(CancellationToken token)
        {
            lock (_lock)
                ThrowIfStopping();
            return _persistence.ReadMaintenanceAsync(token);
        }

        public Task RepairAsync(ScriptKey key, string reviewedSource, CancellationToken token)
        {
            if (reviewedSource == null)
                throw new ArgumentNullException(nameof(reviewedSource));

            var parsed = new UserScriptMetadataParser().Parse(reviewedSource);
            if (!parsed.CanEnable)
                throw new MetadataValidationException(parsed);

            lock (_lock)
            {
                ThrowIfStopping();
                if (_repository != null || _initializeTask != null && !_initializeTask.IsCompleted)
                    throw new InvalidOperationException("Maintenance repair requires a failed or unpublished runtime.");

                return _initializeTask = Task.Run(() => InitializeCoreAsync(key, reviewedSource, token));
            }
        }

        private async Task InitializeCoreAsync(ScriptKey? repairKey, string source, CancellationToken token)
        {
            // Do not execute initialization or completion callbacks under the service lock.
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime.Token))
            {
                try
                {
                    await _storage.RunAsync(() => _storage.Database.TransactionAsync(session =>
                    {
                        linked.Token.ThrowIfCancellationRequested();
                        session.Execute("DELETE FROM monkeysharp_tab_states");
                    }), linked.Token).ConfigureAwait(false);
                    IUserScriptRepositoryPersistence persistence = repairKey.HasValue
                        ? new RepairPersistence(_persistence, repairKey.Value, source)
                        : (IUserScriptRepositoryPersistence)_persistence;
                    var repository = new PersistentUserScriptRepository(persistence,
                        preparer: new UserScriptPreparer(Content, Content));
                    await repository.InitializeAsync(linked.Token).ConfigureAwait(false);
                    linked.Token.ThrowIfCancellationRequested();
                    lock (_lock)
                    {
                        ThrowIfStopping();
                        _repository = repository;
                        InitializationError = null;
                    }
                }
                catch (Exception e)
                {
                    InitializationError = e;
                    try
                    {
                        _report?.Invoke("Userscript initialization failed; maintenance records remain available.", e);
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

        private sealed class RepairPersistence : IUserScriptRepositoryPersistence
        {
            private readonly SqliteUserScriptRepositoryPersistence _inner;
            private readonly ScriptKey _key;
            private readonly string _source;

            public RepairPersistence(SqliteUserScriptRepositoryPersistence inner, ScriptKey key, string source)
            {
                _inner = inner;
                _key = key;
                _source = source;
            }

            public async Task<IReadOnlyList<UserScriptPersistenceRecord>> LoadAsync(CancellationToken token)
            {
                var records = await _inner.LoadAsync(token).ConfigureAwait(false);
                if (!records.Any(record => record.ScriptKey == _key))
                    throw new KeyNotFoundException("The repair installation no longer exists.");

                return records
                    .Select(record => record.ScriptKey != _key ? record : new UserScriptPersistenceRecord(
                        record.ScriptKey, _source, record.SourceOrigin, record.IsEnabled, record.InstalledAt, DateTimeOffset.UtcNow))
                    .ToList().AsReadOnly();
            }

            public Task SaveAsync(UserScriptPersistenceRecord record, CancellationToken token) => _inner.SaveAsync(record, token);

            public Task DeleteAsync(ScriptKey key, CancellationToken token) => _inner.DeleteAsync(key, token);
        }
    }
}
