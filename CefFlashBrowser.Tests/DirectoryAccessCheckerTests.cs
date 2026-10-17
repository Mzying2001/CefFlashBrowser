using CefFlashBrowser.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace CefFlashBrowser.Tests
{
    [TestClass]
    public class DirectoryAccessCheckerTests
    {
        private string _testDirectory;

        [TestInitialize]
        public void Setup()
        {
            _testDirectory = Path.Combine(
                Path.GetTempPath(),
                "DirectoryAccessCheckerTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDirectory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }

        [TestMethod]
        public void CanReadAndWrite_WritableDirectory_ReturnsTrueWithoutLeavingProbeFile()
        {
            bool result = DirectoryAccessChecker.CanReadAndWrite(_testDirectory);

            Assert.IsTrue(result);
            Assert.AreEqual(0, Directory.GetFileSystemEntries(_testDirectory).Length);
        }

        [TestMethod]
        public void CanReadAndWrite_MissingDirectory_ReturnsFalseWithoutCreatingDirectory()
        {
            string missingDirectory = Path.Combine(_testDirectory, "missing");

            bool result = DirectoryAccessChecker.CanReadAndWrite(missingDirectory);

            Assert.IsFalse(result);
            Assert.IsFalse(Directory.Exists(missingDirectory));
        }

        [TestMethod]
        public void CanReadAndWrite_FilePath_ReturnsFalse()
        {
            string filePath = Path.Combine(_testDirectory, "file.txt");
            File.WriteAllText(filePath, "test");

            Assert.IsFalse(DirectoryAccessChecker.CanReadAndWrite(filePath));
        }
    }
}
