using CefFlashBrowser.Services.UserScripts.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mzying2001.MonkeySharp.Core.Domain;
using Mzying2001.MonkeySharp.Core.Repository;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CefFlashBrowser.Tests
{
    public partial class UserScriptPersistenceTests
    {
        private UserScriptSourceFiles Sources => new UserScriptSourceFiles(Path.Combine(_root, "MonkeySharp"), message => { });

        private async Task<UserScriptSourceFiles.Journal> StageAsync(UserScriptInstallation installed, string operation, string source = null)
        {
            var row = await _storage.Database.GetUserScriptAsync(installed.ScriptKey);

            return Sources.Stage(installed.ScriptKey, operation, row.CommitId, source == null ? null : UserScriptFiles.Encode(source));
        }

        private Task CommitAsync(ScriptKey key, UserScriptSourceFiles.Journal journal)
            => _database.ExecuteAsync("UPDATE monkeysharp_scripts SET commit_id = @commit, source_sha256 = @hash WHERE script_id = @id",
                new Dictionary<string, object>
                {
                    ["@id"] = key.ToString(),
                    ["@commit"] = journal.CommitId,
                    ["@hash"] = journal.NewFileHash
                });

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task Recovery_UsesCommitIdWhenNewFileMatchesOldDatabaseHash(bool committed)
        {
            var installed = await InstallAsync();
            var external = Source + "\n// actual old external source";

            File.WriteAllText(PathFor(installed.ScriptKey), external, new UTF8Encoding(false));

            // The target hash equals the old database hash, but the operation's real baseline is different.
            var journal = await StageAsync(installed, "replace", Source);

            Sources.Switch(installed.ScriptKey, journal);

            if (committed)
                await CommitAsync(installed.ScriptKey, journal);

            var loaded = await _persistence.LoadAsync(installed.ScriptKey, None);

            Assert.AreEqual(committed ? Source : external, loaded.Record.Source);
            Assert.IsFalse(File.Exists(Marker(installed.ScriptKey)));
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task CommittedReplace_RestoresVerifiedPendingWhenMainIsOldOrMissing(bool missing)
        {
            var installed = await InstallAsync();
            var next = Source + "\n// committed";
            var journal = await StageAsync(installed, "replace", next);

            await CommitAsync(installed.ScriptKey, journal);

            if (missing)
                File.Delete(PathFor(installed.ScriptKey));

            Assert.AreEqual(next, (await _persistence.LoadAsync(installed.ScriptKey, None)).Record.Source);
            Assert.IsFalse(File.Exists(PathFor(installed.ScriptKey) + ".pending"));
        }

        [TestMethod]
        public async Task CommittedReplace_WithoutNewCandidateDoesNotSubstituteOldBackup()
        {
            var installed = await InstallAsync();
            var journal = await StageAsync(installed, "replace", Source + "\n// committed");

            Sources.Switch(installed.ScriptKey, journal);
            await CommitAsync(installed.ScriptKey, journal);
            File.Delete(PathFor(installed.ScriptKey));

            var loaded = await _persistence.LoadAsync(installed.ScriptKey, None);

            Assert.IsNull(loaded.SourceVersion);
            Assert.IsTrue(loaded.Diagnostics.Any(item => item.Code == "MSR424_RECOVERY_CONFLICT"));
            Assert.IsTrue(File.Exists(PathFor(installed.ScriptKey) + ".backup"));
            Assert.IsTrue(File.Exists(Marker(installed.ScriptKey)));
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task UncommittedReplacementOfMissingSource_RestoresMissingState(bool switched)
        {
            var installed = await InstallAsync();

            File.Delete(PathFor(installed.ScriptKey));

            var journal = await StageAsync(installed, "replace", Source + "\n// new");

            if (switched)
                Sources.Switch(installed.ScriptKey, journal);

            var loaded = await _persistence.LoadAsync(installed.ScriptKey, None);

            Assert.AreEqual("missing", loaded.SourceVersion);
            Assert.IsFalse(File.Exists(PathFor(installed.ScriptKey)));
            Assert.IsFalse(File.Exists(Marker(installed.ScriptKey)));
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task UncommittedDeletion_RestoresObservedSourceOrMissingState(bool missing)
        {
            var installed = await InstallAsync();

            if (missing)
                File.Delete(PathFor(installed.ScriptKey));

            var journal = await StageAsync(installed, "delete");

            Sources.Switch(installed.ScriptKey, journal);

            var loaded = await _persistence.LoadAsync(installed.ScriptKey, None);

            Assert.AreEqual(missing ? null : Source, loaded.Record.Source);
            Assert.IsFalse(File.Exists(Marker(installed.ScriptKey)));
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task Uninstall_RemovesOnlyEmptySourceDirectory(bool hasChildDirectory)
        {
            var installed = await InstallAsync();
            var source = PathFor(installed.ScriptKey);
            var directory = Path.GetDirectoryName(source);
            var child = Path.Combine(directory, "keep");

            if (hasChildDirectory)
                Directory.CreateDirectory(child);

            await _repository.RemoveAsync(installed.ScriptKey, None);

            Assert.IsFalse(File.Exists(source));
            Assert.AreEqual(hasChildDirectory, Directory.Exists(directory));
            Assert.AreEqual(hasChildDirectory, Directory.Exists(child));
        }

        [TestMethod]
        public async Task Startup_RemovesOnlyUnregisteredEmptySourceDirectories()
        {
            var installed = await InstallAsync();
            var installedSource = PathFor(installed.ScriptKey);
            var legacyKey = ScriptKey.Parse(Guid.NewGuid().ToString("D"));
            var legacyDirectory = Path.GetDirectoryName(PathFor(legacyKey));
            var unrelatedDirectory = Path.Combine(Path.GetDirectoryName(legacyDirectory), "unrelated");

            File.Delete(installedSource);
            Directory.CreateDirectory(legacyDirectory);
            Directory.CreateDirectory(unrelatedDirectory);

            var restored = await OpenAsync();

            Assert.IsFalse(Directory.Exists(legacyDirectory));
            Assert.IsTrue(Directory.Exists(Path.GetDirectoryName(installedSource)));
            Assert.IsTrue(Directory.Exists(unrelatedDirectory));
            Assert.AreEqual(installed.ScriptKey, (await restored.GetSnapshotAsync(None)).Single().ScriptKey);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task CommittedDeletion_OrphanScanCleansTombstoneAndRetainsNewUserFile(bool external)
        {
            var installed = await InstallAsync();
            var journal = await StageAsync(installed, "delete");

            Sources.Switch(installed.ScriptKey, journal);
            await _storage.Database.DeleteUserScriptAsync(installed.ScriptKey, null, None);

            if (external)
                File.WriteAllText(PathFor(installed.ScriptKey), "external file");

            Assert.AreEqual(0, (await _persistence.LoadAllAsync(None)).Count);
            Assert.IsFalse(File.Exists(PathFor(installed.ScriptKey) + ".deleted"));
            Assert.IsFalse(File.Exists(Marker(installed.ScriptKey)));
            Assert.AreEqual(external, File.Exists(PathFor(installed.ScriptKey)));
            Assert.AreEqual(external, Directory.Exists(Path.GetDirectoryName(PathFor(installed.ScriptKey))));
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task UncommittedInstallation_OrphanScanRemovesOnlyKnownNewBytes(bool external)
        {
            var key = ScriptKey.Parse(Guid.NewGuid().ToString("D"));
            var journal = Sources.Stage(key, "install", null, UserScriptFiles.Encode(Source));

            Sources.Switch(key, journal);

            if (external)
                File.WriteAllText(PathFor(key), "external file");

            Assert.AreEqual(0, (await _persistence.LoadAllAsync(None)).Count);
            Assert.AreEqual(external, File.Exists(PathFor(key)));
            Assert.AreEqual(external, File.Exists(Marker(key)));

            if (external)
                Assert.AreEqual("external file", File.ReadAllText(PathFor(key)));
        }

        [TestMethod]
        public async Task ExternalContentDuringRollback_IsPreservedAndOnlyOneScriptIsUnavailable()
        {
            var installed = await InstallAsync();
            var other = await InstallAsync();

            _fault = stage =>
            {
                if (stage == "source-switched")
                {
                    File.WriteAllText(PathFor(installed.ScriptKey), "third party source");

                    throw new IOException("SQL commit failed");
                }
            };

            await Assert.ThrowsExceptionAsync<UserScriptPersistenceException>(() => _repository.UpdateAsync(installed.ScriptKey, Source + "\n// new", null, None));

            _fault = null;

            var restored = await OpenAsync();

            Assert.IsFalse((await restored.GetAsync(installed.ScriptKey, None)).IsReady);
            Assert.IsTrue((await restored.GetAsync(other.ScriptKey, None)).CanExecute);
            Assert.AreEqual("third party source", File.ReadAllText(PathFor(installed.ScriptKey)));
            Assert.IsTrue(File.Exists(PathFor(installed.ScriptKey) + ".backup"));
            Assert.IsTrue(File.Exists(Marker(installed.ScriptKey)));
        }

        [TestMethod]
        public async Task InvalidMarkerOrUnrelatedCommit_IsLocalRecoveryConflict()
        {
            var installed = await InstallAsync();

            File.WriteAllText(Marker(installed.ScriptKey), "{}");

            var loaded = await _persistence.LoadAsync(installed.ScriptKey, None);

            Assert.IsTrue(loaded.Diagnostics.Any(item => item.Code == "MSR424_RECOVERY_CONFLICT"));

            File.Delete(Marker(installed.ScriptKey));

            var journal = await StageAsync(installed, "replace", Source + "\n// staged");

            await _database.ExecuteAsync("UPDATE monkeysharp_scripts SET commit_id = '" + Guid.NewGuid().ToString("D") + "'");
            loaded = await _persistence.LoadAsync(installed.ScriptKey, None);

            Assert.IsNull(loaded.SourceVersion);
            Assert.IsTrue(File.Exists(PathFor(installed.ScriptKey) + ".pending"));
            Assert.AreEqual(Source, File.ReadAllText(PathFor(installed.ScriptKey)));
        }

        [TestMethod]
        public async Task UnmarkedBackupAndUnregisteredFiles_AreNeverImportedOrUsedToOverwriteSource()
        {
            var installed = await InstallAsync();
            var external = Source + "\n// user edited";

            File.Copy(PathFor(installed.ScriptKey), PathFor(installed.ScriptKey) + ".backup");
            File.WriteAllText(PathFor(installed.ScriptKey), external, new UTF8Encoding(false));

            var unknown = ScriptKey.Parse(Guid.NewGuid().ToString("D"));

            Directory.CreateDirectory(Path.GetDirectoryName(PathFor(unknown)));
            File.WriteAllText(PathFor(unknown), "unregistered");

            var loaded = await _persistence.LoadAllAsync(None);

            Assert.AreEqual(1, loaded.Count);
            Assert.AreEqual(external, loaded.Single().Record.Source);
            Assert.IsTrue(File.Exists(PathFor(installed.ScriptKey) + ".backup"));
            Assert.IsTrue(File.Exists(PathFor(unknown)));
        }

        [TestMethod]
        public async Task ExistingUnmarkedPending_IsNotDeletedWhenStageCannotCreateItsFile()
        {
            var installed = await InstallAsync();

            File.WriteAllText(PathFor(installed.ScriptKey) + ".pending", Source + "\n// next", new UTF8Encoding(false));

            await Assert.ThrowsExceptionAsync<UserScriptPersistenceException>(() => _repository.UpdateAsync(installed.ScriptKey, Source + "\n// next", null, None));
            Assert.IsTrue(File.Exists(PathFor(installed.ScriptKey) + ".pending"));
            Assert.AreEqual(Source, File.ReadAllText(PathFor(installed.ScriptKey)));
        }

        [TestMethod]
        public async Task MissingCache_AcceptsNewUnpinnedBytesButRejectsChangedPinnedContent()
        {
            var hash = UserScriptFiles.Hash(_provider.Bytes);
            var pinned = await InstallAsync(AssetSource.Replace("dependency.js", "dependency.js#sha256=" + hash));
            var unpinned = await InstallAsync(AssetSource);

            File.Delete(Objects.Single());
            _provider.Bytes = Encoding.UTF8.GetBytes("different remote content");

            var restored = await OpenAsync();

            Assert.IsFalse((await restored.GetAsync(pinned.ScriptKey, None)).CanExecute);
            Assert.IsTrue((await restored.GetAsync(unpinned.ScriptKey, None)).CanExecute);
            Assert.AreEqual("different remote content", (await restored.GetAsync(unpinned.ScriptKey, None)).PreparedRevision.Resources["content"].DecodeText());
        }

        [TestMethod]
        public async Task LockedObjectWrite_FailsBeforePublishingNewAssociations()
        {
            var installed = await InstallAsync(AssetSource);

            using (File.Open(Objects.Single(), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                await Assert.ThrowsExceptionAsync<UserScriptPersistenceException>(() => _repository.RefreshAssetsAsync(installed.ScriptKey, installed.RevisionId, None));

            Assert.AreEqual(installed.RevisionId, (await _repository.GetAsync(installed.ScriptKey, None)).RevisionId);
            Assert.AreEqual(2L, await _database.ScalarAsync("SELECT COUNT(*) FROM monkeysharp_script_assets"));
        }
    }
}
