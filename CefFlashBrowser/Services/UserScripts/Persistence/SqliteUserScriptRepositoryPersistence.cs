using CefFlashBrowser.Database;
using CefFlashBrowser.Models.UserScripts;
using Mzying2001.MonkeySharp.Core.Domain;
using Mzying2001.MonkeySharp.Core.Repository;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Services.UserScripts.Persistence
{
    public sealed class SqliteUserScriptRepositoryPersistence : IUserScriptRepositoryPersistence
    {
        public const int MaximumSourceBytes = 10 * 1024 * 1024;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private readonly UserScriptStorage _storage;
        private readonly string _root;
        private readonly Action<string, Exception> _report;
        private readonly Action<string> _checkpoint;
        private bool _recoveryRequired;

        public SqliteUserScriptRepositoryPersistence(UserScriptStorage storage, string root,
            Action<string, Exception> report = null, Action<string> checkpoint = null)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _root = Path.GetFullPath(root ?? throw new ArgumentNullException(nameof(root)));
            _report = report;
            _checkpoint = checkpoint;
        }

        public async Task<IReadOnlyList<UserScriptPersistenceRecord>> LoadAsync(CancellationToken token)
        {
            var records = await ReadMaintenanceAsync(token).ConfigureAwait(false);
            return records.Select(item =>
            {
                var record = item.Record;
                // Never let a normal startup implicitly fetch development-record dependencies.
                return string.IsNullOrWhiteSpace(record.PreparedRevisionJson)
                    ? new UserScriptPersistenceRecord(record.ScriptKey, null, record.SourceOrigin,
                        record.IsEnabled, record.InstalledAt, record.UpdatedAt)
                    : record;
            }).ToList().AsReadOnly();
        }

        public Task<IReadOnlyList<UserScriptMaintenanceRecord>> ReadMaintenanceAsync(CancellationToken token)
        {
            return _storage.RunAsync<IReadOnlyList<UserScriptMaintenanceRecord>>(async () =>
            {
                var stored = await _storage.Database.QueryAsync(SelectSql + " ORDER BY install_order", ReadRow).ConfigureAwait(false);
                var result = new List<UserScriptMaintenanceRecord>();
                foreach (var row in stored)
                {
                    token.ThrowIfCancellationRequested();
                    var diagnostics = new List<string>();
                    string source = null;
                    try
                    {
                        var path = SourcePath(row.Key);
                        if (!Recover(path, row.Hash))
                            throw new IOException("No readable source matches the committed SHA-256.");

                        var bytes = ReadBounded(path);
                        if (Hash(bytes) != row.Hash)
                            throw new IOException("Source changed while reading the committed revision.");

                        source = Decode(bytes);
                    }
                    catch (Exception e) when (IsFileError(e))
                    {
                        diagnostics.Add(e.Message);
                        Report("Source unavailable for " + row.Key, e);
                    }

                    if (string.IsNullOrWhiteSpace(row.Prepared))
                        diagnostics.Add("Prepared revision is missing; explicit repair is required.");

                    result.Add(new UserScriptMaintenanceRecord(new UserScriptPersistenceRecord(row.Key,
                        source, row.Origin, row.Enabled, row.Installed, row.Updated, row.Prepared), diagnostics.AsReadOnly()));
                }

                CleanupOrphans(new HashSet<string>(stored.Select(row => row.Key.ToString()), StringComparer.OrdinalIgnoreCase));
                _recoveryRequired = false;
                return result.AsReadOnly();
            }, token);
        }

        public Task SaveAsync(UserScriptPersistenceRecord record, CancellationToken token)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));

            if (record.Source == null || string.IsNullOrWhiteSpace(record.PreparedRevisionJson))
                throw new ArgumentException("A saved installation needs source and a prepared revision.", nameof(record));

            var bytes = Utf8.GetBytes(record.Source);
            // Preserve a literal leading BOM in the logical source after BOM-aware decoding.
            if (record.Source.StartsWith("\uFEFF", StringComparison.Ordinal))
                bytes = new byte[] { 239, 187, 191 }.Concat(bytes).ToArray();

            if (bytes.Length > MaximumSourceBytes)
                throw new IOException("Source exceeds the 10 MiB limit.");

            var hash = Hash(bytes);

            return _storage.RunAsync(async () =>
            {
                EnsureRecovered();
                var old = await FindAsync(record.ScriptKey).ConfigureAwait(false);
                var path = SourcePath(record.ScriptKey);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                if (old != null)
                    Recover(path, old.Hash);

                if (HasTransitions(path))
                    throw new IOException("Unresolved source recovery files must be repaired before saving.");

                if (old == null && File.Exists(path))
                    throw new IOException("An unregistered source already occupies the installation directory.");

                var sameContent = old != null && old.Hash == hash && old.Prepared == record.PreparedRevisionJson;
                if (sameContent && !Matches(path, old.Hash))
                    throw new IOException("The installed source changed outside the repository.");

                var replaced = false;
                var existed = File.Exists(path);
                try
                {
                    if (!sameContent)
                    {
                        using (var stream = new FileStream(path + ".pending", FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        {
                            stream.Write(bytes, 0, bytes.Length);
                            stream.Flush(true);
                        }

                        _checkpoint?.Invoke("pending");
                    }

                    await _storage.Database.TransactionAsync(session =>
                    {
                        token.ThrowIfCancellationRequested();
                        session.Execute(@"INSERT INTO monkeysharp_scripts
                            (script_id, install_order, source_origin, is_enabled, installed_at, updated_at, source_sha256, prepared_revision_json)
                            VALUES (@id, (SELECT COALESCE(MAX(install_order), -1) + 1 FROM monkeysharp_scripts), @origin, @enabled, @installed, @updated, @hash, @prepared)
                            ON CONFLICT(script_id) DO UPDATE SET source_origin = excluded.source_origin,
                                is_enabled = excluded.is_enabled, updated_at = excluded.updated_at,
                                source_sha256 = excluded.source_sha256, prepared_revision_json = excluded.prepared_revision_json",
                            Parameters(record, hash));
                        token.ThrowIfCancellationRequested();
                        if (!sameContent)
                        {
                            if (existed)
                                File.Replace(path + ".pending", path, path + ".backup");
                            else
                                File.Move(path + ".pending", path);

                            replaced = true;
                            _checkpoint?.Invoke("replaced");
                        }
                        // No cancellation checks after the file switch: commit is next.
                    }).ConfigureAwait(false);
                }
                catch (Exception failure)
                {
                    Rollback(path, replaced, existed, false, failure);
                    throw;
                }

                CleanupCommitted(path, false);
            }, token);
        }

        public Task DeleteAsync(ScriptKey key, CancellationToken token)
        {
            return _storage.RunAsync(async () =>
            {
                EnsureRecovered();
                var old = await FindAsync(key).ConfigureAwait(false);
                if (old == null)
                    return;

                var path = SourcePath(key);
                Recover(path, old.Hash);
                if (HasTransitions(path))
                    throw new IOException("Unresolved source recovery files must be repaired before removal.");

                var renamed = false;
                try
                {
                    await _storage.Database.TransactionAsync(session =>
                    {
                        token.ThrowIfCancellationRequested();
                        var parameters = new Dictionary<string, object> { ["@id"] = key.ToString() };
                        foreach (var table in new[] { "monkeysharp_values", "monkeysharp_permission_decisions", "monkeysharp_tab_states", "monkeysharp_scripts" })
                            session.Execute("DELETE FROM " + table + " WHERE script_id = @id", parameters);

                        token.ThrowIfCancellationRequested();
                        if (File.Exists(path))
                        {
                            File.Move(path, path + ".deleted");
                            renamed = true;
                            _checkpoint?.Invoke("deleted");
                        }
                    }).ConfigureAwait(false);
                }
                catch (Exception failure)
                {
                    Rollback(path, renamed, true, true, failure);
                    throw;
                }

                CleanupCommitted(path, true);
            }, token);
        }

        private void Rollback(string path, bool changed, bool existed, bool deleting, Exception failure)
        {
            try
            {
                _checkpoint?.Invoke("restore");
                if (changed)
                {
                    if (deleting)
                        File.Move(path + ".deleted", path);
                    else if (existed)
                        File.Replace(path + ".backup", path, null);
                    else
                        File.Delete(path);
                }

                CleanupFile(path + ".pending");
            }
            catch (Exception recoveryFailure)
            {
                _recoveryRequired = true;
                throw new AggregateException("Installation rollback failed; recover before continuing.", failure, recoveryFailure);
            }
        }

        private bool Recover(string path, string hash)
        {
            if (!Matches(path, hash))
            {
                var candidate = new[] { path + ".backup", path + ".deleted" }.FirstOrDefault(file => Matches(file, hash));
                if (candidate == null)
                    return false;

                if (File.Exists(path))
                    File.Replace(candidate, path, null);
                else
                    File.Move(candidate, path);
            }

            CleanupCommitted(path, false);
            return true;
        }

        private void CleanupCommitted(string path, bool deleting)
        {
            try
            {
                _checkpoint?.Invoke("cleanup");
                foreach (var suffix in new[] { ".pending", ".backup", ".deleted" })
                    CleanupFile(path + suffix);

                if (deleting && Directory.Exists(Path.GetDirectoryName(path))
                    && !Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(path)).Any())
                    Directory.Delete(Path.GetDirectoryName(path));
            }
            catch (Exception e) when (IsFileError(e))
            {
                Report("Committed installation cleanup will retry on the next load.", e);
            }
        }

        private void CleanupOrphans(HashSet<string> known)
        {
            if (!Directory.Exists(_root))
                return;

            CheckReparse(_root);
            foreach (var directory in Directory.EnumerateDirectories(_root))
            {
                var name = Path.GetFileName(directory);
                if (known.Contains(name) || !ScriptKey.TryParse(name, out var key))
                    continue;

                try
                {
                    var path = SourcePath(key);
                    // A deletion marker is evidence of our committed uninstall.
                    // Unknown regular source files are never imported or removed.
                    if (File.Exists(path + ".deleted"))
                        CleanupCommitted(path, true);
                    else
                        Report("Unregistered script directory retained: " + key, null);
                }
                catch (Exception e) when (IsFileError(e))
                {
                    Report("Could not inspect an unregistered script directory.", e);
                }
            }
        }

        private string SourcePath(ScriptKey key)
        {
            var id = ScriptKey.Parse(key.ToString()).ToString();
            var path = Path.GetFullPath(Path.Combine(_root, id, "userscript.js"));
            if (!path.StartsWith(_root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Script path escaped the data directory.");

            CheckReparse(_root);
            CheckReparse(Path.GetDirectoryName(path));
            foreach (var suffix in new[] { "", ".pending", ".backup", ".deleted" })
                CheckReparse(path + suffix);

            return path;
        }

        private static void CheckReparse(string path)
        {
            if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Reparse points are not supported in script storage.");
        }

        private static byte[] ReadBounded(string path)
        {
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var output = new MemoryStream())
            {
                var buffer = new byte[65536];
                int count;
                while ((count = input.Read(buffer, 0, buffer.Length)) != 0)
                {
                    if (output.Length + count > MaximumSourceBytes)
                        throw new IOException("Source exceeds the 10 MiB limit.");

                    output.Write(buffer, 0, count);
                }

                return output.ToArray();
            }
        }

        private static string Decode(byte[] bytes)
        {
            var offset = bytes.Length >= 3 && bytes[0] == 239 && bytes[1] == 187 && bytes[2] == 191 ? 3 : 0;
            return Utf8.GetString(bytes, offset, bytes.Length - offset);
        }

        private static bool Matches(string path, string hash)
        {
            try
            {
                return File.Exists(path) && Hash(ReadBounded(path)) == hash;
            }
            catch (Exception e) when (IsFileError(e))
            {
                return false;
            }
        }

        private static string Hash(byte[] bytes)
        {
            using (var algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        private static bool HasTransitions(string path)
        {
            return new[] { ".pending", ".backup", ".deleted" }.Any(suffix => File.Exists(path + suffix));
        }

        private static bool IsFileError(Exception e) => e is IOException || e is UnauthorizedAccessException || e is DecoderFallbackException;

        private static void CleanupFile(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        private void Report(string message, Exception exception)
        {
            try
            {
                _report?.Invoke(message, exception);
            }
            catch { }
        }

        private void EnsureRecovered()
        {
            if (_recoveryRequired)
                throw new IOException("Persistence requires recovery before another mutation.");
        }

        private const string SelectSql = "SELECT script_id, source_origin, is_enabled, installed_at, updated_at, source_sha256, prepared_revision_json FROM monkeysharp_scripts";

        private async Task<StoredRecord> FindAsync(ScriptKey key)
        {
            var rows = await _storage.Database.QueryAsync(SelectSql + " WHERE script_id = @id", ReadRow,
                new Dictionary<string, object> { ["@id"] = key.ToString() }).ConfigureAwait(false);
            return rows.SingleOrDefault();
        }

        private static StoredRecord ReadRow(System.Data.IDataRecord row) => new StoredRecord
        {
            Key = ScriptKey.Parse(row.GetString(0)),
            Origin = row.IsDBNull(1) ? null : row.GetString(1),
            Enabled = row.GetInt64(2) != 0,
            Installed = DateTimeOffset.ParseExact(row.GetString(3), "O", CultureInfo.InvariantCulture),
            Updated = DateTimeOffset.ParseExact(row.GetString(4), "O", CultureInfo.InvariantCulture),
            Hash = row.GetString(5),
            Prepared = row.IsDBNull(6) ? null : row.GetString(6)
        };

        private static Dictionary<string, object> Parameters(UserScriptPersistenceRecord record, string hash) => new Dictionary<string, object>
        {
            ["@id"] = record.ScriptKey.ToString(),
            ["@origin"] = record.SourceOrigin,
            ["@enabled"] = record.IsEnabled ? 1 : 0,
            ["@installed"] = record.InstalledAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            ["@updated"] = record.UpdatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            ["@hash"] = hash,
            ["@prepared"] = record.PreparedRevisionJson
        };

        private sealed class StoredRecord
        {
            public ScriptKey Key;
            public string Origin;
            public string Hash;
            public string Prepared;
            public bool Enabled;
            public DateTimeOffset Installed;
            public DateTimeOffset Updated;
        }
    }
}
