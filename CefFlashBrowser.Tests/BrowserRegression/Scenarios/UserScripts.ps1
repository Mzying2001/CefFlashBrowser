function Complete-UserScriptTask($Context, $Task, [string]$Message) {
    Wait-Regression $Context { $Task.IsCompleted } $Message
    $result = $Task.GetAwaiter().GetResult()
    if ($null -ne $result -and $result.GetType().FullName -ne 'System.Threading.Tasks.VoidTaskResult') { return $result }
}

function Test-UserScriptCloseCancellation($Context) {
    Complete-UserScriptTask $Context ($Context.UserScriptService.InitializeAsync()) 'close-probe runtime'
    $source = @'
// ==UserScript==
// @name Close lifecycle probe
// @match file:///*
// @noframes
// @sandbox DOM
// @grant GM.setValue
// @run-at document-end
// ==/UserScript==
var count = 0;
document.addEventListener('userscriptCloseProbe', async function() {
    await GM.setValue('alive', ++count);
    document.documentElement.setAttribute('data-close-alive', String(count));
});
document.documentElement.setAttribute('data-close-ready', 'yes');
'@
    $repository = $Context.UserScriptService.Repository
    $installation = Complete-UserScriptTask $Context ($repository.InstallAsync($source, $null, $true, [Threading.CancellationToken]::None)) 'install close fixture'
    $savedDialog = [CefFlashBrowser.Data.GlobalData]::Settings.DisableOnBeforeUnloadDialog
    try {
        foreach ($kind in @('Browser', 'Popup', 'Swf')) {
            if ($kind -eq 'Browser') {
                $window = New-RegressionWindow $Context
                $view = Find-BrowserView $window
            } else {
                $type = if ($kind -eq 'Popup') { [CefFlashBrowser.Views.PopupWebPage] } else { [CefFlashBrowser.Views.SwfPlayerWindow] }
                $window = New-AuxiliaryWindow $Context $type -WaitForLoad
                $view = $window.FindName('browserView')
            }
            try {
                $model = $view.ViewModel
                $model.Address = $Context.AuxiliaryAddress
                Wait-Regression $Context {
                    $model.Browser.Title -eq 'Auxiliary regression' -and
                    (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-close-ready')") -eq 'yes'
                } 'close-probe source ready'
                $session = $view.GetType().GetField('_userScriptSession', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($view)
                $scriptHost = $session.Host
                $handler = Set-RegressionBeforeUnload $Context $model.Browser
                $window.Close()
                Wait-Regression $Context {
                    $handler.Count -eq 1 -and $null -eq $view.GetType().GetField('_closeCompletion', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($view)
                } 'beforeunload rejection completes'
                Assert-Regression ($window.IsVisible -and [object]::ReferenceEquals($session.Host, $scriptHost) -and !$model.IsClosing) "$kind keeps its host after rejected close"
                [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('userscriptCloseProbe'))")
                Wait-Regression $Context {
                    (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-close-alive')") -eq '1'
                } 'bridge remains live after rejected close'
                $value = Complete-UserScriptTask $Context ($Context.UserScriptService.ValueStore.GetAsync($installation.ScriptKey.ToString(), 'alive', [Threading.CancellationToken]::None)) 'post-rejection GM commit'
                Assert-Regression ($value.Value.EncodedValue -eq 'n1') "$kind keeps GM storage live after rejected close"
                $handler.Accept = $true
                $window.Close()
                Wait-Regression $Context { !$window.IsVisible -and $model.Browser.IsDisposed } 'accepted close drains scripts and native browser'
                Assert-Regression ($session.IsStopped -and $null -eq $session.Host) "$kind stops scripts before native cleanup finishes"
            } finally { $window.ForceCloseWindow() }
        }
    } finally {
        [CefFlashBrowser.Data.GlobalData]::Settings.DisableOnBeforeUnloadDialog = $savedDialog
        Complete-UserScriptTask $Context ($repository.RemoveAsync($installation.ScriptKey, [Threading.CancellationToken]::None)) 'remove close fixture' | Out-Null
    }
}

function Test-UserScripts($Context) {
    Complete-UserScriptTask $Context ($Context.UserScriptService.InitializeAsync()) 'shared userscript initialization'
    $source = @'
// ==UserScript==
// @name DOM marker
// @match file:///*
// @grant none
// @run-at document-end
// ==/UserScript==
document.documentElement.setAttribute('data-userscript', 'ready');
'@
    $storage = @'
// ==UserScript==
// @name Isolated storage
// @match file:///*
// @sandbox DOM
// @grant GM_getValue
// @grant GM_setValue
// @run-at document-end
// ==/UserScript==
var previous = GM_getValue('visits', 0);
GM_setValue('visits', previous + 1);
document.documentElement.setAttribute('data-gm-before', String(previous));
'@
    $repository = $Context.UserScriptService.Repository
    $none = Complete-UserScriptTask $Context ($repository.InstallAsync($source, $null, $true, [Threading.CancellationToken]::None)) 'install DOM fixture'
    $gm = Complete-UserScriptTask $Context ($repository.InstallAsync($storage, $null, $true, [Threading.CancellationToken]::None)) 'install storage fixture'
    $window = New-Object CefFlashBrowser.Views.BrowserWindow
    $window.Width = 800
    $window.Height = 600
    $window.ShowActivated = $false
    $window.ShowInTaskbar = $false
    $model = $window.ViewModel.CurrentBrowser
    $model.Address = $Context.AuxiliaryAddress
    $identity = $model.BrowserId
    try {
        $window.Show()
        Wait-Regression $Context { $null -ne $model.Browser -and $model.Browser.Title -eq 'Auxiliary regression' } 'cold target navigation'
        Wait-Regression $Context { (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-gm-before')") -eq '0' } 'first-line synchronous GM snapshot'
        Assert-Regression ((Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-userscript')") -eq 'ready') 'grant-none source runs on the first target page'
        $view = Find-BrowserView $window
        $session = $view.GetType().GetField('_userScriptSession', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($view)
        Assert-Regression (!$session.Host.IsLateAttach) 'BrowserView attaches its host before native creation'
        Assert-Regression ($model.UserScriptState -eq [CefFlashBrowser.Models.UserScripts.UserScriptState]::Ready) 'BrowserView reports script readiness'
        Assert-Regression ($model.UserScriptMatchCount -eq 2) 'page status reports matched installations'
        $value = Complete-UserScriptTask $Context ($Context.UserScriptService.ValueStore.GetAsync($gm.ScriptKey.ToString(), 'visits', [Threading.CancellationToken]::None)) 'GM commit'
        Assert-Regression ($value.Value.EncodedValue -eq 'n1') 'legacy GM write reaches shared SQLite storage'
        [CefSharp.WebBrowserExtensions]::Reload($model.Browser, $false)
        Wait-Regression $Context { (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-gm-before')") -eq '1' } 'new-document storage bootstrap'
        Assert-Regression ($model.BrowserId -eq $identity) 'stable browser identity survives navigation'
    } finally {
        $window.ForceCloseWindow()
        Complete-UserScriptTask $Context ($repository.RemoveAsync($none.ScriptKey, [Threading.CancellationToken]::None)) 'remove DOM fixture' | Out-Null
        Complete-UserScriptTask $Context ($repository.RemoveAsync($gm.ScriptKey, [Threading.CancellationToken]::None)) 'remove storage fixture' | Out-Null
    }
}
