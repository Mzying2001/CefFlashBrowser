using CefFlashBrowser.Data;
using CefFlashBrowser.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;
using System.Text.Json.Nodes;

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
            Assert.IsTrue(settings.AllowPageSwfScriptAccess);
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
            Assert.IsTrue(settings.EnableCoreJsPolyfills);
            Assert.AreEqual(UserScriptPermissionBehavior.Prompt, settings.DefaultUserScriptPermissionBehavior);
        }

        [TestMethod]
        public void Deserialize_LegacySettingsAllowPageSwfScriptAccess()
        {
            var settings = SettingsJson.Deserialize("{\"Language\":\"en-US\"}");

            Assert.IsTrue(settings.AllowPageSwfScriptAccess);
        }

        [TestMethod]
        public void AllowPageSwfScriptAccess_RoundTripsWhenDisabled()
        {
            var settings = new Settings { AllowPageSwfScriptAccess = false };
            var json = SettingsJson.Serialize(settings);

            Assert.IsFalse(SettingsJson.Deserialize(json).AllowPageSwfScriptAccess);
        }

        [TestMethod]
        public void Deserialize_LegacySettingsEnableCoreJsPolyfills()
        {
            var settings = SettingsJson.Deserialize("{\"Language\":\"en-US\"}");

            Assert.IsTrue(settings.EnableCoreJsPolyfills);
        }

        [TestMethod]
        public void EnableCoreJsPolyfills_RoundTripsWhenDisabled()
        {
            var settings = new Settings { EnableCoreJsPolyfills = false };
            var json = SettingsJson.Serialize(settings);

            Assert.IsFalse(SettingsJson.Deserialize(json).EnableCoreJsPolyfills);
        }

        [TestMethod]
        public void Deserialize_LegacySettingsDefaultToPrompt()
        {
            var settings = SettingsJson.Deserialize("{\"Language\":\"en-US\"}");

            Assert.AreEqual(UserScriptPermissionBehavior.Prompt, settings.DefaultUserScriptPermissionBehavior);
        }

        [DataTestMethod]
        [DataRow("{}")]
        [DataRow("{\"ScriptEditorSettings\":null}")]
        [DataRow("{\"ScriptEditorSettings\":{}}")]
        [DataRow("{\"ScriptEditorSettings\":{\"FontFamily\":null,\"FontSize\":null,\"IndentWithSpaces\":null,\"TabWidth\":null,\"ShowLineNumbers\":null,\"WordWrap\":null}}")]
        public void Deserialize_MissingEditorPreferencesUseDefaults(string json)
        {
            var settings = SettingsJson.Deserialize(json);
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
            var json = JsonNode.Parse(SettingsJson.Serialize(Settings.Default));
            json[nameof(Settings.ScriptEditorSettings)][nameof(EditorSettings.FontSize)] = size;
            json[nameof(Settings.ScriptEditorSettings)][nameof(EditorSettings.TabWidth)] = width;
            var editor = SettingsJson.Deserialize(json.ToJsonString()).ScriptEditorSettings;

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

            var editor = SettingsJson.Deserialize(SettingsJson.Serialize(settings)).ScriptEditorSettings;

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
            var json = SettingsJson.Serialize(settings);
            var value = JsonNode.Parse(json)[nameof(Settings.DefaultUserScriptPermissionBehavior)];

            Assert.AreEqual(JsonValueKind.Number, value.GetValueKind());
            Assert.AreEqual(expected, value.GetValue<int>());
            Assert.AreEqual(behavior, SettingsJson.Deserialize(json).DefaultUserScriptPermissionBehavior);
        }

        [TestMethod]
        public void Merge_PreservesUnknownFields_AndClearsOldValuesOnReload()
        {
            const string existing = @"{
                ""language"": ""en-US"",
                ""DownloadDirectory"": ""older"",
                ""downloadDirectory"": ""old"",
                ""UserScriptDownloadDirectory"": ""legacy"",
                ""future"": [17, null, { ""name"": ""value"" }],
                ""scriptEditorSettings"": { ""fontFamily"": ""Other"", ""future"": { ""number"": 42 } }
            }";
            var settings = SettingsJson.Deserialize(existing);
            Assert.AreEqual("old", settings.DownloadDirectory);
            settings.Language = "zh-TW";
            settings.DownloadDirectory = null;
            settings.ScriptEditorSettings.FontFamily = null;

            var merged = SettingsJson.Merge(existing, settings);
            var json = JsonNode.Parse(merged).AsObject();
            Assert.IsFalse(json.ContainsKey("language"));
            Assert.IsFalse(json.ContainsKey("downloadDirectory"));
            Assert.IsTrue(json.ContainsKey("DownloadDirectory"));
            Assert.IsNull(json["DownloadDirectory"]);
            Assert.IsTrue(JsonNode.DeepEquals(JsonNode.Parse(existing)["future"], json["future"]));
            Assert.AreEqual(42, json["ScriptEditorSettings"]["future"]["number"].GetValue<int>());

            var restored = SettingsJson.Deserialize(merged);
            Assert.AreEqual("zh-TW", restored.Language);
            Assert.IsNull(restored.DownloadDirectory);
            Assert.AreEqual("legacy", restored.UserScriptDownloadDirectory);
            Assert.AreEqual("Consolas", restored.ScriptEditorSettings.FontFamily);
        }

        [TestMethod]
        public void Deserialize_LegacyDirectoryMigratesOnlyWhenNewKeyIsMissing()
        {
            Assert.AreEqual("legacy", SettingsJson.Deserialize("{\"UserScriptDownloadDirectory\":\"legacy\"}").DownloadDirectory);
            Assert.IsNull(SettingsJson.Deserialize("{\"UserScriptDownloadDirectory\":\"legacy\",\"downloadDirectory\":null}").DownloadDirectory);
        }

        [TestMethod]
        public void LegacyWindowCoordinatesAndNamedEnums_SurviveMergeAndReload()
        {
            const string existing = @"{
                // Legacy settings
                ""Theme"": ""Dark"",
                ""MainWindowSizeInfo"": { ""Left"": ""NaN"", ""Top"": 125, },
            }";
            var settings = SettingsJson.Deserialize(existing);
            Assert.AreEqual(Theme.Dark, settings.Theme);
            Assert.IsTrue(double.IsNaN(settings.MainWindowSizeInfo.Left));

            var merged = SettingsJson.Merge(existing, settings);
            var json = JsonNode.Parse(merged);
            Assert.AreEqual(1, json["Theme"].GetValue<int>());
            Assert.AreEqual("NaN", json["MainWindowSizeInfo"]["Left"].GetValue<string>());
            Assert.AreEqual(125d, SettingsJson.Deserialize(merged).MainWindowSizeInfo.Top);
        }

        [DataTestMethod]
        [DataRow("null")]
        [DataRow("[]")]
        public void NonObjectSettingsAreRejected(string json)
        {
            Assert.ThrowsException<JsonException>(() => SettingsJson.Deserialize(json));
            Assert.ThrowsException<JsonException>(() => SettingsJson.Merge(json, Settings.Default));
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
