using Mzying2001.MonkeySharp.Core.Domain;
using Mzying2001.MonkeySharp.Core.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Services.UserScripts
{
    // Keep unsupported execution worlds out of the host's static-rule registry.
    // Denying an already registered rule blocks its request in the pinned library.
    // The application repository still retains every installation for maintenance.
    public sealed class UserScriptRuntimeRepository : IUserScriptRepository, IDisposable
    {
        private readonly IUserScriptRepository _inner;
        private readonly bool _trusted;
        private readonly object _lock = new object();
        private readonly Dictionary<ScriptKey, Guid> _reported = new Dictionary<ScriptKey, Guid>();
        private readonly Action<UserScriptInstallation> _unavailable;

        public event EventHandler<UserScriptRepositoryChangedEventArgs> Changed;

        public UserScriptRuntimeRepository(IUserScriptRepository inner, bool trusted, Action<UserScriptInstallation> unavailable = null)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _trusted = trusted;
            _unavailable = unavailable;
            inner.Changed += InnerChanged;
        }

        private bool Allows(UserScriptInstallation installation)
        {
            if (UserScriptExecutionPolicy.IsWorldAvailable(installation.Definition.Metadata, _trusted))
                return true;

            var report = false;

            lock (_lock)
            {
                if (!_reported.TryGetValue(installation.ScriptKey, out var revision) || revision != installation.RevisionId)
                {
                    _reported[installation.ScriptKey] = installation.RevisionId;
                    report = true;
                }
            }

            if (report)
            {
                try
                {
                    _unavailable?.Invoke(installation);
                }
                catch { }
            }

            return false;
        }

        public async Task<IReadOnlyList<UserScriptInstallation>> GetSnapshotAsync(CancellationToken token)
        {
            var snapshot = await _inner.GetSnapshotAsync(token).ConfigureAwait(false);
            return snapshot.Where(Allows).ToList().AsReadOnly();
        }

        public async Task<UserScriptInstallation> GetAsync(ScriptKey key, CancellationToken token)
        {
            var installation = await _inner.GetAsync(key, token).ConfigureAwait(false);

            if (!Allows(installation))
                throw new KeyNotFoundException("The installation's execution world is unavailable in this host.");

            return installation;
        }

        private void InnerChanged(object sender, UserScriptRepositoryChangedEventArgs e)
        {
            if (e.Kind == RepositoryChangeKind.Removed)
            {
                lock (_lock)
                    _reported.Remove(e.Installation.ScriptKey);
            }

            var forwarded = e.Kind == RepositoryChangeKind.Removed || Allows(e.Installation) ? e
                : new UserScriptRepositoryChangedEventArgs(RepositoryChangeKind.Removed, e.Installation);
            var handlers = Changed;

            if (handlers == null)
                return;

            foreach (EventHandler<UserScriptRepositoryChangedEventArgs> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(this, forwarded);
                }
                catch { }
            }
        }

        public Task<UserScriptInstallation> InstallAsync(string source, string origin, bool enabled, CancellationToken token)
            => _inner.InstallAsync(source, origin, enabled, token);

        public Task<UserScriptInstallation> UpdateAsync(ScriptKey key, string source, string origin, CancellationToken token)
            => _inner.UpdateAsync(key, source, origin, token);

        public Task<UserScriptInstallation> UpdateIfUnchangedAsync(ScriptKey key, Guid revision, string source, string origin,
            CancellationToken token)
            => _inner.UpdateIfUnchangedAsync(key, revision, source, origin, token);

        public Task<UserScriptInstallation> SetEnabledAsync(ScriptKey key, bool enabled, CancellationToken token)
            => _inner.SetEnabledAsync(key, enabled, token);

        public Task<bool> RemoveAsync(ScriptKey key, CancellationToken token)
            => _inner.RemoveAsync(key, token);

        public void Dispose() => _inner.Changed -= InnerChanged;
    }
}
