using CefFlashBrowser.Services.UserScripts.Persistence;
using Mzying2001.MonkeySharp.Core.Domain;
using Mzying2001.MonkeySharp.Core.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Services.UserScripts
{
    internal enum UserScriptMove
    {
        Up,
        Down,
        First,
        Last
    }

    internal sealed class OrderedUserScriptRepository : IUserScriptRepository
    {
        private readonly IUserScriptRepository _inner;
        private readonly UserScriptStorage _storage;
        private readonly SemaphoreSlim _membershipAndOrder = new SemaphoreSlim(1, 1);
        private IReadOnlyDictionary<ScriptKey, int> _ranks;

        public OrderedUserScriptRepository(IUserScriptRepository inner, UserScriptStorage storage)
        {
            _inner = inner;
            _storage = storage;
        }

        public event EventHandler<UserScriptRepositoryChangedEventArgs> Changed
        {
            add => _inner.Changed += value;
            remove => _inner.Changed -= value;
        }

        public async Task<IReadOnlyList<UserScriptInstallation>> GetSnapshotAsync(CancellationToken token)
        {
            // Snapshots stay available while installations prepare assets or an order is saving.
            while (true)
            {
                var ranks = Volatile.Read(ref _ranks);
                var snapshot = await _inner.GetSnapshotAsync(token).ConfigureAwait(false);

                if (!ReferenceEquals(ranks, Volatile.Read(ref _ranks)))
                    continue;

                // Initialization already loaded the database order. Later installs append in the inner snapshot.
                return ranks == null ? snapshot : Array.AsReadOnly(snapshot.OrderBy(item =>
                    ranks.TryGetValue(item.ScriptKey, out var rank) ? rank : int.MaxValue).ToArray());
            }
        }

        internal Task<bool> MoveAsync(ScriptKey key, UserScriptMove move, CancellationToken token)
            => MoveCoreAsync(key, (keys, index) =>
            {
                switch (move)
                {
                    case UserScriptMove.Up: return index - 1;
                    case UserScriptMove.Down: return index + 1;
                    case UserScriptMove.First: return 0;
                    case UserScriptMove.Last: return keys.Count - 1;
                    default: throw new ArgumentOutOfRangeException(nameof(move));
                }
            }, token);

        internal Task<bool> MoveRelativeAsync(ScriptKey key, ScriptKey target, bool after, CancellationToken token)
            => MoveCoreAsync(key, (keys, index) => GetRelativeDestination(index, keys.IndexOf(target), after), token);

        internal static int GetRelativeDestination(int sourceIndex, int targetIndex, bool after)
        {
            if (sourceIndex < 0 || targetIndex < 0)
                return -1;

            var insertionIndex = targetIndex + (after ? 1 : 0);
            return sourceIndex < insertionIndex ? insertionIndex - 1 : insertionIndex;
        }

        private async Task<bool> MoveCoreAsync(ScriptKey key, Func<List<ScriptKey>, int, int> getDestination, CancellationToken token)
        {
            await _membershipAndOrder.WaitAsync(token).ConfigureAwait(false);

            try
            {
                var snapshot = await GetSnapshotAsync(token).ConfigureAwait(false);
                var keys = snapshot.Select(item => item.ScriptKey).ToList();
                var index = keys.IndexOf(key);

                if (index < 0)
                    return false;

                var destination = getDestination(keys, index);

                if (destination == index || destination < 0 || destination >= keys.Count)
                    return false;

                keys.RemoveAt(index);
                keys.Insert(destination, key);
                var ranks = new Dictionary<ScriptKey, int>(keys.Count);

                for (var i = 0; i < keys.Count; i++)
                    ranks.Add(keys[i], i);

                await _storage.RunAsync(async () =>
                {
                    await _storage.Database.SetUserScriptOrderAsync(keys, token).ConfigureAwait(false);
                    Volatile.Write(ref _ranks, ranks);
                }, token).ConfigureAwait(false);
                return true;
            }
            finally
            {
                _membershipAndOrder.Release();
            }
        }

        private async Task<T> ChangeMembershipAsync<T>(Func<Task<T>> change, CancellationToken token)
        {
            await _membershipAndOrder.WaitAsync(token).ConfigureAwait(false);

            try
            {
                return await change().ConfigureAwait(false);
            }
            finally
            {
                _membershipAndOrder.Release();
            }
        }

        public Task<UserScriptInstallation> InstallAsync(string source, string origin, bool enabled, CancellationToken token)
            => ChangeMembershipAsync(() => _inner.InstallAsync(source, origin, enabled, token), token);

        public Task<bool> RemoveAsync(ScriptKey key, CancellationToken token)
            => ChangeMembershipAsync(() => _inner.RemoveAsync(key, token), token);

        public Task<UserScriptInstallation> GetAsync(ScriptKey key, CancellationToken token)
            => _inner.GetAsync(key, token);

        public Task<UserScriptInstallation> UpdateAsync(ScriptKey key, string source, string origin, CancellationToken token)
            => _inner.UpdateAsync(key, source, origin, token);

        public Task<UserScriptInstallation> UpdateIfUnchangedAsync(ScriptKey key, Guid revision, string source, string origin,
            CancellationToken token)
            => _inner.UpdateIfUnchangedAsync(key, revision, source, origin, token);

        public Task<UserScriptInstallation> ReloadAsync(ScriptKey key, CancellationToken token)
            => _inner.ReloadAsync(key, token);

        public Task<UserScriptInstallation> RefreshAssetsAsync(ScriptKey key, Guid revision, CancellationToken token)
            => _inner.RefreshAssetsAsync(key, revision, token);

        public Task<UserScriptInstallation> SetEnabledAsync(ScriptKey key, bool enabled, CancellationToken token)
            => _inner.SetEnabledAsync(key, enabled, token);
    }
}
