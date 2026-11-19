using CefFlashBrowser.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace CefFlashBrowser.Tests
{
    [TestClass]
    public class UserScriptDownloadPathsTests
    {
        [DataTestMethod]
        [DataRow("../../outside.bin", "outside.bin")]
        [DataRow("C:\\secret\\file.bin", "file.bin")]
        [DataRow("CON.txt", "_CON.txt")]
        [DataRow("com1.exe", "_com1.exe")]
        [DataRow("LPT9", "_LPT9")]
        [DataRow("NUL", "_NUL")]
        [DataRow("CONOUT$", "_CONOUT$")]
        [DataRow("a:b?.bin", "a_b_.bin")]
        [DataRow(" . ", "download.bin")]
        [DataRow(null, "download.bin")]
        public void SuggestedNames_CannotEscapeDestinationOrUseWindowsDevices(string input, string expected)
        {
            var name = UserScriptDownloadPaths.SafeFileName(input);

            Assert.AreEqual(expected, name);
            Assert.AreEqual(name, Path.GetFileName(name));
        }

        [TestMethod]
        public void LongNames_RemainBoundedAndUsable()
        {
            var name = UserScriptDownloadPaths.SafeFileName(new string('a', 400) + ".bin");

            Assert.IsTrue(name.Length <= 150);
            Assert.IsFalse(name.EndsWith(".", StringComparison.Ordinal));
        }
    }
}
