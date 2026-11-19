using CefFlashBrowser.Database;
using CefFlashBrowser.Services;
using CefFlashBrowser.Services.UserScripts;
using CefFlashBrowser.Services.UserScripts.Persistence;
using CefFlashBrowser.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mzying2001.MonkeySharp.Core.Permissions;
using Mzying2001.MonkeySharp.Core.Runtime;
using Mzying2001.MonkeySharp.Core.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CefFlashBrowser.Tests
{
    [TestClass]
    public class UserScriptServiceTests
    {
        private const string Source = "// ==UserScript==\n// @name Service\n// @match https://example.com/*\n// @grant none\n// ==/UserScript==";

        private string _root;
        private SqliteDatabase _database;
        private readonly List<UserScriptService> _services = new List<UserScriptService>();

        [TestInitialize]
        public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "CefFlashBrowserScriptService_" + Guid.NewGuid().ToString("N"));
            _database = new SqliteDatabase(Path.Combine(_root, "browser.db"));
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            foreach (var service in _services)
                await service.StopAsync();

            _database.Dispose();

            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }

        private UserScriptService Create(HttpMessageHandler handler = null)
        {
            var storage = new UserScriptStorage(new DatabaseService(_database));
            var service = new UserScriptService(storage, Path.Combine(_root, "MonkeySharp"), Path.Combine(_root, "Cache", "UserScripts"),
                new UserScriptContentService(handler: handler ?? new ContentHandler()), new RejectUserScriptPermissionPrompt());

            _services.Add(service);
            return service;
        }

        [TestMethod]
        public async Task ManagerOrder_PreservesDraftsAndSurvivesServiceRestart()
        {
            var service = Create();
            await service.InitializeAsync();
            var token = CancellationToken.None;
            var repository = service.Repository;
            var first = await repository.InstallAsync(Source, null, true, token);
            var second = await repository.InstallAsync(Source, null, false, token);
            var third = await repository.InstallAsync(Source, null, true, token);
            var manager = new UserScriptManagerViewModel(() => Task.FromResult(repository), key => key, action => action());
            await manager.OpenAsync();

            var firstItem = manager.Scripts[0];
            var secondItem = manager.Scripts[1];
            var thirdItem = manager.Scripts[2];
            manager.NewScript();
            var earlierDraft = manager.SelectedScript;
            manager.NewScript();
            var laterDraft = manager.SelectedScript;
            Assert.IsFalse(((ICommand)manager.MoveUpCommand).CanExecute(null));
            manager.SelectedScript = firstItem;
            Assert.IsFalse(((ICommand)manager.MoveUpCommand).CanExecute(null));
            Assert.IsFalse(((ICommand)manager.MoveToFirstCommand).CanExecute(null));
            manager.SelectedScript = thirdItem;
            Assert.IsFalse(((ICommand)manager.MoveDownCommand).CanExecute(null));
            Assert.IsFalse(((ICommand)manager.MoveToLastCommand).CanExecute(null));
            manager.SelectedScript = secondItem;
            Assert.IsTrue(((ICommand)manager.MoveUpCommand).CanExecute(null));
            secondItem.Source += "\n// unsaved";

            async Task AssertMoveAsync(UserScriptMove move, params UserScriptItemViewModel[] installed)
            {
                await manager.MoveAsync(secondItem, move);
                CollectionAssert.AreEqual(installed.Concat(new[] { earlierDraft, laterDraft }).ToArray(), manager.Scripts.ToArray());
                CollectionAssert.AreEqual(installed.Select(item => item.Installation.ScriptKey).ToArray(),
                    (await repository.GetSnapshotAsync(token)).Select(item => item.ScriptKey).ToArray());
                Assert.AreSame(secondItem, manager.SelectedScript);
                Assert.IsTrue(secondItem.IsDirty);
            }

            await AssertMoveAsync(UserScriptMove.Up, secondItem, firstItem, thirdItem);
            await AssertMoveAsync(UserScriptMove.Down, firstItem, secondItem, thirdItem);
            await AssertMoveAsync(UserScriptMove.Last, firstItem, thirdItem, secondItem);
            await AssertMoveAsync(UserScriptMove.First, secondItem, firstItem, thirdItem);
            Assert.AreSame(second, await repository.GetAsync(second.ScriptKey, token));
            Assert.IsFalse(secondItem.IsEnabled);

            manager.SelectedScript = laterDraft;
            await manager.SaveAsync();
            CollectionAssert.AreEqual(new[] { secondItem, firstItem, thirdItem, laterDraft, earlierDraft }, manager.Scripts.ToArray());
            manager.SelectedScript = earlierDraft;
            await manager.SaveAsync();
            CollectionAssert.AreEqual(new[] { secondItem, firstItem, thirdItem, laterDraft, earlierDraft }, manager.Scripts.ToArray());
            await repository.UpdateAsync(second.ScriptKey, Source + "\n// updated", null, token);
            await repository.SetEnabledAsync(second.ScriptKey, true, token);
            await repository.RemoveAsync(third.ScriptKey, token);

            manager.Close();
            await manager.OpenAsync();
            CollectionAssert.AreEqual(new[] { secondItem, firstItem, laterDraft, earlierDraft }, manager.Scripts.ToArray());
            Assert.IsTrue(secondItem.IsDirty);
            manager.Close();
            await service.StopAsync();

            var restarted = Create();
            await restarted.InitializeAsync();
            CollectionAssert.AreEqual(new[] { second.ScriptKey, first.ScriptKey, laterDraft.Installation.ScriptKey, earlierDraft.Installation.ScriptKey },
                (await restarted.Repository.GetSnapshotAsync(token)).Select(item => item.ScriptKey).ToArray());
        }

        [TestMethod]
        public async Task ManagerOrder_FailedWriteRollsBackDatabaseAndKeepsDisplayedOrder()
        {
            var service = Create();
            await service.InitializeAsync();
            var token = CancellationToken.None;
            var repository = service.Repository;
            var first = await repository.InstallAsync(Source, null, true, token);
            var second = await repository.InstallAsync(Source, null, true, token);
            var manager = new UserScriptManagerViewModel(() => Task.FromResult(repository), key => key, action => action());
            await manager.OpenAsync();
            var firstItem = manager.Scripts[0];
            var secondItem = manager.Scripts[1];
            await _database.ExecuteAsync("CREATE TRIGGER reject_script_order BEFORE UPDATE OF sort_order ON monkeysharp_scripts " +
                "WHEN NEW.sort_order = 1 BEGIN SELECT RAISE(ABORT, 'sort test failure'); END");

            await manager.MoveAsync(secondItem, UserScriptMove.First);

            StringAssert.Contains(manager.Status, "sort test failure");
            CollectionAssert.AreEqual(new[] { firstItem, secondItem }, manager.Scripts.ToArray());
            CollectionAssert.AreEqual(new[] { first.ScriptKey, second.ScriptKey },
                (await repository.GetSnapshotAsync(token)).Select(item => item.ScriptKey).ToArray());
            Assert.AreEqual(0L, await _database.ScalarAsync("SELECT COUNT(*) FROM monkeysharp_scripts WHERE sort_order IS NOT NULL"));

            await _database.ExecuteAsync("DROP TRIGGER reject_script_order");
            await manager.MoveAsync(secondItem, UserScriptMove.First);
            CollectionAssert.AreEqual(new[] { secondItem, firstItem }, manager.Scripts.ToArray());
            manager.Close();
        }

        [TestMethod]
        public async Task UnusedConstructionAndStop_DoNotOpenDatabase()
        {
            var service = Create();

            Assert.IsFalse(File.Exists(Path.Combine(_root, "browser.db")));

            await service.StopAsync();

            Assert.IsFalse(File.Exists(Path.Combine(_root, "browser.db")));
            Assert.ThrowsException<ObjectDisposedException>(() => service.InitializeAsync());
        }

        [TestMethod]
        public async Task ConcurrentInitialization_IsShared_AndStopWaitsForEverySession()
        {
            var service = Create();
            var first = service.InitializeAsync();

            Assert.AreSame(first, service.InitializeAsync());

            await first;

            Assert.AreSame(first, service.InitializeAsync());

            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            using (service.RegisterSession("stable-tab", () =>
            {
                entered.TrySetResult(true);
                return release.Task;
            }))
            {
                var stop = service.StopAsync();

                Assert.AreSame(stop, service.StopAsync());

                await entered.Task;

                Assert.IsFalse(stop.IsCompleted);

                release.TrySetResult(true);
                await stop;
            }
        }

        [TestMethod]
        public async Task Stop_CancelsCacheRecoveryBeforeTheCacheDirectoryIsMoved()
        {
            var first = Create(new ContentHandler { Available = true });

            await first.InitializeAsync();

            var source = Source.Replace("// @grant none", "// @grant none\n// @require https://example.com/dependency.js");
            var installed = await first.Repository.InstallAsync(source, null, true, CancellationToken.None);

            await first.StopAsync();

            var cache = Path.Combine(_root, "Cache", "UserScripts");

            Directory.Delete(cache, true);

            var blocked = new CancellationHandler();
            var restored = Create(blocked);
            var initialization = restored.InitializeAsync();

            await blocked.Entered.Task;
            await restored.StopAsync();

            try
            {
                await initialization;

                Assert.Fail("Stopped initialization must be cancelled.");
            }
            catch (OperationCanceledException) { }

            Directory.CreateDirectory(cache);
            Directory.Move(cache, cache + ".cleared");

            Assert.IsFalse(Directory.Exists(cache));
            Assert.IsTrue(File.Exists(Path.Combine(_root, "MonkeySharp", installed.ScriptKey.ToString(), "userscript.js")));
            Assert.AreEqual(1L, await _database.ScalarAsync("SELECT COUNT(*) FROM monkeysharp_scripts"));
        }

        private sealed class CancellationHandler : HttpMessageHandler
        {
            internal readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            {
                Entered.TrySetResult(true);
                await Task.Delay(Timeout.Infinite, token);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        }

        [TestMethod]
        public async Task UnavailableSource_IsIsolatedAndReloadPreservesIdentityAndValues()
        {
            var first = Create();

            await first.InitializeAsync();

            var installed = await first.Repository.InstallAsync(Source, null, true, CancellationToken.None);
            var other = await first.Repository.InstallAsync(Source, null, true, CancellationToken.None);

            await first.ValueStore.SetAsync(installed.ScriptKey.ToString(), "saved", UserScriptValue.Undefined, CancellationToken.None);
            await first.StopAsync();

            var path = Path.Combine(_root, "MonkeySharp", installed.ScriptKey.ToString(), "userscript.js");

            File.WriteAllBytes(path, new byte[] { 255 });

            var restored = Create();

            await restored.InitializeAsync();

            Assert.IsNull(restored.InitializationError);
            Assert.IsFalse((await restored.Repository.GetAsync(installed.ScriptKey, CancellationToken.None)).CanExecute);
            Assert.IsTrue((await restored.Repository.GetAsync(other.ScriptKey, CancellationToken.None)).CanExecute);

            File.WriteAllText(path, Source + "\n// reviewed", new UTF8Encoding(false));

            var reloaded = await restored.Repository.ReloadAsync(installed.ScriptKey, CancellationToken.None);

            Assert.AreEqual(installed.InstalledAt.ToUnixTimeMilliseconds(), reloaded.InstalledAt.ToUnixTimeMilliseconds());
            Assert.IsTrue(reloaded.CanExecute);
            Assert.IsTrue((await restored.ValueStore.GetAsync(installed.ScriptKey.ToString(), "saved", CancellationToken.None)).Exists);
        }

        [TestMethod]
        public async Task DatabaseFailure_RemainsGlobalAndInitializationCanRetry()
        {
            await _database.ExecuteAsync("CREATE TABLE monkeysharp_resource_names (value INTEGER)");

            var service = Create();

            await Assert.ThrowsExceptionAsync<System.Data.SQLite.SQLiteException>(() => service.InitializeAsync());
            Assert.IsNotNull(service.InitializationError);
            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                var ignored = service.Repository;
            });

            Assert.AreEqual(0L, await _database.ScalarAsync("PRAGMA user_version"));
            Assert.AreEqual(0L, await _database.ScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE name = 'website_icons'"));

            await _database.ExecuteAsync("DROP TABLE monkeysharp_resource_names");
            await service.InitializeAsync();

            Assert.IsNull(service.InitializationError);
            Assert.AreEqual(0, (await service.Repository.GetSnapshotAsync(CancellationToken.None)).Count);
        }

        [TestMethod]
        public async Task Permissions_KeepExactKindAndOrigin_AndRejectLateWritesAfterUninstall()
        {
            var service = Create();

            await service.InitializeAsync();

            var installed = await service.Repository.InstallAsync(Source, null, true, CancellationToken.None);
            var scope = new UserScriptPermissionScope(installed.ScriptKey.ToString(), "GM.cookie", "https://example.com");

            await service.Decisions.SetAsync(scope, PermissionDecision.Allow, CancellationToken.None);

            Assert.AreEqual("integer", await _database.ScalarAsync("SELECT typeof(updated_at) FROM monkeysharp_permission_decisions"));
            Assert.AreEqual(PermissionDecision.Allow, await service.Decisions.GetAsync(scope, CancellationToken.None));
            Assert.IsNull(await service.Decisions.GetAsync(new UserScriptPermissionScope(scope.ScriptKey, "GM.xmlHttpRequest", scope.Target), CancellationToken.None));
            Assert.IsNull(await service.Decisions.GetAsync(new UserScriptPermissionScope(scope.ScriptKey, scope.Kind, "https://other.example.com"), CancellationToken.None));

            await service.Decisions.RevokeAsync(scope.ScriptKey, CancellationToken.None);

            Assert.IsNull(await service.Decisions.GetAsync(scope, CancellationToken.None));

            await service.Repository.RemoveAsync(installed.ScriptKey, CancellationToken.None);
            await Assert.ThrowsExceptionAsync<KeyNotFoundException>(() => service.Decisions.SetAsync(scope, PermissionDecision.Allow, CancellationToken.None));
        }

        [TestMethod]
        public async Task PagePermissions_MatchScriptsAndIncludeAllTheirTargets()
        {
            var service = Create();
            var token = CancellationToken.None;
            await service.InitializeAsync();

            var source = Source.Replace("// @grant none", "// @grant GM.xmlHttpRequest\n// @connect *")
                .Replace("https://example.com/*", "https://example.com/play/*");
            var current = await service.Repository.InstallAsync(source, null, true, token);
            var disabled = await service.Repository.InstallAsync(source.Replace("@name Service", "@name Disabled")
                .Replace("// @grant", "// @exclude-match https://example.com/play/private/*\n// @grant"), null, false, token);
            var manual = await service.Repository.InstallAsync(source.Replace("@name Service", "@name Manual")
                .Replace("// @match https://example.com/play/*", "// @run-at context-menu\n// @exclude-match https://example.com/play/private/*"),
                null, true, token);
            var other = await service.Repository.InstallAsync(source.Replace("@name Service", "@name Other")
                .Replace("https://example.com/play/*", "https://example.com/other/*"), null, true, token);

            foreach (var script in new[] { current, disabled, manual, other })
            {
                await service.Decisions.SetAsync(new UserScriptPermissionScope(script.ScriptKey.ToString(),
                    "GM.xmlHttpRequest", "https://cdn.example.net:8443"), PermissionDecision.Allow, token);
            }

            await service.Decisions.SetAsync(new UserScriptPermissionScope(current.ScriptKey.ToString(),
                "GM.xmlHttpRequest", "https://example.com"), PermissionDecision.Deny, token);

            var rows = await service.GetPagePermissionsAsync(new Uri("https://example.com/play/index.html"), token);
            CollectionAssert.AreEqual(new[] { "Disabled", "Manual", "Service", "Service" }, rows.Select(row => row.ScriptName).ToArray());
            Assert.IsTrue(rows[0].IsScriptDisabled);
            Assert.AreEqual("https://cdn.example.net:8443", rows[0].Record.Scope.Target);
            Assert.AreEqual(PermissionDecision.Deny, rows[3].Record.Decision);

            var excluded = await service.GetPagePermissionsAsync(new Uri("https://example.com/play/private/index.html"), token);
            Assert.AreEqual(2, excluded.Count);
            Assert.IsTrue(excluded.All(row => row.ScriptName == "Service"));
            Assert.AreEqual(0, (await service.GetPagePermissionsAsync(null, token)).Count);
        }

        [TestMethod]
        public async Task PagePermissions_ChangeOnlyTheSelectedRecordAndRejectStaleEdits()
        {
            var service = Create();
            var token = CancellationToken.None;
            var page = new Uri("https://example.com/");
            await service.InitializeAsync();

            var installed = await service.Repository.InstallAsync(Source, null, true, token);
            var scope = new UserScriptPermissionScope(installed.ScriptKey.ToString(), "GM.cookie", "https://example.com");
            var otherTarget = new UserScriptPermissionScope(scope.ScriptKey, scope.Kind, "https://other.example.com");
            var otherApi = new UserScriptPermissionScope(scope.ScriptKey, "GM.download", scope.Target);
            foreach (var item in new[] { scope, otherTarget, otherApi })
                await service.Decisions.SetAsync(item, PermissionDecision.Allow, token);

            var original = (await service.GetPagePermissionsAsync(page, token))
                .Single(row => row.Record.Scope.Kind == scope.Kind && row.Record.Scope.Target == scope.Target).Record;
            Assert.IsTrue(await service.Decisions.ChangeAsync(original, PermissionDecision.Deny, token));
            Assert.AreEqual(PermissionDecision.Deny, await service.Decisions.GetAsync(scope, token));
            Assert.IsFalse(await service.Decisions.ChangeAsync(original, null, token));

            var denied = (await service.GetPagePermissionsAsync(page, token)).Single(row => row.Record.Decision == PermissionDecision.Deny).Record;
            Assert.IsTrue(await service.Decisions.ChangeAsync(denied, PermissionDecision.Allow, token));
            Assert.IsFalse(await service.Decisions.ChangeAsync(original, null, token));

            var allowed = (await service.GetPagePermissionsAsync(page, token))
                .Single(row => row.Record.Scope.Kind == scope.Kind && row.Record.Scope.Target == scope.Target).Record;
            Assert.IsTrue(await service.Decisions.ChangeAsync(allowed, null, token));
            Assert.IsNull(await service.Decisions.GetAsync(scope, token));
            Assert.IsFalse(await service.Decisions.ChangeAsync(allowed, PermissionDecision.Deny, token));
            Assert.AreEqual(PermissionDecision.Allow, await service.Decisions.GetAsync(otherTarget, token));
            Assert.AreEqual(PermissionDecision.Allow, await service.Decisions.GetAsync(otherApi, token));
        }

        [TestMethod]
        public async Task StaticRequests_RespectPageWorldTrustAndInternalPagePolicy()
        {
            var service = Create();

            await service.InitializeAsync();

            var installed = await service.Repository.InstallAsync(Source.Replace("// @grant none", "// @grant GM.webRequest"), null, true, CancellationToken.None);
            var frame = new DocumentFrame("browser", "document", "frame", new Uri("https://example.com/"), true, TimingGuarantee.BestEffortDocumentStart);
            var request = new ApiAuthorizationRequest(installed, frame, "GM.webRequest", "https://example.com/a", "static @webRequest", new[] { "GM.webRequest" }, false);
            var trusted = false;
            var policy = new UserScriptExecutionPolicy(new AllowDeclaredPermissionsPolicy(), () => trusted);

            Assert.AreEqual(PermissionDecision.Deny, await policy.AuthorizeAsync(request, CancellationToken.None));

            trusted = true;

            Assert.AreEqual(PermissionDecision.Allow, await policy.AuthorizeAsync(request, CancellationToken.None));

            policy = new UserScriptExecutionPolicy(new AllowDeclaredPermissionsPolicy(), () => trusted, url => false);

            Assert.AreEqual(PermissionDecision.Deny, await policy.AuthorizeAsync(request, CancellationToken.None));
        }

        [TestMethod]
        public async Task Startup_ClearsTabStateLeftByAPreviousProcess()
        {
            var first = Create();

            await first.InitializeAsync();

            var installation = await first.Repository.InstallAsync(Source, null, true, CancellationToken.None);

            await first.CreateTabStateService("previous-process-tab").SaveAsync(installation.ScriptKey, null, "{}", CancellationToken.None);
            await first.StopAsync();

            var restored = Create();

            await restored.InitializeAsync();

            Assert.AreEqual(0, (await restored.CreateTabStateService("current-tab").GetAllAsync(installation.ScriptKey, CancellationToken.None)).Count);
        }

        [TestMethod]
        public async Task SharedPermissionBatch_OneCancelledWaiterDoesNotCancelTheOther()
        {
            var service = Create();

            await service.InitializeAsync();

            var source = Source.Replace("// @grant none", "// @sandbox DOM\n// @grant GM.xmlHttpRequest\n// @connect example.com");
            var installed = await service.Repository.InstallAsync(source, null, true, CancellationToken.None);
            var frame = new DocumentFrame("browser", "document", "frame", new Uri("https://example.com/"), true, TimingGuarantee.BestEffortDocumentStart);
            var request = new ApiAuthorizationRequest(installed, frame, "GM.xmlHttpRequest", "https://example.com/path?secret=value", "request", new[] { "GM.xmlHttpRequest" });
            var prompt = new SharedPrompt();

            using (var policy = new InteractiveUserScriptPermissionPolicy(new SensitiveUserScriptPermissionsPolicy(), service.Decisions, prompt))
            using (var cancelled = new CancellationTokenSource())
            {
                var first = policy.AuthorizeAsync(request, cancelled.Token);
                var second = policy.AuthorizeAsync(request, CancellationToken.None);

                await prompt.Entered.Task;
                cancelled.Cancel();

                try
                {
                    await first;
                    Assert.Fail("The first waiter must cancel.");
                }
                catch (OperationCanceledException) { }

                Assert.IsFalse(prompt.Token.IsCancellationRequested);

                prompt.Choice.TrySetResult(new UserScriptPermissionChoice(PermissionDecision.Allow, true));

                Assert.AreEqual(PermissionDecision.Allow, await second);
                Assert.AreEqual(1, prompt.Count);
                Assert.AreEqual("https://example.com", UserScriptPermissionScope.From(request).Target);
                Assert.AreEqual(PermissionDecision.Allow, await service.Decisions.GetAsync(UserScriptPermissionScope.From(request), CancellationToken.None));
            }
        }

        private sealed class SharedPrompt : IUserScriptPermissionPrompt
        {
            public readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public readonly TaskCompletionSource<UserScriptPermissionChoice> Choice = new TaskCompletionSource<UserScriptPermissionChoice>(TaskCreationOptions.RunContinuationsAsynchronously);
            public CancellationToken Token;
            public int Count;

            public Task<UserScriptPermissionChoice> PromptAsync(ApiAuthorizationRequest request, UserScriptPermissionScope scope, CancellationToken token)
            {
                Token = token;
                Count++;
                Entered.TrySetResult(true);
                return Choice.Task;
            }
        }

        private sealed class ContentHandler : HttpMessageHandler
        {
            internal bool Available;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
                => Task.FromResult(new HttpResponseMessage(Available ? HttpStatusCode.OK : HttpStatusCode.NotFound)
                {
                    Content = new StringContent(Available ? "window.dependency = true;" : "missing")
                });
        }
    }

    [TestClass]
    public class UserScriptContentTests
    {
        private sealed class Handler : HttpMessageHandler
        {
            public readonly List<Uri> Requests = new List<Uri>();
            public Func<HttpRequestMessage, HttpResponseMessage> Respond;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            {
                Requests.Add(request.RequestUri);

                Assert.IsFalse(request.Headers.Contains("Cookie"));
                Assert.IsNull(request.Headers.Authorization);
                return Task.FromResult(Respond(request));
            }
        }

        [TestMethod]
        public async Task Redirects_RemoveFragments_AndKeepOriginalResourceBytes()
        {
            var handler = new Handler();

            handler.Respond = request =>
            {
                if (handler.Requests.Count == 1)
                {
                    var response = new HttpResponseMessage(HttpStatusCode.Redirect);
                    response.Headers.Location = new Uri("https://other.example.com/resource#digest");
                    return response;
                }

                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 255, 0, 1 }) };
            };

            using (var content = new UserScriptContentService(handler: handler))
            {
                var result = await content.FetchAsync("https://example.com/start#sha256=abc", CancellationToken.None);

                CollectionAssert.AreEqual(new byte[] { 255, 0, 1 }, result.Bytes);
                Assert.IsTrue(handler.Requests.All(uri => uri.Fragment == ""));
            }
        }

        [DataTestMethod]
        [DataRow("file:///C:/secret")]
        [DataRow("https://user:password@example.com/")]
        public async Task InvalidRedirect_IsRejectedBeforeTheNextRequest(string location)
        {
            var handler = new Handler
            {
                Respond = request =>
                {
                    var response = new HttpResponseMessage(HttpStatusCode.Redirect);
                    response.Headers.Location = new Uri(location);
                    return response;
                }
            };

            using (var content = new UserScriptContentService(handler: handler))
                await Assert.ThrowsExceptionAsync<ArgumentException>(() => content.FetchSourceAsync(null, "https://example.com/", CancellationToken.None));

            Assert.AreEqual(1, handler.Requests.Count);
        }

        [TestMethod]
        public async Task ActualBytes_AreLimitedEvenWhenContentLengthLies()
        {
            var handler = new Handler
            {
                Respond = request =>
                {
                    var body = new ByteArrayContent(new byte[12]);
                    body.Headers.ContentLength = 1;
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = body };
                }
            };

            using (var content = new UserScriptContentService(handler: handler, maximumBytes: 4))
                await Assert.ThrowsExceptionAsync<IOException>(() => content.FetchSourceAsync(null, "https://example.com/", CancellationToken.None));
        }

        [TestMethod]
        public async Task SourceUtf8_IsStrict_AndRedirectLoopsAreBounded()
        {
            var handler = new Handler
            {
                Respond = request => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(new byte[] { 255 })
                }
            };

            using (var content = new UserScriptContentService(handler: handler))
                await Assert.ThrowsExceptionAsync<DecoderFallbackException>(() => content.FetchSourceAsync(null, "https://example.com/", CancellationToken.None));

            handler = new Handler
            {
                Respond = request =>
                {
                    var response = new HttpResponseMessage(HttpStatusCode.Redirect);
                    response.Headers.Location = new Uri("/again", UriKind.Relative);
                    return response;
                }
            };

            using (var content = new UserScriptContentService(handler: handler))
                await Assert.ThrowsExceptionAsync<HttpRequestException>(() => content.FetchSourceAsync(null, "https://example.com/", CancellationToken.None));

            Assert.AreEqual(11, handler.Requests.Count);
        }
    }
}
