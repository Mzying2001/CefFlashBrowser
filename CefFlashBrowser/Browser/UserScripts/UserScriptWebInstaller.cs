using CefFlashBrowser.Data;
using CefFlashBrowser.Services;
using CefFlashBrowser.Services.UserScripts;
using CefFlashBrowser.Utils;
using CefFlashBrowser.Views.Dialogs;
using Mzying2001.MonkeySharp.Core.Domain;
using Mzying2001.MonkeySharp.Core.Parsing;
using Mzying2001.MonkeySharp.Core.Repository;
using SimpleMvvm.Ioc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace CefFlashBrowser.Browser.UserScripts
{
    internal static class UserScriptWebInstaller
    {
        // Access the pending set only on the WPF dispatcher. Keep separate windows
        // from presenting overlapping confirmation and result dialogs.
        private static readonly HashSet<string> _pending = new HashSet<string>(StringComparer.Ordinal);
        private static readonly SemaphoreSlim _turn = new SemaphoreSlim(1, 1);

        internal static bool TryGetOrigin(string address, out string origin)
        {
            origin = null;

            if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                || !string.IsNullOrEmpty(uri.UserInfo)
                || !uri.AbsolutePath.EndsWith(".user.js", StringComparison.OrdinalIgnoreCase))
                return false;

            origin = new UriBuilder(uri) { Fragment = string.Empty }.Uri.AbsoluteUri;
            return true;
        }

        internal static async Task InstallAsync(string origin, Func<Window> getOwner, CancellationToken token)
        {
            if (!_pending.Add(origin))
                return;

            try
            {
                await _turn.WaitAsync(token);

                try
                {
                    await InstallCoreAsync(origin, getOwner, token);
                }
                finally
                {
                    _turn.Release();
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            finally
            {
                _pending.Remove(origin);
            }
        }

        private static async Task InstallCoreAsync(string origin, Func<Window> getOwner, CancellationToken token)
        {
            if (!GlobalData.Settings.EnableUserScripts || getOwner() == null)
                return;

            var errorKey = "userScriptInstall_downloadFailed";
            UserScriptInstallation previous = null;

            try
            {
                var service = SimpleIoc.Global.GetInstance<UserScriptService>();
                var source = await service.Content.FetchSourceAsync(null, origin, token);
                UserScriptSourceText.Validate(source);
                token.ThrowIfCancellationRequested();

                var parsed = new UserScriptMetadataParser().Parse(source);
                if (!parsed.CanEnable)
                {
                    ShowResult(getOwner, "userScriptManager_invalidMetadata", token);
                    return;
                }

                errorKey = "userScriptInstall_saveFailed";
                await service.InitializeAsync();
                token.ThrowIfCancellationRequested();
                var repository = service.Repository;
                var matches = await FindByOriginAsync(repository, origin, token);

                if (matches.Length > 1)
                {
                    ShowResult(getOwner, "userScriptInstall_conflict", token);
                    return;
                }

                previous = matches.SingleOrDefault();
                var owner = getOwner();
                if (owner == null || !GlobalData.Settings.EnableUserScripts)
                    return;

                var dialog = UserScriptInstallDialog.Create(parsed.Metadata, origin, update: previous != null);
                if (ShowDialog(dialog, owner, token) != true)
                    return;

                token.ThrowIfCancellationRequested();
                if (getOwner() == null || !GlobalData.Settings.EnableUserScripts)
                    return;

                var current = await FindByOriginAsync(repository, origin, token);
                if (current.Length != matches.Length || (previous != null
                    && (!current[0].ScriptKey.Equals(previous.ScriptKey) || current[0].RevisionId != previous.RevisionId)))
                {
                    ShowResult(getOwner, "userScriptInstall_conflict", token);
                    return;
                }

                if (previous == null)
                    await repository.InstallAsync(source, origin, true, token);
                else
                    await repository.UpdateIfUnchangedAsync(previous.ScriptKey, previous.RevisionId, source, origin, token);

                ShowResult(getOwner, "userScriptManager_refreshPage", token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception e) when (e is RepositoryRevisionMismatchException || e is UserScriptSourceConflictException
                || (previous != null && e is KeyNotFoundException))
            {
                ShowResult(getOwner, "userScriptInstall_conflict", token);
            }
            catch (Exception e)
            {
                LogHelper.LogError("Web userscript installation failed (" + e.GetType().Name + ").");
                ShowResult(getOwner, errorKey, token);
            }
        }

        private static async Task<UserScriptInstallation[]> FindByOriginAsync(IUserScriptRepository repository,
            string origin, CancellationToken token)
        {
            var snapshot = await repository.GetSnapshotAsync(token);
            return snapshot.Where(installation => TryGetOrigin(installation.SourceOrigin, out var installedOrigin)
                && installedOrigin == origin).Take(2).ToArray();
        }

        private static void ShowResult(Func<Window> getOwner, string messageKey, CancellationToken token)
        {
            var owner = getOwner();
            if (owner == null || token.IsCancellationRequested)
                return;

            var language = SimpleIoc.Global.GetInstance<LanguageService>();
            var dialog = WindowManager.CreateWindow<JsAlertDialog>();
            dialog.Title = language.GetString("userScriptManager_title");
            dialog.Message = language.GetString(messageKey);
            ShowDialog(dialog, owner, token);
        }

        private static bool? ShowDialog(Window dialog, Window owner, CancellationToken token)
        {
            using (token.Register(() => dialog.Dispatcher.InvokeAsync(() =>
            {
                if (dialog.IsVisible)
                    dialog.Close();
            })))
            {
                token.ThrowIfCancellationRequested();
                return DialogHelper.ShowModal(dialog, owner);
            }
        }
    }
}
