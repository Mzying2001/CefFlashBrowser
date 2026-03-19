using CefFlashBrowser.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CefFlashBrowser.Tests
{
    [TestClass]
    public class SettingsTests
    {
        [TestMethod]
        public void DefaultConstructor_SetsExpectedDefaults()
        {
            var settings = new Settings();

            Assert.IsTrue(settings.FirstStart);
            Assert.AreEqual("zh-CN", settings.Language);
            Assert.AreEqual(Theme.Light, settings.Theme);
            Assert.AreEqual(SearchEngine.Bing, settings.SearchEngine);
            Assert.AreEqual(NavigationType.Automatic, settings.NavigationType);
            Assert.AreEqual(NewPageBehavior.OriginalWindow, settings.NewPageBehavior);
            Assert.IsFalse(settings.DisableOnBeforeUnloadDialog);
            Assert.IsNull(settings.MainWindowSizeInfo);
            Assert.IsNull(settings.BrowserWindowSizeInfo);
            Assert.IsNull(settings.SwfWindowSizeInfo);
            Assert.IsNull(settings.SolSaveManagerSizeInfo);
            Assert.IsNotNull(settings.ProxySettings);
            Assert.IsNotNull(settings.UserAgentSetting);
            Assert.IsNotNull(settings.FakeFlashVersionSetting);
            Assert.IsFalse(settings.HideMainWindowOnBrowsing);
            Assert.IsTrue(settings.FollowSystemTheme);
            Assert.IsTrue(settings.EnableIntegratedDevTools);
            Assert.AreEqual(300d, settings.IntegratedDevToolsWidth);
            Assert.IsTrue(settings.SaveZoomLevel);
            Assert.AreEqual(0d, settings.BrowserZoomLevel);
            Assert.IsFalse(settings.DisableFullscreen);
            Assert.AreEqual(30, settings.RetainedLogCount);
            Assert.IsFalse(settings.DisableBrowserShortcuts);
            Assert.IsFalse(settings.DisableGpuAcceleration);
            Assert.IsNull(settings.CustomEmoticon);
            Assert.IsFalse(settings.DisableBrowserContextMenu);
        }

        [TestMethod]
        public void Default_ReturnsNewInstance()
        {
            var s1 = Settings.Default;
            var s2 = Settings.Default;
            Assert.AreNotSame(s1, s2);
        }

        [TestMethod]
        public void SetNullPropertiesToDefault_FillsNullLanguage()
        {
            var settings = new Settings();
            settings.Language = null;

            settings.SetNullPropertiesToDefault();

            Assert.AreEqual("zh-CN", settings.Language);
        }

        [TestMethod]
        public void SetNullPropertiesToDefault_PreservesNonNullValues()
        {
            var settings = new Settings();
            settings.Language = "en-US";

            settings.SetNullPropertiesToDefault();

            Assert.AreEqual("en-US", settings.Language);
        }

        [TestMethod]
        public void SetNullPropertiesToDefault_FillsNullProxySettings()
        {
            var settings = new Settings();
            settings.ProxySettings = null;

            settings.SetNullPropertiesToDefault();

            Assert.IsNotNull(settings.ProxySettings);
        }
    }
}
