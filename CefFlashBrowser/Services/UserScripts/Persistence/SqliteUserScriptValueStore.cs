using Mzying2001.MonkeySharp.Core.Domain;
using Mzying2001.MonkeySharp.Core.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Services.UserScripts.Persistence
{
    public sealed class SqliteUserScriptValueStore : IUserScriptValueStore
    {
        private readonly UserScriptStorage _storage;
        private readonly UserScriptValueChangeJournal _journal;
        private readonly Action<string, Exception> _report;

        public event EventHandler<UserScriptValueChangedEventArgs> ValueChanged;

        public SqliteUserScriptValueStore(UserScriptStorage storage, int journalCapacity = 4096,
            Action<string, Exception> report = null)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _journal = new UserScriptValueChangeJournal(journalCapacity);
            _report = report;
        }

        public Task<StoredValue> GetAsync(string scriptKey, string key, CancellationToken token)
        {
            var id = ScriptKey.Parse(scriptKey).ToString();
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            return _storage.RunAsync(() => _storage.Database.GetUserScriptValueAsync(id, key, token), token);
        }

        public Task SetAsync(string scriptKey, string key, UserScriptValue value, CancellationToken token)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return ApplyAsync(scriptKey, new[] { new UserScriptValueMutation(key, value) }, null, token);
        }

        public async Task<bool> DeleteAsync(string scriptKey, string key, CancellationToken token)
        {
            var commit = await ApplyAsync(scriptKey, new[] { new UserScriptValueMutation(key, null) }, null, token).ConfigureAwait(false);
            return commit.Count != 0;
        }

        public async Task<IReadOnlyList<string>> ListKeysAsync(string scriptKey, CancellationToken token)
        {
            var snapshot = await GetSnapshotAsync(scriptKey, token).ConfigureAwait(false);
            return snapshot.Select(item => item.Key).ToList().AsReadOnly();
        }

        public Task<UserScriptValueSnapshot> GetSnapshotAsync(string scriptKey, CancellationToken token)
        {
            var id = ScriptKey.Parse(scriptKey).ToString();
            return _storage.RunAsync(async () =>
            {
                var values = await _storage.Database.GetUserScriptValuesAsync(id, token).ConfigureAwait(false);
                return new UserScriptValueSnapshot(values, _journal.Sequence(id));
            }, token);
        }

        public Task<UserScriptValueChangeBatch> GetChangesAsync(string scriptKey, long afterSequence, CancellationToken token)
        {
            var id = ScriptKey.Parse(scriptKey).ToString();
            return _storage.RunAsync(() => Task.FromResult(_journal.Read(id, afterSequence)), token);
        }

        public Task<UserScriptValueCommit> ApplyAsync(string scriptKey, IEnumerable<UserScriptValueMutation> mutations,
            UserScriptValueMutationOrigin origin, CancellationToken token)
        {
            var id = ScriptKey.Parse(scriptKey).ToString();
            var batch = UserScriptValueMutation.Validate(mutations);
            return _storage.RunAsync(async () =>
            {
                var pending = await _storage.Database.ApplyUserScriptValuesAsync(id, batch, token).ConfigureAwait(false);

                // The same storage queue covers the database commit, journal and
                // notifications, including empty/no-op acknowledgements.
                var changes = pending.Select(item => _journal.Append(id, item.Key, item.OldValue, item.NewValue, origin)).ToList();
                foreach (var change in changes)
                    Publish(change);

                return new UserScriptValueCommit(changes, _journal.Sequence(id));
            }, token);
        }

        private void Publish(UserScriptValueChangedEventArgs change)
        {
            var handlers = ValueChanged;
            if (handlers == null)
                return;

            foreach (EventHandler<UserScriptValueChangedEventArgs> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(this, change);
                }
                catch (Exception e)
                {
                    try
                    {
                        _report?.Invoke("A value-change subscriber failed after commit.", e);
                    }
                    catch { }
                }
            }
        }
    }
}
