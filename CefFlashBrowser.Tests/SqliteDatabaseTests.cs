using CefFlashBrowser.Database;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Tests
{
    [TestClass]
    public class SqliteDatabaseTests
    {
        private string _testDir;

        [TestInitialize]
        public void Setup()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "CefFlashBrowserDatabaseTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Directory.Delete(_testDir, true);
        }

        [TestMethod]
        public void Dispose_UnusedDatabase_DoesNotCreateFile()
        {
            string path = Path.Combine(_testDir, "unused.db");
            var database = new SqliteDatabase(path);
            database.Dispose();
            database.Dispose();
            Assert.IsFalse(File.Exists(path));
            Assert.ThrowsException<ObjectDisposedException>(() => database.ExecuteAsync("SELECT 1"));
        }

        [TestMethod]
        public async Task ParametersAndQueries_PreserveValuesAndReleaseReaders()
        {
            string path = Path.Combine(_testDir, "values.db");
            using (var database = new SqliteDatabase(path))
            {
                await database.ExecuteAsync("CREATE TABLE entries (name TEXT, value TEXT)");
                const string text = "中文 '); DROP TABLE entries; --";
                Assert.AreEqual(1, await database.ExecuteAsync("INSERT INTO entries VALUES (@name, @value)",
                    new Dictionary<string, object> { ["@name"] = text, ["@value"] = null }));

                var rows = await database.QueryAsync("SELECT name, value FROM entries", row =>
                {
                    Assert.IsTrue(row.IsDBNull(1));
                    return row.GetString(0);
                });
                CollectionAssert.AreEqual(new[] { text }, rows);
                await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => database.QueryAsync<int>(
                    "SELECT name FROM entries", row => throw new InvalidOperationException("mapper failure")));
                Assert.AreEqual(1L, await database.ScalarAsync("SELECT COUNT(*) FROM entries"));
            }
            // No reader, connection or pool retains the database file after disposal.
            using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        }

        [TestMethod]
        public async Task Transaction_RollsBackOnFailure_AndSessionCannotEscape()
        {
            using (var database = new SqliteDatabase(Path.Combine(_testDir, "transaction.db")))
            {
                await database.ExecuteAsync("CREATE TABLE entries (value INTEGER)");
                IDatabaseSession escaped = null;
                await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => database.TransactionAsync(session =>
                {
                    escaped = session;
                    session.Execute("INSERT INTO entries VALUES (1)");
                    throw new InvalidOperationException("rollback");
                }));
                Assert.AreEqual(0L, await database.ScalarAsync("SELECT COUNT(*) FROM entries"));
                Assert.ThrowsException<ObjectDisposedException>(() => escaped.Scalar("SELECT 1"));

                await database.TransactionAsync(session =>
                {
                    session.Execute("INSERT INTO entries VALUES (2)");
                    session.Execute("INSERT INTO entries VALUES (3)");
                });
                Assert.AreEqual(5L, await database.ScalarAsync("SELECT SUM(value) FROM entries"));
            }
        }

        [TestMethod]
        public async Task ConcurrentCallers_AndStop_DoNotInterruptTransaction()
        {
            using (var database = new SqliteDatabase(Path.Combine(_testDir, "concurrent.db")))
            using (var entered = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            {
                await database.ExecuteAsync("CREATE TABLE entries (value INTEGER)");
                var transaction = database.TransactionAsync(session =>
                {
                    session.Execute("INSERT INTO entries VALUES (1)");
                    entered.Set();
                    if (!release.Wait(TimeSpan.FromSeconds(10)))
                        throw new TimeoutException();
                    session.Execute("INSERT INTO entries VALUES (2)");
                });
                Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10)));

                var writes = Enumerable.Range(3, 20).Select(value => Task.Run(() => database.ExecuteAsync(
                    "INSERT INTO entries VALUES (@value)", new Dictionary<string, object> { ["@value"] = value }))).ToArray();
                try
                {
                    // Every outside reader must see the complete transaction.
                    var count = database.ScalarAsync("SELECT COUNT(*) FROM entries WHERE value IN (1, 2)");
                    Assert.IsFalse(count.IsCompleted);
                    release.Set();
                    await Task.WhenAll(writes);
                    Assert.AreEqual(2L, await count);
                    var finalCount = database.ScalarAsync("SELECT COUNT(*) FROM entries");
                    var stopped = database.StopAsync();
                    Assert.ThrowsException<ObjectDisposedException>(() => database.ScalarAsync("SELECT 1"));
                    await stopped;
                    await transaction;
                    Assert.AreEqual(22L, await finalCount);
                }
                finally { release.Set(); }
            }
        }
    }
}
