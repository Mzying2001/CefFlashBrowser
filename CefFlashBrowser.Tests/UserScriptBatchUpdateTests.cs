using CefFlashBrowser.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mzying2001.MonkeySharp.Core.Domain;
using Mzying2001.MonkeySharp.Core.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CefFlashBrowser.Tests
{
    [TestClass]
    public class UserScriptBatchUpdateTests
    {
        [TestMethod]
        public async Task BatchUpdateRequiresAnInstalledScriptAndTracksRepositoryChanges()
        {
            var repository = await RepositoryAsync();
            var manager = await ManagerAsync(repository, (_, __) => Task.FromResult(string.Empty));
            var available = new List<bool>();
            manager.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(manager.CanOpenBatchUpdate)) available.Add(manager.CanOpenBatchUpdate);
            };

            Assert.IsFalse(manager.CanOpenBatchUpdate);
            manager.NewScript();
            Assert.IsNull(manager.CreateBatchUpdateViewModel());

            var installation = await InstallAsync(repository, "Disabled script", enabled: false);
            Assert.IsTrue(manager.CanOpenBatchUpdate);

            await repository.RemoveAsync(installation.ScriptKey, CancellationToken.None);
            Assert.IsFalse(manager.CanOpenBatchUpdate);
            Assert.IsNull(manager.CreateBatchUpdateViewModel());
            CollectionAssert.AreEqual(new[] { false, true, false }, available);
            manager.Close();
        }

        [TestMethod]
        public async Task CheckAndUpdateAllUseTheVisibleRowsAndKeepHiddenResults()
        {
            var repository = await RepositoryAsync();
            await InstallAsync(repository, "Target first", enabled: false);
            await InstallAsync(repository, "Other");
            await InstallAsync(repository, "Target current", version: "3.0");
            await InstallAsync(repository, "Target local", updateSetting: "");
            await InstallAsync(repository, "Target disabled", updateSetting: "// @downloadURL none");
            var requests = new List<string>();
            var manager = await ManagerAsync(repository, (url, _) =>
            {
                requests.Add(url);
                var name = Uri.UnescapeDataString(new Uri(url).AbsolutePath.Substring(1));
                return Task.FromResult(Source(name, "2.0"));
            });
            manager.NewScript();

            using (var batch = manager.CreateBatchUpdateViewModel())
            {
                Assert.AreEqual(5, batch.RowsView.Count);
                Assert.IsFalse(((ICommand)batch.UpdateAllCommand).CanExecute(null));
                var other = batch.RowsView.Cast<UserScriptBatchUpdateRow>().Single(row => row.Name == "Other");
                await batch.CheckRowAsync(other);

                batch.FilterText = " TARGET  ";
                Assert.AreEqual(4, batch.RowsView.Count);
                await batch.CheckAllAsync();
                CollectionAssert.AreEqual(new[] { Url("Other"), Url("Target first"), Url("Target current") }, requests);
                var rows = batch.RowsView.Cast<UserScriptBatchUpdateRow>().ToDictionary(row => row.Name);
                Assert.IsTrue(((ICommand)batch.UpdateRowCommand).CanExecute(rows["Target first"]));
                Assert.IsFalse(((ICommand)batch.UpdateRowCommand).CanExecute(rows["Target current"]));
                var current = rows["Target current"];
                Assert.AreEqual("2.0", current.LatestVersion,
                    $"Status: {current.Status} | Details: {current.StatusDetail} | Result: {current.Check?.Status}");
                Assert.AreEqual("userScriptBatchUpdate_notConfigured", rows["Target local"].Status);
                Assert.AreEqual("userScriptBatchUpdate_disabled", rows["Target disabled"].Status);

                var confirmations = new List<IReadOnlyList<UserScriptUpdateConfirmationEntry>>();
                batch.ConfirmAll = (entries, _) => { confirmations.Add(entries); return true; };
                await batch.UpdateAllAsync();
                var entry = confirmations.Single().Single();
                Assert.AreEqual("Target first", entry.Name);
                Assert.AreEqual("1.0", entry.CurrentVersion);
                Assert.AreEqual("2.0", entry.NewVersion);
                Assert.IsFalse(entry.HasUnsavedEdits);
                Assert.AreEqual("2.0", rows["Target first"].CurrentVersion);
                Assert.IsFalse(rows["Target first"].Item.IsEnabled);
                Assert.IsFalse(((ICommand)batch.UpdateAllCommand).CanExecute(null));
                Assert.AreEqual("1.0", other.CurrentVersion);
                Assert.AreEqual("2.0", other.LatestVersion);
                batch.FilterText = string.Empty;
                Assert.IsTrue(((ICommand)batch.UpdateAllCommand).CanExecute(null));
            }
            manager.Close();
        }

        [TestMethod]
        public async Task RowAndBatchConfirmationsProtectUnsavedEditsAndFailuresDoNotStopTheBatch()
        {
            var repository = await RepositoryAsync();
            await InstallAsync(repository, "Fails");
            await InstallAsync(repository, "Updates");
            var failOnApply = false;
            var manager = await ManagerAsync(repository, (url, _) =>
            {
                if (url == Url("Fails") && failOnApply) throw new HttpRequestException("network failure");
                var name = url == Url("Fails") ? "Fails" : "Updates";
                return Task.FromResult(Source(name, "2.0"));
            });
            manager.SelectedScript = manager.Scripts[1];
            var edited = manager.SelectedScript;
            edited.Source += "\n// pending edits";

            using (var batch = manager.CreateBatchUpdateViewModel())
            {
                await batch.CheckAllAsync();
                var row = batch.RowsView.Cast<UserScriptBatchUpdateRow>().Last();
                var rowReviews = 0;
                batch.ConfirmRow = (metadata, url, warning, _) =>
                {
                    rowReviews++;
                    Assert.AreEqual("2.0", metadata.Version);
                    Assert.AreEqual(Url("Updates"), url);
                    Assert.AreEqual("userScriptBatchUpdate_overwriteEdits", warning);
                    return false;
                };
                await batch.UpdateRowAsync(row);
                Assert.AreEqual(1, rowReviews);
                Assert.AreEqual("1.0", row.CurrentVersion);
                Assert.IsTrue(edited.IsDirty);

                var batchReviews = 0;
                var accepted = false;
                batch.ConfirmAll = (entries, _) =>
                {
                    batchReviews++;
                    CollectionAssert.AreEqual(new[] { "Fails", "Updates" }, entries.Select(entry => entry.Name).ToArray());
                    Assert.IsFalse(entries[0].HasUnsavedEdits);
                    Assert.IsTrue(entries[1].HasUnsavedEdits);
                    return accepted;
                };
                await batch.UpdateAllAsync();
                Assert.AreEqual("1.0", row.CurrentVersion);
                Assert.IsTrue(edited.IsDirty);

                accepted = true;
                failOnApply = true;
                await batch.UpdateAllAsync();
                Assert.AreEqual(2, batchReviews);
                Assert.AreEqual(1, rowReviews);
                Assert.AreEqual("Updated 1; failed 1", batch.Summary);
                var failed = batch.RowsView.Cast<UserScriptBatchUpdateRow>().First();
                Assert.AreEqual("userScriptBatchUpdate_applyFailed", failed.Status);
                Assert.AreEqual("network failure", failed.StatusDetail);
                Assert.AreEqual("1.0", failed.CurrentVersion);
                Assert.AreEqual("2.0", row.CurrentVersion);
                Assert.AreEqual("2.0", row.LatestVersion);
                Assert.AreSame(edited, manager.SelectedScript);
                Assert.AreEqual(Source("Updates", "2.0"), edited.Source);
                Assert.IsFalse(edited.IsDirty);
            }
            manager.Close();
        }

        [TestMethod]
        public async Task FailedCheckAndAChangedInstallationCanBeCheckedAgain()
        {
            var repository = await RepositoryAsync();
            var installation = await InstallAsync(repository, "Changes");
            var failCheck = true;
            var manager = await ManagerAsync(repository, (_, __) => failCheck
                ? throw new HttpRequestException("unreachable")
                : Task.FromResult(Source("Changes", "2.0")));

            using (var batch = manager.CreateBatchUpdateViewModel())
            {
                var row = batch.RowsView.Cast<UserScriptBatchUpdateRow>().Single();
                await batch.CheckRowAsync(row);
                Assert.AreEqual("userScriptBatchUpdate_checkFailed", row.Status);
                Assert.IsTrue(((ICommand)batch.CheckRowCommand).CanExecute(row));
                Assert.IsFalse(((ICommand)batch.UpdateRowCommand).CanExecute(row));

                failCheck = false;
                await batch.CheckRowAsync(row);
                var replacement = Source("Changes", "1.5");
                batch.ConfirmAll = (_, __) =>
                {
                    repository.UpdateAsync(installation.ScriptKey, replacement, null, CancellationToken.None).GetAwaiter().GetResult();
                    return true;
                };
                await batch.UpdateAllAsync();
                Assert.AreEqual("1.5", row.CurrentVersion);
                Assert.AreEqual("userScriptBatchUpdate_recheck", row.Status);
                Assert.AreEqual("Updated 0; failed 1", batch.Summary);
                Assert.IsFalse(((ICommand)batch.UpdateAllCommand).CanExecute(null));

                await batch.CheckAllAsync();
                Assert.IsTrue(((ICommand)batch.UpdateRowCommand).CanExecute(row));
                batch.ConfirmRow = (metadata, _, warning, __) => metadata.Version == "2.0" && warning == null;
                await batch.UpdateRowAsync(row);
                Assert.AreEqual("2.0", row.CurrentVersion);
                Assert.AreEqual("2.0", row.LatestVersion);
                Assert.IsFalse(row.HasStatus);
                Assert.IsFalse(((ICommand)batch.UpdateRowCommand).CanExecute(row));
            }
            manager.Close();
        }

        [TestMethod]
        public async Task CancellationKeepsCommittedUpdatesAndSkipsTheRest()
        {
            var repository = await RepositoryAsync();
            await InstallAsync(repository, "First");
            await InstallAsync(repository, "Second");
            await InstallAsync(repository, "Third");
            UserScriptBatchUpdateViewModel batch = null;
            var applying = false;
            var requests = new List<string>();
            var manager = await ManagerAsync(repository, (url, token) =>
            {
                if (applying)
                {
                    requests.Add(url);
                    if (url == Url("Second")) batch.Cancel();
                }
                token.ThrowIfCancellationRequested();
                return Task.FromResult(Source(new Uri(url).AbsolutePath.Substring(1), "2.0"));
            });

            using (batch = manager.CreateBatchUpdateViewModel())
            {
                await batch.CheckAllAsync();
                batch.ConfirmAll = (_, __) => { applying = true; return true; };
                await batch.UpdateAllAsync();
                CollectionAssert.AreEqual(new[] { Url("First"), Url("Second") }, requests);
                CollectionAssert.AreEqual(new[] { "2.0", "1.0", "1.0" }, manager.Scripts.Select(item => item.Version).ToArray());
                Assert.IsFalse(batch.IsBusy);
            }
            manager.Close();
        }

        private static async Task<UserScriptRepository> RepositoryAsync()
        {
            var repository = new UserScriptRepository(new InMemoryUserScriptPersistence());
            await repository.InitializeAsync(CancellationToken.None);
            return repository;
        }

        private static async Task<UserScriptManagerViewModel> ManagerAsync(IUserScriptRepository repository,
            Func<string, CancellationToken, Task<string>> fetchSource)
        {
            var manager = new UserScriptManagerViewModel(() => Task.FromResult(repository),
                key => key == "userScriptBatchUpdate_summary" ? "Updated {0}; failed {1}" : key, action => action(), fetchSource);
            await manager.OpenAsync();
            return manager;
        }

        private static Task<UserScriptInstallation> InstallAsync(IUserScriptRepository repository,
            string name, string version = "1.0", bool enabled = true, string updateSetting = null)
            => repository.InstallAsync(Source(name, version, updateSetting), null, enabled, CancellationToken.None);

        private static string Source(string name, string version, string updateSetting = null)
            => $"// ==UserScript==\n// @name {name}\n// @namespace batch-tests\n// @version {version}\n// @match https://example.com/*\n"
                + (updateSetting ?? "// @downloadURL " + Url(name)) + "\n// ==/UserScript==\n";

        private static string Url(string name) => "https://example.com/" + Uri.EscapeDataString(name);
    }
}
