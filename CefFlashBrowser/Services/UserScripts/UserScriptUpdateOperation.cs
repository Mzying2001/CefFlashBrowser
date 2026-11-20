using Mzying2001.MonkeySharp.Core.Domain;
using Mzying2001.MonkeySharp.Core.Parsing;
using Mzying2001.MonkeySharp.Core.Repository;
using Mzying2001.MonkeySharp.Core.Updates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Services.UserScripts
{
    internal sealed class UserScriptUpdateOperation : IUserScriptUpdateFetcher
    {
        private readonly Func<string, CancellationToken, Task<string>> _fetchSource;
        private readonly Dictionary<string, string> _sources = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly UserScriptUpdateService _updates;

        public UserScriptUpdateOperation(IUserScriptRepository repository, Func<string, CancellationToken, Task<string>> fetchSource)
        {
            _fetchSource = fetchSource;
            _updates = new UserScriptUpdateService(repository, this);
        }

        public Task<UserScriptUpdateCheckResult> CheckAsync(ScriptKey key, CancellationToken token)
            => _updates.CheckAsync(key, token);

        public string GetCheckedVersion(UserScriptUpdateCheckResult check)
        {
            if (check.AvailableVersion != null || check.Status != UserScriptUpdateStatus.UpToDate) return check.AvailableVersion;

            // MonkeySharp omits AvailableVersion when the remote version is not newer.
            var source = _sources.Values.FirstOrDefault();
            return source == null ? check.CurrentVersion : new UserScriptMetadataParser().Parse(source).Metadata?.Version;
        }

        public async Task<UserScriptMetadata> PrepareAsync(UserScriptInstallation installation,
            UserScriptUpdateCheckResult check, CancellationToken token)
        {
            var source = await FetchSourceAsync(installation, check.DownloadUrl, token);
            var parsed = new UserScriptMetadataParser().Parse(source);

            if (!parsed.CanEnable)
                throw new MetadataValidationException(parsed);
            return parsed.Metadata;
        }

        public Task<UserScriptInstallation> ApplyAsync(UserScriptUpdateCheckResult check, CancellationToken token)
            => _updates.ApplyAsync(check, token);

        public static bool IsStale(Exception error)
            => error is KeyNotFoundException || error is RepositoryRevisionMismatchException
                || (error is UserScriptUpdateException updateError && updateError.Code == "MSU005_STALE_CHECK");

        public async Task<string> FetchSourceAsync(UserScriptInstallation installation, string url,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (_sources.TryGetValue(url, out var source)) return source;

            source = await _fetchSource(url, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            UserScriptSourceText.Validate(source);
            // Keep the source reviewed in the confirmation dialog for the apply step.
            _sources.Add(url, source);
            return source;
        }
    }
}
