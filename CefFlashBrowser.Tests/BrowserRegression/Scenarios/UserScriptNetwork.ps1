function New-UserScriptTargetWindow($Context, [string]$Address) {
    $window = New-Object CefFlashBrowser.Views.BrowserWindow
    $window.Width = 800
    $window.Height = 600
    $window.Left = -10000
    $window.Top = -10000
    $window.ShowActivated = $false
    $window.ShowInTaskbar = $false
    $window.ViewModel.CurrentBrowser.Address = $Address
    $window.Show()
    return $window
}

function Wait-UserScriptNetworkMarker($Context, $Browser, [string]$Name, [string]$Expected, [bool]$Approve = $false) {
    Wait-Regression $Context {
        if ($Approve) {
            foreach ($prompt in @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.Dialogs.UserScriptPermissionDialog] })) {
                $prompt.FindName('allowRememberButton').RaiseEvent((New-Object System.Windows.RoutedEventArgs([System.Windows.Controls.Button]::ClickEvent)))
            }
        }
        $errorText = Invoke-JavaScript $Context $Browser "document.documentElement && document.documentElement.getAttribute('data-network-error')"
        if ($errorText) { throw $errorText }
        (Invoke-JavaScript $Context $Browser "document.documentElement && document.documentElement.getAttribute('$Name')") -eq $Expected
    } $Name
}

function Open-UserScriptPermissions($Context, $Window, [string]$Address) {
    $menu = Open-BrowserActionsMenu $Context $Window
    $downloadLabel = $Window.FindResource('userscript_downloads')
    $downloads = $menu.Items | Where-Object { $_.Header -eq $downloadLabel } | Select-Object -First 1
    Assert-Regression ($null -ne $downloads) 'actions contain script downloads'
    $item = $menu.Items[$menu.Items.IndexOf($downloads) + 1]
    Assert-Regression ($item.Header -eq $Window.FindResource('userscript_managePermissions')) 'script permissions appear directly after script downloads'
    $menu.IsOpen = $false
    $item.RaiseEvent((New-Object System.Windows.RoutedEventArgs([System.Windows.Controls.MenuItem]::ClickEvent)))

    $findWindow = {
        $Context.Application.Windows | Where-Object {
            $_ -is [CefFlashBrowser.Views.UserScriptPermissionsWindow] -and $_.Owner -eq $Window -and
            $_.FindName('pageAddress').Text -eq $Address -and $_.FindName('refreshButton').IsEnabled
        } | Select-Object -First 1
    }
    Wait-Regression $Context { $null -ne (& $findWindow) } 'permissions window loads for the selected main document'
    return (& $findWindow)
}

function Test-UserScriptPermissions($Context, $Window, $Model, $Fixture, $Peer) {
    $currentPage = $Fixture.BaseUrl + '/page'
    $otherPage = $Fixture.BaseUrl + '/login'
    $source = "// ==UserScript==`n// @name Login permissions`n// @match http://127.0.0.1/login*`n// @grant GM.xmlHttpRequest`n// @connect 127.0.0.1`n// ==/UserScript=="
    $token = [Threading.CancellationToken]::None
    $buttonClick = [System.Windows.Controls.Button]::ClickEvent
    $service = $Context.UserScriptService
    $otherScript = Complete-UserScriptTask $Context ($service.Repository.InstallAsync($source, $null, $false, $token)) 'install second tab permissions fixture'
    $primary = $null
    $secondary = $null
    try {
        $scope = New-Object Mzying2001.MonkeySharp.Core.Permissions.UserScriptPermissionScope($otherScript.ScriptKey.ToString(), 'GM.xmlHttpRequest', $Fixture.BaseUrl)
        Complete-UserScriptTask $Context ($service.Decisions.SetAsync($scope, 'Deny', $token)) 'save second tab permission' | Out-Null
        $primary = Open-UserScriptPermissions $Context $Window $currentPage
        $primaryList = $primary.FindName('permissionList')
        Assert-Regression ($primaryList.Items.Count -eq 3 -and @($primaryList.Items | Where-Object { $_.Record.Scope.Target -eq $Peer.BaseUrl }).Count -eq 1) 'page shows matching script decisions including cross-origin targets'
        Assert-Regression ($primaryList.View.Columns[2].Header -eq $Window.FindResource('userscript_permissionTarget')) 'permission columns use localized labels'

        [void](New-RegressionTab $Context $Window $otherPage)
        $secondary = Open-UserScriptPermissions $Context $Window $otherPage
        $secondaryList = $secondary.FindName('permissionList')
        Assert-Regression ($secondaryList.Items.Count -eq 1 -and $secondaryList.Items[0].ScriptName -eq 'Login permissions' -and
            $secondaryList.Items[0].IsScriptDisabled -and $secondaryList.Items[0].Record.Decision -eq 'Deny') 'second tab shows only its matching disabled script and saved denial'
        Assert-Regression ($primary.FindName('pageAddress').Text -eq $currentPage) 'open permission windows keep their original page'
        $secondaryList.SelectedIndex = 0
        Complete-UserScriptTask $Context ($service.Decisions.SetAsync($scope, 'Allow', $token)) 'change permission outside the open window' | Out-Null
        $secondary.FindName('clearButton').RaiseEvent((New-Object System.Windows.RoutedEventArgs($buttonClick)))
        Wait-Regression $Context {
            $secondary.FindName('refreshButton').IsEnabled -and $secondaryList.Items.Count -eq 1 -and
            $secondaryList.Items[0].Record.Decision -eq 'Allow' -and
            $secondary.FindName('statusText').Text -eq $secondary.FindResource('userscript_permissionChanged')
        } 'stale edits refresh the list and report the conflict'
        $secondary.Close()
        $secondary = $null
        $Window.ViewModel.CurrentBrowser = $Model

        $primaryList.SelectedItem = $primaryList.Items | Where-Object {
            $_.Record.Scope.Kind -eq 'GM.xmlHttpRequest' -and $_.Record.Scope.Target -eq $Fixture.BaseUrl
        } | Select-Object -First 1
        $refresh = $primary.FindName('refreshButton')
        $dispatch = "document.documentElement.removeAttribute('data-managed-permission'); document.dispatchEvent(new Event('xhrManagedPermission'));"
        $requestCount = $Fixture.RequestsFor('/echo')
        $primary.FindName('denyButton').RaiseEvent((New-Object System.Windows.RoutedEventArgs($buttonClick)))
        Wait-Regression $Context { $refresh.IsEnabled -and $primaryList.SelectedItem.Record.Decision -eq 'Deny' } 'denied decision saved'
        [void](Invoke-JavaScript $Context $Model.Browser $dispatch)
        Wait-UserScriptNetworkMarker $Context $Model.Browser 'data-managed-permission' 'MSP005_PERMISSION_DENIED'
        Assert-Regression ($Fixture.RequestsFor('/echo') -eq $requestCount) 'changed denial applies to the next invocation'

        $primary.FindName('allowButton').RaiseEvent((New-Object System.Windows.RoutedEventArgs($buttonClick)))
        Wait-Regression $Context { $refresh.IsEnabled -and $primaryList.SelectedItem.Record.Decision -eq 'Allow' } 'allowed decision saved'
        [void](Invoke-JavaScript $Context $Model.Browser $dispatch)
        Wait-UserScriptNetworkMarker $Context $Model.Browser 'data-managed-permission' 'allowed'
        Assert-Regression ($Fixture.RequestsFor('/echo') -eq ($requestCount + 1)) 'changed allowance applies to the next invocation'

        $primary.FindName('clearButton').RaiseEvent((New-Object System.Windows.RoutedEventArgs($buttonClick)))
        Wait-Regression $Context { $refresh.IsEnabled -and $primaryList.Items.Count -eq 2 -and $primaryList.SelectedIndex -eq -1 } 'clear removes only the selected record'
        [void](Invoke-JavaScript $Context $Model.Browser $dispatch)
        $findPrompt = {
            $Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.Dialogs.UserScriptPermissionDialog] } | Select-Object -First 1
        }
        Wait-Regression $Context { $null -ne (& $findPrompt) } 'cleared permission prompts on the next invocation'
        Assert-Regression ($Fixture.RequestsFor('/echo') -eq ($requestCount + 1)) 'the repeated prompt waits before sending the request'
        (& $findPrompt).FindName('allowOnceButton').RaiseEvent((New-Object System.Windows.RoutedEventArgs($buttonClick)))
        Wait-UserScriptNetworkMarker $Context $Model.Browser 'data-managed-permission' 'allowed'
        $refresh.RaiseEvent((New-Object System.Windows.RoutedEventArgs($buttonClick)))
        Wait-Regression $Context { $refresh.IsEnabled -and $primaryList.Items.Count -eq 2 } 'allow once does not create a managed record'
    } finally {
        if ($null -ne $secondary) { $secondary.Close() }
        if ($null -ne $primary) { $primary.Close() }
        Complete-UserScriptTask $Context ($service.Repository.RemoveAsync($otherScript.ScriptKey, $token)) 'remove second tab permissions fixture' | Out-Null
    }
}

function Test-UserScriptPermissionDefaults($Context, $Browser, $Fixture, $ScriptKey) {
    $settings = [CefFlashBrowser.Data.GlobalData]::Settings
    $original = $settings.DefaultUserScriptPermissionBehavior
    $decisions = $Context.UserScriptService.Decisions
    $token = [Threading.CancellationToken]::None
    $scope = New-Object Mzying2001.MonkeySharp.Core.Permissions.UserScriptPermissionScope($ScriptKey.ToString(), 'GM.xmlHttpRequest', $Fixture.BaseUrl)
    $cases = @(
        @{ Default = 'Allow'; Saved = $null; Expected = 'allowed'; Requests = 1 },
        @{ Default = 'Deny'; Saved = $null; Expected = 'MSP005_PERMISSION_DENIED'; Requests = 0 },
        @{ Default = 'Deny'; Saved = 'Allow'; Expected = 'allowed'; Requests = 1 },
        @{ Default = 'Allow'; Saved = 'Deny'; Expected = 'MSP005_PERMISSION_DENIED'; Requests = 0 }
    )

    try {
        foreach ($case in $cases) {
            $settings.DefaultUserScriptPermissionBehavior = $case.Default
            if ($null -ne $case.Saved) {
                Complete-UserScriptTask $Context ($decisions.SetAsync($scope, $case.Saved, $token)) 'save explicit permission' | Out-Null
            }

            $requests = $Fixture.RequestsFor('/echo')
            [void](Invoke-JavaScript $Context $Browser "document.documentElement.removeAttribute('data-managed-permission'); document.dispatchEvent(new Event('xhrManagedPermission'));")
            Wait-UserScriptNetworkMarker $Context $Browser 'data-managed-permission' $case.Expected

            $saved = Complete-UserScriptTask $Context ($decisions.GetAsync($scope, $token)) 'read saved permission'
            $prompts = @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.Dialogs.UserScriptPermissionDialog] })
            Assert-Regression ($prompts.Count -eq 0 -and $saved -eq $case.Saved -and
                $Fixture.RequestsFor('/echo') -eq ($requests + $case.Requests)) "default $($case.Default) with saved decision '$($case.Saved)' resolves without prompting or changing saved decisions"
        }

        [void](Invoke-JavaScript $Context $Browser "document.documentElement.removeAttribute('data-undeclared'); document.dispatchEvent(new Event('xhrUndeclared'));")
        Wait-UserScriptNetworkMarker $Context $Browser 'data-undeclared' 'MSP005_PERMISSION_DENIED'
    } finally {
        $settings.DefaultUserScriptPermissionBehavior = $original
    }
}

function Invoke-UserScriptFrame($Context, $Browser, [string]$Url, [string]$Code) {
    $response = Complete-UserScriptTask $Context ([UserScriptFrameProbe]::EvaluateAsync($Browser, $Url, $Code)) 'fixture frame JavaScript'
    if (!$response.Success) { throw $response.Message }
    return $response.Result
}

function Reset-UserScriptRuntime($Context) {
    Complete-UserScriptTask $Context ($Context.UserScriptService.StopAsync()) 'previous runtime stops'
    $Context.UserScriptService = [UserScriptRuntimeProbe]::Create($Context.DatabaseService, $Context.TemporaryPath)
}

function Test-UserScriptOfflineRecovery($Context) {
    Complete-UserScriptTask $Context ($Context.UserScriptService.InitializeAsync()) 'offline runtime'
    $fixture = New-Object UserScriptHttpFixture
    $source = @'
// ==UserScript==
// @name Prepared offline probe
// @match http://127.0.0.1/*
// @sandbox DOM
// @grant GM_getResourceText
// @grant GM_getValue
// @grant GM_setValue
// @grant GM.webRequest
// @webRequest [{"selector":"__BASE__/blocked.js","action":"cancel"}]
// @require __BASE__/dependency.js
// @resource fixture __BASE__/resource.txt
// @run-at document-end
// ==/UserScript==
var previous = GM_getValue('runs', 0);
var resource = GM_getResourceText('fixture');
GM_setValue('runs', previous + 1);
document.documentElement.setAttribute('data-offline-content', preparedDependency + '|' + resource);
document.documentElement.setAttribute('data-offline-before', String(previous));
'@
    $source = $source.Replace('__BASE__', $fixture.BaseUrl)
    $repository = $Context.UserScriptService.Repository
    $installation = Complete-UserScriptTask $Context ($repository.InstallAsync($source, $null, $true, [Threading.CancellationToken]::None)) 'install prepared fixture'
    $healthySource = "// ==UserScript==`n// @name Healthy neighbor`n// @match http://127.0.0.1/*`n// @grant none`n// @run-at document-end`n// ==/UserScript==`ndocument.documentElement.setAttribute('data-healthy-neighbor','ready');"
    $healthy = Complete-UserScriptTask $Context ($repository.InstallAsync($healthySource, $null, $true, [Threading.CancellationToken]::None)) 'install healthy neighbor'
    $window = $null
    try {
        $window = New-UserScriptTargetWindow $Context ($fixture.BaseUrl + '/page')
        $model = $window.ViewModel.CurrentBrowser
        Wait-Regression $Context { $null -ne $model.Browser -and $model.Browser.Title -eq 'Userscript fixture' -and
            (Invoke-JavaScript $Context $model.Browser "document.documentElement && document.documentElement.getAttribute('data-offline-content')") -eq 'dependency-ready|resource-ready' } 'prepared resource snapshot is ready in the first source statement'
        $value = Complete-UserScriptTask $Context ($Context.UserScriptService.ValueStore.GetAsync($installation.ScriptKey.ToString(), 'runs', [Threading.CancellationToken]::None)) 'first offline-probe commit'
        Assert-Regression ($value.Value.EncodedValue -eq 'n1') 'prepared source commits persistent GM data'
        $window.ForceCloseWindow()
        $window = $null
        $fixture.Offline = $true
        Reset-UserScriptRuntime $Context
        Complete-UserScriptTask $Context ($Context.UserScriptService.InitializeAsync()) 'new runtime restores source and cached assets'
        $repository = $Context.UserScriptService.Repository
        $restored = Complete-UserScriptTask $Context ($repository.GetAsync($installation.ScriptKey, [Threading.CancellationToken]::None)) 'restored installation'
        Assert-Regression ($restored.ScriptKey -eq $installation.ScriptKey -and $restored.InstalledAt -eq $installation.InstalledAt) 'runtime restart preserves installation identity and precise time'
        $window = New-UserScriptTargetWindow $Context ($fixture.BaseUrl + '/page')
        $model = $window.ViewModel.CurrentBrowser
        Wait-Regression $Context { $null -ne $model.Browser -and $model.Browser.Title -eq 'Userscript fixture' -and
            (Invoke-JavaScript $Context $model.Browser "document.documentElement && document.documentElement.getAttribute('data-offline-before')") -eq '1' -and
            (Invoke-JavaScript $Context $model.Browser "document.documentElement && document.documentElement.getAttribute('data-offline-content')") -eq 'dependency-ready|resource-ready' } 'offline dependency and resource execution after runtime restart'
        Assert-Regression ($fixture.RequestsFor('/dependency.js') -eq 1 -and $fixture.RequestsFor('/resource.txt') -eq 1) 'complete cache restores dependencies and resources without HTTP'
        $sourcePath = Join-Path $Context.TemporaryPath ('MonkeySharp\' + $installation.ScriptKey.ToString() + '\userscript.js')
        $external = $source + "`ndocument.documentElement.setAttribute('data-external-reload','ready');"
        $utf8 = New-Object Text.UTF8Encoding($false)
        [IO.File]::WriteAllText($sourcePath, $external, $utf8)
        $reloaded = Complete-UserScriptTask $Context ($repository.ReloadAsync($installation.ScriptKey, [Threading.CancellationToken]::None)) 'reload external source'
        Assert-Regression $reloaded.CanExecute 'external source becomes ready through reload'
        Assert-Regression ($null -eq (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-external-reload')")) 'reload does not inject an already executed document again'
        $window.ForceCloseWindow()
        $window = $null
        $window = New-UserScriptTargetWindow $Context ($fixture.BaseUrl + '/page?external')
        $model = $window.ViewModel.CurrentBrowser
        Wait-Regression $Context { $null -ne $model.Browser -and $model.Browser.Title -eq 'Userscript fixture' -and
            (Invoke-JavaScript $Context $model.Browser "document.documentElement && document.documentElement.getAttribute('data-external-reload')") -eq 'ready' } 'next document uses the externally edited source'
        $window.ForceCloseWindow()
        $window = $null

        # Stop the writer before removing its dedicated dependency cache. CEF's own cache stays live.
        Reset-UserScriptRuntime $Context
        [CefFlashBrowser.Utils.DirectoryHelper]::MoveDirectoryToPendingDelete(
            (Join-Path $Context.TemporaryPath 'Caches\UserScripts'), $true)
        Complete-UserScriptTask $Context ($Context.UserScriptService.InitializeAsync()) 'missing cache remains a local failure offline'
        $repository = $Context.UserScriptService.Repository
        $unavailable = Complete-UserScriptTask $Context ($repository.GetAsync($installation.ScriptKey, [Threading.CancellationToken]::None)) 'unavailable installation snapshot'
        Assert-Regression ($unavailable.IsEnabled -and !$unavailable.IsReady -and $unavailable.Diagnostics.Count -gt 0) 'offline cache loss preserves enabled intent and diagnostics'
        $requests = $fixture.RequestsFor('/blocked.js')
        $window = New-UserScriptTargetWindow $Context ($fixture.BaseUrl + '/page')
        $model = $window.ViewModel.CurrentBrowser
        Wait-Regression $Context { $null -ne $model.Browser -and $model.Browser.Title -eq 'Userscript fixture' -and !$model.Browser.IsLoading -and
            (Invoke-JavaScript $Context $model.Browser "document.documentElement && document.documentElement.getAttribute('data-healthy-neighbor')") -eq 'ready' } 'healthy script executes beside an unavailable installation'
        Assert-Regression (!$model.UserScriptsUnavailable -and $fixture.RequestsFor('/blocked.js') -gt $requests) 'unavailable installation registers no static rule and does not disable the page runtime'
        $window.ForceCloseWindow()
        $window = $null
        $fixture.Offline = $false
        $repaired = Complete-UserScriptTask $Context ($repository.ReloadAsync($installation.ScriptKey, [Threading.CancellationToken]::None)) 'reload automatically rebuilds missing assets'
        Assert-Regression ($repaired.CanExecute -and $repaired.ScriptKey -eq $installation.ScriptKey -and $repaired.InstalledAt -eq $installation.InstalledAt) 'cache rebuild preserves identity and installation time'
        Assert-Regression ($fixture.RequestsFor('/dependency.js') -gt 1 -and $fixture.RequestsFor('/resource.txt') -gt 1) 'missing dependencies and resources are downloaded again'
        $requests = $fixture.RequestsFor('/blocked.js')
        $window = New-UserScriptTargetWindow $Context ($fixture.BaseUrl + '/page?restored')
        $model = $window.ViewModel.CurrentBrowser
        Wait-Regression $Context { $null -ne $model.Browser -and $model.Browser.Title -eq 'Userscript fixture' -and !$model.Browser.IsLoading -and
            (Invoke-JavaScript $Context $model.Browser "document.documentElement && document.documentElement.getAttribute('data-external-reload')") -eq 'ready' } 'rebuilt assets execute with the edited source'
        Assert-Regression ($fixture.RequestsFor('/blocked.js') -eq $requests) 'ready installation restores its static rule'
        [IO.File]::WriteAllBytes($sourcePath, [byte[]]@(255))
        $unavailable = Complete-UserScriptTask $Context ($repository.ReloadAsync($installation.ScriptKey, [Threading.CancellationToken]::None)) 'reload invalid source'
        Assert-Regression (!$unavailable.IsReady) 'invalid source becomes unavailable without rolling back the file'
        $window.ForceCloseWindow()
        $window = $null
        $window = New-UserScriptTargetWindow $Context ($fixture.BaseUrl + '/page?invalid')
        $model = $window.ViewModel.CurrentBrowser
        Wait-Regression $Context { $null -ne $model.Browser -and $model.Browser.Title -eq 'Userscript fixture' -and !$model.Browser.IsLoading -and
            (Invoke-JavaScript $Context $model.Browser "document.documentElement && document.documentElement.getAttribute('data-healthy-neighbor')") -eq 'ready' } 'invalid source leaves the healthy neighbor running'
        Assert-Regression ($fixture.RequestsFor('/blocked.js') -gt $requests) 'failed reload revokes the previously active static rule'
        $saved = Complete-UserScriptTask $Context ($Context.UserScriptService.ValueStore.GetAsync($installation.ScriptKey.ToString(), 'runs', [Threading.CancellationToken]::None)) 'GM data survives cache rebuild and invalid source'
        Assert-Regression $saved.Exists 'installation data partition survives recovery'
    } finally {
        if ($null -ne $window) { $window.ForceCloseWindow() }
        try {
            $repository = $Context.UserScriptService.Repository
            Complete-UserScriptTask $Context ($repository.RemoveAsync($installation.ScriptKey, [Threading.CancellationToken]::None)) 'remove prepared fixture' | Out-Null
            Complete-UserScriptTask $Context ($repository.RemoveAsync($healthy.ScriptKey, [Threading.CancellationToken]::None)) 'remove healthy neighbor' | Out-Null
        } catch { }
        $fixture.Dispose()
    }
}

function Test-UserScriptFrames($Context) {
    Complete-UserScriptTask $Context ($Context.UserScriptService.InitializeAsync()) 'frame runtime'
    $fixture = New-Object UserScriptHttpFixture
    $peer = New-Object UserScriptHttpFixture
    $fixture.PeerUrl = $peer.BaseUrl
    $repository = $Context.UserScriptService.Repository
    $source = @'
// ==UserScript==
// @name Frame storage probe
// @match http://127.0.0.1/*
// @sandbox DOM
// @grant GM_getValue
// @grant GM_setValue
// @grant GM_addValueChangeListener
// @grant window.onurlchange
// @run-at document-end
// ==/UserScript==
var initial = GM_getValue('shared', 0);
document.documentElement.setAttribute('data-bootstrap', String(initial));
document.documentElement.setAttribute('data-frame-runs', String(Number(document.documentElement && document.documentElement.getAttribute('data-frame-runs') || 0) + 1));
GM_addValueChangeListener('shared', function(name, oldValue, value, remote) {
    document.documentElement.setAttribute('data-shared', String(value));
    document.documentElement.setAttribute('data-remote', String(remote));
});
document.addEventListener('broadcastValue', function(event) { GM_setValue('shared', typeof event.detail === 'number' ? event.detail : 42); });
window.onurlchange = function() { document.documentElement.setAttribute('data-urlchange', 'yes'); };
'@
    $top = "// ==UserScript==`n// @name Main frame only`n// @match http://127.0.0.1/*`n// @noframes`n// @grant none`n// @run-at document-end`n// ==/UserScript==`ndocument.documentElement.setAttribute('data-top-only','yes');"
    $installation = Complete-UserScriptTask $Context ($repository.InstallAsync($source, $null, $true, [Threading.CancellationToken]::None)) 'install frame fixture'
    $mainOnly = Complete-UserScriptTask $Context ($repository.InstallAsync($top, $null, $true, [Threading.CancellationToken]::None)) 'install noframes fixture'
    $window = $null
    $secondWindow = $null
    try {
        $window = New-UserScriptTargetWindow $Context ($fixture.BaseUrl + '/frames')
        $model = $window.ViewModel.CurrentBrowser
        Wait-Regression $Context { $null -ne $model.Browser -and $model.Browser.Title -eq 'Frame fixture' -and !$model.Browser.IsLoading } 'main and child frames load'
        foreach ($url in @(($fixture.BaseUrl + '/frames'), ($fixture.BaseUrl + '/frame'), ($peer.BaseUrl + '/frame'))) {
            Wait-Regression $Context { (Invoke-UserScriptFrame $Context $model.Browser $url "document.documentElement && document.documentElement.getAttribute('data-bootstrap')") -eq '0' } 'first-line frame storage bootstrap'
            Assert-Regression ((Invoke-UserScriptFrame $Context $model.Browser $url "document.documentElement && document.documentElement.getAttribute('data-frame-runs')") -eq '1') 'source runs once in each main/same-origin/cross-port document'
        }
        Assert-Regression ((Invoke-UserScriptFrame $Context $model.Browser ($fixture.BaseUrl + '/frames') "document.documentElement && document.documentElement.getAttribute('data-top-only')") -eq 'yes' -and
            $null -eq (Invoke-UserScriptFrame $Context $model.Browser ($peer.BaseUrl + '/frame') "document.documentElement && document.documentElement.getAttribute('data-top-only')")) 'noframes excludes child documents'
        [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('broadcastValue'))")
        foreach ($url in @(($fixture.BaseUrl + '/frames'), ($fixture.BaseUrl + '/frame'), ($peer.BaseUrl + '/frame'))) {
            Wait-Regression $Context { (Invoke-UserScriptFrame $Context $model.Browser $url "document.documentElement && document.documentElement.getAttribute('data-shared')") -eq '42' } 'cross-frame value listener receives commit'
        }
        Assert-Regression ((Invoke-UserScriptFrame $Context $model.Browser ($fixture.BaseUrl + '/frames') "document.documentElement && document.documentElement.getAttribute('data-remote')") -eq 'false' -and
            (Invoke-UserScriptFrame $Context $model.Browser ($peer.BaseUrl + '/frame') "document.documentElement && document.documentElement.getAttribute('data-remote')") -eq 'true') 'listeners distinguish local and remote execution commits'
        [void](Invoke-JavaScript $Context $model.Browser "history.pushState({},'', '/spa-route')")
        Wait-Regression $Context { $model.Address -eq $fixture.BaseUrl + '/spa-route' -and
            (Invoke-JavaScript $Context $model.Browser "document.documentElement && document.documentElement.getAttribute('data-urlchange')") -eq 'yes' } 'SPA urlchange notification'
        Assert-Regression ((Invoke-JavaScript $Context $model.Browser "document.documentElement && document.documentElement.getAttribute('data-frame-runs')") -eq '1') 'same-document routing does not reinject source'
        $secondWindow = New-UserScriptTargetWindow $Context ($peer.BaseUrl + '/page')
        $other = $secondWindow.ViewModel.CurrentBrowser
        Wait-Regression $Context { $null -ne $other.Browser -and $other.Browser.Title -eq 'Userscript fixture' -and
            (Invoke-JavaScript $Context $other.Browser "document.documentElement && document.documentElement.getAttribute('data-bootstrap')") -eq '42' } 'second window bootstraps committed storage'
        [void](Invoke-JavaScript $Context $other.Browser "document.dispatchEvent(new CustomEvent('broadcastValue',{detail:43}))")
        Wait-Regression $Context { (Invoke-JavaScript $Context $model.Browser "document.documentElement && document.documentElement.getAttribute('data-shared')") -eq '43' -and
            (Invoke-UserScriptFrame $Context $model.Browser ($fixture.BaseUrl + '/frame') "document.documentElement && document.documentElement.getAttribute('data-shared')") -eq '43' } 'cross-window and background-frame listeners share the same store'
        Assert-Regression ((Invoke-JavaScript $Context $model.Browser "document.documentElement && document.documentElement.getAttribute('data-remote')") -eq 'true') 'commits from another window are remote notifications'

    } finally {
        if ($null -ne $secondWindow) { $secondWindow.ForceCloseWindow() }
        if ($null -ne $window) { $window.ForceCloseWindow() }
        Complete-UserScriptTask $Context ($repository.RemoveAsync($installation.ScriptKey, [Threading.CancellationToken]::None)) 'remove frame fixture' | Out-Null
        Complete-UserScriptTask $Context ($repository.RemoveAsync($mainOnly.ScriptKey, [Threading.CancellationToken]::None)) 'remove noframes fixture' | Out-Null
        $fixture.Dispose()
        $peer.Dispose()
    }
}

function Test-UserScriptNetwork($Context) {
    Complete-UserScriptTask $Context ($Context.UserScriptService.InitializeAsync()) 'network runtime'
    $fixture = New-Object UserScriptHttpFixture
    $peer = New-Object UserScriptHttpFixture
    $fixture.PeerUrl = $peer.BaseUrl
    $peer.PeerUrl = $fixture.BaseUrl
    $repository = $Context.UserScriptService.Repository
    $installed = New-Object 'System.Collections.Generic.List[Mzying2001.MonkeySharp.Core.Domain.ScriptKey]'
    $savedTrusted = [CefFlashBrowser.Data.GlobalData]::Settings.TrustedUserScriptPageWorld
    $window = $null
    try {
        # Exercise untrusted page-world behavior independently of the application default.
        [CefFlashBrowser.Data.GlobalData]::Settings.TrustedUserScriptPageWorld = $false
        # A blocked page-world script must not silently retain its static rule.
        $raw = "// ==UserScript==`n// @name Untrusted static rule`n// @match http://127.0.0.1/*`n// @grant GM.webRequest`n// @webRequest [{`"selector`":`"$($fixture.BaseUrl)/blocked.js`",`"action`":`"cancel`"}]`n// ==/UserScript==`ndocument.documentElement.setAttribute('data-untrusted','ran');"
        $blocked = Complete-UserScriptTask $Context ($repository.InstallAsync($raw, $null, $true, [Threading.CancellationToken]::None)) 'install untrusted rule fixture'
        $installed.Add($blocked.ScriptKey)
        $window = New-UserScriptTargetWindow $Context ($fixture.BaseUrl + '/page')
        $model = $window.ViewModel.CurrentBrowser
        Wait-Regression $Context { $null -ne $model.Browser -and $model.Browser.Title -eq 'Userscript fixture' -and !$model.Browser.IsLoading } 'untrusted page-world fixture'
        Assert-Regression ($fixture.RequestsFor('/blocked.js') -eq 1 -and $null -eq (Invoke-JavaScript $Context $model.Browser "document.documentElement && document.documentElement.getAttribute('data-untrusted')")) 'blocked page-world scripts cannot activate static request rules'
        $window.ForceCloseWindow()
        $window = $null
        Complete-UserScriptTask $Context ($repository.RemoveAsync($blocked.ScriptKey, [Threading.CancellationToken]::None)) 'remove untrusted rule fixture' | Out-Null
        $installed.Remove($blocked.ScriptKey) | Out-Null

        $static = $raw.Replace('Untrusted static rule', 'Prepared static rule').Replace('// @grant GM.webRequest', "// @sandbox DOM`n// @grant GM.webRequest")
        $allowed = Complete-UserScriptTask $Context ($repository.InstallAsync($static, $null, $true, [Threading.CancellationToken]::None)) 'install prepared static rule'
        $installed.Add($allowed.ScriptKey)
        $requests = $fixture.RequestsFor('/blocked.js')
        $window = New-UserScriptTargetWindow $Context ($fixture.BaseUrl + '/page')
        $model = $window.ViewModel.CurrentBrowser
        Wait-Regression $Context { $null -ne $model.Browser -and $model.Browser.Title -eq 'Userscript fixture' -and !$model.Browser.IsLoading } 'prepared first target page'
        Assert-Regression ($fixture.RequestsFor('/blocked.js') -eq $requests) 'prepared static rules affect the first target script resource'
        $window.ForceCloseWindow()
        $window = $null
        Complete-UserScriptTask $Context ($repository.RemoveAsync($allowed.ScriptKey, [Threading.CancellationToken]::None)) 'remove prepared static rule' | Out-Null
        $installed.Remove($allowed.ScriptKey) | Out-Null

        $window = New-UserScriptTargetWindow $Context ($fixture.BaseUrl + '/login')
        $model = $window.ViewModel.CurrentBrowser
        Wait-Regression $Context { $null -ne $model.Browser -and $model.Browser.Title -eq 'Login fixture' -and !$model.Browser.IsLoading } 'native login cookie jar'
        $source = @'
// ==UserScript==
// @name CEF network probe
// @match http://127.0.0.1/*
// @exclude-match http://127.0.0.1/login*
// @sandbox DOM
// @grant GM.xmlHttpRequest
// @grant GM.cookie
// @connect 127.0.0.1
// ==/UserScript==
function run(name, work) {
    document.addEventListener(name, function() {
        work().catch(function(error) { document.documentElement.setAttribute('data-network-error', (error.code || error.name || '') + ': ' + error.message); });
    });
}
run('xhrAuthenticated', async function() {
    var response = await GM.xmlHttpRequest({url:'__BASE__/echo'});
    document.documentElement.setAttribute('data-authenticated', response.responseText.indexOf('session=fixture') >= 0 ? 'yes':'no');
});
run('xhrManagedPermission', async function() {
    try {
        await GM.xmlHttpRequest({url:'__BASE__/echo'});
        document.documentElement.setAttribute('data-managed-permission', 'allowed');
    } catch (error) {
        document.documentElement.setAttribute('data-managed-permission', String(error.code));
    }
});
run('xhrAnonymous', async function() {
    var response = await GM.xmlHttpRequest({url:'__BASE__/anonymous', anonymous:true});
    document.documentElement.setAttribute('data-anonymous', response.responseText === '' ? 'yes':'no');
});
run('xhrRedirect', async function() {
    var response = await GM.xmlHttpRequest({url:'__BASE__/redirect'});
    document.documentElement.setAttribute('data-redirect', response.finalUrl.indexOf('__PEER__/echo') === 0 ? 'yes':'no');
});
run('xhrUndeclared', async function() {
    try { await GM.xmlHttpRequest({url:'__UNDECLARED__/denied-connect'}); }
    catch (error) { document.documentElement.setAttribute('data-undeclared', String(error.code)); }
});
run('xhrExplicitCookie', async function() {
    try { await GM.xmlHttpRequest({url:'__BASE__/explicit-cookie', headers:{Cookie:'forged=fixture'}}); }
    catch (error) { document.documentElement.setAttribute('data-explicit-cookie', String(error.code)); }
});
run('cookieCrud', async function() {
    await GM.cookie.set({url:'__BASE__/', name:'probeCookie', value:'native-value', path:'/'});
    var cookies = await GM.cookie.list({url:'__BASE__/', name:'probeCookie'});
    if (cookies.length !== 1 || cookies[0].value !== 'native-value') throw new Error('Cookie readback did not match');
    await GM.cookie.delete({url:'__BASE__/', name:'probeCookie', path:'/'});
    var empty = await GM.cookie.list({url:'__BASE__/', name:'probeCookie'});
    document.documentElement.setAttribute('data-cookie-crud', empty.length === 0 ? 'yes':'no');
    try { await GM.cookie.set({url:'__BASE__/', name:'partitionProbe', value:'blocked', secure:true, partitionKey:{topLevelSite:'https://example.test'}}); }
    catch (error) { document.documentElement.setAttribute('data-partition', String(error.code)); }
    var partition = await GM.cookie.list({url:'__BASE__/', name:'partitionProbe'});
    document.documentElement.setAttribute('data-partition-clean', partition.length === 0 ? 'yes':'no');
});
document.documentElement.setAttribute('data-network-ready', 'yes');
'@
        $undeclared = $peer.BaseUrl.Replace('127.0.0.1', 'localhost')
        $source = $source.Replace('__BASE__', $fixture.BaseUrl).Replace('__PEER__', $peer.BaseUrl).Replace('__UNDECLARED__', $undeclared)
        $script = Complete-UserScriptTask $Context ($repository.InstallAsync($source, $null, $true, [Threading.CancellationToken]::None)) 'install network fixture'
        $installed.Add($script.ScriptKey)
        $model.Address = $fixture.BaseUrl + '/page'
        Wait-Regression $Context { $model.Browser.Title -eq 'Userscript fixture' -and
            (Invoke-JavaScript $Context $model.Browser "document.documentElement && document.documentElement.getAttribute('data-network-ready')") -eq 'yes' } 'network source ready'
        [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('xhrAuthenticated'))")
        Wait-UserScriptNetworkMarker $Context $model.Browser 'data-authenticated' 'yes' $true
        Assert-Regression ($fixture.CookieFor('/echo').Contains('session=fixture')) 'GM XHR uses the actual CEF login cookie jar'
        [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('xhrAnonymous'))")
        Wait-UserScriptNetworkMarker $Context $model.Browser 'data-anonymous' 'yes'
        Assert-Regression ($fixture.CookieFor('/anonymous') -eq '') 'anonymous XHR omits browser credentials'
        [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('xhrRedirect'))")
        Wait-UserScriptNetworkMarker $Context $model.Browser 'data-redirect' 'yes' $true
        Assert-Regression ($peer.RequestsFor('/echo') -eq 1) 'redirect authorization reaches the exact second origin'
        [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('xhrUndeclared'))")
        Wait-UserScriptNetworkMarker $Context $model.Browser 'data-undeclared' 'MSP005_PERMISSION_DENIED'
        Assert-Regression ($peer.RequestsFor('/denied-connect') -eq 0) 'undeclared XHR is rejected before server side effects'
        [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('xhrExplicitCookie'))")
        Wait-UserScriptNetworkMarker $Context $model.Browser 'data-explicit-cookie' 'MSP006_NOT_SUPPORTED'
        Assert-Regression ($fixture.RequestsFor('/explicit-cookie') -eq 0) 'CefSharp 84 explicit Cookie requests fail before sending'
        [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('cookieCrud'))")
        Wait-UserScriptNetworkMarker $Context $model.Browser 'data-cookie-crud' 'yes' $true
        Wait-UserScriptNetworkMarker $Context $model.Browser 'data-partition' 'MSP006_NOT_SUPPORTED'
        Wait-UserScriptNetworkMarker $Context $model.Browser 'data-partition-clean' 'yes'
        Assert-Regression $true 'native Cookie CRUD reads back and partition refusal leaves no mutation'
        Test-UserScriptPermissions $Context $window $model $fixture $peer
        Test-UserScriptPermissionDefaults $Context $model.Browser $fixture $script.ScriptKey
    } finally {
        [CefFlashBrowser.Data.GlobalData]::Settings.TrustedUserScriptPageWorld = $savedTrusted
        if ($null -ne $window) { $window.ForceCloseWindow() }
        foreach ($key in $installed) { Complete-UserScriptTask $Context ($repository.RemoveAsync($key, [Threading.CancellationToken]::None)) 'remove network fixture' | Out-Null }
        $fixture.Dispose()
        $peer.Dispose()
    }
}
