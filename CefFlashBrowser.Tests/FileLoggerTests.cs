using CefFlashBrowser.Log;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
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
