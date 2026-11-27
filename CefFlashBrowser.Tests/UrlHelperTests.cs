using CefFlashBrowser.Models;
using CefFlashBrowser.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CefFlashBrowser.Tests
{
    [TestClass]
    public class UrlHelperTests
    {
        [DataTestMethod]
        [DataRow(SearchEngine.Baidu, "https://www.baidu.com/")]
        [DataRow(SearchEngine.Google, "https://www.google.com/")]
        [DataRow(SearchEngine.Bing, "https://www.bing.com/")]
        [DataRow(SearchEngine.Sogou, "https://www.sogou.com/")]
        [DataRow(SearchEngine.So360, "https://www.so.com/")]
        [DataRow(SearchEngine.DuckDuckGo, "https://duckduckgo.com/")]
        [DataRow(SearchEngine.Yandex, "https://yandex.com/")]
        [DataRow(SearchEngine.Bilibili, "https://www.bilibili.com/")]
        [DataRow(SearchEngine.Game4399, "https://www.4399.com/")]
        [DataRow((SearchEngine)int.MaxValue, "https://www.bing.com/")]
        public void GetSearchEngineHomepage_ReturnsExpectedAddress(SearchEngine engine, string expected)
        {
            Assert.AreEqual(expected, UrlHelper.GetSearchEngineHomepage(engine));
        }

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
