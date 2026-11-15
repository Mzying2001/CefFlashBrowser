function Complete-UserScriptTask($Context, $Task, [string]$Message) {
    Wait-Regression $Context { $Task.IsCompleted } $Message
    $result = $Task.GetAwaiter().GetResult()
    if ($null -ne $result -and $result.GetType().FullName -ne 'System.Threading.Tasks.VoidTaskResult') { return $result }
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
