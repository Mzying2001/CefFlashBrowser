using CefFlashBrowser.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mzying2001.MonkeySharp.Core.Repository;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Tests
{
    [TestClass]
    public class UserScriptItemViewModelTests
    {
        [DataTestMethod]
        [DataRow("// @icon https://example.com/small.png\n// @icon64 https://example.com/large.png", "https://example.com/small.png")]
        [DataRow("// @icon64 https://example.com/large.png", "https://example.com/large.png")]
        [DataRow("// @icon   \n// @icon64 https://example.com/large.png", "https://example.com/large.png")]
        [DataRow("// @icon data:image/png;base64,AA==\n// @icon64 https://example.com/large.png", "data:image/png;base64,AA==")]
        [DataRow("// @icon64 DATA:image/svg+xml,%3Csvg%2F%3E", "DATA:image/svg+xml,%3Csvg%2F%3E")]
        [DataRow("// @icon https://example.com/small.png\n// @icon64 data:image/png;base64,AA==", "https://example.com/small.png")]
        [DataRow("", null)]
        [DataRow("// @icon https://[invalid", null)]
        [DataRow("// @icon javascript:alert(1)", null)]
        public void DraftUsesDeclaredIcon(string metadata, string expected)
        {
            var item = new UserScriptItemViewModel(Source("test", metadata), null, "test", true);
            Assert.AreEqual(expected, item.IconUrl);
        }

        [DataTestMethod]
        [DataRow("icons/icon.png", "https://example.com/scripts/game.user.js", "https://example.com/scripts/icons/icon.png")]
        [DataRow("/icon.png", "https://example.com/scripts/game.user.js", "https://example.com/icon.png")]
        [DataRow("//cdn.example.com/icon.png", "https://example.com/game.user.js", "https://cdn.example.com/icon.png")]
        [DataRow("../icon.png", "C:\\scripts\\games\\game.user.js", "file:///C:/scripts/icon.png")]
        [DataRow("icon.png", "file:///C:/scripts/game.user.js", "file:///C:/scripts/icon.png")]
        [DataRow("file:///C:/scripts/icon.png", null, "file:///C:/scripts/icon.png")]
        [DataRow("icon.png", null, null)]
        [DataRow("icon.png", "relative.user.js", null)]
        public void RelativeIconUsesScriptOrigin(string icon, string origin, string expected)
        {
            var item = new UserScriptItemViewModel(Source("test", "// @icon " + icon), origin, "test", true);
            Assert.AreEqual(expected, item.IconUrl);
        }

        [DataTestMethod]
        [DataRow("  脚本名称  ", "脚")]
        [DataRow("lowercase", "l")]
        [DataRow("\uD83D\uDE80 launch", "\uD83D\uDE80")]
        [DataRow("e\u0301clair", "e\u0301")]
        [DataRow("  ", "?")]
        [DataRow(null, "?")]
        public void DefaultIconUsesFirstTextElement(string name, string expected)
        {
            var item = new UserScriptItemViewModel(Source("test"), null, name, false);
            Assert.AreEqual(expected, item.IconInitial);
        }

        [TestMethod]
        public void EditingDraftKeepsInitialIconUntilSaved()
        {
            var item = new UserScriptItemViewModel(Source("original", "// @icon original.png"),
                "https://example.com/script.user.js", "original", true);

            item.Source = Source("changed", "// @icon changed.png");

            Assert.AreEqual("https://example.com/original.png", item.IconUrl);
            Assert.AreEqual("o", item.IconInitial);
        }

        [TestMethod]
        public async Task SaveAndRepositorySynchronizationRefreshIcons()
        {
            var repository = new UserScriptRepository(new InMemoryUserScriptPersistence());
            await repository.InitializeAsync(CancellationToken.None);
            await repository.InstallAsync(Source("original", "// @icon original.png"),
                "https://example.com/script.user.js", true, CancellationToken.None);
            var manager = new UserScriptManagerViewModel(() => Task.FromResult<IUserScriptRepository>(repository),
                key => key, action => action());

            try
            {
                await manager.OpenAsync();
                var item = manager.SelectedScript;
                var changes = new List<string>();
                item.PropertyChanged += (sender, e) => changes.Add(e.PropertyName);
                item.Source = Source("修改后", "// @icon changed.png");
                Assert.AreEqual("https://example.com/original.png", item.IconUrl);
                Assert.AreEqual("o", item.IconInitial);
                Assert.IsFalse(changes.Contains(nameof(item.IconUrl)));
                Assert.IsFalse(changes.Contains(nameof(item.IconInitial)));

                await manager.SaveAsync();
                Assert.IsFalse(item.IsDirty, manager.Status);
                Assert.AreEqual("https://example.com/changed.png", item.IconUrl);
                Assert.AreEqual("修", item.IconInitial);
                CollectionAssert.IsSubsetOf(new[] { nameof(item.IconUrl), nameof(item.IconInitial) }, changes);

                await repository.UpdateAsync(item.Installation.ScriptKey, Source("Remote", "// @icon64 /remote.png"),
                    item.Origin, CancellationToken.None);
                Assert.AreSame(item, manager.Scripts.Single());
                Assert.AreEqual("https://example.com/remote.png", item.IconUrl);
                Assert.AreEqual("R", item.IconInitial);

                item.Source = "invalid metadata";
                await manager.SaveAsync();
                Assert.IsTrue(item.IsDirty);
                Assert.AreEqual("https://example.com/remote.png", item.IconUrl);
                Assert.AreEqual("R", item.IconInitial);
            }
            finally
            {
                manager.Close();
            }
        }

        private static string Source(string name, string iconMetadata = "")
            => "// ==UserScript==\n// @name " + name + "\n// @match https://example.com/*\n// @grant none\n"
                + iconMetadata + "\n// ==/UserScript==\n";
    }
}
