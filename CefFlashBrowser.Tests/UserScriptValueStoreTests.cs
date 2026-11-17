using CefFlashBrowser.Database;
using CefFlashBrowser.Services;
using CefFlashBrowser.Services.UserScripts.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mzying2001.MonkeySharp.Core.Repository;
using Mzying2001.MonkeySharp.Core.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Tests
{
    [TestClass]
    public class UserScriptValueStoreTests
    {
        private string _root, _id;
        private SqliteDatabase _database;
        private UserScriptStorage _storage;
        private SqliteUserScriptValueStore _store;
        private UserScriptRepository _repository;

        [TestInitialize]
        public async Task Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "CefFlashBrowserValues_" + Guid.NewGuid().ToString("N"));
            _database = new SqliteDatabase(Path.Combine(_root, "browser.db"));
            _storage = new UserScriptStorage(new DatabaseService(_database));
            _store = new SqliteUserScriptValueStore(_storage, 4);
            _repository = new UserScriptRepository(new SqliteUserScriptRepositoryPersistence(
                _storage, Path.Combine(_root, "MonkeySharp"), Path.Combine(_root, "Cache", "UserScripts")));
            await _repository.InitializeAsync(CancellationToken.None);
            _id = (await _repository.InstallAsync("// ==UserScript==\n// @name Storage\n// @match https://example.com/*\n// @grant none\n// ==/UserScript==", null, true, CancellationToken.None)).ScriptKey.ToString();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await _storage.StopAsync();
            _database.Dispose();
            Directory.Delete(_root, true);
        }

        [DataTestMethod]
        [DataRow("u")]
        [DataRow("onull")]
        [DataRow("nNaN")]
        [DataRow("nInfinity")]
        [DataRow("n-Infinity")]
        [DataRow("bfalse")]
        [DataRow("s")]
        [DataRow("o[1,null,true]")]
        public async Task TypedValues_PersistAcrossStoreInstances_AndMissingRemainsDistinct(string encoded)
        {
            var value = UserScriptValue.FromEncoded(encoded);

            await _store.SetAsync(_id, "", value, CancellationToken.None);

            var restored = new SqliteUserScriptValueStore(_storage);
            var stored = await restored.GetAsync(_id, "", CancellationToken.None);

            Assert.IsTrue(stored.Exists);
            Assert.AreEqual(value.EncodedValue, stored.Value.EncodedValue);
            Assert.IsFalse((await restored.GetAsync(_id, "missing", CancellationToken.None)).Exists);
            Assert.AreEqual(value.EncodedValue, await _database.ScalarAsync("SELECT encoded_value FROM monkeysharp_values"));
        }

        [TestMethod]
        public async Task KeyOrder_IsCaseSensitive_AndOverwriteDoesNotMoveKeys()
        {
            foreach (var key in new[] { "a", "A", "", "last" })
                await _store.SetAsync(_id, key, UserScriptValue.Undefined, CancellationToken.None);

            await _store.SetAsync(_id, "a", UserScriptValue.FromJson("1"), CancellationToken.None);

            CollectionAssert.AreEqual(new[] { "a", "A", "", "last" }, (await _store.ListKeysAsync(_id, CancellationToken.None)).ToArray());

            await _store.DeleteAsync(_id, "A", CancellationToken.None);
            await _store.SetAsync(_id, "A", UserScriptValue.Undefined, CancellationToken.None);

            CollectionAssert.AreEqual(new[] { "a", "", "last", "A" }, (await _store.ListKeysAsync(_id, CancellationToken.None)).ToArray());
        }

        [TestMethod]
        public async Task Changes_HavePreciseWatermarksAndOrigin_AndNoOpsDoNotNotify()
        {
            var events = new List<UserScriptValueChangedEventArgs>();

            _store.ValueChanged += (sender, change) => events.Add(change);

            var origin = new UserScriptValueMutationOrigin("mutation", "execution", "GM.setValues");
            var commit = await _store.ApplyAsync(_id, new[] { new UserScriptValueMutation("", UserScriptValue.Undefined),
                new UserScriptValueMutation("other", UserScriptValue.FromJson("null")) }, origin, CancellationToken.None);

            Assert.AreEqual(2L, commit.Sequence);
            Assert.AreEqual(2, events.Count);
            Assert.IsFalse(events[0].OldValue.Exists);
            Assert.IsTrue(events[0].NewValue.Exists);
            Assert.AreEqual("mutation", events[0].MutationId);
            Assert.AreEqual("execution", events[0].OriginExecutionId);
            Assert.AreEqual("GM.setValues", events[0].MutationMethod);

            var empty = await _store.ApplyAsync(_id, Array.Empty<UserScriptValueMutation>(), null, CancellationToken.None);

            Assert.AreEqual(2L, empty.Sequence);

            await _store.SetAsync(_id, "", UserScriptValue.Undefined, CancellationToken.None);

            Assert.IsFalse(await _store.DeleteAsync(_id, "absent", CancellationToken.None));
            Assert.AreEqual(2, events.Count);
            Assert.IsTrue(await _store.DeleteAsync(_id, "", CancellationToken.None));
            Assert.IsTrue(events[2].OldValue.Exists);
            Assert.IsFalse(events[2].NewValue.Exists);
        }

        [TestMethod]
        public async Task BatchFailure_RollsBackEveryValueAndJournal()
        {
            await _database.ExecuteAsync("CREATE TRIGGER fail_value BEFORE INSERT ON monkeysharp_values WHEN NEW.value_key = 'bad' BEGIN SELECT RAISE(ABORT, 'injected'); END");
            await Assert.ThrowsExceptionAsync<System.Data.SQLite.SQLiteException>(() => _store.ApplyAsync(_id,
                new[] { new UserScriptValueMutation("first", UserScriptValue.Undefined), new UserScriptValueMutation("bad", UserScriptValue.Undefined) }, null, CancellationToken.None));

            var snapshot = await _store.GetSnapshotAsync(_id, CancellationToken.None);

            Assert.AreEqual(0, snapshot.Count);
            Assert.AreEqual(0L, snapshot.Sequence);

            await Assert.ThrowsExceptionAsync<ArgumentException>(() => _store.ApplyAsync(_id,
                new[] { new UserScriptValueMutation("same", UserScriptValue.Undefined), new UserScriptValueMutation("same", null) }, null, CancellationToken.None));

            Assert.AreEqual(0L, (await _store.GetSnapshotAsync(_id, CancellationToken.None)).Sequence);
        }

        [TestMethod]
        public async Task SnapshotAndReplay_ShareCommitBoundary_AndExpiredHistoryRequestsReset()
        {
            var writes = Enumerable.Range(0, 3).Select(i => _store.SetAsync(_id, "value", UserScriptValue.FromJson(i.ToString()), CancellationToken.None)).ToList();
            var capture = _store.GetSnapshotAsync(_id, CancellationToken.None);

            writes.AddRange(Enumerable.Range(3, 3).Select(i => _store.SetAsync(_id, "value", UserScriptValue.FromJson(i.ToString()), CancellationToken.None)));
            await Task.WhenAll(writes);

            var snapshot = await capture;

            Assert.AreEqual(3L, snapshot.Sequence);
            Assert.AreEqual("n2", snapshot.Single().Value.EncodedValue);

            var replay = await _store.GetChangesAsync(_id, snapshot.Sequence, CancellationToken.None);

            Assert.IsTrue(replay.Complete);
            CollectionAssert.AreEqual(new long[] { 4, 5, 6 }, replay.Changes.Select(change => change.Sequence).ToArray());
            Assert.IsFalse((await _store.GetChangesAsync(_id, 0, CancellationToken.None)).Complete);
            Assert.IsFalse((await _store.GetChangesAsync(_id, 7, CancellationToken.None)).Complete);
            Assert.AreEqual(6L, (await _store.ApplyAsync(_id, Array.Empty<UserScriptValueMutation>(), null, CancellationToken.None)).Sequence);
        }

        [TestMethod]
        public async Task SubscriberFailureAndCancellationAfterCommit_DoNotChangeSuccess()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                var delivered = 0;

                _store.ValueChanged += (sender, change) =>
                {
                    cancellation.Cancel();
                    throw new InvalidOperationException();
                };
                _store.ValueChanged += (sender, change) => delivered++;

                var result = await _store.ApplyAsync(_id, new[] { new UserScriptValueMutation("value", UserScriptValue.Undefined) }, null, cancellation.Token);

                Assert.AreEqual(1, delivered);
                Assert.AreEqual(1L, result.Sequence);
                Assert.IsTrue((await _store.GetAsync(_id, "value", CancellationToken.None)).Exists);
            }
        }

        [TestMethod]
        public async Task UninstallAndLateWrite_CannotRecreatePartition()
        {
            var removal = _repository.RemoveAsync(Mzying2001.MonkeySharp.Core.Domain.ScriptKey.Parse(_id), CancellationToken.None);
            var lateWrite = _store.SetAsync(_id, "late", UserScriptValue.Undefined, CancellationToken.None);

            await removal;
            await Assert.ThrowsExceptionAsync<KeyNotFoundException>(() => lateWrite);

            Assert.AreEqual(0L, await _database.ScalarAsync("SELECT COUNT(*) FROM monkeysharp_values"));
        }

        [TestMethod]
        public async Task CancellationWhileWaitingForDatabase_RollsBackBeforeAnyEffects()
        {
            using (var entered = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            using (var cancellation = new CancellationTokenSource())
            {
                var blocker = _database.TransactionAsync(session =>
                {
                    entered.Set();

                    if (!release.Wait(TimeSpan.FromSeconds(10)))
                        throw new TimeoutException();
                });

                Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10)));

                var pending = _store.SetAsync(_id, "cancelled", UserScriptValue.Undefined, cancellation.Token);

                cancellation.Cancel();
                release.Set();
                await blocker;

                try
                {
                    await pending;
                    Assert.Fail("A queued cancelled mutation must not commit.");
                }
                catch (OperationCanceledException) { }

                Assert.AreEqual(0L, (await _store.GetSnapshotAsync(_id, CancellationToken.None)).Sequence);
                Assert.AreEqual(0L, await _database.ScalarAsync("SELECT COUNT(*) FROM monkeysharp_values"));
            }
        }

        [TestMethod]
        public async Task ValuesAndWatermarks_AreIsolatedByInstallation()
        {
            var second = await _repository.InstallAsync("// ==UserScript==\n// @name Other\n// @match https://example.com/*\n// @grant none\n// ==/UserScript==", null, true, CancellationToken.None);

            await _store.SetAsync(_id, "same", UserScriptValue.FromJson("1"), CancellationToken.None);
            await _store.SetAsync(second.ScriptKey.ToString(), "same", UserScriptValue.FromJson("2"), CancellationToken.None);

            Assert.AreEqual("n1", (await _store.GetAsync(_id, "same", CancellationToken.None)).Value.EncodedValue);
            Assert.AreEqual("n2", (await _store.GetAsync(second.ScriptKey.ToString(), "same", CancellationToken.None)).Value.EncodedValue);
            Assert.AreEqual(1L, (await _store.GetSnapshotAsync(_id, CancellationToken.None)).Sequence);
        }

        [TestMethod]
        public async Task TabState_IsStableAcrossFrames_AndCloseRejectsQueuedWrites()
        {
            var key = Mzying2001.MonkeySharp.Core.Domain.ScriptKey.Parse(_id);
            var first = new SqliteUserScriptTabStateService(_storage, "first-tab");
            var second = new SqliteUserScriptTabStateService(_storage, "second-tab");

            await first.SaveAsync(key, null, "{\"count\":1}", CancellationToken.None);
            await second.SaveAsync(key, null, "{\"count\":2}", CancellationToken.None);

            Assert.AreEqual("{\"count\":1}", await first.GetAsync(key, null, CancellationToken.None));
            Assert.AreEqual(2, (await second.GetAllAsync(key, CancellationToken.None)).Count);

            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var held = _storage.RunAsync(() => release.Task, CancellationToken.None);
            var late = first.SaveAsync(key, null, "{\"late\":true}", CancellationToken.None);
            var close = first.CloseAsync();

            release.TrySetResult(true);
            await held;

            try
            {
                await late;
                Assert.Fail("A closed tab must not save queued state.");
            }
            catch (OperationCanceledException) { }

            await close;

            Assert.AreEqual("{}", await first.GetAsync(key, null, CancellationToken.None));
            Assert.AreEqual(1, (await second.GetAllAsync(key, CancellationToken.None)).Count);
            Assert.AreSame(close, first.CloseAsync());
        }

        [TestMethod]
        public async Task TabState_RejectsNonObjectsAndWritesAfterUninstall()
        {
            var key = Mzying2001.MonkeySharp.Core.Domain.ScriptKey.Parse(_id);
            var state = new SqliteUserScriptTabStateService(_storage, "tab");

            await Assert.ThrowsExceptionAsync<ArgumentException>(() => state.SaveAsync(key, null, "[]", CancellationToken.None));
            await _repository.RemoveAsync(key, CancellationToken.None);
            await Assert.ThrowsExceptionAsync<KeyNotFoundException>(() => state.SaveAsync(key, null, "{}", CancellationToken.None));

            Assert.AreEqual(0L, await _database.ScalarAsync("SELECT COUNT(*) FROM monkeysharp_tab_states"));
        }

        [TestMethod]
        public async Task Stop_DrainsAcceptedWrites_AndRejectsNewWork()
        {
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var held = _storage.RunAsync(() => release.Task, CancellationToken.None);
            var accepted = _store.SetAsync(_id, "accepted", UserScriptValue.Undefined, CancellationToken.None);
            var stop = _storage.StopAsync();

            Assert.IsFalse(stop.IsCompleted);

            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() => _store.SetAsync(_id, "late", UserScriptValue.Undefined, CancellationToken.None));
            release.TrySetResult(true);
            await Task.WhenAll(held, accepted, stop);

            Assert.AreEqual(1L, await _database.ScalarAsync("SELECT COUNT(*) FROM monkeysharp_values"));
        }
    }
}
