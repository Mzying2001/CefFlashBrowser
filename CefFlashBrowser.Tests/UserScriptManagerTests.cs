using CefFlashBrowser.Services.UserScripts;
using CefFlashBrowser.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mzying2001.MonkeySharp.Core.Domain;
using Mzying2001.MonkeySharp.Core.Repository;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

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
            => new UserScriptManagerViewModel(() => Task.FromResult(repository),
                key => key == "userScriptManager_confirmDelete" ? "Delete \"{0}\"?" : key, action => action());

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
            Assert.AreEqual("userScriptManager_conflict", manager.Status);
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
            manager.ConfirmDeletion = _ => true;
            await manager.DeleteAsync();
            Assert.AreEqual(0, (await repository.GetSnapshotAsync(CancellationToken.None)).Count);
            Assert.AreEqual(1, manager.Scripts.Count);
            manager.Close();
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task DeleteRequiresExplicitConfirmationAndPreservesUnsavedSource(bool installed)
        {
            var repository = await RepositoryAsync();
            var manager = Manager(repository);
            await manager.OpenAsync();

            try
            {
                manager.NewScript();
                if (installed) await manager.SaveAsync();
                var item = manager.SelectedScript;
                var savedSource = item.Source;
                item.Source += "\n// unsaved";
                var source = item.Source;

                await manager.DeleteAsync();
                Assert.AreSame(item, manager.SelectedScript);
                Assert.AreEqual(1, manager.Scripts.Count);
                Assert.AreEqual(source, item.Source);
                Assert.AreEqual(installed ? 1 : 0, (await repository.GetSnapshotAsync(CancellationToken.None)).Count);

                var confirmations = 0;
                manager.ConfirmDeletion = message =>
                {
                    confirmations++;
                    Assert.AreEqual("Delete \"" + item.Name + "\"?", message);
                    Assert.IsTrue(manager.IsBusy);
                    Assert.IsFalse(((ICommand)manager.DeleteCommand).CanExecute(null));
                    Assert.IsTrue(manager.DeleteAsync().IsCompleted);
                    return false;
                };

                await manager.DeleteAsync();
                Assert.AreEqual(1, confirmations);
                Assert.AreSame(item, manager.SelectedScript);
                Assert.AreEqual(1, manager.Scripts.Count);
                Assert.AreEqual(source, item.Source);
                Assert.IsTrue(item.IsDirty);
                Assert.IsFalse(manager.IsBusy);
                Assert.AreEqual("userScriptManager_cancelled", manager.Status);
                Assert.AreEqual(installed ? 1 : 0, (await repository.GetSnapshotAsync(CancellationToken.None)).Count);
                if (installed)
                    Assert.AreEqual(savedSource, (await repository.GetAsync(item.Installation.ScriptKey, CancellationToken.None)).Definition.Source);
            }
            finally
            {
                manager.Close();
            }
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ConfirmedDeleteRemovesOnlyReviewedScript(bool installed)
        {
            var repository = await RepositoryAsync();
            var manager = Manager(repository);
            await manager.OpenAsync();

            try
            {
                manager.NewScript();
                if (installed) await manager.SaveAsync();
                var item = manager.SelectedScript;
                item.Source += "\n// discard after confirmation";
                manager.NewScript();
                var other = manager.SelectedScript;
                manager.SelectedScript = item;

                var confirmations = 0;
                manager.ConfirmDeletion = message =>
                {
                    confirmations++;
                    Assert.AreEqual("Delete \"" + item.Name + "\"?", message);
                    manager.SelectedScript = other;
                    return true;
                };

                await manager.DeleteAsync();
                Assert.AreEqual(1, confirmations);
                Assert.AreSame(other, manager.Scripts.Single());
                Assert.AreSame(other, manager.SelectedScript);
                Assert.AreEqual(0, (await repository.GetSnapshotAsync(CancellationToken.None)).Count);
                Assert.AreEqual("userScriptManager_deleted", manager.Status);

                manager.ConfirmDeletion = _ => true;
                await manager.DeleteAsync();
                Assert.AreEqual(0, manager.Scripts.Count);
                Assert.IsNull(manager.SelectedScript);
                Assert.IsFalse(((ICommand)manager.DeleteCommand).CanExecute(null));
            }
            finally
            {
                manager.Close();
            }
        }

        [TestMethod]
        public async Task ExternalScriptRequiresConfirmationOnlyForFirstSave()
        {
            var repository = await RepositoryAsync();
            var source = Template();
            const string origin = "https://example.com/game.user.js";
            var model = new UserScriptManagerViewModel(() => Task.FromResult<IUserScriptRepository>(repository), key => key,
                action => action(), (url, token) => Task.FromResult(source));
            await model.OpenAsync();
            await model.ImportUrlAsync(origin);
            Assert.AreEqual(0, (await repository.GetSnapshotAsync(CancellationToken.None)).Count);
            var reviewedSource = source.Replace("@name test", "@name reviewed\n// @icon icons/edited.png");
            model.SelectedScript.Source = reviewedSource;

            var confirmations = 0;
            UserScriptMetadata reviewedMetadata = null;
            string reviewedOrigin = null;
            bool? updateRequested = null;

            model.ConfirmInstallation = (metadata, sourceOrigin, update) =>
            {
                confirmations++;
                reviewedMetadata = metadata;
                reviewedOrigin = sourceOrigin;
                updateRequested = update;
                return confirmations == 2;
            };

            await model.SaveAsync();
            Assert.AreEqual(1, confirmations);
            Assert.AreEqual("reviewed", reviewedMetadata?.Name);
            Assert.AreEqual("icons/edited.png", reviewedMetadata?.IconUrl);
            Assert.AreEqual(origin, reviewedOrigin);
            Assert.AreEqual(false, updateRequested);
            Assert.IsTrue(model.SelectedScript.IsDirty);
            Assert.IsFalse(model.SelectedScript.IsInstalled);
            Assert.AreEqual(reviewedSource, model.SelectedScript.Source);
            Assert.AreEqual(0, (await repository.GetSnapshotAsync(CancellationToken.None)).Count);

            await model.SaveAsync();
            Assert.AreEqual(reviewedSource, model.SelectedScript.Installation?.Definition.Source);
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

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ImportFromSameSourceUpdatesExistingInstallation(bool localFile)
        {
            var repository = await RepositoryAsync();
            var source = Template();
            var model = new UserScriptManagerViewModel(() => Task.FromResult<IUserScriptRepository>(repository), key => key,
                action => action(), (_, __) => Task.FromResult(source));
            var path = localFile ? Path.GetTempFileName() : null;
            const string url = "https://example.com/scripts/game?channel=stable";

            async Task ImportAsync(bool secondImport)
            {
                if (localFile)
                {
                    File.WriteAllText(path, source);
                    var importPath = secondImport ? Path.Combine(Path.GetDirectoryName(path), ".", Path.GetFileName(path)) : path;
                    await model.ImportFileAsync(importPath);
                }
                else
                {
                    await model.ImportUrlAsync(url + (secondImport ? "#second" : "#first"));
                }
            }

            try
            {
                await model.OpenAsync();
                var confirmations = new List<bool>();
                var confirm = true;
                string reviewedName = null;
                model.ConfirmInstallation = (metadata, _, update) =>
                {
                    confirmations.Add(update);
                    reviewedName = metadata.Name;
                    return confirm;
                };

                await ImportAsync(false);
                await model.SaveAsync();
                var original = model.SelectedScript;
                Assert.IsTrue(original.IsInstalled, model.Status);
                var key = original.Installation.ScriptKey;
                var installedAt = original.Installation.InstalledAt;
                await model.ToggleAsync(original);
                var unsaved = original.Source + "\n// local edits";
                original.Source = unsaved;
                var originalSource = source;

                source += "\n// new version";
                await ImportAsync(true);
                var draft = model.SelectedScript;
                var reviewed = source.Replace("@name test", "@name reviewed");
                draft.Source = reviewed;
                CollectionAssert.AreEqual(new[] { false }, confirmations);

                confirm = false;
                await model.SaveAsync();
                Assert.AreEqual("userScriptManager_cancelled", model.Status);
                Assert.AreSame(draft, model.SelectedScript);
                Assert.IsFalse(draft.IsInstalled);
                Assert.AreEqual(reviewed, draft.Source);
                Assert.AreEqual(originalSource, (await repository.GetAsync(key, CancellationToken.None)).Definition.Source);

                confirm = true;
                await model.SaveAsync();
                CollectionAssert.AreEqual(new[] { false, true, true }, confirmations);
                Assert.AreEqual("reviewed", reviewedName);
                var installed = (await repository.GetSnapshotAsync(CancellationToken.None)).Single();
                Assert.AreEqual(key, installed.ScriptKey);
                Assert.AreEqual(installedAt, installed.InstalledAt);
                Assert.AreEqual(reviewed, installed.Definition.Source);
                Assert.IsFalse(installed.IsEnabled);
                Assert.AreSame(model.SelectedScript, model.Scripts.Single(item => item.IsInstalled));
                Assert.IsFalse(model.SelectedScript.IsDirty);
                Assert.AreEqual(unsaved, model.Scripts.Single(item => !item.IsInstalled).Source);
            }
            finally
            {
                model.Close();
                if (path != null) File.Delete(path);
            }
        }

        [TestMethod]
        public async Task SameNameFromDifferentUrlsCreatesSeparateInstallations()
        {
            var repository = await RepositoryAsync();
            var model = new UserScriptManagerViewModel(() => Task.FromResult<IUserScriptRepository>(repository), key => key,
                action => action(), (url, token) => Task.FromResult(Template()));
            await model.OpenAsync();
            model.ConfirmInstallation = (_, __, update) => !update;

            await model.ImportUrlAsync("https://example.com/same.user.js?channel=stable");
            await model.SaveAsync();
            await model.ImportUrlAsync("https://example.com/same.user.js?channel=beta");
            await model.SaveAsync();

            Assert.AreEqual(2, (await repository.GetSnapshotAsync(CancellationToken.None)).Count);
            model.Close();
        }

        [TestMethod]
        public async Task ImportWithHistoricalDuplicatesRetainsDraftWithoutConfirming()
        {
            var repository = await RepositoryAsync();
            var token = CancellationToken.None;
            const string url = "https://example.com/duplicate.user.js";
            var original = Template();
            var updated = original + "\n// update";
            await repository.InstallAsync(original, url, true, token);
            await repository.InstallAsync(original, url + "#old-import", true, token);
            var model = new UserScriptManagerViewModel(() => Task.FromResult<IUserScriptRepository>(repository), key => key,
                action => action(), (_, __) => Task.FromResult(updated));
            await model.OpenAsync();
            var confirmations = 0;
            model.ConfirmInstallation = (_, __, ___) =>
            {
                confirmations++;
                return true;
            };

            await model.ImportUrlAsync(url);
            await model.SaveAsync();

            Assert.AreEqual(0, confirmations);
            Assert.AreEqual("userScriptInstall_conflict", model.Status);
            Assert.IsFalse(model.SelectedScript.IsInstalled);
            Assert.AreEqual(updated, model.SelectedScript.Source);
            var snapshot = await repository.GetSnapshotAsync(token);
            Assert.AreEqual(2, snapshot.Count);
            Assert.IsTrue(snapshot.All(item => item.Definition.Source == original));
            model.Close();
        }

        [DataTestMethod]
        [DataRow("installed")]
        [DataRow("updated")]
        [DataRow("removed")]
        public async Task ImportRejectsStaleConfirmation(string change)
        {
            var repository = await RepositoryAsync();
            var token = CancellationToken.None;
            const string url = "https://example.com/concurrent.user.js";
            var source = Template();
            var concurrentSource = source + "\n// concurrent change";
            var draftSource = source + "\n// imported update";
            var original = change == "installed" ? null : await repository.InstallAsync(source, url, true, token);
            var model = new UserScriptManagerViewModel(() => Task.FromResult<IUserScriptRepository>(repository), key => key,
                action => action(), (_, __) => Task.FromResult(draftSource));
            await model.OpenAsync();
            bool? updateRequested = null;
            model.ConfirmInstallation = (_, __, update) =>
            {
                updateRequested = update;
                switch (change)
                {
                    case "installed":
                        repository.InstallAsync(concurrentSource, url, true, token).GetAwaiter().GetResult();
                        break;
                    case "updated":
                        repository.UpdateAsync(original.ScriptKey, concurrentSource, url, token).GetAwaiter().GetResult();
                        break;
                    case "removed":
                        repository.RemoveAsync(original.ScriptKey, token).GetAwaiter().GetResult();
                        break;
                }
                return true;
            };

            await model.ImportUrlAsync(url);
            await model.SaveAsync();

            Assert.AreEqual(original != null, updateRequested);
            Assert.AreEqual("userScriptInstall_conflict", model.Status);
            Assert.IsFalse(model.SelectedScript.IsInstalled);
            Assert.AreEqual(draftSource, model.SelectedScript.Source);
            var saved = await repository.GetSnapshotAsync(token);
            if (change == "removed")
                Assert.AreEqual(0, saved.Count);
            else
                Assert.AreEqual(concurrentSource, saved.Single().Definition.Source);
            model.Close();
        }

        [TestMethod]
        public void OversizeSourceIsRejected()
        {
            Assert.ThrowsException<IOException>(() => UserScriptSourceText.Validate(new string('a', UserScriptSourceText.MaximumBytes + 1)));
        }

        [TestMethod]
        public async Task ExternalRemovalWhileClosedKeepsUnsavedText()
        {
            var repository = await RepositoryAsync();
            var model = Manager(repository);
            await model.OpenAsync();
            model.NewScript();
            await model.SaveAsync();
            var item = model.SelectedScript;
            item.Source += "\n// retained after removal";
            model.Close();
            await repository.RemoveAsync(item.Installation.ScriptKey, CancellationToken.None);
            await model.OpenAsync();
            Assert.AreEqual(1, model.Scripts.Count);
            Assert.IsFalse(model.SelectedScript.IsInstalled);
            Assert.IsTrue(model.SelectedScript.Source.EndsWith("// retained after removal"));
            model.Close();
        }

        [TestMethod]
        public async Task CloseDuringInitializationCanReopenWithoutLosingDrafts()
        {
            var repository = await RepositoryAsync();
            var gate = new TaskCompletionSource<IUserScriptRepository>();
            var model = new UserScriptManagerViewModel(() => gate.Task, key => key, action => action());
            var opening = model.OpenAsync();
            model.Close();
            var reopening = model.OpenAsync();
            gate.SetResult(repository);
            await opening;
            await reopening;
            model.NewScript();
            await model.SaveAsync();
            Assert.IsTrue(model.SelectedScript.IsInstalled);
            Assert.IsFalse(model.IsBusy);
            model.Close();
        }

        [TestMethod]
        public async Task InvalidUrlAndDownloadFailureKeepExistingDraft()
        {
            var repository = await RepositoryAsync();
            var model = new UserScriptManagerViewModel(() => Task.FromResult<IUserScriptRepository>(repository), key => key,
                action => action(), (url, token) => { throw new IOException("offline"); });
            await model.OpenAsync();
            model.NewScript();
            var item = model.SelectedScript;
            item.Source += "\n// keep me";
            await model.ImportUrlAsync("file:///script.user.js");
            Assert.AreEqual("userScriptManager_invalidUrl", model.Status);
            await model.ImportUrlAsync("https://example.com/script.user.js");
            Assert.AreEqual("offline", model.Status);
            Assert.AreSame(item, model.SelectedScript);
            Assert.IsTrue(item.Source.EndsWith("// keep me"));
            Assert.AreEqual(1, model.Scripts.Count);
            model.Close();
        }

        private static string Template()
            => "// ==UserScript==\n// @name test\n// @match https://example.com/*\n// @grant none\n// ==/UserScript==\n";
    }
}
