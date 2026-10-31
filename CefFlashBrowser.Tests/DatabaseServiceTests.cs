using CefFlashBrowser.Database;
using CefFlashBrowser.Models;
using CefFlashBrowser.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
                Assert.AreEqual(1L, await _database.ScalarAsync("PRAGMA user_version"));
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
        public async Task ExistingDatabase_PreservesSchemaAndRecords(int version)
        {
            await _database.TransactionAsync(session =>
            {
                session.Execute(@"CREATE TABLE website_icons (
                    page_url TEXT PRIMARY KEY NOT NULL,
                    icon_url TEXT NOT NULL,
                    created_at INTEGER NOT NULL,
                    updated_at INTEGER NOT NULL,
                    deleted_at INTEGER NULL)");
                session.Execute("INSERT INTO website_icons VALUES ('https://example.com/a', 'https://example.com/icon', 10, 20, NULL)");
                session.Execute("PRAGMA user_version = " + version);
            });
            string schema = (string)await _database.ScalarAsync("SELECT sql FROM sqlite_master WHERE name = 'website_icons'");

            var icon = (await _service.GetWebsiteIconsAsync()).Single();
            Assert.AreEqual("https://example.com/a", icon.PageUrl);
            Assert.AreEqual("https://example.com/icon", icon.IconUrl);
            Assert.AreEqual(20L, icon.UpdatedAt);
            Assert.AreEqual(10L, await _database.ScalarAsync("SELECT created_at FROM website_icons"));
            Assert.AreEqual(schema, await _database.ScalarAsync("SELECT sql FROM sqlite_master WHERE name = 'website_icons'"));
            Assert.AreEqual(1L, await _database.ScalarAsync("PRAGMA user_version"));
        }

        [TestMethod]
        public async Task UnsupportedVersion_RejectsReadsAndWrites_WithoutChangingDatabase()
        {
            await _database.ExecuteAsync("PRAGMA user_version = 2");
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => _service.InitializeAsync());
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => _service.GetWebsiteIconsAsync());
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => _service.SaveWebsiteIconAsync(
                new WebsiteIconRecord("https://example.com/a", "https://example.com/icon", 10)));
            Assert.AreEqual(2L, await _database.ScalarAsync("PRAGMA user_version"));
            Assert.AreEqual(0L, await _database.ScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE name = 'website_icons'"));
        }

        [TestMethod]
        public async Task Query_FiltersDeletedRows_PreservesValuesAndSnapshotOrder()
        {
            var first = new WebsiteIconRecord("https://example.com/a", "https://example.com/icon?v=1", 10);
            await _service.SaveWebsiteIconAsync(first);
            await _service.SaveWebsiteIconAsync(new WebsiteIconRecord("https://example.com/z", "https://example.com/z-icon", 10));
            await _service.SaveWebsiteIconAsync(new WebsiteIconRecord("https://example.com/b", "https://example.com/b-icon", 20));
            await _service.SaveWebsiteIconAsync(new WebsiteIconRecord("https://example.com/deleted", "https://example.com/deleted-icon", 30));
            await _database.ExecuteAsync("UPDATE website_icons SET deleted_at = 40 WHERE page_url = 'https://example.com/deleted'");

            var records = await _service.GetWebsiteIconsAsync();
            CollectionAssert.AreEqual(new[] { "https://example.com/z", first.PageUrl, "https://example.com/b" },
                records.Select(icon => icon.PageUrl).ToArray());
            Assert.AreEqual(first.IconUrl, records[1].IconUrl);
            Assert.AreEqual(first.UpdatedAt, records[1].UpdatedAt);
        }

        [TestMethod]
        public async Task Save_PreservesCreationAndUnchangedTimestamps_UpdatesAndRestoresMappings()
        {
            const string page = "https://example.com/a";
            const string icon = "https://example.com/icon?q=';--";
            await _service.SaveWebsiteIconAsync(new WebsiteIconRecord(page, icon, 10));
            await _service.SaveWebsiteIconAsync(new WebsiteIconRecord(page, icon, 20));
            Assert.AreEqual(10L, await _database.ScalarAsync("SELECT created_at FROM website_icons"));
            Assert.AreEqual(10L, await _database.ScalarAsync("SELECT updated_at FROM website_icons"));
            Assert.AreEqual(icon, (await _service.GetWebsiteIconsAsync()).Single().IconUrl);

            await _service.SaveWebsiteIconAsync(new WebsiteIconRecord(page, "https://example.com/new", 30));
            Assert.AreEqual(30L, await _database.ScalarAsync("SELECT updated_at FROM website_icons"));
            await _database.ExecuteAsync("UPDATE website_icons SET deleted_at = 40");
            Assert.AreEqual(0, (await _service.GetWebsiteIconsAsync()).Count);
            await _service.SaveWebsiteIconAsync(new WebsiteIconRecord(page, "https://example.com/new", 50));
            Assert.AreEqual(10L, await _database.ScalarAsync("SELECT created_at FROM website_icons"));
            Assert.AreEqual(50L, await _database.ScalarAsync("SELECT updated_at FROM website_icons"));
            Assert.AreEqual(DBNull.Value, await _database.ScalarAsync("SELECT deleted_at FROM website_icons"));
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

        private sealed class ControlledDatabase : IDatabase
        {
            private readonly IDatabase _inner;
            private int _initializationCount;
            public int InitializationCount => _initializationCount;
            public bool HoldInitialization { get; set; }
            public bool FailNextInitialization { get; set; }
            public bool FailNextWrite { get; set; }
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
                await _inner.TransactionAsync(action).ConfigureAwait(false);
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
