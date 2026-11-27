using CefFlashBrowser.Database;
using CefFlashBrowser.Models.Downloads;
using CefFlashBrowser.Services;
using CefFlashBrowser.Services.Downloads;
using CefFlashBrowser.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mzying2001.MonkeySharp.Core.Apis;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Tests
{
    [TestClass]
    public class DownloadTransferTests
    {
        private string _directory;

        [TestInitialize]
        public void Initialize()
        {
            _directory = Path.Combine(Path.GetTempPath(), "CefFlashBrowser-download-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Directory.Delete(_directory, true);
        }

        private DownloadRecord CreateRecord()
        {
            return new DownloadRecord
            {
                Url = "https://example.test/file",
                FilePath = Path.Combine(_directory, "file.bin"),
                FileName = "file.bin"
            };
        }

        [TestMethod]
        public async Task PauseResumeUsesActualPartialBytesAndSingleCompletion()
        {
            var record = CreateRecord();
            var http = new FakeHttp { HoldFirst = true };
            var transfer = new HttpDownloadTransfer(record, http, null);
            int finished = 0;
            transfer.Finished += (_, __) => finished++;

            var running = transfer.StartAsync();
            await http.FirstChunk.Task;
            await transfer.PauseAsync();
            await running;

            Assert.AreEqual(DownloadState.Paused, record.State);
            Assert.AreEqual(0, finished);
            Assert.IsFalse(File.Exists(record.FilePath));

            await transfer.StartAsync();

            CollectionAssert.AreEqual(http.Payload, File.ReadAllBytes(record.FilePath));
            Assert.AreEqual("bytes=4-", http.Ranges.Last());
            Assert.AreEqual(DownloadState.Completed, record.State);
            Assert.AreEqual(1, finished);
        }

        [DataTestMethod]
        [DataRow(true, false)]
        [DataRow(false, true)]
        public async Task IncompatibleResumeRestartsWithoutAppending(bool ignoreRange, bool changedVersion)
        {
            var record = CreateRecord();
            record.PartialPath = record.FilePath + ".part";
            File.WriteAllBytes(record.PartialPath, new byte[] { 100, 100, 100, 100 });
            record.ETag = "\"old\"";
            var http = new FakeHttp { IgnoreRange = ignoreRange, ChangedVersion = changedVersion };

            await new HttpDownloadTransfer(record, http, null).StartAsync();

            CollectionAssert.AreEqual(http.Payload, File.ReadAllBytes(record.FilePath));
            Assert.AreEqual(2, http.Ranges.Count);
            Assert.IsNull(http.Ranges.Last());
        }

        [TestMethod]
        public async Task CancellationCleansPartialAndPreservesExistingTarget()
        {
            var record = CreateRecord();
            File.WriteAllText(record.FilePath, "existing");
            var http = new FakeHttp { HoldFirst = true };
            var transfer = new HttpDownloadTransfer(record, http, null);

            var running = transfer.StartAsync();
            await http.FirstChunk.Task;
            await transfer.CancelAsync();
            await running;

            Assert.AreEqual("existing", File.ReadAllText(record.FilePath));
            Assert.AreEqual(DownloadState.Cancelled, record.State);
            Assert.IsFalse(File.Exists(record.PartialPath));
        }

        [TestMethod]
        public async Task CompletionUniquifiesExistingFile()
        {
            var record = CreateRecord();
            File.WriteAllText(record.FilePath, "existing");

            await new HttpDownloadTransfer(record, new FakeHttp(), null).StartAsync();

            Assert.AreEqual("file (1).bin", record.FileName);
            Assert.AreEqual("existing", File.ReadAllText(Path.Combine(_directory, "file.bin")));
        }

        [TestMethod]
        public async Task ShutdownHistoryRestoresInterruptedAndActualLength()
        {
            var record = CreateRecord();
            record.Headers["Authorization"] = "test-secret";
            var http = new FakeHttp { HoldFirst = true };
            var transfer = new HttpDownloadTransfer(record, http, null);

            var running = transfer.StartAsync();
            await http.FirstChunk.Task;
            await transfer.ShutdownAsync();
            await running;

            // Simulate the last snapshot before an abrupt exit, with more bytes reported than were flushed.
            record.State = DownloadState.Downloading;
            record.Received = 10;
            var path = Path.Combine(_directory, "browser.db");
            using (var database = new SqliteDatabase(path))
            {
                await new DatabaseService(database).ApplyDownloadChangesAsync(new[] { record }, Array.Empty<string>());
                var protectedHeaders = (string)await database.ScalarAsync("SELECT protected_headers FROM downloads");
                StringAssert.StartsWith(protectedHeaders, "dpapi:v1:");
                Assert.IsFalse(protectedHeaders.Contains("test-secret"));
            }

            DownloadRecord restored;
            using (var database = new SqliteDatabase(path))
                restored = (await new DatabaseService(database).GetDownloadsAsync()).Single();

            Assert.AreEqual(DownloadState.Interrupted, restored.State);
            Assert.AreEqual(4L, restored.Received);
            Assert.AreEqual("test-secret", restored.Headers["authorization"]);

            await new HttpDownloadTransfer(restored, new FakeHttp(), null).StartAsync();

            CollectionAssert.AreEqual(http.Payload, File.ReadAllBytes(restored.FilePath));
        }

        [TestMethod]
        public async Task HistoryRestoresLegacyProtectedHeadersWithCaseInsensitiveNames()
        {
            using (var database = new SqliteDatabase(Path.Combine(_directory, "browser.db")))
            {
                const string legacyHeaders = "{\"Authorization\":\"test-secret\",\"X-Label\":\"quoted \\\"value\\\"\"}";
                var service = new DatabaseService(database);
                var record = CreateRecord();
                await service.ApplyDownloadChangesAsync(new[] { record }, Array.Empty<string>());
                await database.ExecuteAsync("UPDATE downloads SET protected_headers = @headers WHERE id = @id",
                    new Dictionary<string, object>
                    {
                        ["@headers"] = PasswordProtector.Protect(legacyHeaders),
                        ["@id"] = record.Id
                    });

                var restored = (await service.GetDownloadsAsync()).Single();
                Assert.AreEqual("test-secret", restored.Headers["authorization"]);
                Assert.AreEqual("quoted \"value\"", restored.Headers["x-label"]);
            }
        }

        [TestMethod]
        public async Task HistoryAppliesOnlyChangedRecordsAndRollsBackFailedBatches()
        {
            using (var database = new SqliteDatabase(Path.Combine(_directory, "browser.db")))
            {
                var service = new DatabaseService(database);
                var completed = CreateRecord();
                completed.State = DownloadState.Completed;
                completed.Finished = completed.Created.AddSeconds(1);
                completed.Received = 12;
                completed.Total = 12;
                completed.Overwrite = true;
                completed.FileMissing = true;
                completed.Speed = 12;

                var waiting = CreateRecord();
                waiting.Created = completed.Created.AddSeconds(2);
                waiting.PartialPath = Path.Combine(_directory, "missing.part");
                waiting.Received = 5;

                await service.ApplyDownloadChangesAsync(new[] { completed, waiting }, Array.Empty<string>());

                waiting.Error = "Uncommitted change";
                var invalid = CreateRecord();
                invalid.Url = null;
                await Assert.ThrowsExceptionAsync<SQLiteException>(() =>
                    service.ApplyDownloadChangesAsync(new[] { waiting, invalid }, new[] { completed.Id }));

                var restored = await service.GetDownloadsAsync();
                CollectionAssert.AreEqual(new[] { waiting.Id, completed.Id }, restored.Select(record => record.Id).ToArray());
                Assert.AreEqual(DownloadState.Interrupted, restored[0].State);
                Assert.AreEqual(0L, restored[0].Received);
                Assert.IsNull(restored[0].Total);
                Assert.IsNull(restored[0].Finished);
                Assert.IsNull(restored[0].Error);
                Assert.AreEqual(DownloadState.Completed, restored[1].State);
                Assert.AreEqual(completed.Finished, restored[1].Finished);
                Assert.AreEqual(completed.Total, restored[1].Total);
                Assert.AreEqual(completed.Received, restored[1].Received);
                Assert.IsTrue(restored[1].Overwrite);
                Assert.IsFalse(restored[1].FileMissing);
                Assert.AreEqual(0d, restored[1].Speed);

                foreach (var operation in new[] { "INSERT", "UPDATE", "DELETE" })
                {
                    var row = operation == "INSERT" ? "NEW" : "OLD";
                    await database.ExecuteAsync($"CREATE TRIGGER protect_completed_{operation} BEFORE {operation} ON downloads " +
                        $"WHEN {row}.id = '{completed.Id}' BEGIN SELECT RAISE(ABORT, 'Unrelated history write'); END");
                }

                var added = CreateRecord();
                waiting.State = DownloadState.Paused;
                await service.ApplyDownloadChangesAsync(new[] { waiting, added }, Array.Empty<string>());
                restored = await service.GetDownloadsAsync();
                Assert.AreEqual(3, restored.Count);
                Assert.AreEqual(DownloadState.Paused, restored.Single(record => record.Id == waiting.Id).State);
                Assert.AreEqual(waiting.Error, restored.Single(record => record.Id == waiting.Id).Error);

                await service.ApplyDownloadChangesAsync(Array.Empty<DownloadRecord>(), new[] { waiting.Id, added.Id });
                Assert.AreEqual(completed.Id, (await service.GetDownloadsAsync()).Single().Id);
            }
        }

        [TestMethod]
        public async Task CancelBeforeStartHasOneTerminalAndStartsNoNetwork()
        {
            var http = new FakeHttp();
            var transfer = new HttpDownloadTransfer(CreateRecord(), http, null);
            int finished = 0;
            transfer.Finished += (_, __) => finished++;

            await transfer.CancelAsync();
            await transfer.CancelAsync();
            await transfer.StartAsync();

            Assert.AreEqual(1, finished);
            Assert.AreEqual(0, http.Ranges.Count);
        }

        [TestMethod]
        public async Task CancelPausedTaskPublishesTerminalAndRemovesItsPartial()
        {
            var record = CreateRecord();
            var http = new FakeHttp { HoldFirst = true };
            var transfer = new HttpDownloadTransfer(record, http, null);
            int finished = 0;
            transfer.Finished += (_, __) => finished++;

            var running = transfer.StartAsync();
            await http.FirstChunk.Task;
            await transfer.PauseAsync();
            await transfer.CancelAsync();
            await running;

            Assert.AreEqual(1, finished);
            Assert.AreEqual(DownloadState.Cancelled, record.State);
            Assert.IsFalse(File.Exists(record.PartialPath));
        }

        [TestMethod]
        public async Task MalformedRangeRestartsAndUnknownLengthCanComplete()
        {
            var record = CreateRecord();
            record.PartialPath = record.FilePath + ".part";
            File.WriteAllBytes(record.PartialPath, new byte[] { 1, 2, 3, 4 });
            record.ETag = "\"old\"";
            var http = new FakeHttp { MalformedRange = true, UnknownLength = true };

            await new HttpDownloadTransfer(record, http, null).StartAsync();

            Assert.AreEqual(DownloadState.Completed, record.State);
            CollectionAssert.AreEqual(http.Payload, File.ReadAllBytes(record.FilePath));
            Assert.AreEqual(2, http.Ranges.Count);
        }

        [TestMethod]
        public async Task TruncatedResponseFailsAndPreservesPartialWithoutCommitting()
        {
            var record = CreateRecord();

            await new HttpDownloadTransfer(record, new FakeHttp { ShortResponse = true }, null).StartAsync();

            Assert.AreEqual(DownloadState.Failed, record.State);
            Assert.IsFalse(File.Exists(record.FilePath));
            Assert.AreEqual(4L, new FileInfo(record.PartialPath).Length);
        }

        [DataTestMethod]
        [DataRow(200, DownloadState.Completed)]
        [DataRow(204, DownloadState.Failed)]
        [DataRow(205, DownloadState.Failed)]
        public async Task EmptyResponseOnlyCommitsWhenItRepresentsAFile(int status, DownloadState expectedState)
        {
            var record = CreateRecord();
            var http = new FakeHttp { EmptyResponseStatus = status };

            await new HttpDownloadTransfer(record, http, null).StartAsync();

            Assert.AreEqual(expectedState, record.State);
            Assert.AreEqual(expectedState == DownloadState.Completed, File.Exists(record.FilePath));
        }

        [TestMethod]
        public void InvalidDownloadDirectoriesUseTheSystemFolder()
        {
            var expected = CefFlashBrowser.Utils.UserScriptDownloadPaths.DefaultDirectory();
            Assert.AreEqual(expected, DownloadFiles.DirectoryOrDefault(null));
            Assert.AreEqual(expected, DownloadFiles.DirectoryOrDefault("relative\\path"));
            Assert.AreEqual(expected, DownloadFiles.DirectoryOrDefault("C:\\invalid|name"));
        }

        private sealed class FakeHttp : IHttpRequestService
        {
            public byte[] Payload { get; } = Enumerable.Range(0, 12).Select(value => (byte)value).ToArray();
            public bool HoldFirst, IgnoreRange, ChangedVersion, MalformedRange, UnknownLength, ShortResponse;
            public int? EmptyResponseStatus;
            public List<string> Ranges { get; } = new List<string>();
            public TaskCompletionSource<bool> FirstChunk { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public IHttpRequestOperation SendAsync(UserScriptHttpRequest request, IUserScriptHttpObserver observer, CancellationToken token)
            {
                request.Headers.TryGetValue("Range", out var range);
                Ranges.Add(range);
                if (EmptyResponseStatus.HasValue)
                {
                    var emptyResponse = new UserScriptHttpResponse(EmptyResponseStatus.Value, string.Empty,
                        request.Url, new Dictionary<string, string>(), null, null, null);
                    observer.OnResponseStarted(emptyResponse);
                    return new FakeOperation(Task.FromResult(emptyResponse));
                }

                var offset = range == null || IgnoreRange ? 0 : int.Parse(range.Substring(6).TrimEnd('-'));
                var headers = new Dictionary<string, string>
                {
                    ["ETag"] = ChangedVersion ? "\"new\"" : "\"old\"",
                    ["Content-Length"] = (Payload.Length - offset).ToString()
                };

                if (offset != 0)
                    headers["Content-Range"] = MalformedRange ? "bytes 0-11/12" : "bytes " + offset + "-11/12";
                if (UnknownLength)
                    headers.Remove("Content-Length");

                var response = new UserScriptHttpResponse(offset == 0 ? 200 : 206, "OK", request.Url, headers, null, null, null);
                return new FakeOperation(Task.Run(async () =>
                {
                    observer.OnResponseStarted(response);
                    observer.OnResponseData(Payload, offset, 4);
                    FirstChunk.TrySetResult(true);
                    if (HoldFirst && Ranges.Count == 1)
                        await Task.Delay(Timeout.Infinite, token);
                    token.ThrowIfCancellationRequested();
                    if (!ShortResponse)
                        observer.OnResponseData(Payload, offset + 4, Payload.Length - offset - 4);
                    return response;
                }, token));
            }
        }

        private sealed class FakeOperation : IHttpRequestOperation
        {
            public Task<UserScriptHttpResponse> Completion { get; }

            public FakeOperation(Task<UserScriptHttpResponse> task)
            {
                Completion = task;
            }

            public void Abort() { }
        }
    }
}
