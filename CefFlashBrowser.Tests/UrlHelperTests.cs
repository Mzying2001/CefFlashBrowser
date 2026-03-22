using CefFlashBrowser.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CefFlashBrowser.Tests
{
    [TestClass]
    public class UrlHelperTests
    {
        [TestMethod]
        public void IsUrl_NullInput_ReturnsFalse()
        {
            Assert.IsFalse(UrlHelper.IsUrl(null));
        }

        [TestMethod]
        public void IsUrl_EmptyString_ReturnsFalse()
        {
            Assert.IsFalse(UrlHelper.IsUrl(""));
        }

        [TestMethod]
        public void IsUrl_WhitespaceOnly_ReturnsFalse()
        {
            Assert.IsFalse(UrlHelper.IsUrl("   "));
        }

        [TestMethod]
        public void IsUrl_HttpScheme_ReturnsTrue()
        {
            Assert.IsTrue(UrlHelper.IsUrl("http://example.com"));
        }

        [TestMethod]
        public void IsUrl_HttpsScheme_ReturnsTrue()
        {
            Assert.IsTrue(UrlHelper.IsUrl("https://example.com"));
        }

        [TestMethod]
        public void IsUrl_FtpScheme_ReturnsTrue()
        {
            Assert.IsTrue(UrlHelper.IsUrl("ftp://files.example.com"));
        }

        [TestMethod]
        public void IsUrl_Localhost_ReturnsTrue()
        {
            Assert.IsTrue(UrlHelper.IsUrl("localhost"));
        }

        [TestMethod]
        public void IsUrl_LocalhostWithPort_ReturnsTrue()
        {
            Assert.IsTrue(UrlHelper.IsUrl("localhost:8080"));
        }

        [TestMethod]
        public void IsUrl_IpAddress_ReturnsTrue()
        {
            Assert.IsTrue(UrlHelper.IsUrl("192.168.1.1"));
        }

        [TestMethod]
        public void IsUrl_IpAddressWithPort_ReturnsTrue()
        {
            Assert.IsTrue(UrlHelper.IsUrl("192.168.1.1:8080"));
        }

        [TestMethod]
        public void IsUrl_DomainLikeString_ReturnsTrue()
        {
            Assert.IsTrue(UrlHelper.IsUrl("example.com"));
        }

        [TestMethod]
        public void IsUrl_SubdomainString_ReturnsTrue()
        {
            Assert.IsTrue(UrlHelper.IsUrl("www.example.com"));
        }

        [TestMethod]
        public void IsUrl_PlainWord_ReturnsFalse()
        {
            Assert.IsFalse(UrlHelper.IsUrl("hello"));
        }

        [TestMethod]
        public void IsUrl_PlainNumber_ReturnsFalse2()
        {
            Assert.IsFalse(UrlHelper.IsUrl("3.14"));
        }

        [TestMethod]
        public void IsUrl_PureNumber_ReturnsFalse()
        {
            Assert.IsFalse(UrlHelper.IsUrl("123"));
        }

        [TestMethod]
        public void IsUrl_PlainWord_WithSpaces_ReturnsFalse()
        {
            Assert.IsFalse(UrlHelper.IsUrl("hello world"));
        }

        [TestMethod]
        public void IsUrl_FileScheme_ReturnsFalse()
        {
            Assert.IsFalse(UrlHelper.IsUrl("file:///C:/test.txt"));
        }

        [TestMethod]
        public void IsUrl_UnknownScheme_ReturnsFalse()
        {
            Assert.IsFalse(UrlHelper.IsUrl("abc:abc"));
        }

        [TestMethod]
        public void IsUrl_WithPath_ReturnsTrue()
        {
            Assert.IsTrue(UrlHelper.IsUrl("example.com/path/to/page"));
        }

        [TestMethod]
        public void IsUrl_WithQueryString_ReturnsTrue()
        {
            Assert.IsTrue(UrlHelper.IsUrl("example.com?q=test"));
        }
    }
}
