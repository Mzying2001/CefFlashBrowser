using CefFlashBrowser.Log;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Tests
{
    [TestClass]
    public class FileLoggerTests
    {
        private string _testDir;

        [TestInitialize]
        public void Setup()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "CefFlashBrowserTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                if (Directory.Exists(_testDir))
                    Directory.Delete(_testDir, true);
            }
            catch { }
        }

        private string GetLogPath(string name = "test.log")
        {
            return Path.Combine(_testDir, name);
        }

        private string GetDailyLogPath(DateTime time)
        {
            return GetLogPath($"app_{time:yyyyMMdd}.log");
        }

        [TestMethod]
        public void Log_AcrossMidnight_WritesToMatchingDateFiles()
        {
            var beforeMidnight = new DateTime(2026, 10, 27, 23, 59, 59, 999);
            var afterMidnight = beforeMidnight.AddMilliseconds(1);
            var now = beforeMidnight;

            using (var logger = new FileLogger(GetDailyLogPath, () => now))
            {
                logger.Log(LogLevel.Info, "before midnight");
                now = afterMidnight;
                logger.Log(LogLevel.Info, "after midnight");
            }

            CollectionAssert.AreEqual(
                new[] { "2026-10-27 23:59:59.999 | INFO    | before midnight" },
                File.ReadAllLines(GetDailyLogPath(beforeMidnight)));
            CollectionAssert.AreEqual(
                new[] { "2026-10-28 00:00:00.000 | INFO    | after midnight" },
                File.ReadAllLines(GetDailyLogPath(afterMidnight)));
        }

        [DataTestMethod]
        [DataRow(false, 0)]
        [DataRow(false, 1)]
        [DataRow(false, 2)]
        [DataRow(true, 0)]
        [DataRow(true, 1)]
        [DataRow(true, 2)]
        public async Task Log_AllOverloads_RotateAcrossMidnight(bool asynchronous, int overload)
        {
            var beforeMidnight = new DateTime(2026, 10, 27, 23, 59, 59, 999);
            var now = beforeMidnight;
            var exception = new InvalidOperationException("test exception");

            using (var logger = new FileLogger(GetDailyLogPath, () => now))
            {
                logger.Log(LogLevel.Info, "before midnight");
                now = beforeMidnight.AddMilliseconds(1);

                switch (overload)
                {
                    case 0:
                        if (asynchronous)
                            await logger.LogAsync(LogLevel.Error, "after midnight");
                        else
                            logger.Log(LogLevel.Error, "after midnight");
                        break;
                    case 1:
                        if (asynchronous)
                            await logger.LogAsync(LogLevel.Error, exception);
                        else
                            logger.Log(LogLevel.Error, exception);
                        break;
                    case 2:
                        if (asynchronous)
                            await logger.LogAsync(LogLevel.Error, "after midnight", exception);
                        else
                            logger.Log(LogLevel.Error, "after midnight", exception);
                        break;
                }
            }

            string body = overload == 1 ? string.Empty : "after midnight";

            if (overload != 0)
                body += Environment.NewLine + exception;

            Assert.AreEqual(
                "2026-10-28 00:00:00.000 | ERROR   | " + body + Environment.NewLine,
                File.ReadAllText(GetDailyLogPath(now)));
            Assert.AreEqual(1, File.ReadAllLines(GetDailyLogPath(beforeMidnight)).Length);
        }

        [TestMethod]
        public async Task Log_ClockAdvancesAtMidnight_UsesSameTimeForPathAndTimestamp()
        {
            var beforeMidnight = new DateTime(2026, 10, 27, 23, 59, 59, 999);
            var now = beforeMidnight;

            using (var logger = new FileLogger(GetDailyLogPath, () =>
            {
                var time = now;
                now = now.AddMilliseconds(1);
                return time;
            }))
            {
                now = beforeMidnight;
                logger.Log(LogLevel.Info, "synchronous");
                await logger.LogAsync(LogLevel.Info, "asynchronous");
            }

            CollectionAssert.AreEqual(
                new[] { "2026-10-27 23:59:59.999 | INFO    | synchronous" },
                File.ReadAllLines(GetDailyLogPath(beforeMidnight)));
            CollectionAssert.AreEqual(
                new[] { "2026-10-28 00:00:00.000 | INFO    | asynchronous" },
                File.ReadAllLines(GetDailyLogPath(beforeMidnight.AddDays(1))));
        }

        [TestMethod]
        public async Task Log_SkippedDaysAndClockRollback_AppendToExistingFiles()
        {
            var firstDay = new DateTime(2026, 10, 27, 12, 0, 0);
            var laterDay = firstDay.AddDays(3);
            var now = firstDay;
            File.WriteAllText(GetDailyLogPath(laterDay), "existing log" + Environment.NewLine);

            using (var logger = new FileLogger(GetDailyLogPath, () => now))
            {
                logger.Log(LogLevel.Info, "first day");
                now = laterDay;
                await logger.LogAsync(LogLevel.Info, "later day");
                now = firstDay;
                logger.Log(LogLevel.Info, "clock rolled back");
                now = laterDay;
                await logger.LogAsync(LogLevel.Info, "later day again");
            }

            CollectionAssert.AreEqual(
                new[]
                {
                    "2026-10-27 12:00:00.000 | INFO    | first day",
                    "2026-10-27 12:00:00.000 | INFO    | clock rolled back"
                },
                File.ReadAllLines(GetDailyLogPath(firstDay)));
            CollectionAssert.AreEqual(
                new[]
                {
                    "existing log",
                    "2026-10-30 12:00:00.000 | INFO    | later day",
                    "2026-10-30 12:00:00.000 | INFO    | later day again"
                },
                File.ReadAllLines(GetDailyLogPath(laterDay)));
            Assert.AreEqual(2, Directory.GetFiles(_testDir).Length);
        }

        [TestMethod]
        public async Task Log_AfterRotation_ReleasesOldFile()
        {
            var now = new DateTime(2026, 10, 27, 12, 0, 0);
            var oldPath = GetDailyLogPath(now);

            using (var logger = new FileLogger(GetDailyLogPath, () => now))
            {
                logger.Log(LogLevel.Info, "old file");
                now = now.AddDays(1);
                await logger.LogAsync(LogLevel.Info, "new file");

                using (File.Open(oldPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                }
            }
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task Log_RotationFails_PreservesOldWriterAndCanRetry(bool asynchronous)
        {
            var firstDay = new DateTime(2026, 10, 27, 12, 0, 0);
            var nextDay = firstDay.AddDays(1);
            var now = firstDay;
            var nextPath = GetDailyLogPath(nextDay);
            File.WriteAllText(nextPath, "existing log" + Environment.NewLine);

            using (var logger = new FileLogger(GetDailyLogPath, () => now))
            {
                logger.Log(LogLevel.Info, "before rotation");

                using (File.Open(nextPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    now = nextDay;

                    if (asynchronous)
                        await Assert.ThrowsExceptionAsync<IOException>(() => logger.LogAsync(LogLevel.Info, "failed write"));
                    else
                        Assert.ThrowsException<IOException>(() => logger.Log(LogLevel.Info, "failed write"));

                    now = firstDay;
                    logger.Log(LogLevel.Info, "old writer still works");
                }

                now = nextDay;

                if (asynchronous)
                    await logger.LogAsync(LogLevel.Info, "retry succeeded");
                else
                    logger.Log(LogLevel.Info, "retry succeeded");
            }

            CollectionAssert.AreEqual(
                new[]
                {
                    "2026-10-27 12:00:00.000 | INFO    | before rotation",
                    "2026-10-27 12:00:00.000 | INFO    | old writer still works"
                },
                File.ReadAllLines(GetDailyLogPath(firstDay)));
            CollectionAssert.AreEqual(
                new[] { "existing log", "2026-10-28 12:00:00.000 | INFO    | retry succeeded" },
                File.ReadAllLines(nextPath));
        }

        [TestMethod]
        public async Task Log_ConcurrentSyncAndAsyncWritesAcrossMidnight_PreserveEveryMessage()
        {
            const int messageCount = 200;
            var beforeMidnight = new DateTime(2026, 10, 27, 23, 59, 59, 999);
            var afterMidnight = beforeMidnight.AddMilliseconds(1);
            int clockReads = 0;

            using (var logger = new FileLogger(GetDailyLogPath, () =>
                Interlocked.Increment(ref clockReads) <= messageCount / 2 ? beforeMidnight : afterMidnight))
            {
                clockReads = 0;
                var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var writes = Enumerable.Range(0, messageCount).Select(async index =>
                {
                    await start.Task.ConfigureAwait(false);

                    if (index % 2 == 0)
                        logger.Log(LogLevel.Info, "message " + index);
                    else
                        await logger.LogAsync(LogLevel.Info, "message " + index).ConfigureAwait(false);
                }).ToArray();

                start.SetResult(true);
                await Task.WhenAll(writes);
            }

            var oldLines = File.ReadAllLines(GetDailyLogPath(beforeMidnight));
            var newLines = File.ReadAllLines(GetDailyLogPath(afterMidnight));
            Assert.AreEqual(messageCount / 2, oldLines.Length);
            Assert.AreEqual(messageCount / 2, newLines.Length);
            Assert.IsTrue(oldLines.All(line => line.StartsWith("2026-10-27 23:59:59.999 | INFO    | ")));
            Assert.IsTrue(newLines.All(line => line.StartsWith("2026-10-28 00:00:00.000 | INFO    | ")));
            CollectionAssert.AreEquivalent(
                Enumerable.Range(0, messageCount).Select(index => "message " + index).ToArray(),
                oldLines.Concat(newLines).Select(line => line.Split(new[] { " | " }, StringSplitOptions.None)[2]).ToArray());
        }

        [TestMethod]
        public async Task LogAsync_AfterRepeatedDispose_ThrowsObjectDisposedException()
        {
            var logger = new FileLogger(GetLogPath());
            logger.Dispose();
            logger.Dispose();

            var exception = await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() =>
                logger.LogAsync(LogLevel.Info, "should fail"));
            Assert.AreEqual(nameof(FileLogger), exception.ObjectName);
        }

        [TestMethod]
        [Timeout(15000)]
        public async Task Dispose_WithActiveAndQueuedWrites_CompletesSafely()
        {
            var now = new DateTime(2026, 10, 27, 12, 0, 0);
            int clockReads = 0;

            using (var writeEntered = new ManualResetEventSlim())
            using (var releaseWrite = new ManualResetEventSlim())
            using (var logger = new FileLogger(GetDailyLogPath, () =>
            {
                if (Interlocked.Increment(ref clockReads) == 2)
                {
                    writeEntered.Set();

                    if (!releaseWrite.Wait(TimeSpan.FromSeconds(10)))
                        throw new TimeoutException("The active write was not released.");
                }

                return now;
            }))
            {
                var activeWrite = Task.Run(() => logger.Log(LogLevel.Info, "active write"));

                try
                {
                    Assert.IsTrue(writeEntered.Wait(TimeSpan.FromSeconds(10)), "The active write did not start.");
                    var queuedAsyncWrite = TryLogDuringDisposeAsync(logger, true, "queued async write");
                    var queuedSyncWrite = Task.Run(() => TryLogDuringDisposeAsync(logger, false, "queued sync write"));
                    var dispose = Task.Run(() => logger.Dispose());
                    releaseWrite.Set();

                    await Task.WhenAll(activeWrite, dispose);
                    bool asyncWritten = await queuedAsyncWrite;
                    bool syncWritten = await queuedSyncWrite;
                    var lines = File.ReadAllLines(GetDailyLogPath(now));
                    Assert.AreEqual(1 + (asyncWritten ? 1 : 0) + (syncWritten ? 1 : 0), lines.Length);
                    Assert.AreEqual("2026-10-27 12:00:00.000 | INFO    | active write", lines[0]);
                    Assert.AreEqual(asyncWritten, lines.Any(line => line.EndsWith(" | queued async write")));
                    Assert.AreEqual(syncWritten, lines.Any(line => line.EndsWith(" | queued sync write")));
                    Assert.ThrowsException<ObjectDisposedException>(() => logger.Log(LogLevel.Info, "after disposal"));
                }
                finally
                {
                    releaseWrite.Set();
                    await activeWrite;
                }

                using (File.Open(GetDailyLogPath(now), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                }
            }
        }

        private static async Task<bool> TryLogDuringDisposeAsync(FileLogger logger, bool asynchronous, string message)
        {
            try
            {
                if (asynchronous)
                    await logger.LogAsync(LogLevel.Info, message);
                else
                    logger.Log(LogLevel.Info, message);

                return true;
            }
            catch (ObjectDisposedException exception)
            {
                Assert.AreEqual(nameof(FileLogger), exception.ObjectName);
                return false;
            }
        }

        [TestMethod]
        public void Log_WritesMessageToFile()
        {
            var path = GetLogPath();
            using (var logger = new FileLogger(path))
            {
                logger.Log(LogLevel.Info, "Hello World");
            }

            var content = File.ReadAllText(path);
            Assert.IsTrue(content.Contains("INFO"));
            Assert.IsTrue(content.Contains("Hello World"));
        }

        [TestMethod]
        public void Log_WritesCorrectFormat()
        {
            var path = GetLogPath();
            using (var logger = new FileLogger(path))
            {
                logger.Log(LogLevel.Error, "Something failed");
            }

            var content = File.ReadAllText(path);
            Assert.IsTrue(content.Contains("ERROR"));
            Assert.IsTrue(content.Contains("Something failed"));
            Assert.IsTrue(content.Contains("|"));
        }

        [TestMethod]
        public void Log_AllLevels_WriteCorrectLevelString()
        {
            var path = GetLogPath();
            using (var logger = new FileLogger(path))
            {
                logger.Log(LogLevel.Debug, "debug msg");
                logger.Log(LogLevel.Info, "info msg");
                logger.Log(LogLevel.Warning, "warning msg");
                logger.Log(LogLevel.Error, "error msg");
                logger.Log(LogLevel.Fatal, "fatal msg");
            }

            var content = File.ReadAllText(path);
            Assert.IsTrue(content.Contains("DEBUG"));
            Assert.IsTrue(content.Contains("INFO"));
            Assert.IsTrue(content.Contains("WARNING"));
            Assert.IsTrue(content.Contains("ERROR"));
            Assert.IsTrue(content.Contains("FATAL"));
        }

        [TestMethod]
        public void Log_WithException_WritesExceptionInfo()
        {
            var path = GetLogPath();
            var exception = new InvalidOperationException("test exception");
            using (var logger = new FileLogger(path))
            {
                logger.Log(LogLevel.Error, exception);
            }

            var content = File.ReadAllText(path);
            Assert.IsTrue(content.Contains("test exception"));
            Assert.IsTrue(content.Contains("InvalidOperationException"));
        }

        [TestMethod]
        public void Log_WithMessageAndException_WritesBoth()
        {
            var path = GetLogPath();
            var exception = new InvalidOperationException("test exception");
            using (var logger = new FileLogger(path))
            {
                logger.Log(LogLevel.Error, "context message", exception);
            }

            var content = File.ReadAllText(path);
            Assert.IsTrue(content.Contains("context message"));
            Assert.IsTrue(content.Contains("test exception"));
        }

        [TestMethod]
        public async Task LogAsync_WritesMessageToFile()
        {
            var path = GetLogPath();
            using (var logger = new FileLogger(path))
            {
                await logger.LogAsync(LogLevel.Info, "async message");
            }

            var content = File.ReadAllText(path);
            Assert.IsTrue(content.Contains("async message"));
        }

        [TestMethod]
        [ExpectedException(typeof(ObjectDisposedException))]
        public void Log_AfterDispose_ThrowsObjectDisposedException()
        {
            var path = GetLogPath();
            var logger = new FileLogger(path);
            logger.Dispose();
            logger.Log(LogLevel.Info, "should fail");
        }

        [TestMethod]
        public void FileLogger_CreatesDirectoryIfNotExists()
        {
            var subDir = Path.Combine(_testDir, "subdir", "nested");
            var path = Path.Combine(subDir, "test.log");

            using (var logger = new FileLogger(path))
            {
                logger.Log(LogLevel.Info, "test");
            }

            Assert.IsTrue(File.Exists(path));
        }

        [TestMethod]
        public void FileLogger_AppendsToExistingFile()
        {
            var path = GetLogPath();

            using (var logger = new FileLogger(path))
            {
                logger.Log(LogLevel.Info, "first message");
            }

            using (var logger = new FileLogger(path))
            {
                logger.Log(LogLevel.Info, "second message");
            }

            var content = File.ReadAllText(path);
            Assert.IsTrue(content.Contains("first message"));
            Assert.IsTrue(content.Contains("second message"));
        }
    }
}
