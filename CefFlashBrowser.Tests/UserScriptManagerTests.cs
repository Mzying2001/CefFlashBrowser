using CefFlashBrowser.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mzying2001.MonkeySharp.Core.Repository;
using System;
using System.IO;
using System.Text;
using CefFlashBrowser.Services.UserScripts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Tests
{
    [TestClass]
    public class UserScriptManagerTests
    {
        private static async Task<UserScriptRepository> RepositoryAsync()
        {
            var repository = new UserScriptRepository(new InMemoryUserScriptPersistence());
            await repository.InitializeAsync(CancellationToken.None);
            return repository;
        }

        private static UserScriptManagerViewModel Manager(IUserScriptRepository repository)
            => new UserScriptManagerViewModel(() => Task.FromResult(repository), key => key, action => action());

        [TestMethod]
        public async Task NewSaveToggleAndEditKeepInstallationIdentity()
        {
            var repository = await RepositoryAsync();
            var manager = Manager(repository);
            await manager.OpenAsync();
            manager.NewScript();
            var draft = manager.SelectedScript;
            Assert.IsFalse(draft.IsInstalled);
            await manager.SaveAsync();
            Assert.IsTrue(draft.IsInstalled, manager.Status);
            Assert.IsFalse(draft.IsDirty);
            var key = draft.Installation.ScriptKey;
            draft.Source += "\n// edited";
            await manager.ToggleAsync(draft);
            Assert.IsFalse(draft.IsEnabled);
            Assert.IsTrue(draft.IsDirty);
            await manager.SaveAsync();
            Assert.IsFalse(draft.IsDirty, manager.Status);
            Assert.AreEqual(key, draft.Installation.ScriptKey);
            Assert.IsFalse(draft.IsEnabled);
            Assert.AreEqual(1, manager.Scripts.Count);
            manager.Close();
        }

        [TestMethod]
        public async Task SwitchingAndReopeningKeepSessionDrafts()
        {
            var repository = await RepositoryAsync();
            var manager = Manager(repository);
            await manager.OpenAsync();
            manager.NewScript();
            await manager.SaveAsync();
            var installed = manager.SelectedScript;
            installed.Source += "\n// unsaved";
            manager.NewScript();
            var draft = manager.SelectedScript;
            manager.Close();
            await manager.OpenAsync();
            Assert.AreSame(draft, manager.SelectedScript);
            Assert.IsTrue(installed.Source.EndsWith("// unsaved"));
            Assert.IsTrue(installed.IsDirty);
            Assert.AreEqual(2, manager.Scripts.Count);
            manager.Close();
        }

        [TestMethod]
        public async Task ConcurrentUpdateDoesNotOverwriteDirtyDraft()
        {
            var repository = await RepositoryAsync();
            var manager = Manager(repository);
            await manager.OpenAsync();
            manager.NewScript();
            await manager.SaveAsync();
            var item = manager.SelectedScript;
            var original = item.Source;
            item.Source += "\n// local";
            await repository.UpdateAsync(item.Installation.ScriptKey, original + "\n// remote", null, CancellationToken.None);
            await manager.SaveAsync();
            Assert.IsTrue(item.Source.EndsWith("// local"));
            Assert.IsTrue(item.IsDirty);
            Assert.AreEqual("userScripts_conflict", manager.Status);
            var saved = await repository.GetAsync(item.Installation.ScriptKey, CancellationToken.None);
            Assert.IsTrue(saved.Definition.Source.EndsWith("// remote"));
            manager.Close();
        }

        [TestMethod]
        public async Task InvalidSaveRetainsOriginalAndDraft()
        {
            var repository = await RepositoryAsync();
            var manager = Manager(repository);
            await manager.OpenAsync();
            manager.NewScript();
            await manager.SaveAsync();
            var item = manager.SelectedScript;
            var original = item.Source;
            item.Source = "invalid metadata";
            await manager.SaveAsync();
            Assert.IsTrue(item.IsDirty);
            Assert.AreEqual("invalid metadata", item.Source);
            Assert.AreEqual(original, (await repository.GetAsync(item.Installation.ScriptKey, CancellationToken.None)).Definition.Source);
            manager.Close();
        }

        [TestMethod]
        public async Task ReloadKeepsUnsavedTextAsSeparateDraft()
        {
            var repository = await RepositoryAsync();
            var manager = Manager(repository);
            await manager.OpenAsync();
            manager.NewScript();
            await manager.SaveAsync();
            var item = manager.SelectedScript;
            var original = item.Source;
            item.Source += "\n// draft";
            await manager.ReloadAsync();
            Assert.AreEqual(original, item.Source);
            Assert.IsFalse(item.IsDirty);
            Assert.IsTrue(manager.Scripts.Single(value => !value.IsInstalled).Source.EndsWith("// draft"));
            await manager.DeleteAsync();
            Assert.AreEqual(0, (await repository.GetSnapshotAsync(CancellationToken.None)).Count);
            Assert.AreEqual(1, manager.Scripts.Count);
            manager.Close();
        }
        [TestMethod]
        public async Task ExternalScriptRequiresConfirmationOnlyForFirstSave()
        {
            var repository = await RepositoryAsync();
            var source = Template();
            var model = new UserScriptManagerViewModel(() => Task.FromResult<IUserScriptRepository>(repository), key => key,
                action => action(), (url, token) => Task.FromResult(source));
            await model.OpenAsync();
            await model.ImportUrlAsync("https://example.com/game.user.js");
            Assert.AreEqual(0, (await repository.GetSnapshotAsync(CancellationToken.None)).Count);
            var confirmations = 0;
            model.ConfirmInstallation = message => { confirmations++; return false; };
            await model.SaveAsync();
            Assert.AreEqual(1, confirmations);
            Assert.IsTrue(model.SelectedScript.IsDirty);
            Assert.IsFalse(model.SelectedScript.IsInstalled);
            model.ConfirmInstallation = message => { confirmations++; return true; };
            await model.SaveAsync();
            model.SelectedScript.Source += "\n// edit";
            await model.SaveAsync();
            await model.ToggleAsync(model.SelectedScript);
            Assert.AreEqual(2, confirmations);
            Assert.AreEqual(1, model.Scripts.Count);
            Assert.IsFalse(model.SelectedScript.IsDirty);
            model.Close();
        }

        [TestMethod]
        public async Task ToggleAdvancesRevisionWithoutDiscardingEdits()
        {
            var repository = await RepositoryAsync();
            var model = Manager(repository);
            await model.OpenAsync();
            model.NewScript();
            await model.SaveAsync();
            var item = model.SelectedScript;
            item.Source += "\n// edited";
            await model.ToggleAsync(item);
            await model.SaveAsync();
            Assert.IsFalse(item.IsEnabled);
            Assert.IsFalse(item.IsDirty);
            Assert.IsTrue((await repository.GetAsync(item.Installation.ScriptKey, CancellationToken.None)).Definition.Source.EndsWith("// edited"));
            model.Close();
        }

        [TestMethod]
        public async Task DownloadDoesNotOverwriteEditsAndClosingCancelsImport()
        {
            var repository = await RepositoryAsync();
            var download = new TaskCompletionSource<string>();
            var model = new UserScriptManagerViewModel(() => Task.FromResult<IUserScriptRepository>(repository), key => key,
                action => action(), (url, token) => download.Task);
            await model.OpenAsync();
            model.NewScript();
            var original = model.SelectedScript;
            var pending = model.ImportUrlAsync("https://example.com/script.user.js");
            original.Source += "\n// typing";
            model.Close();
            download.SetResult(Template());
            await pending;
            await model.OpenAsync();
            Assert.AreEqual(1, model.Scripts.Count);
            Assert.IsTrue(original.Source.EndsWith("// typing"));
            model.Close();
        }

        [TestMethod]
        public async Task FileImportAndExportUseCurrentDraftAndStrictUtf8()
        {
            var repository = await RepositoryAsync();
            var model = Manager(repository);
            var path = Path.GetTempFileName();
            var output = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, Template(), new UTF8Encoding(true));
                await model.OpenAsync();
                await model.ImportFileAsync(path);
                Assert.IsTrue(model.SelectedScript.RequiresConfirmation);
                Assert.IsFalse(model.SelectedScript.Source.StartsWith("\uFEFF", StringComparison.Ordinal));
                model.SelectedScript.Source += "\n// export draft";
                await model.ExportAsync(output);
                Assert.AreEqual(model.SelectedScript.Source, File.ReadAllText(output));
                Assert.AreEqual(0, (await repository.GetSnapshotAsync(CancellationToken.None)).Count);
                File.WriteAllBytes(path, new byte[] { 0xff, 0xfe });
                await model.ImportFileAsync(path);
                Assert.AreEqual(1, model.Scripts.Count);
            }
            finally
            {
                model.Close();
                File.Delete(path);
                File.Delete(output);
            }
        }

        [TestMethod]
        public async Task SameNameImportsCreateDistinctInstallations()
        {
            var repository = await RepositoryAsync();
            var model = new UserScriptManagerViewModel(() => Task.FromResult<IUserScriptRepository>(repository), key => key,
                action => action(), (url, token) => Task.FromResult(Template()));
            await model.OpenAsync();
            model.ConfirmInstallation = _ => true;
            for (var index = 0; index < 2; index++)
            {
                await model.ImportUrlAsync("https://example.com/same.user.js");
                await model.SaveAsync();
            }
            Assert.AreEqual(2, model.Scripts.Count);
            Assert.AreEqual(2, model.Scripts.Select(item => item.Installation.ScriptKey).Distinct().Count());
            model.Close();
        }

        [TestMethod]
        public void OversizeSourceIsRejected()
        {
            Assert.ThrowsException<IOException>(() => UserScriptSourceText.Validate(new string('a', UserScriptSourceText.MaximumBytes + 1)));
        }

        private static string Template()
            => "// ==UserScript==\n// @name test\n// @match https://example.com/*\n// @grant none\n// ==/UserScript==\n";


    }
}
