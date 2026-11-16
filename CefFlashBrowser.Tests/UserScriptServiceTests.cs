using CefFlashBrowser.Database;
using CefFlashBrowser.Services;
using CefFlashBrowser.Services.UserScripts;
using CefFlashBrowser.Services.UserScripts.Persistence;
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

        private UserScriptService Create()
        {
            var storage = new UserScriptStorage(new DatabaseService(_database));
            var service = new UserScriptService(storage, Path.Combine(_root, "MonkeySharp"),
                new UserScriptContentService(handler: new ContentHandler()), new RejectUserScriptPermissionPrompt());

            _services.Add(service);
            return service;
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
        public async Task CorruptPrepared_MaintenanceRepairPreservesKeyTimeAndValues()
        {
            var first = Create();

            await first.InitializeAsync();

            var installed = await first.Repository.InstallAsync(Source, null, true, CancellationToken.None);

            await first.ValueStore.SetAsync(installed.ScriptKey.ToString(), "saved", UserScriptValue.Undefined, CancellationToken.None);
            await first.StopAsync();

            const string corrupt = "{\"format\":999}";

            await _database.ExecuteAsync("UPDATE monkeysharp_scripts SET prepared_revision_json = @payload", new Dictionary<string, object> { ["@payload"] = corrupt });

            var restored = Create();

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => restored.InitializeAsync());

            Assert.IsNotNull(restored.InitializationError);
            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                var ignored = restored.Repository;
            });
            Assert.AreEqual(Source, (await restored.ReadMaintenanceAsync(CancellationToken.None)).Single().Record.Source);

            await restored.RepairAsync(installed.ScriptKey, Source + "\n// reviewed", CancellationToken.None);

            var repaired = await restored.Repository.GetAsync(installed.ScriptKey, CancellationToken.None);

            Assert.AreEqual(installed.InstalledAt, repaired.InstalledAt);
            Assert.IsTrue(repaired.IsEnabled);
            Assert.IsTrue((await restored.ValueStore.GetAsync(installed.ScriptKey.ToString(), "saved", CancellationToken.None)).Exists);
            Assert.AreNotEqual(corrupt, await _database.ScalarAsync("SELECT prepared_revision_json FROM monkeysharp_scripts"));
        }

        [TestMethod]
        public async Task FailedRepair_DoesNotClearCorruptPayload_AndCanRetry()
        {
            var first = Create();

            await first.InitializeAsync();

            var installed = await first.Repository.InstallAsync(Source, null, true, CancellationToken.None);

            await first.StopAsync();
            await _database.ExecuteAsync("UPDATE monkeysharp_scripts SET prepared_revision_json = '{\"format\":999}'");

            var restored = Create();

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => restored.InitializeAsync());

            var withDependency = Source.Replace("// @grant none", "// @grant none\n// @require https://example.com/missing.js");

            await Assert.ThrowsExceptionAsync<HttpRequestException>(() => restored.RepairAsync(installed.ScriptKey, withDependency, CancellationToken.None));

            Assert.AreEqual("{\"format\":999}", await _database.ScalarAsync("SELECT prepared_revision_json FROM monkeysharp_scripts"));

            await restored.RepairAsync(installed.ScriptKey, Source, CancellationToken.None);

            Assert.IsNotNull(restored.Repository);
        }

        [TestMethod]
        public async Task Permissions_KeepExactKindAndOrigin_AndRejectLateWritesAfterUninstall()
        {
            var service = Create();

            await service.InitializeAsync();

            var installed = await service.Repository.InstallAsync(Source, null, true, CancellationToken.None);
            var scope = new UserScriptPermissionScope(installed.ScriptKey.ToString(), "GM.cookie", "https://example.com");

            await service.Decisions.SetAsync(scope, PermissionDecision.Allow, CancellationToken.None);

            Assert.AreEqual(PermissionDecision.Allow, await service.Decisions.GetAsync(scope, CancellationToken.None));
            Assert.IsNull(await service.Decisions.GetAsync(new UserScriptPermissionScope(scope.ScriptKey, "GM.xmlHttpRequest", scope.Target), CancellationToken.None));
            Assert.IsNull(await service.Decisions.GetAsync(new UserScriptPermissionScope(scope.ScriptKey, scope.Kind, "https://other.example.com"), CancellationToken.None));

            await service.Decisions.RevokeAsync(scope.ScriptKey, CancellationToken.None);

            Assert.IsNull(await service.Decisions.GetAsync(scope, CancellationToken.None));

            await service.Repository.RemoveAsync(installed.ScriptKey, CancellationToken.None);
            await Assert.ThrowsExceptionAsync<KeyNotFoundException>(() => service.Decisions.SetAsync(scope, PermissionDecision.Allow, CancellationToken.None));
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
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
                => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("missing") });
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
                var result = await content.FetchAsync("https://example.com/start#sha256=abc", false, false, CancellationToken.None);

                CollectionAssert.AreEqual(new byte[] { 255, 0, 1 }, result.Bytes);
                Assert.IsNull(result.Text);
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
