using Mzying2001.MonkeySharp.Core.Domain;
using Mzying2001.MonkeySharp.Core.Permissions;
using Mzying2001.MonkeySharp.Core.Runtime;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Services.UserScripts
{
    // Static metadata rules also pass here; the library's page-world bridge option
    // alone does not prevent static rules belonging to a blocked script.
    public sealed class UserScriptExecutionPolicy : IUserScriptPermissionPolicy
    {
        private readonly IUserScriptPermissionPolicy _inner;
        private readonly Func<bool> _trusted;
        private readonly Func<Uri, bool> _allowedPage;

        public UserScriptExecutionPolicy(IUserScriptPermissionPolicy inner, Func<bool> trusted, Func<Uri, bool> allowedPage = null)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _trusted = trusted ?? throw new ArgumentNullException(nameof(trusted));
            _allowedPage = allowedPage;
        }

        public Task<PermissionDecision> AuthorizeAsync(ApiAuthorizationRequest request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (!IsWorldAvailable(request.Installation.Definition.Metadata, _trusted())
                || _allowedPage != null && !_allowedPage(request.Frame.Url))
                return Task.FromResult(PermissionDecision.Deny);

            return _inner.AuthorizeAsync(request, token);
        }

        public static bool IsWorldAvailable(UserScriptMetadata metadata, bool trusted)
        {
            var world = UserScriptEngine.RequestedWorld(metadata);
            var needsBridge = metadata.GrantDeclarationState == GrantDeclarationState.ExplicitList
                && metadata.Grants.Any(grant => grant != "none" && grant != "GM.info");

            return world != null && !metadata.Unwrap && !(world == "isolated" && metadata.Capabilities.Contains("unsafeWindow"))
                && !(world == "page" && needsBridge && !trusted);
        }
    }
}
