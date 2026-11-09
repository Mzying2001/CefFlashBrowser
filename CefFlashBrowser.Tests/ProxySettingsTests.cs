using CefFlashBrowser.Models;
using CefFlashBrowser.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CefFlashBrowser.Tests
{
    [TestClass]
    public class ProxySettingsTests
    {
        [TestMethod]
        public void SerializeAndDeserialize_Settings_StoresOnlyCiphertextAndRestoresPassword()
        {
            const string password = "proxy-secret-\u5bc6\u7801";
            var settings = new Settings();
            settings.ProxySettings.EnableProxy = true;
            settings.ProxySettings.IP = "127.0.0.1";
            settings.ProxySettings.Port = "8080";
            settings.ProxySettings.UserName = "proxy-user";
            settings.ProxySettings.PlainPassword = password;

            string json = JsonConvert.SerializeObject(settings);
            var proxyJson = (JObject)JObject.Parse(json)["ProxySettings"];

            Assert.AreEqual(settings.ProxySettings.Password, (string)proxyJson["Password"]);
            StringAssert.StartsWith((string)proxyJson["Password"], "dpapi:v1:");
            Assert.IsNull(proxyJson["PlainPassword"]);
            Assert.IsFalse(json.Contains(password));

            var restored = JsonConvert.DeserializeObject<Settings>(json);
            Assert.AreEqual(password, restored.ProxySettings.PlainPassword);
            Assert.AreEqual(settings.ProxySettings.Password, restored.ProxySettings.Password);
            Assert.AreEqual("127.0.0.1", restored.ProxySettings.IP);
            Assert.AreEqual("8080", restored.ProxySettings.Port);
            Assert.AreEqual("proxy-user", restored.ProxySettings.UserName);
            Assert.IsTrue(restored.ProxySettings.EnableProxy);
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("legacy-plaintext-secret")]
        [DataRow("dpapi:v1:invalid-base64!")]
        [DataRow("dpapi:v1:AQID")]
        public void Deserialize_UnreadablePassword_ClearsStoredValueAndPreservesOtherSettings(string password)
        {
            var json = new JObject
            {
                ["Language"] = "en-US",
                ["ProxySettings"] = new JObject
                {
                    ["EnableProxy"] = true,
                    ["IP"] = "127.0.0.1",
                    ["Port"] = "8080",
                    ["UserName"] = "proxy-user",
                    ["Password"] = password
                }
            };

            var settings = JsonConvert.DeserializeObject<Settings>(json.ToString());

            Assert.AreEqual(string.Empty, settings.ProxySettings.Password);
            Assert.AreEqual(string.Empty, settings.ProxySettings.PlainPassword);
            Assert.AreEqual("en-US", settings.Language);
            Assert.IsTrue(settings.ProxySettings.EnableProxy);
            Assert.AreEqual("127.0.0.1", settings.ProxySettings.IP);
            Assert.AreEqual("8080", settings.ProxySettings.Port);
            Assert.AreEqual("proxy-user", settings.ProxySettings.UserName);
            Assert.AreEqual(string.Empty, (string)JObject.FromObject(settings)["ProxySettings"]["Password"]);
        }

        [TestMethod]
        public void Deserialize_PlainPasswordProperty_IsIgnored()
        {
            string password = PasswordProtector.Protect("original-password");
            var json = new JObject
            {
                ["Password"] = password,
                ["PlainPassword"] = "unexpected-plaintext"
            };

            var settings = JsonConvert.DeserializeObject<ProxySettings>(json.ToString());

            Assert.AreEqual(password, settings.Password);
            Assert.AreEqual("original-password", settings.PlainPassword);
            Assert.IsFalse(JsonConvert.SerializeObject(settings).Contains("unexpected-plaintext"));
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        public void PlainPassword_Cleared_RemovesCiphertext(string password)
        {
            var settings = new ProxySettings { PlainPassword = "previous-password" };

            settings.PlainPassword = password;

            Assert.AreEqual(string.Empty, settings.Password);
            Assert.AreEqual(string.Empty, settings.PlainPassword);
        }
    }
}
