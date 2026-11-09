using CefFlashBrowser.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Security.Cryptography;
using System.Text;

namespace CefFlashBrowser.Tests
{
    [TestClass]
    public class PasswordProtectorTests
    {
        [DataTestMethod]
        [DataRow("proxy-password")]
        [DataRow("\u4ee3\u7406\u5bc6\u7801\ud83d\udd12\r\n p@ss\\word\" ")]
        [DataRow("dpapi:v1:this-is-the-password")]
        public void Protect_Password_RoundTripsWithCurrentUserDpapi(string password)
        {
            string protectedPassword = PasswordProtector.Protect(password);

            StringAssert.StartsWith(protectedPassword, "dpapi:v1:");
            Assert.AreNotEqual(password, protectedPassword);
            Assert.AreEqual(password, PasswordProtector.UnprotectOrEmpty(protectedPassword));

            byte[] protectedData = Convert.FromBase64String(protectedPassword.Substring("dpapi:v1:".Length));
            byte[] data = ProtectedData.Unprotect(protectedData, null, DataProtectionScope.CurrentUser);
            Assert.AreEqual(password, Encoding.UTF8.GetString(data));
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        public void Protect_EmptyPassword_ReturnsEmpty(string password)
        {
            Assert.AreEqual(string.Empty, PasswordProtector.Protect(password));
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("legacy-plaintext-password")]
        [DataRow("bGVnYWN5LXBhc3N3b3Jk")]
        [DataRow("dpapi:v2:AQID")]
        [DataRow("DPAPI:v1:AQID")]
        [DataRow("dpapi:v1:")]
        [DataRow("dpapi:v1:invalid-base64!")]
        [DataRow("dpapi:v1:AQID")]
        public void UnprotectOrEmpty_UnreadablePassword_ReturnsEmpty(string password)
        {
            Assert.AreEqual(string.Empty, PasswordProtector.UnprotectOrEmpty(password));
        }

        [TestMethod]
        public void UnprotectOrEmpty_TamperedCiphertext_ReturnsEmpty()
        {
            string password = PasswordProtector.Protect("proxy-password");
            byte[] data = Convert.FromBase64String(password.Substring("dpapi:v1:".Length));
            data[data.Length - 1] ^= 0x80;

            Assert.AreEqual(string.Empty, PasswordProtector.UnprotectOrEmpty("dpapi:v1:" + Convert.ToBase64String(data)));
        }

        [TestMethod]
        public void UnprotectOrEmpty_CiphertextRequiringDifferentEntropy_ReturnsEmpty()
        {
            byte[] data = ProtectedData.Protect(Encoding.UTF8.GetBytes("proxy-password"),
                Encoding.UTF8.GetBytes("other-application"), DataProtectionScope.CurrentUser);

            Assert.AreEqual(string.Empty, PasswordProtector.UnprotectOrEmpty("dpapi:v1:" + Convert.ToBase64String(data)));
        }
    }
}
