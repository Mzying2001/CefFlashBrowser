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
    public partial class UserScriptPersistenceTests
    {
        private const string Source = "// ==UserScript==\n// @name Example\n// @match https://example.com/*\n// @grant none\n// ==/UserScript==\nwindow.example = 1;";
        private static readonly CancellationToken None = CancellationToken.None;

        private static string AssetSource => Source.Replace("// @grant none", "// @grant GM.getResourceText\n// @require https://example.com/dependency.js\n// @resource content https://example.com/resource.txt");

        private string _root;
        private SqliteDatabase _database;
        private UserScriptStorage _storage;
        private SqliteUserScriptRepositoryPersistence _persistence;
        private UserScriptRepository _repository;
        private ContentProvider _provider;
        private Action<string> _fault;

        [TestInitialize]
        public async Task Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "CefFlashBrowserScripts_" + Guid.NewGuid().ToString("N"));
            _database = new SqliteDatabase(Path.Combine(_root, "browser.db"));
            _storage = new UserScriptStorage(new DatabaseService(_database));
            _persistence = CreatePersistence();
            _provider = new ContentProvider();
            _repository = await OpenAsync();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await _storage.StopAsync();
            _database.Dispose();
            Directory.Delete(_root, true);
        }

        private SqliteUserScriptRepositoryPersistence CreatePersistence() => new SqliteUserScriptRepositoryPersistence(
            _storage, Path.Combine(_root, "MonkeySharp"), Path.Combine(_root, "Cache", "UserScripts"), checkpoint: stage => _fault?.Invoke(stage));

        private async Task<UserScriptRepository> OpenAsync()
        {
            var repository = new UserScriptRepository(_persistence, preparer: new UserScriptPreparer(_provider, _provider));

            await repository.InitializeAsync(None);

            return repository;
        }

        private string PathFor(ScriptKey key) => Path.Combine(_root, "MonkeySharp", key.ToString(), "userscript.js");

        private string[] Objects => Directory.GetFiles(Path.Combine(_root, "Cache", "UserScripts", "objects"));

        private string Marker(ScriptKey key) => Path.Combine(Path.GetDirectoryName(PathFor(key)), "transaction.json");

        private Task<UserScriptInstallation> InstallAsync(string source = Source, bool enabled = true)
            => _repository.InstallAsync(source, "https://example.com/test.user.js", enabled, None);

        [TestMethod]
        public async Task RoundTrip_StoresOnlyDescriptorsAndPreservesIdentityOrderTimes()
        {
            var first = await InstallAsync(AssetSource);
            var second = await InstallAsync();
            var path = PathFor(first.ScriptKey);
            var time = DateTime.UtcNow.AddHours(-1);

            File.SetLastWriteTimeUtc(path, time);
            var disabled = await _repository.SetEnabledAsync(first.ScriptKey, false, None);

            Assert.AreEqual(time, File.GetLastWriteTimeUtc(path));

            var changed = await _repository.UpdateAsync(second.ScriptKey, Source.Replace("Example", "Renamed"), null, None);
            var restored = await OpenAsync();
            var records = await restored.GetSnapshotAsync(None);

            CollectionAssert.AreEqual(new[] { first.ScriptKey, second.ScriptKey }, records.Select(record => record.ScriptKey).ToArray());
            Assert.IsFalse(records[0].IsEnabled);
            Assert.IsTrue(records[0].IsReady);
            Assert.AreEqual(first.InstalledAt.ToUnixTimeMilliseconds(), records[0].InstalledAt.ToUnixTimeMilliseconds());
            Assert.AreEqual(disabled.UpdatedAt.ToUnixTimeMilliseconds(), records[0].UpdatedAt.ToUnixTimeMilliseconds());
            Assert.AreEqual(second.InstalledAt.ToUnixTimeMilliseconds(), records[1].InstalledAt.ToUnixTimeMilliseconds());
            Assert.AreEqual(changed.UpdatedAt.ToUnixTimeMilliseconds(), records[1].UpdatedAt.ToUnixTimeMilliseconds());
            Assert.AreEqual("Renamed", records[1].Definition.Metadata.Name);
            Assert.AreEqual(1, Objects.Length);
            CollectionAssert.AreEqual(_provider.Bytes, File.ReadAllBytes(Objects.Single()));
            Assert.AreEqual(2L, await _database.ScalarAsync("SELECT COUNT(*) FROM monkeysharp_script_assets"));

            foreach (var table in new[] { "monkeysharp_scripts", "monkeysharp_script_assets" })
            {
                var columns = await _database.QueryAsync("PRAGMA table_info(" + table + ")", row => row.GetString(1));

                Assert.IsFalse(columns.Intersect(new[] { "source", "text", "bytes", "base64", "prepared_revision_json", "display_name" }).Any());
            }
        }

        [TestMethod]
        public async Task OfflineRestartAndBodyEdit_ReuseObjectsWithoutNetworkOrRewrites()
        {
            var installed = await InstallAsync(AssetSource);
            var path = Objects.Single();
            var time = DateTime.UtcNow.AddHours(-2);

            File.SetLastWriteTimeUtc(path, time);

            var commit = await _database.ScalarAsync("SELECT commit_id FROM monkeysharp_scripts");

            _provider.Offline = true;

            var restored = await OpenAsync();

            Assert.AreEqual(2, _provider.Reads);
            Assert.AreEqual(commit, await _database.ScalarAsync("SELECT commit_id FROM monkeysharp_scripts"));

            var snapshot = await restored.GetAsync(installed.ScriptKey, None);

            Assert.AreEqual("prepared content", snapshot.PreparedRevision.Resources["content"].DecodeText());

            await restored.UpdateAsync(installed.ScriptKey, AssetSource + "\n// body edit", null, None);

            Assert.AreEqual(2, _provider.Reads);
            Assert.AreEqual(time, File.GetLastWriteTimeUtc(path));
        }

        [TestMethod]
        public async Task RenameAndDuplicateDeclarations_ReuseContentInCurrentOrder()
        {
            var installed = await InstallAsync(AssetSource);
            var changed = AssetSource.Replace("@resource content", "@resource renamed")
                .Replace("// @require https://example.com/dependency.js", "// @require https://example.com/dependency.js\n// @require https://example.com/dependency.js");

            await _repository.UpdateAsync(installed.ScriptKey, changed, null, None);

            var loaded = await _persistence.LoadAsync(installed.ScriptKey, None);

            Assert.AreEqual(2, _provider.Reads);
            Assert.AreEqual(3, loaded.Record.Assets.Count);
            CollectionAssert.AreEqual(new[] { 0, 1 }, loaded.Record.Assets.Where(asset => asset.Kind == UserScriptAssetKind.Require).Select(asset => asset.Ordinal).ToArray());
            Assert.AreEqual("renamed", loaded.Record.Assets.Single(asset => asset.Kind == UserScriptAssetKind.Resource).Name);
        }

        [TestMethod]
        public async Task ExternalReloadAndRefresh_PreserveBomNewlinesBytesAndTime()
        {
            var installed = await InstallAsync(AssetSource);
            var source = AssetSource.Replace("\n", "\r\n") + "\r\n// external";
            var bytes = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(source)).ToArray();

            File.WriteAllBytes(PathFor(installed.ScriptKey), bytes);

            var time = DateTime.UtcNow.AddHours(-3);

            File.SetLastWriteTimeUtc(PathFor(installed.ScriptKey), time);

            Assert.IsTrue((await _persistence.LoadAsync(installed.ScriptKey, None)).SourceChangedSinceSave);

            var reloaded = await _repository.ReloadAsync(installed.ScriptKey, None);

            Assert.AreEqual(source, reloaded.Definition.Source);

            _provider.Bytes = Encoding.UTF8.GetBytes("new content");
            await _repository.RefreshAssetsAsync(installed.ScriptKey, reloaded.RevisionId, None);

            Assert.AreEqual(4, _provider.Reads);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(PathFor(installed.ScriptKey)));
            Assert.AreEqual(time, File.GetLastWriteTimeUtc(PathFor(installed.ScriptKey)));
            Assert.IsFalse((await _persistence.LoadAsync(installed.ScriptKey, None)).SourceChangedSinceSave);
        }

        [TestMethod]
        public async Task StaleSourceAndRevision_CannotOverwriteExternalEdits()
        {
            var installed = await InstallAsync();
            var external = Source + "\n// external";

            File.WriteAllText(PathFor(installed.ScriptKey), external, new UTF8Encoding(false));

            await Assert.ThrowsExceptionAsync<UserScriptSourceConflictException>(() => _repository.UpdateAsync(installed.ScriptKey, Source, null, None));
            Assert.AreEqual(external, File.ReadAllText(PathFor(installed.ScriptKey)));

            var reloaded = await _repository.ReloadAsync(installed.ScriptKey, None);

            await Assert.ThrowsExceptionAsync<RepositoryRevisionMismatchException>(() => _repository.UpdateIfUnchangedAsync(installed.ScriptKey, installed.RevisionId, Source, null, None));
            Assert.AreEqual(external, (await (await OpenAsync()).GetAsync(installed.ScriptKey, None)).Definition.Source);
            Assert.AreNotEqual(installed.RevisionId, reloaded.RevisionId);
        }

        [TestMethod]
        public async Task MissingAndInvalidSource_CanBeRepairedAfterObservingActualVersion()
        {
            var installed = await InstallAsync();

            File.Delete(PathFor(installed.ScriptKey));

            Assert.AreEqual("missing", (await _persistence.LoadAsync(installed.ScriptKey, None)).SourceVersion);
            await Assert.ThrowsExceptionAsync<UserScriptSourceConflictException>(() => _repository.UpdateAsync(installed.ScriptKey, Source, null, None));

            var missing = await _repository.ReloadAsync(installed.ScriptKey, None);

            Assert.IsFalse(missing.IsReady);

            await _repository.UpdateIfUnchangedAsync(installed.ScriptKey, missing.RevisionId, Source, null, None);
            File.WriteAllBytes(PathFor(installed.ScriptKey), new byte[] { 255 });

            var invalid = await _persistence.LoadAsync(installed.ScriptKey, None);

            Assert.IsNull(invalid.Record.Source);
            Assert.IsNotNull(invalid.SourceVersion);

            var snapshot = await _repository.ReloadAsync(installed.ScriptKey, None);

            Assert.IsTrue((await _repository.UpdateIfUnchangedAsync(installed.ScriptKey, snapshot.RevisionId, Source, null, None)).CanExecute);
        }

        [TestMethod]
        public async Task LockedSource_HasNoReliableVersion_AndDoesNotAffectOtherScripts()
        {
            var installed = await InstallAsync();
            var other = await InstallAsync();

            using (File.Open(PathFor(installed.ScriptKey), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.IsNull((await _persistence.LoadAsync(installed.ScriptKey, None)).SourceVersion);

                var restored = await OpenAsync();

                Assert.IsFalse((await restored.GetAsync(installed.ScriptKey, None)).IsReady);
                Assert.IsTrue((await restored.GetAsync(other.ScriptKey, None)).CanExecute);
                await Assert.ThrowsExceptionAsync<UserScriptPersistenceException>(() => restored.UpdateAsync(installed.ScriptKey, Source, null, None));

                await restored.SetEnabledAsync(installed.ScriptKey, false, None);
            }

            Assert.AreEqual(Source, File.ReadAllText(PathFor(installed.ScriptKey)));
        }

        [TestMethod]
        public async Task LostAndCorruptCache_RedownloadsOrIsolatesOfflineFailure()
        {
            var installed = await InstallAsync(AssetSource);
            var other = await InstallAsync();

            File.WriteAllBytes(Objects.Single(), new byte[] { 1 });

            var loaded = await _persistence.LoadAsync(installed.ScriptKey, None);

            Assert.IsTrue(loaded.Record.Assets.All(asset => asset.Content == null));

            await _repository.ReloadAsync(installed.ScriptKey, None);

            Assert.AreEqual(4, _provider.Reads);
            CollectionAssert.AreEqual(_provider.Bytes, File.ReadAllBytes(Objects.Single()));

            Directory.Delete(Path.Combine(_root, "Cache"), true);
            _provider.Offline = true;

            var restored = await OpenAsync();

            Assert.IsFalse((await restored.GetAsync(installed.ScriptKey, None)).IsReady);
            Assert.IsTrue((await restored.GetAsync(other.ScriptKey, None)).CanExecute);

            _provider.Offline = false;

            Assert.IsTrue((await restored.ReloadAsync(installed.ScriptKey, None)).CanExecute);
        }

        [TestMethod]
        public async Task DisabledStartup_DoesNotFetch_AndFailedEnableRetainsIntent()
        {
            var installed = await InstallAsync(AssetSource, false);

            File.Delete(Objects.Single());

            var restored = await OpenAsync();

            Assert.AreEqual(2, _provider.Reads);

            var disabled = await restored.GetAsync(installed.ScriptKey, None);

            Assert.IsFalse(disabled.IsEnabled);
            Assert.IsFalse(disabled.IsReady);

            _provider.Offline = true;

            var enabled = await restored.SetEnabledAsync(installed.ScriptKey, true, None);

            Assert.IsTrue(enabled.IsEnabled);
            Assert.IsFalse(enabled.CanExecute);
            Assert.IsTrue(enabled.Diagnostics.Any());
            Assert.AreEqual(1L, await _database.ScalarAsync("SELECT is_enabled FROM monkeysharp_scripts"));
        }

        [DataTestMethod]
        [DataRow("objects-written")]
        [DataRow("pending")]
        [DataRow("marker-staged")]
        [DataRow("marker-written")]
        [DataRow("rows-written")]
        [DataRow("source-switched")]
        public async Task FailedSave_RollsBackFileDatabaseAndSnapshot(string stage)
        {
            var installed = await InstallAsync();
            var commit = await _database.ScalarAsync("SELECT commit_id FROM monkeysharp_scripts");

            _fault = point =>
            {
                if (point == stage)
                    throw new IOException("simulated failure");
            };

            await Assert.ThrowsExceptionAsync<UserScriptPersistenceException>(() => _repository.UpdateAsync(installed.ScriptKey, Source + "\n// changed", null, None));

            _fault = null;

            Assert.AreEqual(Source, File.ReadAllText(PathFor(installed.ScriptKey)));
            Assert.AreEqual(commit, await _database.ScalarAsync("SELECT commit_id FROM monkeysharp_scripts"));
            Assert.AreEqual(installed.RevisionId, (await _repository.GetAsync(installed.ScriptKey, None)).RevisionId);
            Assert.AreEqual(Source, (await _persistence.LoadAsync(installed.ScriptKey, None)).Record.Source);
        }

        [TestMethod]
        public async Task FailedRollback_RetainsMarker_AndOtherInstallationsRemainWritable()
        {
            var installed = await InstallAsync();
            var other = await InstallAsync();

            _fault = stage =>
            {
                if (stage == "source-switched" || stage == "restore")
                    throw new IOException(stage);
            };

            await Assert.ThrowsExceptionAsync<UserScriptPersistenceException>(() => _repository.UpdateAsync(installed.ScriptKey, Source + "\n// changed", null, None));
            Assert.IsTrue(File.Exists(Marker(installed.ScriptKey)));

            _fault = null;
            await _repository.UpdateAsync(other.ScriptKey, Source + "\n// other", null, None);

            Assert.AreEqual(Source, (await _persistence.LoadAsync(installed.ScriptKey, None)).Record.Source);
            Assert.IsFalse(File.Exists(Marker(installed.ScriptKey)));
        }

        [DataTestMethod]
        [DataRow("marker-written")]
        [DataRow("rows-written")]
        public async Task CancellationBeforeSwitch_DoesNotPublishInstallation(string stage)
        {
            using (var cancellation = new CancellationTokenSource())
            {
                _fault = point =>
                {
                    if (point == stage)
                        cancellation.Cancel();
                };

                await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => _repository.InstallAsync(Source, null, true, cancellation.Token));

                _fault = null;

                Assert.AreEqual(0, (await _persistence.LoadAllAsync(None)).Count);
                Assert.AreEqual(0, (await _repository.GetSnapshotAsync(None)).Count);
            }
        }

        [TestMethod]
        public async Task CommittedCancellationAndCleanupFailure_StillPublishSuccessOnce()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                var events = 0;

                _repository.Changed += (sender, args) => events++;
                _fault = stage =>
                {
                    if (stage == "source-switched")
                        cancellation.Cancel();

                    if (stage == "cleanup" || stage == "committed")
                        throw new IOException(stage);
                };

                var installed = await _repository.InstallAsync(Source, null, true, cancellation.Token);

                Assert.AreEqual(1, events);
                Assert.IsTrue(installed.CanExecute);
                Assert.IsTrue(File.Exists(Marker(installed.ScriptKey)));

                _fault = null;

                Assert.AreEqual(Source, (await _persistence.LoadAsync(installed.ScriptKey, None)).Record.Source);
                Assert.IsFalse(File.Exists(Marker(installed.ScriptKey)));
            }
        }

        [TestMethod]
        public async Task SharedObjectsAndQueuedCollection_ProtectUncommittedAndDisabledReferences()
        {
            Task collection = null;

            _fault = stage =>
            {
                if (stage == "objects-written")
                    collection = _persistence.CollectAsync(None);
            };

            var first = await InstallAsync(AssetSource);

            await collection;
            _fault = null;

            var second = await InstallAsync(AssetSource, false);

            Assert.AreEqual(1, Objects.Length);

            _provider.Bytes = Encoding.UTF8.GetBytes("updated content");
            await _repository.RefreshAssetsAsync(first.ScriptKey, first.RevisionId, None);

            Assert.AreEqual(2, Objects.Length);

            await _repository.RemoveAsync(first.ScriptKey, None);

            Assert.AreEqual(1, Objects.Length);
            CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("prepared content"), File.ReadAllBytes(Objects.Single()));

            await _repository.RemoveAsync(second.ScriptKey, None);

            Assert.AreEqual(0, Objects.Length);
        }

        [TestMethod]
        public async Task BrokenReferences_SkipCollection_AndIsolateOnlyAffectedInstallation()
        {
            var installed = await InstallAsync(AssetSource);
            var other = await InstallAsync();
            var orphan = Path.Combine(Path.GetDirectoryName(Objects.Single()), new string('a', 64));

            File.WriteAllText(orphan, "unreferenced");
            await _database.ExecuteAsync("UPDATE monkeysharp_script_assets SET content_sha256 = 'invalid'");
            await _persistence.CollectAsync(None);

            Assert.IsTrue(File.Exists(orphan));

            var restored = await OpenAsync();

            Assert.IsFalse((await restored.GetAsync(installed.ScriptKey, None)).IsReady);
            Assert.IsTrue((await restored.GetAsync(other.ScriptKey, None)).CanExecute);

            await restored.RemoveAsync(installed.ScriptKey, None);
        }

        [TestMethod]
        public async Task Uninstall_DeletesAssociatedRowsAndKeepsUnknownFiles()
        {
            var installed = await InstallAsync(AssetSource);
            var id = installed.ScriptKey.ToString();

            await _database.ExecuteAsync("INSERT INTO monkeysharp_values VALUES ('" + id + "', '', 'u', 0, '')");
            await _database.ExecuteAsync("INSERT INTO monkeysharp_permission_decisions VALUES ('" + id + "', 'kind', 'origin', 1, '')");
            await _database.ExecuteAsync("INSERT INTO monkeysharp_tab_states VALUES ('" + id + "', 'tab', '{}', '')");

            var unknown = Path.Combine(Path.GetDirectoryName(PathFor(installed.ScriptKey)), "keep.txt");

            File.WriteAllText(unknown, "external file");
            _fault = stage =>
            {
                if (stage == "source-switched")
                    throw new IOException("delete refused");
            };

            await Assert.ThrowsExceptionAsync<UserScriptPersistenceException>(() => _repository.RemoveAsync(installed.ScriptKey, None));
            Assert.AreEqual(2L, await _database.ScalarAsync("SELECT COUNT(*) FROM monkeysharp_script_assets"));

            _fault = null;
            await _repository.RemoveAsync(installed.ScriptKey, None);

            foreach (var table in new[] { "monkeysharp_scripts", "monkeysharp_script_assets", "monkeysharp_values", "monkeysharp_permission_decisions", "monkeysharp_tab_states" })
                Assert.AreEqual(0L, await _database.ScalarAsync("SELECT COUNT(*) FROM " + table));

            Assert.IsTrue(File.Exists(unknown));
            Assert.IsFalse(File.Exists(PathFor(installed.ScriptKey)));
        }

        [TestMethod]
        public async Task LiteralBom_RoundTripsLogicalSource()
        {
            var installed = await InstallAsync("\uFEFF" + Source);
            var restored = await OpenAsync();

            Assert.AreEqual("\uFEFF" + Source, (await restored.GetAsync(installed.ScriptKey, None)).Definition.Source);
        }

        [TestMethod]
        public async Task Startup_AdoptsExternalSourceWithNoWatcherOrPriorReload()
        {
            var installed = await InstallAsync();
            var external = Source.Replace("@name Example", "@name External startup") + "\n// saved outside the app";

            File.WriteAllText(PathFor(installed.ScriptKey), external, new UTF8Encoding(false));

            var restored = await OpenAsync();
            var snapshot = await restored.GetAsync(installed.ScriptKey, None);

            Assert.AreEqual("External startup", snapshot.Definition.Metadata.Name);
            Assert.IsTrue(snapshot.CanExecute);
            Assert.AreEqual(external, File.ReadAllText(PathFor(installed.ScriptKey)));
            Assert.IsFalse((await _persistence.LoadAsync(installed.ScriptKey, None)).SourceChangedSinceSave);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ResourceBytesAndResponseEncoding_RoundTripWithoutTextCopies(bool binary)
        {
            _provider.Bytes = binary ? new byte[] { 0, 255, 254, 1 } : new byte[] { 99, 97, 102, 233 };
            _provider.Mime = binary ? "application/octet-stream" : "text/plain";
            _provider.Charset = binary ? "utf-8" : "windows-1252";

            var installed = await InstallAsync(Source.Replace("// @grant none", "// @grant GM.getResourceText\n// @resource content https://example.com/resource"));

            _provider.Offline = true;

            var restored = await OpenAsync();
            var content = (await restored.GetAsync(installed.ScriptKey, None)).PreparedRevision.Resources["content"];

            CollectionAssert.AreEqual(_provider.Bytes, content.Bytes);
            Assert.AreEqual(_provider.Mime, content.MediaType);
            Assert.AreEqual(_provider.Charset, content.Charset);

            if (!binary)
                Assert.AreEqual("caf\u00e9", content.DecodeText());
        }

        private sealed class ContentProvider : IUserScriptDependencyProvider, IResourceProvider
        {
            internal int Reads;
            internal bool Offline;
            internal byte[] Bytes = Encoding.UTF8.GetBytes("prepared content");
            internal string Mime = "text/plain";
            internal string Charset = "utf-8";

            private Task<ResourceContent> Read()
            {
                Reads++;

                if (Offline)
                    throw new IOException("offline");

                return Task.FromResult(new ResourceContent(Bytes, Mime, Charset));
            }

            public Task<ResourceContent> GetAsync(UserScriptInstallation installation, ResourceDeclaration resource, CancellationToken token) => Read();

            public Task<ResourceContent> GetScriptAsync(UserScriptInstallation installation, UserScriptDependencyDeclaration dependency, CancellationToken token) => Read();
        }
    }
}
