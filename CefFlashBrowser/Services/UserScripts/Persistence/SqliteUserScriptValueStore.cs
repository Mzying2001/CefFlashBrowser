using CefFlashBrowser.Database;
using Mzying2001.MonkeySharp.Core.Domain;
using Mzying2001.MonkeySharp.Core.Storage;
using System;
using System.Collections.Generic;
using System.Globalization;
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
            return _storage.RunAsync(async () =>
            {
                var value = StoredValue.Missing;
                await _storage.Database.TransactionAsync(session =>
                {
                    token.ThrowIfCancellationRequested();
                    value = Read(session, id, key);
                }).ConfigureAwait(false);
                return value;
            }, token);
        }

        public Task SetAsync(string scriptKey, string key, UserScriptValue value, CancellationToken token)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            return ApplyAsync(scriptKey, new[] { new UserScriptValueMutation(key, value) }, null, token);
        }

        public async Task<bool> DeleteAsync(string scriptKey, string key, CancellationToken token)
        {
            return (await ApplyAsync(scriptKey, new[] { new UserScriptValueMutation(key, null) }, null, token).ConfigureAwait(false)).Count != 0;
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
                List<KeyValuePair<string, UserScriptValue>> values = null;
                await _storage.Database.TransactionAsync(session =>
                {
                    token.ThrowIfCancellationRequested();
                    values = session.Query("SELECT value_key, encoded_value FROM monkeysharp_values WHERE script_id = @id ORDER BY value_order",
                        row => new KeyValuePair<string, UserScriptValue>(row.GetString(0), UserScriptValue.FromEncoded(row.GetString(1))),
                        new Dictionary<string, object> { ["@id"] = id });
                }).ConfigureAwait(false);
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
                var pending = new List<Tuple<string, StoredValue, StoredValue>>();
                await _storage.Database.TransactionAsync(session =>
                {
                    token.ThrowIfCancellationRequested();
                    if (Convert.ToInt64(session.Scalar("SELECT COUNT(*) FROM monkeysharp_scripts WHERE script_id = @id",
                        new Dictionary<string, object> { ["@id"] = id })) == 0)
                        throw new KeyNotFoundException("The script installation has been removed.");

                    foreach (var mutation in batch)
                    {
                        var old = Read(session, id, mutation.Key);
                        var value = mutation.Value;
                        if (value == null && !old.Exists || value != null && old.Exists && old.Value.Equals(value))
                            continue;
                        var parameters = new Dictionary<string, object>
                        {
                            ["@id"] = id, ["@key"] = mutation.Key, ["@value"] = value?.EncodedValue,
                            ["@updated"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                        };
                        if (value == null)
                            session.Execute("DELETE FROM monkeysharp_values WHERE script_id = @id AND value_key = @key", parameters);
                        else
                            session.Execute(@"INSERT INTO monkeysharp_values (script_id, value_key, encoded_value, value_order, updated_at)
                                VALUES (@id, @key, @value, (SELECT COALESCE(MAX(value_order), -1) + 1 FROM monkeysharp_values WHERE script_id = @id), @updated)
                                ON CONFLICT(script_id, value_key) DO UPDATE SET encoded_value = excluded.encoded_value, updated_at = excluded.updated_at", parameters);
                        pending.Add(Tuple.Create(mutation.Key, old, value == null ? StoredValue.Missing : new StoredValue(true, value)));
                    }
                    token.ThrowIfCancellationRequested();
                }).ConfigureAwait(false);

                // The same storage queue covers the database commit, journal and
                // notifications, including empty/no-op acknowledgements.
                var changes = pending.Select(item => _journal.Append(id, item.Item1, item.Item2, item.Item3, origin)).ToList();
                foreach (var change in changes)
                    Publish(change);
                return new UserScriptValueCommit(changes, _journal.Sequence(id));
            }, token);
        }

        private static StoredValue Read(IDatabaseSession session, string id, string key)
        {
            var encoded = session.Scalar("SELECT encoded_value FROM monkeysharp_values WHERE script_id = @id AND value_key = @key",
                new Dictionary<string, object> { ["@id"] = id, ["@key"] = key });
            return encoded == null || encoded == DBNull.Value ? StoredValue.Missing
                : new StoredValue(true, UserScriptValue.FromEncoded((string)encoded));
        }

        private void Publish(UserScriptValueChangedEventArgs change)
        {
            var handlers = ValueChanged;
            if (handlers == null)
                return;
            foreach (EventHandler<UserScriptValueChangedEventArgs> handler in handlers.GetInvocationList())
            {
                try { handler(this, change); }
                catch (Exception e)
                {
                    try { _report?.Invoke("A value-change subscriber failed after commit.", e); } catch { }
                }
            }
        }
    }
}
