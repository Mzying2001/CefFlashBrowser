using CefFlashBrowser.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mzying2001.MonkeySharp.Core.Repository;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CefFlashBrowser.Tests
{
    public partial class UserScriptPersistenceTests
    {
        [TestMethod]
        public async Task ManagerSaveAndToggleSurviveRepositoryReinitialization()
        {
            var manager = new UserScriptManagerViewModel(() => Task.FromResult<IUserScriptRepository>(_repository), text => text, action => action());
            await manager.OpenAsync();
            manager.NewScript();
            manager.SelectedScript.Source = Source;
            await manager.SaveAsync();
            var item = manager.SelectedScript;
            var key = item.Installation.ScriptKey;
            await manager.ToggleAsync(item);
            manager.Close();

            var restored = await OpenAsync();
            var reopened = new UserScriptManagerViewModel(() => Task.FromResult<IUserScriptRepository>(restored), text => text, action => action());
            await reopened.OpenAsync();
            var saved = reopened.Scripts.Single();
            Assert.AreEqual(key, saved.Installation.ScriptKey);
            Assert.AreEqual(Source, saved.Source);
            Assert.IsFalse(saved.IsEnabled);
            Assert.IsFalse(saved.IsDirty);
            reopened.Close();
        }

        [TestMethod]
        public async Task ManagerSourceConflictRetainsDraftAndExternalFile()
        {
            var manager = new UserScriptManagerViewModel(() => Task.FromResult<IUserScriptRepository>(_repository), text => text, action => action());
            await manager.OpenAsync();
            manager.NewScript();
            manager.SelectedScript.Source = Source;
            await manager.SaveAsync();
            var item = manager.SelectedScript;
            var external = Source + "\n// external edit";
            File.WriteAllText(PathFor(item.Installation.ScriptKey), external);
            item.Source += "\n// unsaved editor";
            await manager.SaveAsync();
            Assert.AreEqual("userScriptManager_conflict", manager.Status);
            Assert.IsTrue(item.IsDirty);
            Assert.IsTrue(item.Source.EndsWith("// unsaved editor"));
            Assert.AreEqual(external, File.ReadAllText(PathFor(item.Installation.ScriptKey)));
            manager.Close();
        }
    }
}
