using CefFlashBrowser.Services;
using CefFlashBrowser.Views.Dialogs;
using Mzying2001.MonkeySharp.Core.Permissions;
using SimpleMvvm.Ioc;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace CefFlashBrowser.Browser.UserScripts
{
    public sealed class UserScriptPermissionPrompt : IUserScriptPermissionPrompt
    {
        public async Task<UserScriptPermissionChoice> PromptAsync(ApiAuthorizationRequest request,
            UserScriptPermissionScope scope, CancellationToken token)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted)
                return new UserScriptPermissionChoice(PermissionDecision.Deny);

            var completion = new TaskCompletionSource<UserScriptPermissionChoice>(TaskCreationOptions.RunContinuationsAsynchronously);
            UserScriptPermissionDialog window = null;
            using (token.Register(() =>
            {
                completion.TrySetCanceled();
                if (!dispatcher.HasShutdownStarted)
                    dispatcher.BeginInvoke(new Action(() => window?.Close()));
            }))
            {
                await dispatcher.InvokeAsync(() =>
                {
                    if (token.IsCancellationRequested || dispatcher.HasShutdownStarted)
                        return;

                    var language = SimpleIoc.Global.GetInstance<LanguageService>();
                    var message = language.GetFormattedString("userscript_permissionMessage",
                        request.Installation.Definition.Metadata.Name, scope.Kind, scope.Target);
                    window = new UserScriptPermissionDialog(message);
                    // An owner can disappear while another tab still waits on the
                    // same shared permission batch. The batch token owns this UI.
                    window.Closed += (sender, e) => completion.TrySetResult(window.Choice);
                    window.Show();
                });
                return await completion.Task.ConfigureAwait(false);
            }
        }
    }
}
