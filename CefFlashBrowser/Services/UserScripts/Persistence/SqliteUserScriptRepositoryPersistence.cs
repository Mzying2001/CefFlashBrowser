using CefFlashBrowser.Models.UserScripts;
using Mzying2001.MonkeySharp.Core.Apis;
using Mzying2001.MonkeySharp.Core.Domain;
using Mzying2001.MonkeySharp.Core.Repository;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Services.UserScripts.Persistence
{
    public sealed class SqliteUserScriptRepositoryPersistence : IUserScriptPersistence
    {
        public const int MaximumSourceBytes = UserScriptFiles.MaximumBytes;

        private readonly UserScriptStorage _storage;
        private readonly UserScriptSourceFiles _sources;
        private readonly UserScriptContentCache _cache;
        private readonly Action<string, Exception> _report;
        private readonly Action<string> _checkpoint;

        public SqliteUserScriptRepositoryPersistence(UserScriptStorage storage, string sourceRoot, string cacheRoot,
            Action<string, Exception> report = null, Action<string> checkpoint = null)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _report = report;
            _checkpoint = checkpoint;
            _sources = new UserScriptSourceFiles(sourceRoot, message => Report(message, null), checkpoint);
            _cache = new UserScriptContentCache(cacheRoot, message => Report(message, null));
        }

        public Task<IReadOnlyList<UserScriptLoadResult>> LoadAllAsync(CancellationToken token)
        {
            return _storage.RunAsync<IReadOnlyList<UserScriptLoadResult>>(async () =>
            {
                var rows = await _storage.Database.GetUserScriptsAsync().ConfigureAwait(false);
                var known = new HashSet<ScriptKey>(rows.Select(row => row.ScriptKey));

                foreach (var key in _sources.RecoveryKeys())
                {
                    token.ThrowIfCancellationRequested();

                    if (known.Contains(key))
                        continue;

                    try { _sources.Recover(key, null); }
                    catch (Exception e) when (UserScriptFiles.IsFileError(e)) { Report("Orphan source recovery pending for " + key, e); }
                }

                var result = new List<UserScriptLoadResult>();

                foreach (var row in rows)
                {
                    token.ThrowIfCancellationRequested();
                    result.Add(await LoadOneAsync(row).ConfigureAwait(false));
                }

                return result.AsReadOnly();
            }, token);
        }

        public Task<UserScriptLoadResult> LoadAsync(ScriptKey key, CancellationToken token)
        {
            return _storage.RunAsync(async () =>
            {
                var row = await _storage.Database.GetUserScriptAsync(key).ConfigureAwait(false);

                if (row != null)
                    return await LoadOneAsync(row).ConfigureAwait(false);

                try { _sources.Recover(key, null); }
                catch (Exception e) when (UserScriptFiles.IsFileError(e))
                {
                    throw new UserScriptPersistenceException(key, "Orphan source recovery failed.", e);
                }

                throw new KeyNotFoundException("Script not found: " + key);
            }, token);
        }

        private async Task<UserScriptLoadResult> LoadOneAsync(UserScriptDatabaseRecord row)
        {
            var diagnostics = new List<UserScriptDiagnostic>();
            var assets = new List<UserScriptAsset>();
            string source = null, version = null;
            var changed = false;

            try
            {
                ValidateRow(row);
                _sources.Recover(row.ScriptKey, row.CommitId);

                // Database failures remain global; malformed associations belong to this installation.
                var descriptors = await _storage.Database.GetUserScriptAssetsAsync(row.ScriptKey).ConfigureAwait(false);

                foreach (var descriptor in descriptors)
                {
                    ValidateAsset(descriptor);

                    ResourceContent content = null;

                    try { content = new ResourceContent(_cache.Read(descriptor.ContentSha256), descriptor.Mime, descriptor.Charset); }
                    catch (Exception e) when (UserScriptFiles.IsFileError(e))
                    {
                        diagnostics.Add(Diagnostic(row.ScriptKey, "MSR421_ASSET_UNAVAILABLE", "Asset cache unavailable: " + descriptor.Url, e));
                    }

                    assets.Add(new UserScriptAsset((UserScriptAssetKind)descriptor.Kind, descriptor.Ordinal,
                        descriptor.Name, descriptor.Url, content));
                }

                new UserScriptPersistenceRecord(row.ScriptKey, null, row.SourceOrigin, row.IsEnabled,
                    row.InstalledAt, row.UpdatedAt, assets);

                try
                {
                    var bytes = _sources.Read(row.ScriptKey);
                    var hash = UserScriptFiles.Hash(bytes);

                    version = UserScriptSourceFiles.VersionOf(hash);
                    changed = hash != row.SourceSha256;

                    try { source = UserScriptFiles.Decode(bytes); }
                    catch (DecoderFallbackException e)
                    {
                        diagnostics.Add(Diagnostic(row.ScriptKey, "MSR420_SOURCE_UNAVAILABLE", "Source is not valid UTF-8.", e));
                    }
                }
                catch (Exception e) when (e is FileNotFoundException || e is DirectoryNotFoundException)
                {
                    version = "missing";
                    diagnostics.Add(Diagnostic(row.ScriptKey, "MSR420_SOURCE_UNAVAILABLE", "Source file is missing.", e));
                }
                catch (Exception e) when (UserScriptFiles.IsFileError(e))
                {
                    diagnostics.Add(Diagnostic(row.ScriptKey, "MSR420_SOURCE_UNAVAILABLE", "Source could not be read.", e));
                }
            }
            catch (Exception e) when (UserScriptFiles.IsFileError(e) || e is ArgumentException || e is FormatException
                || e is InvalidCastException || e is OverflowException)
            {
                source = null;
                version = null;
                assets.Clear();
                diagnostics.Add(Diagnostic(row.ScriptKey, e is RecoveryConflictException ? "MSR424_RECOVERY_CONFLICT"
                    : "MSR423_PERSISTENCE_FAILED", e.Message, e));
            }

            foreach (var diagnostic in diagnostics)
                Report("Script " + row.ScriptKey + ": " + diagnostic.Message, diagnostic.Exception);

            return new UserScriptLoadResult(new UserScriptPersistenceRecord(row.ScriptKey, source, row.SourceOrigin,
                row.IsEnabled, row.InstalledAt, row.UpdatedAt, assets), version, changed, diagnostics);
        }

        public Task<string> SaveAsync(UserScriptSaveRequest request, CancellationToken token)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            return _storage.RunAsync(async () =>
            {
                var record = request.Record;
                var previous = await _storage.Database.GetUserScriptAsync(record.ScriptKey).ConfigureAwait(false);
                UserScriptSourceFiles.Journal journal = null;

                try
                {
                    if (previous != null)
                        ValidateRow(previous);

                    _sources.Recover(record.ScriptKey, previous?.CommitId);

                    if (previous == null)
                    {
                        if (request.ExpectedSourceVersion != null || _sources.Hash(record.ScriptKey) != null
                            || request.SourceWriteMode != SourceWriteMode.Replace)
                            throw new UserScriptSourceConflictException(record.ScriptKey);
                    }
                    else
                    {
                        if (request.ExpectedSourceVersion == null)
                            throw new UserScriptSourceConflictException(record.ScriptKey);

                        _sources.CheckVersion(record.ScriptKey, request.ExpectedSourceVersion);
                    }

                    if (request.SourceWriteMode == SourceWriteMode.Preserve)
                        CheckPreservedSource(request);

                    var bytes = request.SourceWriteMode == SourceWriteMode.Replace ? UserScriptFiles.Encode(record.Source) : null;

                    if (bytes?.Length > MaximumSourceBytes)
                        throw new IOException("Source exceeds the 10 MiB limit.");

                    var assets = record.Assets.Select(asset =>
                    {
                        var descriptor = new UserScriptAssetDatabaseRecord(record.ScriptKey.ToString(), (int)asset.Kind,
                            asset.Ordinal, asset.Name, asset.Url, _cache.Write(asset.Content.Bytes), asset.Content.MediaType, asset.Content.Charset);

                        ValidateAsset(descriptor);

                        return descriptor;
                    }).ToArray();

                    _checkpoint?.Invoke("objects-written");
                    token.ThrowIfCancellationRequested();

                    if (bytes != null)
                        journal = _sources.Stage(record.ScriptKey, previous == null ? "install" : "replace", previous?.CommitId, bytes);

                    _checkpoint?.Invoke("marker-written");

                    var hash = bytes == null ? _sources.Hash(record.ScriptKey) : UserScriptFiles.Hash(bytes);
                    var stored = new UserScriptDatabaseRecord(record.ScriptKey, record.SourceOrigin, record.IsEnabled,
                        previous?.InstalledAt ?? record.InstalledAt, record.UpdatedAt, hash, journal?.CommitId ?? Guid.NewGuid().ToString("D"));

                    await _storage.Database.SaveUserScriptAsync(stored, assets, () =>
                    {
                        _checkpoint?.Invoke("rows-written");
                        token.ThrowIfCancellationRequested();

                        if (journal == null)
                            CheckPreservedSource(request);
                        else
                        {
                            _sources.CheckVersion(record.ScriptKey, request.ExpectedSourceVersion ?? "missing");
                            _sources.Switch(record.ScriptKey, journal);
                        }

                        _checkpoint?.Invoke("source-switched");
                    }, token).ConfigureAwait(false);

                    // No cancellation or fallible observers may change a committed result.
                    Complete(record.ScriptKey, journal);
                    ReportCheckpoint("committed");

                    return UserScriptSourceFiles.VersionOf(hash);
                }
                catch (Exception e)
                {
                    await RollbackAsync(record.ScriptKey, journal, e).ConfigureAwait(false);

                    if (UserScriptFiles.IsFileError(e) || IsConstraint(e))
                        throw new UserScriptPersistenceException(record.ScriptKey, "Installation save failed: " + e.Message, e);

                    throw;
                }
            }, token);
        }

        private void CheckPreservedSource(UserScriptSaveRequest request)
        {
            _sources.CheckVersion(request.Record.ScriptKey, request.ExpectedSourceVersion);

            try
            {
                if (UserScriptFiles.Decode(_sources.Read(request.Record.ScriptKey)) != request.Record.Source)
                    throw new UserScriptSourceConflictException(request.Record.ScriptKey);
            }
            catch (FileNotFoundException) { throw new UserScriptSourceConflictException(request.Record.ScriptKey); }
            catch (DirectoryNotFoundException) { throw new UserScriptSourceConflictException(request.Record.ScriptKey); }
        }

        public Task SetEnabledAsync(ScriptKey key, bool enabled, DateTimeOffset updatedAt, CancellationToken token)
            => _storage.RunAsync(() => _storage.Database.SetUserScriptEnabledAsync(key, enabled, updatedAt, token), token);

        public Task DeleteAsync(ScriptKey key, CancellationToken token)
        {
            return _storage.RunAsync(async () =>
            {
                var previous = await _storage.Database.GetUserScriptAsync(key).ConfigureAwait(false);
                UserScriptSourceFiles.Journal journal = null;

                try
                {
                    _sources.Recover(key, previous?.CommitId);

                    if (previous == null)
                        return;

                    journal = _sources.Stage(key, "delete", previous.CommitId, null);
                    await _storage.Database.DeleteUserScriptAsync(key, () =>
                    {
                        _checkpoint?.Invoke("rows-written");
                        token.ThrowIfCancellationRequested();
                        _sources.Switch(key, journal);
                        _checkpoint?.Invoke("source-switched");
                    }, token).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    await RollbackAsync(key, journal, e).ConfigureAwait(false);

                    if (UserScriptFiles.IsFileError(e) || IsConstraint(e))
                        throw new UserScriptPersistenceException(key, "Installation removal failed: " + e.Message, e);

                    throw;
                }

                Complete(key, journal);
                await CollectCoreAsync().ConfigureAwait(false);
            }, token);
        }

        private async Task RollbackAsync(ScriptKey key, UserScriptSourceFiles.Journal journal, Exception failure)
        {
            if (journal == null)
                return;

            try
            {
                _checkpoint?.Invoke("restore");

                var row = await _storage.Database.GetUserScriptAsync(key).ConfigureAwait(false);

                _sources.Recover(key, row?.CommitId);
            }
            catch (Exception e)
            {
                throw new UserScriptPersistenceException(key, "Commit failed and source recovery remains unresolved.", new AggregateException(failure, e));
            }
        }

        private void Complete(ScriptKey key, UserScriptSourceFiles.Journal journal)
        {
            if (journal == null)
                return;

            try
            {
                _checkpoint?.Invoke("cleanup");
                _sources.Complete(key, journal);
            }
            catch (Exception e) { Report("Committed source cleanup will retry on the next load.", e); }
        }

        internal Task CollectAsync(CancellationToken token) => _storage.RunAsync(CollectCoreAsync, token);

        private async Task CollectCoreAsync()
        {
            try
            {
                var rows = await _storage.Database.GetUserScriptsAsync().ConfigureAwait(false);
                var assets = await _storage.Database.GetUserScriptAssetsAsync().ConfigureAwait(false);
                var known = new HashSet<string>(rows.Select(row => row.ScriptKey.ToString()), StringComparer.Ordinal);

                foreach (var row in rows)
                {
                    ValidateRow(row);

                    var descriptors = assets.Where(asset => asset.ScriptId == row.ScriptKey.ToString()).ToArray();

                    foreach (var asset in descriptors)
                        ValidateAsset(asset);

                    new UserScriptPersistenceRecord(row.ScriptKey, null, row.SourceOrigin, row.IsEnabled,
                        row.InstalledAt, row.UpdatedAt, descriptors.Select(asset => new UserScriptAsset(
                            (UserScriptAssetKind)asset.Kind, asset.Ordinal, asset.Name, asset.Url, null)));
                }

                if (assets.Any(asset => !known.Contains(asset.ScriptId)))
                    throw new FormatException("Orphan asset associations.");

                _cache.Collect(new HashSet<string>(assets.Select(asset => asset.ContentSha256), StringComparer.Ordinal));
            }
            catch (Exception e) when (UserScriptFiles.IsFileError(e) || e is ArgumentException || e is FormatException
                || e is InvalidCastException || e is OverflowException || e is SQLiteException)
            {
                Report("Asset collection skipped or incomplete.", e);
            }
        }

        private static void ValidateRow(UserScriptDatabaseRecord row)
        {
            if (!UserScriptFiles.IsHash(row.SourceSha256) || !Guid.TryParseExact(row.CommitId, "D", out var commit) || commit == Guid.Empty)
                throw new FormatException("Invalid installation source hash or commit ID.");
        }

        private static void ValidateAsset(UserScriptAssetDatabaseRecord asset)
        {
            if (!UserScriptFiles.IsHash(asset.ContentSha256))
                throw new FormatException("Invalid asset object hash.");

            if (!Uri.TryCreate(asset.Url, UriKind.Absolute, out var url) || !string.IsNullOrEmpty(url.UserInfo)
                || url.Scheme != "http" && url.Scheme != "https")
                throw new FormatException("Asset URLs must use HTTP(S) without embedded credentials.");

            if (string.IsNullOrWhiteSpace(asset.Mime))
                throw new FormatException("Asset MIME type is missing.");

            new ResourceContent(Array.Empty<byte>(), asset.Mime, asset.Charset);
            new UserScriptAsset((UserScriptAssetKind)asset.Kind, asset.Ordinal, asset.Name, asset.Url, null);
        }

        private static bool IsConstraint(Exception error) => error is SQLiteException sql && ((int)sql.ResultCode & 255) == 19;

        private static UserScriptDiagnostic Diagnostic(ScriptKey key, string code, string message, Exception error)
            => new UserScriptDiagnostic(code, DiagnosticSeverity.Error, message, error, key);

        private void ReportCheckpoint(string point)
        {
            try { _checkpoint?.Invoke(point); }
            catch (Exception e) { Report("Post-commit observer failed.", e); }
        }

        private void Report(string message, Exception error)
        {
            try { _report?.Invoke(message, error); }
            catch { }
        }
    }
}
