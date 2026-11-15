function Complete-UserScriptTask($Context, $Task, [string]$Message) {
    Wait-Regression $Context { $Task.IsCompleted } $Message
    $result = $Task.GetAwaiter().GetResult()
    if ($null -ne $result -and $result.GetType().FullName -ne 'System.Threading.Tasks.VoidTaskResult') { return $result }
}

function Test-UserScriptTabs($Context) {
    Complete-UserScriptTask $Context ($Context.UserScriptService.InitializeAsync()) 'tab runtime'
    $source = @'
// ==UserScript==
// @name Tab lifecycle probe
// @match file:///*
// @noframes
// @sandbox DOM
// @grant GM.openInTab
// @grant GM.getTab
// @grant window.close
// ==/UserScript==
(async function() {
    var tab = await GM.getTab();
    var previous = tab.visits || 0;
    tab.visits = previous + 1;
    await GM.saveTab(tab);
    document.documentElement.setAttribute('data-tab-state-id', Object.keys(await GM.getTabs())[0]);
    document.documentElement.setAttribute('data-tab-state-before', String(previous));
    var child;
    document.addEventListener('userscriptOpenTab', async function() {
        child = await GM.openInTab('about:blank', {active:false, insert:true, setParent:true});
        child.onclose = function() { document.documentElement.setAttribute('data-child-closed', 'yes'); };
        document.documentElement.setAttribute('data-child-open', 'yes');
    });
    document.addEventListener('userscriptCloseTab', function() { child.close(); });
    document.addEventListener('userscriptCheckTab', function() {
        document.documentElement.setAttribute('data-child-state', String(child.closed));
    });
    document.addEventListener('userscriptCloseSelf', async function() {
        document.documentElement.setAttribute('data-self-result', String(await window.close()));
    });
    document.addEventListener('userscriptPrivateTab', async function() {
        try { await GM.openInTab('about:blank', {incognito:true}); }
        catch (error) { document.documentElement.setAttribute('data-private-denied', 'yes'); }
    });
})();
'@
    $repository = $Context.UserScriptService.Repository
    $installation = Complete-UserScriptTask $Context ($repository.InstallAsync($source, $null, $true, [Threading.CancellationToken]::None)) 'install tab fixture'
    $savedBehavior = [CefFlashBrowser.Data.GlobalData]::Settings.NewPageBehavior
    $window = $null
    $popup = $null
    try {
        [CefFlashBrowser.Data.GlobalData]::Settings.NewPageBehavior = [CefFlashBrowser.Models.NewPageBehavior]::OriginalWindow
        $window = New-RegressionWindow $Context
        $model = $window.ViewModel.CurrentBrowser
        $identity = $model.BrowserId
        $model.Address = $Context.AuxiliaryAddress
        Wait-Regression $Context { $model.Browser.Title -eq 'Auxiliary regression' -and
            (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-tab-state-before')") -eq '0' } 'initial tab-state snapshot'
        Assert-Regression ((Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-tab-state-id')") -eq $identity) 'GM tab state uses the stable host tab ID'
        [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('userscriptCloseSelf'))")
        Wait-Regression $Context { (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-self-result')") -eq 'undefined' } 'window.close preserves its void JavaScript facade'
        $view = Find-BrowserView $window
        $session = $view.GetType().GetField('_userScriptSession', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($view)
        $tabs = $session.GetType().GetField('_tabs', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($session)
        $accepted = Complete-UserScriptTask $Context ($tabs.CloseAsync($null, [Threading.CancellationToken]::None)) 'last-tab close result'
        Assert-Regression (!$accepted -and $window.IsVisible -and $window.ViewModel.Browsers.Count -eq 1) 'script window.close preserves the last browser'
        [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('userscriptPrivateTab'))")
        Wait-Regression $Context { (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-private-denied')") -eq 'yes' } 'private request is rejected'
        Assert-Regression ($window.ViewModel.Browsers.Count -eq 1) 'unsupported private tabs fail before creation'
        [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('userscriptOpenTab'))")
        Wait-Regression $Context { $window.ViewModel.Browsers.Count -eq 2 -and
            (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-child-open')") -eq 'yes' } 'GM opens a background child'
        $child = $window.ViewModel.Browsers[1]
        Assert-Regression ($window.ViewModel.CurrentBrowser -eq $model -and $child.Address -eq 'about:blank' -and $model.Address -eq $Context.AuxiliaryAddress) 'GM tab options preserve source selection and ignore OriginalWindow preference'
        Wait-Regression $Context { $null -ne $child.Browser -and $child.HasDocument -and !$child.Browser.IsLoading -and $child.Browser.CanExecuteJavascriptInMainFrame } 'child document ready'
        $handler = Set-RegressionBeforeUnload $Context $child.Browser
        [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('userscriptCloseTab'))")
        Wait-Regression $Context { $handler.Count -eq 1 } 'handle close requests confirmation'
        [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('userscriptCheckTab'))")
        Wait-Regression $Context { (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-child-state')") -eq 'false' } 'rejected handle close stays open'
        Assert-Regression (!$child.IsClosed -and $window.ViewModel.Browsers.Count -eq 2) 'handle state reflects rejected close'
        $handler.Accept = $true
        $window.ViewModel.CloseBrowser($child)
        Wait-Regression $Context { $child.IsClosed -and (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-child-closed')") -eq 'yes' } 'actual child close notifies its handle'
        Assert-Regression ($window.ViewModel.Browsers.Count -eq 1 -and $child.Browser.IsDisposed) 'tab handle closes only after accepted native cleanup'
        [CefSharp.WebBrowserExtensions]::Reload($model.Browser, $false)
        Wait-Regression $Context { (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-tab-state-before')") -eq '1' } 'tab state survives document navigation'
        Assert-Regression ($model.BrowserId -eq $identity) 'navigation keeps the tab-state partition identity'
        [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('userscriptOpenTab'))")
        Wait-Regression $Context { $window.ViewModel.Browsers.Count -eq 2 -and
            (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-child-open')") -eq 'yes' } 'open a child before source self-close'
        $remainingChild = $window.ViewModel.Browsers[1]
        [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('userscriptCloseSelf'))")
        Wait-Regression $Context { $model.IsClosed -and $window.ViewModel.Browsers.Count -eq 1 } 'source window.close does not block its own execution cleanup'
        Wait-Regression $Context { $null -ne $remainingChild.Browser -and $remainingChild.Browser.IsBrowserInitialized } 'remaining child initialization'
        Assert-Regression (!$remainingChild.IsClosed -and !$remainingChild.Browser.IsDisposed) 'disposing a source handle never closes its user tab'

        $window.ForceCloseWindow()
        $window = $null
        $rows = Complete-UserScriptTask $Context ($Context.Database.ScalarAsync('SELECT COUNT(*) FROM monkeysharp_tab_states')) 'closed tab-state rows'
        Assert-Regression ($rows -eq 0) 'closing the tab removes its state'

        $popup = New-AuxiliaryWindow $Context ([CefFlashBrowser.Views.PopupWebPage]) -WaitForLoad
        $popupModel = $popup.FindName('browserView').ViewModel
        Wait-Regression $Context { (Invoke-JavaScript $Context $popupModel.Browser "document.documentElement.getAttribute('data-tab-state-before')") -eq '0' } 'popup tab fixture ready'
        [void](Invoke-JavaScript $Context $popupModel.Browser "document.dispatchEvent(new Event('userscriptOpenTab'))")
        Wait-Regression $Context { @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.BrowserWindow] }).Count -eq 1 } 'auxiliary source opens a normal browser tab'
        $childWindow = @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.BrowserWindow] })[0]
        Assert-Regression ($popup.IsVisible -and $childWindow.ViewModel.CurrentBrowser.Address -eq 'about:blank') 'auxiliary source retains its page and returns a real normal tab'
        [void](Invoke-JavaScript $Context $popupModel.Browser "document.dispatchEvent(new Event('userscriptCloseTab'))")
        Wait-Regression $Context { !$childWindow.IsVisible -and (Invoke-JavaScript $Context $popupModel.Browser "document.documentElement.getAttribute('data-child-closed')") -eq 'yes' } 'auxiliary-source handle observes actual child-window close'
        $popup.ForceCloseWindow()
        $popup = $null
        $first = New-RegressionWindow $Context
        $second = New-RegressionWindow $Context
        try {
            $firstView = Find-BrowserView $first
            $secondView = Find-BrowserView $second
            $firstSession = $firstView.GetType().GetField('_userScriptSession', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($firstView)
            $secondSession = $secondView.GetType().GetField('_userScriptSession', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($secondView)
            $firstService = $firstSession.GetType().GetField('_tabs', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($firstSession)
            $secondService = $secondSession.GetType().GetField('_tabs', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($secondSession)
            $one = $firstService.CloseAsync($null, [Threading.CancellationToken]::None)
            $two = $secondService.CloseAsync($null, [Threading.CancellationToken]::None)
            $firstResult = Complete-UserScriptTask $Context $one 'first concurrent script close'
            $secondResult = Complete-UserScriptTask $Context $two 'second concurrent script close'
            Assert-Regression ($firstResult -and !$secondResult -and $second.IsVisible) 'concurrent script closes preserve the last available browser'
        } finally {
            $first.ForceCloseWindow()
            $second.ForceCloseWindow()
        }

    } finally {
        [CefFlashBrowser.Data.GlobalData]::Settings.NewPageBehavior = $savedBehavior
        if ($null -ne $window) { $window.ForceCloseWindow() }
        if ($null -ne $popup) { $popup.ForceCloseWindow() }
        foreach ($created in @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.BrowserWindow] })) { $created.ForceCloseWindow() }
        Complete-UserScriptTask $Context ($repository.RemoveAsync($installation.ScriptKey, [Threading.CancellationToken]::None)) 'remove tab fixture' | Out-Null
    }
}

function Test-UserScriptMenus($Context) {
    Complete-UserScriptTask $Context ($Context.UserScriptService.InitializeAsync()) 'menu runtime'
    $repository = $Context.UserScriptService.Repository
    $installed = New-Object 'System.Collections.Generic.List[Mzying2001.MonkeySharp.Core.Domain.ScriptKey]'
    $savedDisabled = [CefFlashBrowser.Data.GlobalData]::Settings.DisableBrowserContextMenu
    $window = $null
    try {
        for ($index = 0; $index -lt 5; $index++) {
            $source = "// ==UserScript==`n// @name Manual $index`n// @match file:///*`n// @run-at context-menu`n// @grant none`n// ==/UserScript==`ndocument.documentElement.setAttribute('data-manual-$index','yes');"
            $item = Complete-UserScriptTask $Context ($repository.InstallAsync($source, $null, $true, [Threading.CancellationToken]::None)) 'install manual menu fixture'
            $installed.Add($item.ScriptKey)
        }
        $source = @'
// ==UserScript==
// @name Runtime menu
// @match file:///*
// @sandbox DOM
// @grant GM.registerMenuCommand
// ==/UserScript==
(async function() {
    var count = 0;
    await GM.registerMenuCommand('Runtime command', function() {
        document.documentElement.setAttribute('data-runtime-menu', String(++count));
    });
    await GM.registerMenuCommand('Frame command', function() {
        document.documentElement.setAttribute('data-frame-menu', 'yes');
    }, {clickedFrame:true});
    document.documentElement.setAttribute('data-runtime-menu-ready', 'yes');
})();
'@
        $item = Complete-UserScriptTask $Context ($repository.InstallAsync($source, $null, $true, [Threading.CancellationToken]::None)) 'install runtime menu fixture'
        $installed.Add($item.ScriptKey)
        $window = New-RegressionWindow $Context
        $model = $window.ViewModel.CurrentBrowser
        $model.Address = $Context.AuxiliaryAddress
        Wait-Regression $Context {
            $model.UserScriptMenus.Count -eq 2 -and $model.Browser.Title -eq 'Auxiliary regression' -and
            (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-runtime-menu-ready')") -eq 'yes'
        } 'runtime menu registration'
        [CefFlashBrowser.Data.GlobalData]::Settings.DisableBrowserContextMenu = $false
        $menu = Complete-UserScriptTask $Context ([UserScriptMenuProbe]::BuildAsync($model.Browser)) 'build native script menu'
        $ids = @($menu.Commands | Where-Object { $_ -ne [CefSharp.CefMenuCommand]::NotFound })
        Assert-Regression (@($ids | Sort-Object -Unique).Count -eq $ids.Count) 'manual, dynamic and application menu IDs are unique'
        Assert-Regression ($ids -contains [CefFlashBrowser.Browser.Handlers.ContextMenuHandler]::Search -and
            $ids -contains [CefFlashBrowser.Browser.Handlers.ContextMenuHandler]::OpenInNewTab) 'script menus retain application search and new-tab commands'
        for ($index = 0; $index -lt 5; $index++) {
            $command = [CefSharp.CefMenuCommand]([int][CefSharp.CefMenuCommand]::UserFirst + $index)
            Assert-Regression ($ids -contains $command) "manual source $index has a native command"
            $handled = Complete-UserScriptTask $Context ([UserScriptMenuProbe]::InvokeAsync($model.Browser, $command)) 'invoke manual source'
            Wait-Regression $Context { (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-manual-$index')") -eq 'yes' } 'manual source executes'
            Assert-Regression ($handled -and $window.ViewModel.Browsers.Count -eq 1) "manual source $index cannot trigger an application command"
        }
        Invoke-BrowserCommand $model.UserScriptMenus[0].InvokeCommand
        Wait-Regression $Context { (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-runtime-menu')") -eq '1' } 'toolbar GM command callback'
        Invoke-BrowserCommand $model.UserScriptMenus[1].InvokeCommand
        Assert-Regression ($null -eq (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-frame-menu')")) 'toolbar invocation cannot attest a clicked frame'
        $dynamicId = [CefSharp.CefMenuCommand]([int][CefSharp.CefMenuCommand]::UserFirst + [CefFlashBrowser.Browser.UserScripts.UserScriptContextMenuHandler]::ManualLimit)
        [void](Complete-UserScriptTask $Context ([UserScriptMenuProbe]::InvokeAsync($model.Browser, $dynamicId)) 'invoke native GM command')
        Wait-Regression $Context { (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-runtime-menu')") -eq '2' } 'native GM command callback'
        [void](Complete-UserScriptTask $Context ([UserScriptMenuProbe]::InvokeAsync($model.Browser, [CefSharp.CefMenuCommand]([int]$dynamicId + 1))) 'invoke frame-attested GM menu')
        Wait-Regression $Context { (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-frame-menu')") -eq 'yes' } 'native frame attests the isolated menu subscriber'

        $overflow = Complete-UserScriptTask $Context ([UserScriptMenuProbe]::BuildOverflowAsync($model.Browser)) 'bounded oversized script menu'
        $overflowIds = @($overflow.Commands | Where-Object { $_ -ne [CefSharp.CefMenuCommand]::NotFound })
        Assert-Regression (@($overflowIds | Sort-Object -Unique).Count -eq $overflowIds.Count -and
            $overflowIds -contains [CefFlashBrowser.Browser.Handlers.ContextMenuHandler]::Search -and
            @($overflowIds | Where-Object { [int]$_ -ge [int][CefSharp.CefMenuCommand]::UserFirst -and [int]$_ -lt ([int][CefSharp.CefMenuCommand]::UserFirst + 1024) }).Count -eq 1024) 'oversized manual menus preserve reserved IDs and enforce their cap'
        Complete-UserScriptTask $Context ([UserScriptMenuProbe]::DismissAsync($model.Browser)) 'dismiss script menu'
        [CefFlashBrowser.Data.GlobalData]::Settings.DisableBrowserContextMenu = $true
        $disabled = Complete-UserScriptTask $Context ([UserScriptMenuProbe]::BuildAsync($model.Browser)) 'disabled native script menu'
        Assert-Regression ($disabled.Commands.Count -eq 0) 'disabled browser context menu also removes library and GM entries'
        $oldCommand = $model.UserScriptMenus[0]
        Complete-UserScriptTask $Context ($repository.SetEnabledAsync($installed[5], $false, [Threading.CancellationToken]::None)) 'disable runtime menu script' | Out-Null
        Wait-Regression $Context { $model.UserScriptMenus.Count -eq 0 } 'disabled execution unregisters GM menus'
        Invoke-BrowserCommand $oldCommand.InvokeCommand
        Assert-Regression ((Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-runtime-menu')") -eq '2') 'released menu commands cannot invoke an old execution'

    } finally {
        [CefFlashBrowser.Data.GlobalData]::Settings.DisableBrowserContextMenu = $savedDisabled
        if ($null -ne $window) { $window.ForceCloseWindow() }
        foreach ($key in $installed) {
            Complete-UserScriptTask $Context ($repository.RemoveAsync($key, [Threading.CancellationToken]::None)) 'remove menu fixture' | Out-Null
        }
    }
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
