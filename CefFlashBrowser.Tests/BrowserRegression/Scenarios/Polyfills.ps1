function Disable-PolyfillsInSettings($Context) {
    $window = New-Object CefFlashBrowser.Views.SettingsWindow
    $window.WindowStartupLocation = [System.Windows.WindowStartupLocation]::Manual
    $window.Left = -10000
    $window.ShowInTaskbar = $false
    $window.ShowActivated = $false
    try {
        $window.Show()
        $toggle = $window.FindName('enableCoreJsPolyfills')
        Assert-Regression ($null -ne $toggle -and $toggle.IsChecked) 'website compatibility is enabled by default in settings'
        $item = [System.Windows.LogicalTreeHelper]::GetParent($toggle)
        $group = [System.Windows.LogicalTreeHelper]::GetParent($item)
        Assert-Regression ($group.Header -eq $Context.LanguageService.GetString('settings_headerAdvanced') -and
            $item.Description -eq $Context.LanguageService.GetString('settings_enableCoreJsPolyfills') -and
            ![string]::IsNullOrEmpty($item.Description)) 'the setting has a localized label in the Advanced group'
        $toggle.SetCurrentValue([System.Windows.Controls.Primitives.ToggleButton]::IsCheckedProperty, $false)
        Wait-Regression $Context { ![CefFlashBrowser.Data.GlobalData]::Settings.EnableCoreJsPolyfills } 'toggling the setting updates the model'
    } finally {
        $window.Close()
    }
}

function Start-PolyfillRegressionCef($Context, [switch]$DisablePolyfills) {
    $globalData = [CefFlashBrowser.Data.GlobalData]
    $privateStatic = [Reflection.BindingFlags]'Static,NonPublic'
    foreach ($path in @{
        CachesPath = (Join-Path $Context.TemporaryPath 'Cache')
        CefLogPath = (Join-Path $Context.TemporaryPath 'cef.log')
        CommandProxyPath = (Join-Path $Context.ApplicationPath 'Binaries\CmdProxy\CmdProxy.exe')
        FlashPath = (Join-Path $Context.ApplicationPath 'Binaries\Plugins\pepflashplayer.dll')
        SubprocessPath = (Join-Path $Context.CefPath 'CefSharp.BrowserSubprocess.exe')
    }.GetEnumerator()) {
        $globalData.GetField('<' + $path.Key + '>k__BackingField', $privateStatic).SetValue($null, $path.Value)
    }

    $settings = @{ DisableGpuAcceleration = $true; EnableUserScripts = $false }
    $settings | ConvertTo-Json | Set-Content -LiteralPath $globalData::SettingsPath -Encoding UTF8
    $globalData::InitSettings()
    if ($DisablePolyfills) {
        Disable-PolyfillsInSettings $Context
        $globalData::InitSettings()
        Assert-Regression (!$globalData::Settings.EnableCoreJsPolyfills) 'closing settings persists the disabled state'
    }

    $program = $globalData.Assembly.GetType('CefFlashBrowser.Program')
    [void]$program.GetMethod('InitCefFlash', $privateStatic).Invoke($null, $null)
    $Context.CefStarted = [CefSharp.Cef]::IsInitialized
    Assert-Regression $Context.CefStarted 'CEF initializes through the application startup path'
}

function Assert-PolyfillProbe($Context, $Browser, [string]$Description) {
    $probe = 'document.documentElement && document.documentElement.getAttribute("data-polyfill-result")'
    Wait-Regression $Context {
        $Browser.CanExecuteJavascriptInMainFrame -and $null -ne (Invoke-JavaScript $Context $Browser $probe)
    } "$Description first script runs"
    $result = Invoke-JavaScript $Context $Browser $probe
    Assert-Regression ($result -eq 'ready') "$Description first script sees core-js ($result)"
}

function Test-BrowserPolyfillsDisabled($Context) {
    $fixture = New-Object UserScriptHttpFixture
    $window = $null
    try {
        $fixture.PolyfillProbeHtml = [IO.File]::ReadAllText((Join-Path $Context.ScriptPath 'PolyfillProbe.html'))
        $window = New-UserScriptTargetWindow $Context ($fixture.BaseUrl + '/polyfill')
        $model = $window.ViewModel.CurrentBrowser
        Wait-Regression $Context {
            $null -ne $model.Browser -and $model.Browser.Title -eq 'Polyfill probe' -and !$model.Browser.IsLoading
        } 'polyfill test document loads with core-js disabled'

        $probe = 'document.documentElement.getAttribute("data-polyfill-apis")'
        $result = Invoke-JavaScript $Context $model.Browser $probe
        Assert-Regression ($result -eq 'undefined,undefined,undefined,undefined') "first script sees no core-js APIs ($result)"
    } finally {
        if ($null -ne $window) { $window.ForceCloseWindow() }
        $fixture.Dispose()
    }
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
