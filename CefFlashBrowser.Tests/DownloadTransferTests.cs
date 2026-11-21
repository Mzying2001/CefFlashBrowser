using CefFlashBrowser.Models.Downloads;
using CefFlashBrowser.Services.Downloads;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mzying2001.MonkeySharp.Core.Apis;
using System;
using System.Collections.Generic;
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
        public void Cleanup() { Directory.Delete(_directory, true); }

        private DownloadRecord CreateRecord()
        {
            return new DownloadRecord { Url = "https://example.test/file", FilePath = Path.Combine(_directory, "file.bin"), FileName = "file.bin" };
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
            var path = Path.Combine(_directory, "history.json");
            var history = new DownloadHistory(path);
            history.Save(new[] { record });
            Assert.IsFalse(File.ReadAllText(path).Contains("test-secret"));
            var restored = history.Load().Single();
            Assert.AreEqual(DownloadState.Interrupted, restored.State);
            Assert.AreEqual(4L, restored.Received);
            Assert.AreEqual("test-secret", restored.Headers["Authorization"]);
            await new HttpDownloadTransfer(restored, new FakeHttp(), null).StartAsync();
            CollectionAssert.AreEqual(http.Payload, File.ReadAllBytes(restored.FilePath));
        }

        private sealed class FakeHttp : IHttpRequestService
        {
            public byte[] Payload { get; } = Enumerable.Range(0, 12).Select(value => (byte)value).ToArray();
            public bool HoldFirst, IgnoreRange, ChangedVersion;
            public List<string> Ranges { get; } = new List<string>();
            public TaskCompletionSource<bool> FirstChunk { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public IHttpRequestOperation SendAsync(UserScriptHttpRequest request, IUserScriptHttpObserver observer, CancellationToken token)
            {
                request.Headers.TryGetValue("Range", out var range);
                Ranges.Add(range);
                var offset = range == null || IgnoreRange ? 0 : int.Parse(range.Substring(6).TrimEnd('-'));
                var headers = new Dictionary<string, string> { ["ETag"] = ChangedVersion ? "\"new\"" : "\"old\"", ["Content-Length"] = (Payload.Length - offset).ToString() };
                if (offset != 0)
                    headers["Content-Range"] = "bytes " + offset + "-11/12";
                var response = new UserScriptHttpResponse(offset == 0 ? 200 : 206, "OK", request.Url, headers, null, null, null);
                return new FakeOperation(Task.Run(async () =>
                {
                    observer.OnResponseStarted(response);
                    observer.OnResponseData(Payload, offset, 4);
                    FirstChunk.TrySetResult(true);
                    if (HoldFirst && Ranges.Count == 1)
                        await Task.Delay(Timeout.Infinite, token);
                    token.ThrowIfCancellationRequested();
                    observer.OnResponseData(Payload, offset + 4, Payload.Length - offset - 4);
                    return response;
                }, token));
            }
        }

        private sealed class FakeOperation : IHttpRequestOperation
        {
            public Task<UserScriptHttpResponse> Completion { get; }
            public FakeOperation(Task<UserScriptHttpResponse> task) { Completion = task; }
            public void Abort() { }
        }
    }
}
