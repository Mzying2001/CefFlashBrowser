using Mzying2001.MonkeySharp.Core.Domain;
using Mzying2001.MonkeySharp.Core.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Services.UserScripts
{
    internal static class UserScriptSourceInstaller
    {
        private static readonly SemaphoreSlim _commitGate = new SemaphoreSlim(1, 1);

        internal static string NormalizeHttpOrigin(Uri uri)
            => new UriBuilder(uri) { Fragment = string.Empty }.Uri.AbsoluteUri;

        private static bool TryNormalizeHttpOrigin(string origin, out string normalized)
        {
            normalized = null;

            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return false;

            normalized = NormalizeHttpOrigin(uri);
            return true;
        }

        internal static async Task<UserScriptInstallation[]> FindMatchesAsync(IUserScriptRepository repository,
            string origin, CancellationToken token)
        {
            var isHttp = TryNormalizeHttpOrigin(origin, out var normalized);
            var snapshot = await repository.GetSnapshotAsync(token).ConfigureAwait(false);

            return snapshot.Where(installation => isHttp
                ? TryNormalizeHttpOrigin(installation.SourceOrigin, out var installedOrigin) && installedOrigin == normalized
                : string.Equals(installation.SourceOrigin, origin, StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
        }

        internal static async Task<UserScriptInstallation> TryCommitAsync(IUserScriptRepository repository,
            UserScriptInstallation previous, string source, string origin, CancellationToken token)
        {
            await _commitGate.WaitAsync(token).ConfigureAwait(false);

            try
            {
                var matches = await FindMatchesAsync(repository, origin, token).ConfigureAwait(false);
                if (matches.Length > 1) return null;

                var current = matches.SingleOrDefault();
                if (previous == null)
                    return current == null ? await repository.InstallAsync(source, origin, true, token).ConfigureAwait(false) : null;

                if (current == null || !current.ScriptKey.Equals(previous.ScriptKey) || current.RevisionId != previous.RevisionId)
                    return null;

                return await repository.UpdateIfUnchangedAsync(previous.ScriptKey, previous.RevisionId, source, origin, token)
                    .ConfigureAwait(false);
            }
            catch (Exception e) when (e is RepositoryRevisionMismatchException || e is UserScriptSourceConflictException
                || (previous != null && e is KeyNotFoundException))
            {
                return null;
            }
            finally
            {
                _commitGate.Release();
            }
        }
    }
}
