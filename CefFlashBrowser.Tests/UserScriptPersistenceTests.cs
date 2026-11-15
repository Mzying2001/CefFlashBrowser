using CefFlashBrowser.Database;
using CefFlashBrowser.Services;
using CefFlashBrowser.Services.UserScripts.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mzying2001.MonkeySharp.Core.Apis;
using Mzying2001.MonkeySharp.Core.Domain;
using Mzying2001.MonkeySharp.Core.Repository;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Tests
{
    [TestClass]
    public class UserScriptPersistenceTests
    {
        private const string Source = "// ==UserScript==\n// @name Example\n// @match https://example.com/*\n// @grant none\n// ==/UserScript==\nwindow.example = 1;";
        private string _root;
        private SqliteDatabase _database;
        private UserScriptStorage _storage;
        private SqliteUserScriptRepositoryPersistence _persistence;
        private PersistentUserScriptRepository _repository;
        private Action<string> _fault;

        [TestInitialize]
        public async Task Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "CefFlashBrowserScripts_" + Guid.NewGuid().ToString("N"));
            _database = new SqliteDatabase(Path.Combine(_root, "browser.db"));
            _storage = new UserScriptStorage(new DatabaseService(_database));
            _persistence = CreatePersistence();
            _repository = new PersistentUserScriptRepository(_persistence);
            await _repository.InitializeAsync(CancellationToken.None);
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await _storage.StopAsync();
            _database.Dispose();
            Directory.Delete(_root, true);
        }

        private SqliteUserScriptRepositoryPersistence CreatePersistence() => new SqliteUserScriptRepositoryPersistence(
            _storage, Path.Combine(_root, "MonkeySharp"), checkpoint: stage => _fault?.Invoke(stage));

        private string PathFor(ScriptKey key) => Path.Combine(_root, "MonkeySharp", key.ToString(), "userscript.js");
        private Task<UserScriptInstallation> InstallAsync() => _repository.InstallAsync(Source, "https://example.com/test.user.js", true, CancellationToken.None);

        [TestMethod]
        public async Task RoundTrip_PreservesIdentityOrderTimesAndPreparedPayload()
        {
            var first = await InstallAsync();
            var second = await InstallAsync();
            Assert.AreNotEqual(first.ScriptKey, second.ScriptKey);
            var path = PathFor(first.ScriptKey);
            var writeTime = DateTime.UtcNow.AddHours(-1);
            File.SetLastWriteTimeUtc(path, writeTime);
            await _repository.SetEnabledAsync(first.ScriptKey, false, CancellationToken.None);
            Assert.AreEqual(writeTime, File.GetLastWriteTimeUtc(path));
            var changed = await _repository.UpdateAsync(second.ScriptKey, Source.Replace("Example", "Renamed"), null, CancellationToken.None);

            var restored = new PersistentUserScriptRepository(CreatePersistence());
            await restored.InitializeAsync(CancellationToken.None);
            var records = await restored.GetSnapshotAsync(CancellationToken.None);
            CollectionAssert.AreEqual(new[] { first.ScriptKey, second.ScriptKey }, records.Select(record => record.ScriptKey).ToArray());
            Assert.IsFalse(records[0].IsEnabled);
            Assert.AreEqual(first.InstalledAt, records[0].InstalledAt);
            Assert.AreEqual(changed.UpdatedAt, records[1].UpdatedAt);
            Assert.AreEqual("Renamed", records[1].Definition.Metadata.Name);
            Assert.IsNotNull(records[0].PreparedRevision);
            Assert.IsFalse(File.Exists(path + ".backup"));
        }

        [DataTestMethod]
        [DataRow("pending")]
        [DataRow("replaced")]
        public async Task FailedSave_RollsBackFileDatabaseAndRepository(string stage)
        {
            var installed = await InstallAsync();
            _fault = point => { if (point == stage) throw new IOException("simulated failure"); };
            await Assert.ThrowsExceptionAsync<IOException>(() => _repository.UpdateAsync(installed.ScriptKey, Source + "\n// changed", null, CancellationToken.None));
            _fault = null;
            Assert.AreEqual(Source, File.ReadAllText(PathFor(installed.ScriptKey)));
            Assert.AreEqual(installed.RevisionId, (await _repository.GetAsync(installed.ScriptKey, CancellationToken.None)).RevisionId);
            Assert.AreEqual(Source, (await CreatePersistence().LoadAsync(CancellationToken.None)).Single().Source);
        }

        [TestMethod]
        public async Task FailedRollback_BlocksMutationsUntilRecovery_AndRetainsCommittedSource()
        {
            var installed = await InstallAsync();
            _fault = stage => { if (stage == "replaced" || stage == "restore") throw new IOException(stage); };
            await Assert.ThrowsExceptionAsync<AggregateException>(() => _repository.UpdateAsync(installed.ScriptKey, Source + "\n// uncommitted", null, CancellationToken.None));
            _fault = null;
            await Assert.ThrowsExceptionAsync<IOException>(() => _repository.SetEnabledAsync(installed.ScriptKey, false, CancellationToken.None));
            var loaded = await _persistence.LoadAsync(CancellationToken.None);
            Assert.AreEqual(Source, loaded.Single().Source);
            await _repository.SetEnabledAsync(installed.ScriptKey, false, CancellationToken.None);
        }

        [DataTestMethod]
        [DataRow(".backup")]
        [DataRow(".deleted")]
        public async Task Restart_RecoversOnlyCommittedDigest(string suffix)
        {
            var installed = await InstallAsync();
            var path = PathFor(installed.ScriptKey);
            File.Move(path, path + suffix);
            if (suffix == ".backup")
                File.WriteAllText(path, "uncommitted new source");
            File.WriteAllText(path + ".pending", "incomplete");
            Assert.AreEqual(Source, (await CreatePersistence().LoadAsync(CancellationToken.None)).Single().Source);
            Assert.AreEqual(Source, File.ReadAllText(path));
            Assert.IsFalse(File.Exists(path + suffix));
            Assert.IsFalse(File.Exists(path + ".pending"));
        }

        [TestMethod]
        public async Task Commit_CancellationAndCleanupFailure_DoNotReportFailure()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                _fault = stage =>
                {
                    if (stage == "replaced") cancellation.Cancel();
                    if (stage == "cleanup") throw new IOException("cleanup failed");
                };
                var installed = await _repository.InstallAsync(Source, null, true, cancellation.Token);
                Assert.IsTrue(cancellation.IsCancellationRequested);
                Assert.IsTrue((await _repository.GetAsync(installed.ScriptKey, CancellationToken.None)).IsEnabled);
                _fault = null;
                Assert.AreEqual(Source, (await CreatePersistence().LoadAsync(CancellationToken.None)).Single().Source);
            }
        }

        [TestMethod]
        public async Task MissingPrepared_IsUnavailableWithoutImplicitPreparation_ButMaintenanceKeepsSource()
        {
            var installed = await InstallAsync();
            await _database.ExecuteAsync("UPDATE monkeysharp_scripts SET prepared_revision_json = ''");
            var records = await _persistence.LoadAsync(CancellationToken.None);
            Assert.IsNull(records.Single().Source);
            var maintenance = (await _persistence.ReadMaintenanceAsync(CancellationToken.None)).Single();
            Assert.AreEqual(Source, maintenance.Record.Source);
            Assert.IsTrue(maintenance.Diagnostics.Any());
            var restored = new PersistentUserScriptRepository(_persistence);
            await restored.InitializeAsync(CancellationToken.None);
            Assert.IsFalse((await restored.GetAsync(installed.ScriptKey, CancellationToken.None)).IsEnabled);
        }

        [TestMethod]
        public async Task CorruptPrepared_BlocksInitializationWithoutErasingMaintenanceData()
        {
            var installed = await InstallAsync();
            await _database.ExecuteAsync("UPDATE monkeysharp_scripts SET prepared_revision_json = '{\"format\":999}'");
            var restored = new PersistentUserScriptRepository(_persistence);
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => restored.InitializeAsync(CancellationToken.None));
            Assert.AreEqual(installed.ScriptKey, (await _persistence.ReadMaintenanceAsync(CancellationToken.None)).Single().Record.ScriptKey);
            Assert.AreEqual(Source, File.ReadAllText(PathFor(installed.ScriptKey)));
        }

        [TestMethod]
        public async Task UnavailableSource_IsRetained_AndUnreviewedEnableDoesNotAdoptChanges()
        {
            var installed = await InstallAsync();
            File.WriteAllBytes(PathFor(installed.ScriptKey), new byte[] { 255, 254 });
            Assert.IsNull((await _persistence.LoadAsync(CancellationToken.None)).Single().Source);
            await Assert.ThrowsExceptionAsync<IOException>(() => _repository.SetEnabledAsync(installed.ScriptKey, false, CancellationToken.None));
            CollectionAssert.AreEqual(new byte[] { 255, 254 }, File.ReadAllBytes(PathFor(installed.ScriptKey)));
            await _repository.UpdateAsync(installed.ScriptKey, Source + "\n// reviewed", null, CancellationToken.None);
            Assert.AreEqual(Source + "\n// reviewed", (await _persistence.LoadAsync(CancellationToken.None)).Single().Source);
        }

        [TestMethod]
        public async Task Uninstall_DeletesAssociatedRows_PreservesUnknownFiles_AndCannotResurrect()
        {
            var installed = await InstallAsync();
            var id = installed.ScriptKey.ToString();
            await _database.ExecuteAsync("INSERT INTO monkeysharp_values VALUES ('" + id + "', '', 'u', 0, '')");
            await _database.ExecuteAsync("INSERT INTO monkeysharp_permission_decisions VALUES ('" + id + "', 'kind', 'origin', 1, '')");
            await _database.ExecuteAsync("INSERT INTO monkeysharp_tab_states VALUES ('" + id + "', 'tab', '{}', '')");
            var unknown = Path.Combine(Path.GetDirectoryName(PathFor(installed.ScriptKey)), "keep.txt");
            File.WriteAllText(unknown, "external file");
            _fault = stage => { if (stage == "cleanup") throw new IOException("busy"); };
            await _repository.RemoveAsync(installed.ScriptKey, CancellationToken.None);
            _fault = null;
            Assert.AreEqual(0, (await _persistence.LoadAsync(CancellationToken.None)).Count);
            foreach (var table in new[] { "monkeysharp_scripts", "monkeysharp_values", "monkeysharp_permission_decisions", "monkeysharp_tab_states" })
                Assert.AreEqual(0L, await _database.ScalarAsync("SELECT COUNT(*) FROM " + table));
            Assert.IsTrue(File.Exists(unknown));
            Assert.IsFalse(File.Exists(PathFor(installed.ScriptKey) + ".deleted"));
        }

        [TestMethod]
        public async Task DeleteFailure_RestoresSourceAndInstallation()
        {
            var installed = await InstallAsync();
            _fault = stage => { if (stage == "deleted") throw new IOException("commit refused"); };
            await Assert.ThrowsExceptionAsync<IOException>(() => _repository.RemoveAsync(installed.ScriptKey, CancellationToken.None));
            _fault = null;
            Assert.AreEqual(Source, (await _persistence.LoadAsync(CancellationToken.None)).Single().Source);
        }

        [TestMethod]
        public async Task PreparedDependencies_RestoreOfflineWithoutFetchingAgain()
        {
            var provider = new ContentProvider();
            var repository = new PersistentUserScriptRepository(_persistence, preparer: new UserScriptPreparer(provider, provider));
            await repository.InitializeAsync(CancellationToken.None);
            var source = Source.Replace("// @grant none", "// @grant GM.getResourceText\n// @require https://example.com/dependency.js\n// @resource content https://example.com/resource.txt");
            var installed = await repository.InstallAsync(source, null, true, CancellationToken.None);
            Assert.AreEqual(2, provider.Reads);
            provider.Offline = true;
            var restored = new PersistentUserScriptRepository(CreatePersistence(), preparer: new UserScriptPreparer(provider, provider));
            await restored.InitializeAsync(CancellationToken.None);
            var snapshot = await restored.GetAsync(installed.ScriptKey, CancellationToken.None);
            Assert.AreEqual(2, provider.Reads);
            Assert.AreEqual("prepared content", snapshot.PreparedRevision.Resources["content"].DecodeText());
        }

        [TestMethod]
        public async Task LockedSource_IsUnavailableAndNotDeleted()
        {
            var installed = await InstallAsync();
            using (File.Open(PathFor(installed.ScriptKey), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.IsNull((await _persistence.LoadAsync(CancellationToken.None)).Single().Source);
            }
            Assert.AreEqual(Source, (await _persistence.LoadAsync(CancellationToken.None)).Single().Source);
        }

        private sealed class ContentProvider : IUserScriptDependencyProvider, IResourceProvider
        {
            public int Reads;
            public bool Offline;
            private Task<ResourceContent> Read()
            {
                Reads++;
                if (Offline) throw new IOException("offline");
                return Task.FromResult(new ResourceContent(Encoding.UTF8.GetBytes("prepared content"), "text/plain", "prepared content"));
            }
            public Task<ResourceContent> GetAsync(UserScriptInstallation installation, ResourceDeclaration resource, CancellationToken token) => Read();
            public Task<ResourceContent> GetScriptAsync(UserScriptInstallation installation, UserScriptDependencyDeclaration dependency, CancellationToken token) => Read();
        }

        [TestMethod]
        public async Task LiteralBom_RoundTripsWithoutPreparedSourceMismatch()
        {
            var installed = await _repository.InstallAsync("\uFEFF" + Source, null, true, CancellationToken.None);
            var restored = new PersistentUserScriptRepository(CreatePersistence());
            await restored.InitializeAsync(CancellationToken.None);
            Assert.AreEqual("\uFEFF" + Source, (await restored.GetAsync(installed.ScriptKey, CancellationToken.None)).Definition.Source);
        }
    }
}
