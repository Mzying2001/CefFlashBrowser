using CefFlashBrowser.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
            Assert.AreEqual(NewPageBehavior.NewTab, settings.NewPageBehavior);
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
            Assert.IsFalse(settings.AlwaysShowTabStrip);
            Assert.AreEqual(0d, settings.BrowserZoomLevel);
            Assert.IsFalse(settings.DisableFullscreen);
            Assert.AreEqual(30, settings.RetainedLogCount);
            Assert.IsFalse(settings.DisableBrowserShortcuts);
            Assert.IsFalse(settings.DisableGpuAcceleration);
            Assert.IsNull(settings.CustomEmoticon);
            Assert.IsFalse(settings.DisableBrowserContextMenu);
            Assert.AreEqual(UserScriptPermissionBehavior.Prompt, settings.DefaultUserScriptPermissionBehavior);
        }

        [TestMethod]
        public void Deserialize_LegacySettingsDefaultToPrompt()
        {
            var settings = JsonConvert.DeserializeObject<Settings>("{\"Language\":\"en-US\"}");

            Assert.AreEqual(UserScriptPermissionBehavior.Prompt, settings.DefaultUserScriptPermissionBehavior);
        }

        [DataTestMethod]
        [DataRow("{}")]
        [DataRow("{\"ScriptEditorSettings\":null}")]
        [DataRow("{\"ScriptEditorSettings\":{}}")]
        [DataRow("{\"ScriptEditorSettings\":{\"FontFamily\":null,\"FontSize\":null,\"IndentWithSpaces\":null,\"TabWidth\":null,\"ShowLineNumbers\":null,\"WordWrap\":null}}")]
        public void Deserialize_MissingEditorPreferencesUseDefaults(string json)
        {
            var settings = JsonConvert.DeserializeObject<Settings>(json);
            settings.SetNullPropertiesToDefault();
            var editor = settings.ScriptEditorSettings;

            Assert.AreEqual("Consolas", editor.FontFamily);
            Assert.AreEqual(14, editor.FontSize);
            Assert.IsTrue(editor.IndentWithSpaces);
            Assert.AreEqual(2, editor.TabWidth);
            Assert.IsTrue(editor.ShowLineNumbers);
            Assert.IsFalse(editor.WordWrap);
        }

        [DataTestMethod]
        [DataRow(7, 0)]
        [DataRow(73, 17)]
        public void Deserialize_OutOfRangeEditorValuesUseDefaults(int size, int width)
        {
            var json = JObject.FromObject(Settings.Default);
            json[nameof(Settings.ScriptEditorSettings)][nameof(EditorSettings.FontSize)] = size;
            json[nameof(Settings.ScriptEditorSettings)][nameof(EditorSettings.TabWidth)] = width;
            var editor = json.ToObject<Settings>().ScriptEditorSettings;

            Assert.AreEqual(14, editor.FontSize);
            Assert.AreEqual(2, editor.TabWidth);
        }

        [TestMethod]
        public void ScriptEditorSettings_RoundTrip()
        {
            var settings = new Settings
            {
                ScriptEditorSettings = new EditorSettings
                {
                    FontFamily = "Courier New",
                    FontSize = 20,
                    IndentWithSpaces = false,
                    TabWidth = 4,
                    ShowLineNumbers = false,
                    WordWrap = true
                }
            };

            var editor = JObject.FromObject(settings).ToObject<Settings>().ScriptEditorSettings;

            Assert.AreEqual("Courier New", editor.FontFamily);
            Assert.AreEqual(20, editor.FontSize);
            Assert.IsFalse(editor.IndentWithSpaces);
            Assert.AreEqual(4, editor.TabWidth);
            Assert.IsFalse(editor.ShowLineNumbers);
            Assert.IsTrue(editor.WordWrap);
        }

        [DataTestMethod]
        [DataRow(UserScriptPermissionBehavior.Prompt, 0)]
        [DataRow(UserScriptPermissionBehavior.Allow, 1)]
        [DataRow(UserScriptPermissionBehavior.Deny, 2)]
        public void UserScriptPermissionBehavior_RoundTripsAsANumber(UserScriptPermissionBehavior behavior, int expected)
        {
            var settings = new Settings { DefaultUserScriptPermissionBehavior = behavior };
            var json = JObject.FromObject(settings);
            var value = json[nameof(Settings.DefaultUserScriptPermissionBehavior)];

            Assert.AreEqual(JTokenType.Integer, value.Type);
            Assert.AreEqual(expected, (int)value);
            Assert.AreEqual(behavior, json.ToObject<Settings>().DefaultUserScriptPermissionBehavior);
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
