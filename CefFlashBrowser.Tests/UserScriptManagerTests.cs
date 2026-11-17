using CefFlashBrowser.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mzying2001.MonkeySharp.Core.Repository;
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
    }
}
