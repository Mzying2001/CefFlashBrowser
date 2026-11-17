using CefFlashBrowser.Database;
using CefFlashBrowser.Models;
using CefFlashBrowser.Models.UserScripts;
using CefFlashBrowser.Services;
using CefFlashBrowser.Services.UserScripts.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mzying2001.MonkeySharp.Core.Domain;
using Mzying2001.MonkeySharp.Core.Permissions;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Tests
{
    [TestClass]
    public class DatabaseServiceTests
    {
        private string _testDir;
        private SqliteDatabase _database;
        private DatabaseService _service;

        [TestInitialize]
        public void Setup()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "CefFlashBrowserDatabaseServiceTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
            _database = new SqliteDatabase(Path.Combine(_testDir, "browser.db"));
            _service = new DatabaseService(_database);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _database.Dispose();
            Directory.Delete(_testDir, true);
        }

        [TestMethod]
        public void Construction_DoesNotOpenDatabase()
        {
            Assert.IsFalse(File.Exists(Path.Combine(_testDir, "browser.db")));
        }

        [TestMethod]
        public async Task ConcurrentConsumers_ShareInitialization_AndWaitBeforeReadingOrWriting()
        {
            var controlled = new ControlledDatabase(_database) { HoldInitialization = true };
            _service = new DatabaseService(controlled);
            var initialization = _service.InitializeAsync();
            try
            {
                await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
                    Assert.AreSame(initialization, _service.InitializeAsync()))));
                var read = _service.GetWebsiteIconsAsync();
                var write = _service.SaveWebsiteIconAsync(new WebsiteIconRecord("https://example.com/a", "https://example.com/icon", 10));
                Assert.IsFalse(read.IsCompleted);
                Assert.IsFalse(write.IsCompleted);
                Assert.IsFalse(File.Exists(Path.Combine(_testDir, "browser.db")));
                controlled.ReleaseInitialization.TrySetResult(true);
                await Task.WhenAll(initialization, read, write);
                Assert.AreSame(initialization, _service.InitializeAsync());
                Assert.AreEqual(1, controlled.InitializationCount);
                Assert.AreEqual(3L, await _database.ScalarAsync("PRAGMA user_version"));
                Assert.AreEqual(1, (await _service.GetWebsiteIconsAsync()).Count);
            }
            finally { controlled.ReleaseInitialization.TrySetResult(true); }
        }

        [TestMethod]
        public async Task FailedInitialization_Propagates_AndNextOperationRetries()
        {
            var controlled = new ControlledDatabase(_database) { FailNextInitialization = true };
            _service = new DatabaseService(controlled);
            await Assert.ThrowsExceptionAsync<IOException>(() => _service.GetWebsiteIconsAsync());
            Assert.IsFalse(File.Exists(Path.Combine(_testDir, "browser.db")));

            await _service.SaveWebsiteIconAsync(new WebsiteIconRecord("https://example.com/a", "https://example.com/icon", 10));
            Assert.AreEqual(2, controlled.InitializationCount);
            Assert.AreEqual("https://example.com/icon", (await _service.GetWebsiteIconsAsync()).Single().IconUrl);
        }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        public async Task ExistingOldDatabase_IsRejectedWithoutMigration(int version)
        {
            await _database.TransactionAsync(session =>
            {
                session.Execute("CREATE TABLE website_icons (page_url TEXT PRIMARY KEY, icon_url TEXT, updated_at INTEGER)");
                session.Execute("INSERT INTO website_icons VALUES ('page', 'icon', 20)");
                session.Execute("PRAGMA user_version = " + version);
            });

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => _service.InitializeAsync());
            Assert.AreEqual((long)version, await _database.ScalarAsync("PRAGMA user_version"));
            Assert.AreEqual("icon", await _database.ScalarAsync("SELECT icon_url FROM website_icons"));
            Assert.AreEqual(0L, await _database.ScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE name = 'monkeysharp_scripts'"));
        }

        [TestMethod]
        public async Task UnsupportedVersion_RejectsReadsAndWrites_WithoutChangingDatabase()
        {
            await _database.ExecuteAsync("PRAGMA user_version = 4");
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => _service.InitializeAsync());
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => _service.GetWebsiteIconsAsync());
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => _service.SaveWebsiteIconAsync(
                new WebsiteIconRecord("https://example.com/a", "https://example.com/icon", 10)));

            Assert.AreEqual(4L, await _database.ScalarAsync("PRAGMA user_version"));
            Assert.AreEqual(0L, await _database.ScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE name = 'website_icons'"));
        }

        [TestMethod]
        public async Task LatestSchema_HasAllScriptTables_AndReinitializesWithoutChanges()
        {
            await _service.InitializeAsync();

            var names = await _database.QueryAsync("SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name",
                row => row.GetString(0));

            CollectionAssert.AreEquivalent(new[] { "website_icons", "monkeysharp_scripts", "monkeysharp_script_assets", "monkeysharp_values",
                "monkeysharp_permission_decisions", "monkeysharp_tab_states" }, names);

            var columns = await _database.QueryAsync("PRAGMA table_info(website_icons)", row => row.GetString(1));

            CollectionAssert.AreEqual(new[] { "page_url", "icon_url", "updated_at" }, columns);

            await new DatabaseService(_database).InitializeAsync();

            Assert.AreEqual(3L, await _database.ScalarAsync("PRAGMA user_version"));
        }

        [TestMethod]
        public async Task Query_PreservesValuesAndSnapshotOrder_AfterPhysicalDeletion()
        {
            var first = new WebsiteIconRecord("https://example.com/a", "https://example.com/icon?v=1", 10);
            await _service.SaveWebsiteIconAsync(first);
            await _service.SaveWebsiteIconAsync(new WebsiteIconRecord("https://example.com/z", "https://example.com/z-icon", 10));
            await _service.SaveWebsiteIconAsync(new WebsiteIconRecord("https://example.com/b", "https://example.com/b-icon", 20));
            await _service.SaveWebsiteIconAsync(new WebsiteIconRecord("https://example.com/deleted", "https://example.com/deleted-icon", 30));
            await _database.ExecuteAsync("DELETE FROM website_icons WHERE page_url = 'https://example.com/deleted'");

            var records = await _service.GetWebsiteIconsAsync();
            CollectionAssert.AreEqual(new[] { "https://example.com/z", first.PageUrl, "https://example.com/b" },
                records.Select(icon => icon.PageUrl).ToArray());
            Assert.AreEqual(first.IconUrl, records[1].IconUrl);
            Assert.AreEqual(first.UpdatedAt, records[1].UpdatedAt);
        }

        [TestMethod]
        public async Task Save_PreservesUnchangedTimestamp_UpdatesAndReinsertsMappings()
        {
            const string page = "https://example.com/a";
            const string icon = "https://example.com/icon?q=';--";
            await _service.SaveWebsiteIconAsync(new WebsiteIconRecord(page, icon, 10));
            await _service.SaveWebsiteIconAsync(new WebsiteIconRecord(page, icon, 20));
            Assert.AreEqual(10L, await _database.ScalarAsync("SELECT updated_at FROM website_icons"));
            Assert.AreEqual(icon, (await _service.GetWebsiteIconsAsync()).Single().IconUrl);

            await _service.SaveWebsiteIconAsync(new WebsiteIconRecord(page, "https://example.com/new", 30));
            Assert.AreEqual(30L, await _database.ScalarAsync("SELECT updated_at FROM website_icons"));

            await _database.ExecuteAsync("DELETE FROM website_icons");

            Assert.AreEqual(0, (await _service.GetWebsiteIconsAsync()).Count);
            await _service.SaveWebsiteIconAsync(new WebsiteIconRecord(page, "https://example.com/new", 50));
            Assert.AreEqual(50L, await _database.ScalarAsync("SELECT updated_at FROM website_icons"));
            Assert.AreEqual("https://example.com/new", (await _service.GetWebsiteIconsAsync()).Single().IconUrl);
            Assert.AreEqual(1L, await _database.ScalarAsync("SELECT COUNT(*) FROM website_icons"));
        }

        [TestMethod]
        public async Task FailedWrite_Propagates_AndCanRetryWithoutReinitializing()
        {
            var controlled = new ControlledDatabase(_database);
            _service = new DatabaseService(controlled);
            await _service.InitializeAsync();
            controlled.FailNextWrite = true;
            var icon = new WebsiteIconRecord("https://example.com/a", "https://example.com/icon", 10);
            await Assert.ThrowsExceptionAsync<IOException>(() => _service.SaveWebsiteIconAsync(icon));
            Assert.AreEqual(0, (await _service.GetWebsiteIconsAsync()).Count);
            await _service.SaveWebsiteIconAsync(icon);
            Assert.AreEqual(icon.IconUrl, (await _service.GetWebsiteIconsAsync()).Single().IconUrl);
            Assert.AreEqual(1, controlled.InitializationCount);
        }

        [DataTestMethod]
        [DataRow("scripts")]
        [DataRow("values")]
        [DataRow("permissions")]
        [DataRow("tabs")]
        public async Task UserScriptReads_InitializeWithoutStorageQueue(string operation)
        {
            var key = ScriptKey.Parse(Guid.NewGuid().ToString());

            switch (operation)
            {
                case "scripts":
                    Assert.AreEqual(0, (await _service.GetUserScriptsAsync()).Count);
                    break;
                case "values":
                    Assert.AreEqual(0, (await _service.GetUserScriptValuesAsync(key.ToString(), CancellationToken.None)).Count);
                    break;
                case "permissions":
                    var scope = new UserScriptPermissionScope(key.ToString(), "GM.cookie", "https://example.com");
                    Assert.IsNull(await _service.GetUserScriptPermissionDecisionAsync(scope, CancellationToken.None));
                    break;
                case "tabs":
                    Assert.AreEqual(0, (await _service.GetUserScriptTabStatesAsync(key.ToString(), CancellationToken.None)).Count);
                    break;
                default:
                    Assert.Fail("Unknown userscript operation.");
                    break;
            }

            Assert.AreEqual(3L, await _database.ScalarAsync("PRAGMA user_version"));
        }

        [TestMethod]
        public async Task UserScriptSave_InitializesAndRollsBackWhenSourceCommitFails()
        {
            var key = ScriptKey.Parse(Guid.NewGuid().ToString());
            var now = DateTimeOffset.UtcNow;
            var record = new UserScriptDatabaseRecord(key, null, true, now, now, new string('a', 64), Guid.NewGuid().ToString("D"));

            await Assert.ThrowsExceptionAsync<IOException>(() => _service.SaveUserScriptAsync(record, Array.Empty<UserScriptAssetDatabaseRecord>(),
                () => throw new IOException("source switch failed"), CancellationToken.None));

            Assert.IsNull(await _service.GetUserScriptAsync(key));
            Assert.AreEqual(3L, await _database.ScalarAsync("PRAGMA user_version"));

            await _service.SaveUserScriptAsync(record, Array.Empty<UserScriptAssetDatabaseRecord>(), null, CancellationToken.None);

            var restored = await _service.GetUserScriptAsync(key);

            Assert.AreEqual(key, restored.ScriptKey);
            Assert.AreEqual(now, restored.InstalledAt);
            Assert.AreEqual(record.SourceSha256, restored.SourceSha256);
            Assert.AreEqual(record.CommitId, restored.CommitId);
        }

        [TestMethod]
        public async Task TabState_CloseRejectsWriteAlreadyWaitingForDatabase()
        {
            var controlled = new ControlledDatabase(_database);
            var database = new DatabaseService(controlled);
            var storage = new UserScriptStorage(database);

            await database.InitializeAsync();

            using (var entered = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            {
                var queued = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                controlled.TransactionQueued = () => queued.TrySetResult(true);

                var blocker = _database.TransactionAsync(session =>
                {
                    entered.Set();

                    if (!release.Wait(TimeSpan.FromSeconds(10)))
                        throw new TimeoutException();
                });

                try
                {
                    Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10)));

                    var state = new SqliteUserScriptTabStateService(storage, "closing-tab");
                    var pending = state.SaveAsync(ScriptKey.Parse(Guid.NewGuid().ToString()), null, "{}", CancellationToken.None);

                    Assert.AreSame(queued.Task, await Task.WhenAny(queued.Task, Task.Delay(TimeSpan.FromSeconds(10))));
                    Assert.IsFalse(pending.IsCompleted);

                    var close = state.CloseAsync();

                    release.Set();
                    await blocker;

                    try
                    {
                        await pending;
                        Assert.Fail("A closed tab must not save queued state.");
                    }
                    catch (OperationCanceledException) { }

                    await close;

                    Assert.AreEqual(0L, await _database.ScalarAsync("SELECT COUNT(*) FROM monkeysharp_tab_states"));
                }
                finally
                {
                    release.Set();
                    await blocker;
                    await storage.StopAsync();
                }
            }
        }

        private sealed class ControlledDatabase : IDatabase
        {
            private readonly IDatabase _inner;
            private int _initializationCount;
            public int InitializationCount => _initializationCount;
            public bool HoldInitialization { get; set; }
            public bool FailNextInitialization { get; set; }
            public bool FailNextWrite { get; set; }

            public Action TransactionQueued { get; set; }

            public TaskCompletionSource<bool> ReleaseInitialization { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public ControlledDatabase(IDatabase inner)
            {
                _inner = inner;
            }

            public async Task TransactionAsync(Action<IDatabaseSession> action)
            {
                Interlocked.Increment(ref _initializationCount);
                if (FailNextInitialization)
                {
                    FailNextInitialization = false;
                    throw new IOException("simulated initialization failure");
                }
                if (HoldInitialization)
                    await ReleaseInitialization.Task.ConfigureAwait(false);

                var transaction = _inner.TransactionAsync(action);

                TransactionQueued?.Invoke();
                await transaction.ConfigureAwait(false);
            }

            public Task<int> ExecuteAsync(string sql, IDictionary<string, object> parameters = null)
            {
                if (FailNextWrite)
                {
                    FailNextWrite = false;
                    return Task.FromException<int>(new IOException("simulated write failure"));
                }
                return _inner.ExecuteAsync(sql, parameters);
            }

            public Task<object> ScalarAsync(string sql, IDictionary<string, object> parameters = null) => _inner.ScalarAsync(sql, parameters);
            public Task<List<T>> QueryAsync<T>(string sql, Func<IDataRecord, T> map, IDictionary<string, object> parameters = null) => _inner.QueryAsync(sql, map, parameters);
            public Task StopAsync() => _inner.StopAsync();
            public void Dispose() => _inner.Dispose();
        }
    }
}
