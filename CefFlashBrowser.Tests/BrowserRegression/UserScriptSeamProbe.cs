using CefSharp;
using CefFlashBrowser.FlashBrowser;
using Mzying2001.MonkeySharp.CefSharp;
using Mzying2001.MonkeySharp.Core.Repository;
using System;
using System.Threading;
using System.Threading.Tasks;

public sealed class UserScriptSeamProbe
{
    public readonly CefSharpUserScriptHost Host;
    public int Navigations;
    public bool ReadyAfterAwait;

    public UserScriptSeamProbe(ChromiumFlashBrowser browser)
    {
        var repository = new InMemoryUserScriptRepository();
        repository.InstallAsync("// ==UserScript==\n// @name Seam probe\n// @match about:blank\n// @grant none\n// ==/UserScript==\nwindow.__seamExecuted = true;",
            null, true, CancellationToken.None).GetAwaiter().GetResult();
        Host = new CefSharpUserScriptHostBuilder(repository).Build();
        Host.Attach(browser);
        browser.NavigationHandler = async (address, token) =>
        {
            await Host.PrepareAsync(token);
            token.ThrowIfCancellationRequested();
            Navigations++;
            await Host.NavigateAsync(address, token);
        };
    }

    public async Task ReadAsync(IWebBrowser browser)
    {
        await Task.Delay(20).ConfigureAwait(false);
        ReadyAfterAwait = browser.IsBrowserInitialized;
        using (var audio = new CefSharpTabAudioService(() =>
            browser.IsBrowserInitialized && !browser.IsDisposed ? browser.GetBrowser().GetHost() : null))
        {
            var original = await audio.GetStateAsync(CancellationToken.None).ConfigureAwait(false);
            await audio.SetMuteAsync(!original.IsMuted, CancellationToken.None).ConfigureAwait(false);
            await audio.SetMuteAsync(original.IsMuted, CancellationToken.None).ConfigureAwait(false);
            await Task.Delay(250).ConfigureAwait(false);
        }
    }
}
