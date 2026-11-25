function Assert-PolyfillProbe($Context, $Browser, [string]$Description) {
    $probe = 'document.documentElement && document.documentElement.getAttribute("data-polyfill-result")'
    Wait-Regression $Context {
        $Browser.CanExecuteJavascriptInMainFrame -and $null -ne (Invoke-JavaScript $Context $Browser $probe)
    } "$Description first script runs"
    $result = Invoke-JavaScript $Context $Browser $probe
    Assert-Regression ($result -eq 'ready') "$Description first script sees core-js ($result)"
}

function Test-BrowserPolyfills($Context) {
    $html = [IO.File]::ReadAllText((Join-Path $Context.ScriptPath 'PolyfillProbe.html'))
    $fixture = New-Object UserScriptHttpFixture
    $peer = New-Object UserScriptHttpFixture
    $settings = [CefFlashBrowser.Data.GlobalData]::Settings
    $previousUserScripts = $settings.EnableUserScripts
    $window = $null
    $popup = $null
    try {
        $settings.EnableUserScripts = $false
        $fixture.PolyfillProbeHtml = $html
        $peer.PolyfillProbeHtml = $html
        $fixture.PeerUrl = $peer.BaseUrl.Replace('127.0.0.1', 'localhost')
        $address = $fixture.BaseUrl + '/polyfill-frames'
        $window = New-UserScriptTargetWindow $Context $address
        $model = $window.ViewModel.CurrentBrowser
        Wait-Regression $Context {
            $null -ne $model.Browser -and $model.Browser.Title -eq 'Polyfill probe' -and !$model.Browser.IsLoading
        } 'polyfill test documents load'
        $browser = $model.Browser
        Assert-PolyfillProbe $Context $browser 'initial navigation with strict CSP'

        $probe = 'document.documentElement.getAttribute("data-polyfill-result")'
        foreach ($origin in @($fixture.BaseUrl, $fixture.PeerUrl)) {
            $result = Invoke-UserScriptFrame $Context $browser ($origin + '/polyfill') $probe
            Assert-Regression ($result -eq 'ready') "iframe at $origin sees core-js in its first script ($result)"
        }
        $result = Invoke-JavaScript $Context $browser "document.querySelector('iframe[srcdoc]').contentDocument.documentElement.getAttribute('data-polyfill-result')"
        Assert-Regression ($result -eq 'ready') "srcdoc iframe sees core-js in its first script ($result)"

        $encodedHtml = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($html))
        $addFrames = @"
(() => {
    const html = atob('$encodedHtml');
    const sources = ['about:blank', URL.createObjectURL(new Blob([html], { type: 'text/html' })), 'data:text/html;base64,$encodedHtml'];
    for (const source of sources) {
        const iframe = document.createElement('iframe');
        iframe.src = source;
        iframe.addEventListener('load', () => iframe.setAttribute('data-polyfill-loaded', ''));
        document.body.append(iframe);
        if (source === 'about:blank') {
            iframe.contentDocument.write(html);
            iframe.contentDocument.close();
        }
    }
    return sources;
})()
"@
        $sources = @(Invoke-JavaScript $Context $browser $addFrames)
        Wait-Regression $Context {
            (Invoke-JavaScript $Context $browser "document.querySelectorAll('iframe[data-polyfill-loaded]').length") -eq $sources.Count -and !$browser.IsLoading
        } 'about:blank, blob and data probe frames load'
        foreach ($source in $sources) {
            $scheme = $source.Split(':')[0]
            $result = Invoke-UserScriptFrame $Context $browser $source $probe
            Assert-Regression ($result -eq 'ready') "$scheme iframe sees core-js in its first script ($result)"
        }

        [void](Invoke-JavaScript $Context $browser 'document.documentElement.removeAttribute("data-polyfill-result")')
        $browser.Reload()
        Assert-PolyfillProbe $Context $browser 'reload'

        [void](Invoke-JavaScript $Context $browser "window.open('/polyfill', 'polyfillPopup', 'width=420,height=360,left=0,top=0'); true")
        Wait-Regression $Context {
            @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.PopupWebPage] }).Count -eq 1
        } 'polyfill popup opens'
        $popup = $Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.PopupWebPage] }
        Assert-PolyfillProbe $Context ($popup.FindName('browserView').ViewModel.Browser) 'popup navigation'

        $path = Join-Path $Context.TemporaryPath 'polyfill-probe.html'
        [IO.File]::WriteAllText($path, $html)
        $model.Address = ([Uri]$path).AbsoluteUri
        Wait-Regression $Context { $browser.GetBrowser().MainFrame.Url -eq $model.Address -and !$browser.IsLoading } 'local HTML probe loads'
        Assert-PolyfillProbe $Context $browser 'local HTML navigation'
    } finally {
        if ($null -ne $popup) { $popup.ForceCloseWindow() }
        if ($null -ne $window) { $window.ForceCloseWindow() }
        $fixture.Dispose()
        $peer.Dispose()
        $settings.EnableUserScripts = $previousUserScripts
    }
}
