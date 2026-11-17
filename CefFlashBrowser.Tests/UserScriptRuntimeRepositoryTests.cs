using CefFlashBrowser.Services.UserScripts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mzying2001.MonkeySharp.Core.Domain;
using Mzying2001.MonkeySharp.Core.Repository;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Tests
{
    [TestClass]
    public class UserScriptRuntimeRepositoryTests
    {
        private const string Source = "// ==UserScript==\n// @name Runtime world\n// @match https://example.com/*\n// @grant GM.getValue\n// ==/UserScript==";

        [TestMethod]
        public async Task HostView_ExcludesUntrustedWorlds_WithoutRemovingInstallations()
        {
            var repository = new UserScriptRepository(new InMemoryUserScriptPersistence());

            await repository.InitializeAsync(CancellationToken.None);

            var raw = await repository.InstallAsync(Source, null, true, CancellationToken.None);
            var isolated = await repository.InstallAsync(Source.Replace("// @grant", "// @sandbox DOM\n// @grant"), null, true, CancellationToken.None);

            using (var view = new UserScriptRuntimeRepository(repository, false))
            {
                var visible = await view.GetSnapshotAsync(CancellationToken.None);

                Assert.AreEqual(1, visible.Count);
                Assert.AreEqual(isolated.ScriptKey, visible[0].ScriptKey);

                await Assert.ThrowsExceptionAsync<KeyNotFoundException>(() => view.GetAsync(raw.ScriptKey, CancellationToken.None));

                Assert.AreEqual(2, (await repository.GetSnapshotAsync(CancellationToken.None)).Count);
            }

            using (var trusted = new UserScriptRuntimeRepository(repository, true))
                Assert.AreEqual(2, (await trusted.GetSnapshotAsync(CancellationToken.None)).Count);
        }

        [TestMethod]
        public async Task WorldChange_PublishesRemovalToRevokePreviouslyRegisteredRules()
        {
            var repository = new UserScriptRepository(new InMemoryUserScriptPersistence());

            await repository.InitializeAsync(CancellationToken.None);

            var installation = await repository.InstallAsync(Source.Replace("// @grant", "// @sandbox DOM\n// @grant"), null, true, CancellationToken.None);

            using (var view = new UserScriptRuntimeRepository(repository, false))
            {
                UserScriptRepositoryChangedEventArgs changed = null;

                view.Changed += (sender, e) => changed = e;
                await repository.UpdateAsync(installation.ScriptKey, Source, null, CancellationToken.None);

                Assert.AreEqual(RepositoryChangeKind.Removed, changed.Kind);
                Assert.AreEqual(installation.ScriptKey, changed.Installation.ScriptKey);
                Assert.AreEqual(0, (await view.GetSnapshotAsync(CancellationToken.None)).Count);
                Assert.IsNotNull(await repository.GetAsync(installation.ScriptKey, CancellationToken.None));
            }
        }

        [TestMethod]
        public async Task UnavailableReload_RevokesHostViewAndConditionalRepairRestoresIt()
        {
            var persistence = new InMemoryUserScriptPersistence();
            var repository = new UserScriptRepository(persistence);

            await repository.InitializeAsync(CancellationToken.None);

            var installed = await repository.InstallAsync(Source.Replace("// @grant", "// @sandbox DOM\n// @grant"), null, true, CancellationToken.None);

            using (var view = new UserScriptRuntimeRepository(repository, false))
            {
                UserScriptRepositoryChangedEventArgs changed = null;

                view.Changed += (sender, e) => changed = e;

                var loaded = await persistence.LoadAsync(installed.ScriptKey, CancellationToken.None);

                await persistence.SaveAsync(new UserScriptSaveRequest(new UserScriptPersistenceRecord(installed.ScriptKey,
                    "invalid metadata", null, true, installed.InstalledAt, installed.UpdatedAt, loaded.Record.Assets),
                    loaded.SourceVersion, SourceWriteMode.Replace), CancellationToken.None);

                var unavailable = await view.ReloadAsync(installed.ScriptKey, CancellationToken.None);

                Assert.IsFalse(unavailable.CanExecute);
                Assert.AreEqual(RepositoryChangeKind.Removed, changed.Kind);
                Assert.AreEqual(0, (await view.GetSnapshotAsync(CancellationToken.None)).Count);
                Assert.IsNotNull(await repository.GetAsync(installed.ScriptKey, CancellationToken.None));

                var repaired = await view.UpdateIfUnchangedAsync(installed.ScriptKey, unavailable.RevisionId,
                    installed.Definition.Source, null, CancellationToken.None);

                Assert.IsTrue(repaired.CanExecute);
                Assert.AreEqual(RepositoryChangeKind.Updated, changed.Kind);
                Assert.IsTrue((await view.RefreshAssetsAsync(installed.ScriptKey, repaired.RevisionId, CancellationToken.None)).CanExecute);

                await view.SetEnabledAsync(installed.ScriptKey, false, CancellationToken.None);

                Assert.AreEqual(RepositoryChangeKind.Removed, changed.Kind);
                Assert.AreEqual(0, (await view.GetSnapshotAsync(CancellationToken.None)).Count);
            }
        }
    }
}
