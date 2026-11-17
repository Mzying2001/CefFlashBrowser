using Mzying2001.MonkeySharp.Core.Domain;
using Mzying2001.MonkeySharp.Core.Repository;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CefFlashBrowser.Services.UserScripts.Persistence
{
    internal sealed class RecoveryConflictException : IOException
    {

        internal RecoveryConflictException(string message) : base(message) { }
    }

    // File helpers never enter the storage queue or commit the database transaction.
    internal sealed class UserScriptSourceFiles
    {

        internal sealed class Journal
        {
    
        internal string Operation, CommitId, PreviousCommitId, PreviousFileHash, NewFileHash;
        }

        private readonly string _root;

        private readonly Action<string> _report;

        internal UserScriptSourceFiles(string root, Action<string> report) { _root = Path.GetFullPath(root ?? throw new ArgumentNullException(nameof(root))); _report = report; }

        internal string PathFor(ScriptKey key)
        {
            if (!ScriptKey.TryParse(key.ToString(), out _)) throw new ArgumentException("Invalid script key.", nameof(key));
            return Path.Combine(_root, key.ToString(), "userscript.js");
        }

        internal IEnumerable<ScriptKey> RecoveryKeys()
        {
            UserScriptFiles.CheckPath(_root);
            FileAttributes rootAttributes;
            try { rootAttributes = File.GetAttributes(_root); }
            catch (FileNotFoundException) { yield break; }
            catch (DirectoryNotFoundException) { yield break; }
            if ((rootAttributes & FileAttributes.Directory) == 0) throw new IOException("Script storage root is not a directory.");
            foreach (var directory in Directory.EnumerateDirectories(_root))
            {
                var name = Path.GetFileName(directory);
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) continue;
                if (ScriptKey.TryParse(name, out var key) && name == key.ToString()) yield return key;
            }
        }

        internal byte[] Read(ScriptKey key)
        {
            var path = PathFor(key); UserScriptFiles.CheckPath(path);
            return UserScriptFiles.ReadBytes(path);
        }

        internal string Hash(ScriptKey key)
        {
            var path = PathFor(key); UserScriptFiles.CheckPath(path);
            return UserScriptFiles.Exists(path) ? UserScriptFiles.Hash(Read(key)) : null;
        }

        internal string Version(ScriptKey key) => VersionOf(Hash(key));

        internal static string VersionOf(string hash) => hash == null ? "missing" : "sha256:" + hash;

        internal void CheckVersion(ScriptKey key, string expected)
        {
            if (Version(key) != expected) throw new UserScriptSourceConflictException(key);
        }

        internal Journal Stage(ScriptKey key, string operation, string previousCommitId, byte[] newBytes)
        {
            var path = PathFor(key); UserScriptFiles.CheckPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (UserScriptFiles.Exists(Marker(path))) throw Conflict(path, "Unresolved transaction marker.");
            var journal = new Journal { Operation = operation, PreviousCommitId = previousCommitId,
                CommitId = Guid.NewGuid().ToString("D"), PreviousFileHash = Hash(key), NewFileHash = newBytes == null ? null : UserScriptFiles.Hash(newBytes) };
            if (operation == "install" && journal.PreviousFileHash != null) throw new UserScriptSourceConflictException(key);
            var markerTemporary = Marker(path) + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                if (newBytes != null) UserScriptFiles.WriteNew(path + ".pending", newBytes);
                UserScriptFiles.WriteNew(markerTemporary, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new
                {
                    operation = journal.Operation, commitId = journal.CommitId, previousCommitId = journal.PreviousCommitId,
                    previousFileHash = journal.PreviousFileHash, newFileHash = journal.NewFileHash
                })));
                File.Move(markerTemporary, Marker(path));
            }
            catch
            {
                Cleanup(markerTemporary);
                // No marker means the source has not switched. Only remove our own staged bytes.
                if (!UserScriptFiles.Exists(Marker(path)) && newBytes != null) CleanupKnown(path + ".pending", journal.NewFileHash);
                throw;
            }
            return journal;
        }

        internal void Switch(ScriptKey key, Journal journal)
        {
            var path = PathFor(key);
            CheckVersion(key, VersionOf(journal.PreviousFileHash));
            if (journal.Operation == "delete")
            {
                if (journal.PreviousFileHash != null)
                {
                    UserScriptFiles.CheckPath(path + ".deleted");
                    File.Move(path, path + ".deleted");
                }
            }
            else if (journal.PreviousFileHash == null)
            {
                CheckPending(path, journal);
                File.Move(path + ".pending", path);
            }
            else
            {
                CheckPending(path, journal);
                UserScriptFiles.CheckPath(path + ".backup");
                if (UserScriptFiles.Exists(path + ".backup")) throw Conflict(path, "Unknown backup already exists.");
                File.Replace(path + ".pending", path, path + ".backup");
            }
        }

        private static void CheckPending(string path, Journal journal)
        {
            UserScriptFiles.CheckPath(path + ".pending");
            if (UserScriptFiles.Hash(UserScriptFiles.ReadBytes(path + ".pending")) != journal.NewFileHash)
                throw Conflict(path, "Staged source changed before switching.");
        }


        internal void Recover(ScriptKey key, string databaseCommitId)
        {
            var path = PathFor(key); UserScriptFiles.CheckPath(path);
            if (!UserScriptFiles.Exists(Marker(path))) return;
            var journal = ReadJournal(path);
            var actual = Hash(key);
            if (journal.Operation == "delete")
            {
                if (databaseCommitId == null)
                {
                    // A new unregistered main file belongs to the user, not to this deletion.
                    Complete(path, journal);
                    return;
                }
                if (databaseCommitId != journal.PreviousCommitId) throw Conflict(path, "Database commit does not match the deletion.");
                if (actual != journal.PreviousFileHash)
                {
                    if (actual != null) throw Conflict(path, "Unknown source appeared during deletion.");
                    if (journal.PreviousFileHash != null) RestoreFile(path + ".deleted", path, journal.PreviousFileHash, actual);
                }
                Complete(path, journal);
                return;
            }
            if (databaseCommitId == journal.CommitId)
            {
                if (actual != journal.NewFileHash)
                {
                    if (actual != null && actual != journal.PreviousFileHash) throw Conflict(path, "Source is neither the old nor the committed new content.");
                    RestoreFile(path + ".pending", path, journal.NewFileHash, actual);
                }
                Complete(path, journal);
                return;
            }
            if (journal.Operation == "install" && databaseCommitId == null)
            {
                if (actual != null)
                {
                    if (actual != journal.NewFileHash) throw Conflict(path, "Unregistered source has unknown content.");
                    DeleteSource(path, journal.NewFileHash);
                }
                Complete(path, journal);
                return;
            }
            if (journal.Operation != "replace" || databaseCommitId != journal.PreviousCommitId) throw Conflict(path, "Database commit does not match either transaction state.");
            if (actual != journal.PreviousFileHash)
            {
                if (actual != null && actual != journal.NewFileHash) throw Conflict(path, "External content prevents rollback.");
                if (journal.PreviousFileHash == null)
                {
                    if (actual == journal.NewFileHash) DeleteSource(path, journal.NewFileHash);
                }
                else RestoreFile(path + ".backup", path, journal.PreviousFileHash, actual);
            }
            Complete(path, journal);
        }

        internal void Complete(ScriptKey key, Journal journal)
        {
            try { Complete(PathFor(key), journal); }
            catch (RecoveryConflictException error) { _report(error.Message); }
        }

        private void Complete(string path, Journal journal)
        {
            // Cleanup failure does not change a successful commit. Retain the marker until all known material is cleaned.
            try
            {
                CleanupKnown(path + ".pending", journal.NewFileHash);
                CleanupKnown(path + ".backup", journal.PreviousFileHash);
                CleanupKnown(path + ".deleted", journal.PreviousFileHash);
                File.Delete(Marker(path));
            }
            catch (Exception error) when (!(error is RecoveryConflictException) && (error is IOException || error is UnauthorizedAccessException))
            { _report("Transaction cleanup pending for " + path + ": " + error.Message); }
        }

        private static Journal ReadJournal(string path)
        {
            UserScriptFiles.CheckPath(Marker(path));
            try
            {
                var root = JObject.Parse(UserScriptFiles.Decode(UserScriptFiles.ReadBytes(Marker(path))));
                foreach (var name in new[] { "operation", "commitId", "previousCommitId", "previousFileHash", "newFileHash" })
                    if (root.Property(name) == null || root[name].Type != JTokenType.String && root[name].Type != JTokenType.Null)
                        throw new FormatException("Missing or invalid transaction field: " + name);

                var result = new Journal { Operation = (string)root["operation"], CommitId = (string)root["commitId"],
                    PreviousCommitId = (string)root["previousCommitId"], PreviousFileHash = (string)root["previousFileHash"], NewFileHash = (string)root["newFileHash"] };
                if (!Guid.TryParseExact(result.CommitId, "D", out var commit) || commit == Guid.Empty ||
                    (result.PreviousCommitId != null && (!Guid.TryParseExact(result.PreviousCommitId, "D", out var previous) || previous == Guid.Empty)) ||
                    (result.PreviousFileHash != null && !UserScriptFiles.IsHash(result.PreviousFileHash)) || (result.NewFileHash != null && !UserScriptFiles.IsHash(result.NewFileHash)) ||
                    (result.Operation != "install" && result.Operation != "replace" && result.Operation != "delete") ||
                    (result.Operation == "install" ? result.PreviousCommitId != null || result.PreviousFileHash != null : result.PreviousCommitId == null) ||
                    (result.Operation == "delete" ? result.NewFileHash != null : result.NewFileHash == null))
                    throw new FormatException("Invalid transaction fields.");
                return result;
            }
            catch (Exception error) when (error is JsonException || error is FormatException || error is KeyNotFoundException || error is InvalidOperationException)
            { throw Conflict(path, "Invalid transaction marker: " + error.Message); }
        }

        private static void RestoreFile(string candidate, string path, string hash, string expectedCurrentHash)
        {
            UserScriptFiles.CheckPath(candidate);
            if (!UserScriptFiles.Exists(candidate) || UserScriptFiles.Hash(UserScriptFiles.ReadBytes(candidate)) != hash) throw Conflict(path, "A verified recovery candidate is unavailable: " + candidate);
            CheckCurrentHash(path, expectedCurrentHash);
            if (UserScriptFiles.Exists(path)) File.Replace(candidate, path, null);
            else File.Move(candidate, path);
        }

        private static void CheckCurrentHash(string path, string expected)
        {
            UserScriptFiles.CheckPath(path);
            var actual = UserScriptFiles.Exists(path) ? UserScriptFiles.Hash(UserScriptFiles.ReadBytes(path)) : null;
            if (actual != expected) throw Conflict(path, "Source changed during recovery; preserve all transaction material.");
        }

        private static void DeleteSource(string path, string expected)
        {
            CheckCurrentHash(path, expected);
            File.Delete(path);
        }

        private static void CleanupKnown(string path, string hash)
        {
            UserScriptFiles.CheckPath(path);
            if (!UserScriptFiles.Exists(path)) return;
            if (hash == null || UserScriptFiles.Hash(UserScriptFiles.ReadBytes(path)) != hash) throw Conflict(path, "Unknown recovery material must be preserved.");
            File.Delete(path);
        }

        private void Cleanup(string path)
        {
            try { File.Delete(path); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { _report("Staging cleanup pending: " + error.Message); }
        }

        private static string Marker(string sourcePath) => Path.Combine(Path.GetDirectoryName(sourcePath), "transaction.json");

        private static RecoveryConflictException Conflict(string path, string message) => new RecoveryConflictException("MSR424_RECOVERY_CONFLICT: " + message + " Source: " + path + "; transaction: " + Marker(path));
    }
}
