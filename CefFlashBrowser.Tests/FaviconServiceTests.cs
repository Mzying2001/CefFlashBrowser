using CefFlashBrowser.Database;
using CefFlashBrowser.Services;
using CefFlashBrowser.Views.Converters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace CefFlashBrowser.Tests
{
    [TestClass]
    public class FaviconServiceTests
    {
        private string _testDir;
        private SynchronizationContext _previousContext;
        private TestContextQueue _context;
        private SqliteDatabase _database;
        private FaviconService _service;

        [TestInitialize]
        public void Setup()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "CefFlashBrowserFaviconTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
            _previousContext = SynchronizationContext.Current;
            _context = new TestContextQueue();
            SynchronizationContext.SetSynchronizationContext(_context);
            _database = new SqliteDatabase(Path.Combine(_testDir, "browser.db"));
            _service = new FaviconService(_database);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _service.StopAsync().GetAwaiter().GetResult();
            _database.Dispose();
            _context.Drain();
            SynchronizationContext.SetSynchronizationContext(_previousContext);
            Directory.Delete(_testDir, true);
        }

        private void Initialize()
        {
            _context.Complete(_service.InitializeAsync());
        }

        private void Record(string pageUrl, string iconUrl)
        {
            _service.RecordAsync(pageUrl, iconUrl).GetAwaiter().GetResult();
        }

        [TestMethod]
        public void ConstructionAndLookup_DoNotOpenDatabase()
        {
            Assert.IsNull(_service.GetIconUrl("https://example.com/a"));
            Assert.IsFalse(File.Exists(Path.Combine(_testDir, "browser.db")));
            _service.StopAsync().GetAwaiter().GetResult();
            Assert.IsFalse(File.Exists(Path.Combine(_testDir, "browser.db")));
        }

        [TestMethod]
        public void Urls_NormalizeFragmentsAndDefaultPorts_PreservePathsAndQueries()
        {
            Initialize();
            Record("HTTPS://EXAMPLE.COM:443/a#top", "../icons/logo?v=1");
            Assert.AreEqual("https://example.com/icons/logo?v=1", _service.GetIconUrl("https://example.com/a#bottom"));
            foreach (string page in new[] { "https://example.com/A", "https://example.com/a/", "https://example.com/b", "https://example.com/a?id=1", "http://example.com/a" })
                Assert.IsNull(_service.GetIconUrl(page), page);

            Record("https://example.com/a?id=1", "/one");
            Record("https://example.com/a?id=2", "//cdn.example.com/two?q=2");
            Assert.AreEqual("https://example.com/one", _service.GetIconUrl("https://example.com/a?id=1"));
            Assert.AreEqual("https://cdn.example.com/two?q=2", _service.GetIconUrl("https://example.com/a?id=2"));
        }

        [DataTestMethod]
        [DataRow("file:///C:/game.swf", "https://example.com/icon")]
        [DataRow("about:blank", "/icon")]
        [DataRow("C:\\game.swf", "/icon")]
        [DataRow("invalid", "/icon")]
        [DataRow(null, "/icon")]
        [DataRow("https://example.com/a", "data:image/png;base64,abc")]
        [DataRow("https://example.com/a", "blob:https://example.com/id")]
        [DataRow("https://example.com/a", "file:///C:/icon.png")]
        [DataRow("https://example.com/a", "")]
        [DataRow("https://example.com/a", null)]
        public void InvalidDiscovery_DoesNotCreateMapping(string pageUrl, string iconUrl)
        {
            Initialize();
            int revision = _service.Revision;
            Record(pageUrl, iconUrl);
            Assert.AreEqual(revision, _service.Revision);
            Assert.AreEqual(0L, _database.ScalarAsync("SELECT COUNT(*) FROM website_icons").GetAwaiter().GetResult());
        }

        [TestMethod]
        public void DuplicateAndEmptyDiscovery_PreserveTimestamps_ChangedIconAndRestorePreserveCreation()
        {
            Initialize();
            int threadId = Thread.CurrentThread.ManagedThreadId;
            int notifications = 0;
            _service.PropertyChanged += (sender, e) =>
            {
                Assert.AreEqual(threadId, Thread.CurrentThread.ManagedThreadId);
                Assert.AreEqual(nameof(IFaviconService.Revision), e.PropertyName);
                notifications++;
            };
            Record("https://example.com/a", "/icon");
            _database.ExecuteAsync("UPDATE website_icons SET created_at = 10, updated_at = 20").GetAwaiter().GetResult();
            Record("https://example.com/a#fragment", "/icon");
            Record("https://example.com/a", null);
            Assert.AreEqual(1, notifications);
            Assert.AreEqual(20L, _database.ScalarAsync("SELECT updated_at FROM website_icons").GetAwaiter().GetResult());

            Record("https://example.com/a", "/new");
            Assert.AreEqual(2, notifications);
            Assert.AreEqual(10L, _database.ScalarAsync("SELECT created_at FROM website_icons").GetAwaiter().GetResult());
            _database.ExecuteAsync("UPDATE website_icons SET deleted_at = 30").GetAwaiter().GetResult();
            Record("https://example.com/a", "/new");
            Assert.AreEqual(DBNull.Value, _database.ScalarAsync("SELECT deleted_at FROM website_icons").GetAwaiter().GetResult());
            Assert.AreEqual(1L, _database.ScalarAsync("SELECT COUNT(*) FROM website_icons").GetAwaiter().GetResult());

            _service.StopAsync().GetAwaiter().GetResult();
            _service = new FaviconService(_database);
            Initialize();
            Assert.AreEqual("https://example.com/new", _service.GetIconUrl("https://example.com/a"));
        }

        [TestMethod]
        public void Initialization_ReplaysDiscoveriesAfterSnapshot_AndPreservesWriteOrder()
        {
            Initialize();
            Record("https://example.com/a", "/old");
            _service.StopAsync().GetAwaiter().GetResult();
            var controlled = new ControlledDatabase(_database) { HoldSnapshot = true };
            _service = new FaviconService(controlled);
            var initialization = _service.InitializeAsync();
            Assert.AreSame(initialization, _service.InitializeAsync());
            Assert.IsTrue(controlled.SnapshotCaptured.Task.Wait(TimeSpan.FromSeconds(10)));
            _service.RecordAsync("https://example.com/a", "/first");
            var lastWrite = _service.RecordAsync("https://example.com/a", "/last");
            Assert.IsNull(_service.GetIconUrl("https://example.com/a"));
            controlled.ReleaseSnapshot.SetResult(true);
            _context.Complete(initialization);
            lastWrite.GetAwaiter().GetResult();
            Assert.AreEqual("https://example.com/last", _service.GetIconUrl("https://example.com/a"));
            Assert.AreEqual("https://example.com/last", _database.ScalarAsync("SELECT icon_url FROM website_icons").GetAwaiter().GetResult());
        }

        [TestMethod]
        public void ReentrantDiscovery_PersistsInReceivedOrder()
        {
            Initialize();
            bool reported = false;
            _service.PropertyChanged += (sender, e) =>
            {
                if (!reported)
                {
                    reported = true;
                    _service.RecordAsync("https://example.com/a", "/last");
                }
            };

            Record("https://example.com/a", "/first");
            _service.StopAsync().GetAwaiter().GetResult();
            Assert.AreEqual("https://example.com/last", _service.GetIconUrl("https://example.com/a"));
            Assert.AreEqual("https://example.com/last", _database.ScalarAsync("SELECT icon_url FROM website_icons").GetAwaiter().GetResult());
        }

        [TestMethod]
        public void FailedWrite_KeepsMemory_AndSameDiscoveryRetries()
        {
            var controlled = new ControlledDatabase(_database);
            _service = new FaviconService(controlled);
            Initialize();
            controlled.FailNextWrite = true;
            Record("https://example.com/a", "/icon");
            Assert.AreEqual("https://example.com/icon", _service.GetIconUrl("https://example.com/a"));
            Assert.AreEqual(0L, _database.ScalarAsync("SELECT COUNT(*) FROM website_icons").GetAwaiter().GetResult());
            int revision = _service.Revision;
            Record("https://example.com/a", "/icon");
            Assert.AreEqual(revision, _service.Revision);
            Assert.AreEqual(1L, _database.ScalarAsync("SELECT COUNT(*) FROM website_icons").GetAwaiter().GetResult());
        }

        [TestMethod]
        public void Stop_DrainsPendingWritesWithoutUiPublication_AndLeavesSharedDatabaseUsable()
        {
            var controlled = new ControlledDatabase(_database) { HoldSnapshot = true };
            _service = new FaviconService(controlled);
            _service.InitializeAsync();
            Assert.IsTrue(controlled.SnapshotCaptured.Task.Wait(TimeSpan.FromSeconds(10)));
            _service.RecordAsync("https://example.com/a", "/icon");
            var stop = _service.StopAsync();
            controlled.ReleaseSnapshot.SetResult(true);
            Assert.IsTrue(stop.Wait(TimeSpan.FromSeconds(10)));
            Record("https://example.com/a", "/ignored");
            Assert.AreEqual("https://example.com/icon", _database.ScalarAsync("SELECT icon_url FROM website_icons").GetAwaiter().GetResult());
            Assert.AreEqual(0, _service.Revision);
        }

        [TestMethod]
        public void UnsupportedVersion_LeavesDatabaseUntouched_AndAcceptsMemoryMappings()
        {
            _database.ExecuteAsync("PRAGMA user_version = 2").GetAwaiter().GetResult();
            Initialize();
            Record("https://example.com/a", "/icon");
            Assert.AreEqual("https://example.com/icon", _service.GetIconUrl("https://example.com/a"));
            Assert.AreEqual(2L, _database.ScalarAsync("PRAGMA user_version").GetAwaiter().GetResult());
            Assert.AreEqual(0L, _database.ScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE name = 'website_icons'").GetAwaiter().GetResult());
        }

        [TestMethod]
        public void CorruptDatabase_IsNotRecreated_AndMemoryMappingsRemainAvailable()
        {
            string path = Path.Combine(_testDir, "browser.db");
            File.WriteAllText(path, "existing invalid database");
            byte[] original = File.ReadAllBytes(path);
            Initialize();
            Record("https://example.com/a", "/icon");
            Assert.AreEqual("https://example.com/icon", _service.GetIconUrl("https://example.com/a"));
            _service.StopAsync().GetAwaiter().GetResult();
            _database.Dispose();
            CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
        }

        [TestMethod]
        public void Converter_UsesItemUrl_AndIgnoresIncompleteInputs()
        {
            Initialize();
            Record("https://example.com/a", "/a-icon");
            Record("https://example.com/b", "/b-icon");
            var converter = new WebsiteIconUrlConverter();
            Assert.AreEqual("https://example.com/b-icon", converter.Convert(
                new object[] { "https://example.com/b", _service, _service.Revision }, typeof(string), null, null));
            Assert.IsNull(converter.Convert(new object[] { "https://example.com/a", _service, DependencyProperty.UnsetValue }, typeof(string), null, null));
            Assert.IsNull(converter.Convert(null, typeof(string), null, null));
        }

        private sealed class TestContextQueue : SynchronizationContext
        {
            private readonly BlockingCollection<Action> _callbacks = new BlockingCollection<Action>();

            public override void Post(SendOrPostCallback callback, object state)
            {
                _callbacks.Add(() => callback(state));
            }

            public void Complete(Task task)
            {
                var deadline = DateTime.UtcNow.AddSeconds(10);
                while (!task.IsCompleted && DateTime.UtcNow < deadline)
                {
                    if (_callbacks.TryTake(out var action, 10))
                        action();
                }
                Assert.IsTrue(task.IsCompleted, "The service did not finish within ten seconds.");
                task.GetAwaiter().GetResult();
            }

            public void Drain()
            {
                while (_callbacks.TryTake(out var action))
                    action();
            }
        }

        private sealed class ControlledDatabase : IDatabase
        {
            private readonly IDatabase _inner;
            public bool HoldSnapshot { get; set; }
            public bool FailNextWrite { get; set; }
            public TaskCompletionSource<bool> SnapshotCaptured { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource<bool> ReleaseSnapshot { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public ControlledDatabase(IDatabase inner)
            {
                _inner = inner;
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

            public Task<object> ScalarAsync(string sql, IDictionary<string, object> parameters = null)
            {
                return _inner.ScalarAsync(sql, parameters);
            }

            public async Task<List<T>> QueryAsync<T>(string sql, Func<IDataRecord, T> map, IDictionary<string, object> parameters = null)
            {
                var snapshot = await _inner.QueryAsync(sql, map, parameters).ConfigureAwait(false);
                SnapshotCaptured.TrySetResult(true);
                if (HoldSnapshot)
                    await ReleaseSnapshot.Task.ConfigureAwait(false);
                return snapshot;
            }

            public Task TransactionAsync(Action<IDatabaseSession> action) => _inner.TransactionAsync(action);
            public Task StopAsync() => _inner.StopAsync();
            public void Dispose() => _inner.Dispose();
        }
    }
}
