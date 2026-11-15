function Complete-UserScriptTask($Context, $Task, [string]$Message) {
    Wait-Regression $Context { $Task.IsCompleted } $Message
    $result = $Task.GetAwaiter().GetResult()
    if ($null -ne $result -and $result.GetType().FullName -ne 'System.Threading.Tasks.VoidTaskResult') { return $result }
}

function Test-UserScriptDownloadsAndAudio($Context) {
    Complete-UserScriptTask $Context ($Context.UserScriptService.InitializeAsync()) 'download runtime'
    $fixture = New-Object UserScriptHttpFixture
    $directory = [CefFlashBrowser.Data.GlobalData]::Settings.UserScriptDownloadDirectory
    $source = @'
// ==UserScript==
// @name Download and audio probe
// @match file:///*
// @noframes
// @sandbox DOM
// @grant GM.download
// @grant GM.audio
// ==/UserScript==
(async function() {
    var pending;
    function start(path, name, timeout) {
        document.documentElement.setAttribute('data-download-state', 'pending');
        var options = {url:'__BASE__' + path, name:name};
        if (timeout) options.timeout = timeout;
        pending = GM.download(options);
        pending.then(function() { document.documentElement.setAttribute('data-download-state', 'completed'); },
            function(error) { document.documentElement.setAttribute('data-download-state', error.name); });
    }
    document.addEventListener('downloadSimple', function() { start('/download', 'actual.bin'); });
    document.addEventListener('downloadFailed', function() { start('/failed', 'failed.bin'); });
    document.addEventListener('downloadBlob', function() {
        document.documentElement.setAttribute('data-download-state', 'pending');
        var url = URL.createObjectURL(new Blob([new Uint8Array([0,1,2,3,127,128,254,255,42,17,9,0])]));
        pending = GM.download({url:url, name:'blob.bin'});
        pending.then(function() { URL.revokeObjectURL(url); document.documentElement.setAttribute('data-download-state', 'completed'); }, function(error) { document.documentElement.setAttribute('data-blob-error', (error.code || '') + ': ' + error.details); });
    });
    document.addEventListener('downloadSaveAs', function() {
        document.documentElement.setAttribute('data-download-state', 'pending');
        pending = GM.download({url:'__BASE__/download', name:'saveas.bin', saveAs:true});
        pending.catch(function(error) { document.documentElement.setAttribute('data-download-state', error.name); });
        var saveOperation = pending;
        setTimeout(function() { saveOperation.abort(); }, 5000);
    });
    document.addEventListener('downloadUiCancel', function() { start('/slow-ui-cancel', 'ui-cancel.bin'); });

    document.addEventListener('downloadAbort', function() { start('/slow-abort', 'abort.bin'); });
    document.addEventListener('downloadCancel', function() { pending.abort(); });
    document.addEventListener('downloadTimeout', function() { start('/slow-timeout', 'timeout.bin', 250); });
    document.addEventListener('downloadNavigation', function() { start('/slow-navigation', 'navigation.bin'); });
    document.addEventListener('downloadRetire', function() { start('/slow-retire', 'retired.bin'); });
    document.addEventListener('downloadClose', function() { start('/slow-close', 'close.bin'); });
    document.addEventListener('audioMute', async function() {
        await GM.audio.setMute({isMuted:true});
        document.documentElement.setAttribute('data-audio-set', 'yes');
    });
    document.addEventListener('audioRead', async function() {
        document.documentElement.setAttribute('data-audio-read', String((await GM.audio.getState()).isMuted));
    });
    await GM.audio.addStateChangeListener(function(state) {
        document.documentElement.setAttribute('data-audio-change', String(state.muted));
    });
    document.documentElement.setAttribute('data-download-ready', 'yes');
})();
'@
    $source = $source.Replace('__BASE__', $fixture.BaseUrl)
    $repository = $Context.UserScriptService.Repository
    $installation = Complete-UserScriptTask $Context ($repository.InstallAsync($source, $null, $true, [Threading.CancellationToken]::None)) 'install download fixture'
    $window = $null
    try {
        $window = New-RegressionWindow $Context
        $model = $window.ViewModel.CurrentBrowser
        $model.Address = $Context.AuxiliaryAddress
        Wait-Regression $Context { $model.Browser.Title -eq 'Auxiliary regression' -and
            (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-download-ready')") -eq 'yes' } 'download source ready'
        $view = Find-BrowserView $window
        $session = $view.GetType().GetField('_userScriptSession', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($view)
        $downloads = $session.GetType().GetField('_downloads', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($session)
        $probe = New-Object UserScriptDownloadProbe($downloads, $installation.ScriptKey, ($fixture.BaseUrl + '/download'))
        Assert-Regression ($fixture.RequestsFor('/download') -eq 0 -and !$probe.Operation.Completion.IsCompleted -and $model.UserScriptDownloads.Count -eq 0) 'download creation starts neither UI nor network work'
        $probe.Operation.Start()
        $probe.Operation.Start()
        Complete-UserScriptTask $Context $probe.Operation.Completion 'unstarted download commit'
        $probe.Operation.Abort()
        Assert-Regression ($probe.Completed -eq 1 -and $probe.Aborted -eq 0 -and $probe.Failed -eq 0 -and $fixture.RequestsFor('/download') -eq 1) 'download terminal event is published once after commit'
        Assert-Regression ([Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $directory 'contract.bin'))) -eq [Convert]::ToBase64String($fixture.Payload)) 'CEF download commits the exact response bytes'

        [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('audioMute'))")
        Wait-Regression $Context { $model.IsMuted -and (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-audio-set')") -eq 'yes' } 'GM mute updates tab UI'
        Assert-BrowserAudioMuted $Context $model.Browser $true 'GM mute matches the native CEF state'
        $model.ToggleMute()
        Wait-Regression $Context { !$model.IsMuted } 'UI mute toggle reads back native state'
        [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('audioRead'))")
        Wait-Regression $Context { (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-audio-read')") -eq 'false' -and
            (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-audio-change')") -eq 'false' } 'GM observes UI mute changes'
        Assert-BrowserAudioMuted $Context $model.Browser $false 'UI mute readback matches native CEF'

        [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('downloadSimple'))")
        Wait-Regression $Context { @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.Dialogs.UserScriptPermissionDialog] }).Count -eq 1 } 'download permission prompt'
        $permission = @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.Dialogs.UserScriptPermissionDialog] })[0]
        $permission.FindName('allowRememberButton').RaiseEvent((New-Object System.Windows.RoutedEventArgs([System.Windows.Controls.Button]::ClickEvent)))
        Wait-Regression $Context { (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-download-state')") -eq 'completed' } 'modern download completion acknowledgement'
        Assert-Regression ([Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $directory 'actual.bin'))) -eq [Convert]::ToBase64String($fixture.Payload)) 'modern GM download resolves after bytes are committed'
        [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('downloadSimple'))")
        Wait-Regression $Context { [IO.File]::Exists((Join-Path $directory 'actual (1).bin')) -and
            (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-download-state')") -eq 'completed' } 'default collision uniquifies the file'
        Assert-Regression ([IO.File]::Exists((Join-Path $directory 'actual.bin'))) 'uniquify preserves the existing download'
        [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('downloadBlob'))")
        Wait-Regression $Context {
            $blobPrompt = @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.Dialogs.UserScriptPermissionDialog] })
            if ($blobPrompt.Count -eq 1) {
                $blobPrompt[0].FindName('allowRememberButton').RaiseEvent((New-Object System.Windows.RoutedEventArgs([System.Windows.Controls.Button]::ClickEvent)))
            }
            $errorText = Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-blob-error')"
            if ($errorText) { throw $errorText }
            [IO.File]::Exists((Join-Path $directory 'blob.bin')) -and
            (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-download-state')") -eq 'completed'
        } 'renderer Blob download is committed'
        Assert-Regression ([Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $directory 'blob.bin'))) -eq [Convert]::ToBase64String($fixture.Payload)) 'Blob download preserves staged bytes'
        $picker = New-Object UserScriptSaveDialogProbe($model.Browser, $Context.LanguageService.GetString('userscript_saveDownload'))
        try {
            [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('downloadSaveAs'))")
            Wait-Regression $Context { $model.UserScriptDownloads[$model.UserScriptDownloads.Count - 1].FileName -eq 'saveas.bin' -and
                $model.UserScriptDownloads[$model.UserScriptDownloads.Count - 1].State -eq [CefFlashBrowser.ViewModels.UserScriptDownloadState]::Cancelled } 'abort closes the native save picker'
            Assert-Regression ($picker.Seen -and ![IO.File]::Exists((Join-Path $directory 'saveas.bin'))) 'saveAs starts native UI and aborts without committing'
        } finally { $picker.Dispose() }
        [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('downloadUiCancel'))")
        Wait-Regression $Context { $fixture.RequestsFor('/slow-ui-cancel') -eq 1 -and $model.UserScriptDownloads[$model.UserScriptDownloads.Count - 1].FileName -eq 'ui-cancel.bin' } 'download history exposes an active cancel action'
        $cancelItem = $model.UserScriptDownloads[$model.UserScriptDownloads.Count - 1]
        Invoke-BrowserCommand $cancelItem.CancelCommand
        Wait-Regression $Context { $cancelItem.State -eq [CefFlashBrowser.ViewModels.UserScriptDownloadState]::Cancelled } 'download UI cancels the accepted operation'


        [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('downloadFailed'))")
        Wait-Regression $Context { $model.UserScriptDownloads[$model.UserScriptDownloads.Count - 1].State -eq [CefFlashBrowser.ViewModels.UserScriptDownloadState]::Failed } 'failed response reaches terminal UI state'
        Assert-Regression (![IO.File]::Exists((Join-Path $directory 'failed.bin'))) 'failed download does not commit a file'
        [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('downloadAbort'))")
        Wait-Regression $Context { $fixture.RequestsFor('/slow-abort') -eq 1 } 'abort fixture starts transfer'
        [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('downloadCancel'))")
        Wait-Regression $Context { $model.UserScriptDownloads[$model.UserScriptDownloads.Count - 1].State -eq [CefFlashBrowser.ViewModels.UserScriptDownloadState]::Cancelled } 'explicit abort reaches terminal UI state'
        Assert-Regression (![IO.File]::Exists((Join-Path $directory 'abort.bin'))) 'aborted download does not commit a file'
        [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('downloadTimeout'))")
        Wait-Regression $Context { $model.UserScriptDownloads[$model.UserScriptDownloads.Count - 1].State -eq [CefFlashBrowser.ViewModels.UserScriptDownloadState]::TimedOut } 'timeout reaches terminal UI state'
        Assert-Regression (![IO.File]::Exists((Join-Path $directory 'timeout.bin'))) 'timed-out download does not commit a file'

        [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('downloadNavigation'))")
        Wait-Regression $Context { $fixture.RequestsFor('/slow-navigation') -eq 1 } 'accepted transfer before navigation'
        $model.Browser.Load('about:blank')
        Wait-Regression $Context { $model.Address -eq 'about:blank' -and $model.Browser.CanExecuteJavascriptInMainFrame } 'source navigates while accepted download runs'
        $fixture.Release('/slow-navigation')
        Wait-Regression $Context { [IO.File]::Exists((Join-Path $directory 'navigation.bin')) } 'accepted download completes after document navigation'
        Assert-Regression ([Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $directory 'navigation.bin'))) -eq [Convert]::ToBase64String($fixture.Payload)) 'navigation retains the accepted transfer and its network context'

        $model.Address = $Context.AuxiliaryAddress
        Wait-Regression $Context { $model.Browser.Title -eq 'Auxiliary regression' -and
            (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-download-ready')") -eq 'yes' } 'download source reloads'
        [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('downloadRetire'))")
        Wait-Regression $Context { $fixture.RequestsFor('/slow-retire') -eq 1 } 'accepted transfer before host replacement'
        $model.Address = 'view-source:' + $Context.AuxiliaryAddress
        Assert-ViewSourcePage $Context $model ('view-source:' + $Context.AuxiliaryAddress)
        $fixture.Release('/slow-retire')
        Wait-Regression $Context { [IO.File]::Exists((Join-Path $directory 'retired.bin')) } 'retired host drains its accepted transfer'
        Assert-Regression ([Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $directory 'retired.bin'))) -eq [Convert]::ToBase64String($fixture.Payload)) 'host replacement keeps accepted download bytes intact'

        $model.Address = $Context.AuxiliaryAddress
        Wait-Regression $Context { $model.Browser.Title -eq 'Auxiliary regression' -and
            (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-download-ready')") -eq 'yes' } 'source returns after host replacement'
        [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('downloadClose'))")
        Wait-Regression $Context { $fixture.RequestsFor('/slow-close') -eq 1 } 'pending transfer before close'
        $window.ForceCloseWindow()
        Assert-Regression (![IO.File]::Exists((Join-Path $directory 'close.bin')) -and @(Get-ChildItem -LiteralPath $directory -Filter '*.part').Count -eq 0) 'tab close cancels transfers and drains partial-file cleanup'
    } finally {
        if ($null -ne $window) { $window.ForceCloseWindow() }
        $fixture.Dispose()
        Complete-UserScriptTask $Context ($repository.RemoveAsync($installation.ScriptKey, [Threading.CancellationToken]::None)) 'remove download fixture' | Out-Null
    }
}

function Find-UserScriptNotification($Context, [string]$Title) {
    return @($Context.Application.Windows | Where-Object {
        $_ -is [CefFlashBrowser.Views.Dialogs.UserScriptNotificationWindow] -and $_.Title -eq $Title -and $_.IsVisible
    })[0]
}

function Invoke-UserScriptNotificationButton($Window, [string]$Name) {
    $Window.FindName($Name).RaiseEvent((New-Object System.Windows.RoutedEventArgs([System.Windows.Controls.Button]::ClickEvent)))
}

function Test-UserScriptInteractions($Context) {
    Complete-UserScriptTask $Context ($Context.UserScriptService.InitializeAsync()) 'interaction runtime'
    $fixture = New-Object FaviconHttpFixture
    $source = @'
// ==UserScript==
// @name Native interaction probe
// @match file:///*
// @noframes
// @sandbox DOM
// @grant GM.setClipboard
// @grant GM.notification
// ==/UserScript==
document.addEventListener('userscriptClipboardText', async function() {
    await GM.setClipboard('\u7528\u6237\u811a\u672c \u2713', 'text');
    document.documentElement.setAttribute('data-clipboard', 'text');
});
document.addEventListener('userscriptClipboardHtml', async function() {
    await GM.setClipboard('\u4e2d\u6587<b>\u03c0</b>', {type:'html'});
    document.documentElement.setAttribute('data-clipboard', 'html');
});
document.addEventListener('userscriptNotifyPrevent', function() {
    GM.notification({title:'Prevented notice', text:'Click acknowledgement fixture', silent:true, image:'__IMAGE_URL__',
        url:'__DEFAULT_URL__', onclick:function(event) {
            event.preventDefault();
            document.documentElement.setAttribute('data-notice-clicked', 'yes');
        }}).then(function() { document.documentElement.setAttribute('data-notice-done', 'yes'); });
});
document.addEventListener('userscriptNotifyDefault', function() {
    GM.notification({title:'Default notice', text:'Default navigation fixture', silent:true, url:'__DEFAULT_URL__'})
        .then(function() { document.documentElement.setAttribute('data-default-notice-done', 'yes'); });
});
document.addEventListener('userscriptNotifyTag', function() {
    GM.notification({title:'Tag first', text:'First tag fixture', tag:'replace', silent:true})
        .then(function() { document.documentElement.setAttribute('data-tag-first-done', 'yes'); });
    GM.notification({title:'Tag second', text:'Second tag fixture', tag:'replace', silent:true})
        .then(function() { document.documentElement.setAttribute('data-tag-second-done', 'yes'); });
});
document.addEventListener('userscriptNotifyPending', function() {
    GM.notification({title:'Pending notice', text:'Close cancellation fixture', silent:true, image:'__SLOW_IMAGE_URL__'});
});
document.addEventListener('userscriptNotifyTimeout', function() {
    GM.notification({title:'Timed notice', text:'Timeout fixture', silent:true, timeout:250})
        .then(function() { document.documentElement.setAttribute('data-timeout-notice-done', 'yes'); });
});
document.documentElement.setAttribute('data-interaction-ready', 'yes');
'@
    $source = $source.Replace('__DEFAULT_URL__', $fixture.BaseUrl + '/a').Replace('__IMAGE_URL__', $fixture.BaseUrl + '/a-icon.png').Replace('__SLOW_IMAGE_URL__', $fixture.BaseUrl + '/slow-bad.png')
    $repository = $Context.UserScriptService.Repository
    $installation = Complete-UserScriptTask $Context ($repository.InstallAsync($source, $null, $true, [Threading.CancellationToken]::None)) 'install interaction fixture'
    $window = $null
    $clipboard = $null
    $clipboardCaptured = $false
    try {
        $window = New-RegressionWindow $Context
        $model = $window.ViewModel.CurrentBrowser
        $model.Address = $Context.AuxiliaryAddress
        Wait-Regression $Context { $model.Browser.Title -eq 'Auxiliary regression' -and
            (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-interaction-ready')") -eq 'yes' } 'native interaction fixture ready'
        # Snapshot privately in memory; never print the user's clipboard contents.
        $clipboard = [UserScriptClipboardProbe]::Capture()
        $clipboardCaptured = $true
        [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('userscriptClipboardText'))")
        Wait-Regression $Context { (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-clipboard')") -eq 'text' } 'text clipboard commit'
        $expectedText = -join ([char[]]@(0x7528,0x6237,0x811a,0x672c,0x20,0x2713))
        Assert-Regression ([System.Windows.Clipboard]::GetText() -eq $expectedText) 'GM clipboard writes real Unicode text'
        [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('userscriptClipboardHtml'))")
        Wait-Regression $Context { (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-clipboard')") -eq 'html' } 'HTML clipboard commit'
        $html = [string][System.Windows.Clipboard]::GetData([System.Windows.DataFormats]::Html)
        $bytes = [Text.Encoding]::UTF8.GetBytes($html)
        $start = [int][regex]::Match($html, 'StartFragment:(\d+)').Groups[1].Value
        $end = [int][regex]::Match($html, 'EndFragment:(\d+)').Groups[1].Value
        $expectedHtml = (-join ([char[]]@(0x4e2d,0x6587))) + '<b>' + [char]0x03c0 + '</b>'
        Assert-Regression ([Text.Encoding]::UTF8.GetString($bytes, $start, $end - $start) -eq $expectedHtml) 'CF_HTML offsets identify the exact UTF-8 fragment'

        $view = Find-BrowserView $window
        $session = $view.GetType().GetField('_userScriptSession', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($view)
        $notifications = $session.GetType().GetField('_notifications', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($session)
        $probe = New-Object UserScriptNotificationProbe($notifications, $installation.ScriptKey)
        Assert-Regression (!$probe.Handle.Completion.IsCompleted -and $probe.Closes -eq 0 -and $null -eq (Find-UserScriptNotification $Context 'Unstarted notification probe')) 'notification creation has no UI or terminal event before Start'
        $probe.Handle.Start()
        $probe.Handle.Start()
        Wait-Regression $Context { $null -ne (Find-UserScriptNotification $Context 'Unstarted notification probe') } 'unstarted notification begins on Start'
        $probe.Handle.Dispose()
        $probe.Handle.Dispose()
        Complete-UserScriptTask $Context $probe.Handle.Completion 'notification terminal completion'
        Assert-Regression ($probe.Closes -eq 1) 'notification disposal publishes one closed event'

        [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('userscriptNotifyPrevent'))")
        Wait-Regression $Context { $null -ne (Find-UserScriptNotification $Context 'Prevented notice') } 'native notification is shown'
        Assert-Regression $window.IsEnabled 'native notification is non-modal'
        Wait-Regression $Context { (Find-UserScriptNotification $Context 'Prevented notice').FindName('notificationImage').Visibility -eq [System.Windows.Visibility]::Visible } 'bounded notification image content is displayed'

        Invoke-UserScriptNotificationButton (Find-UserScriptNotification $Context 'Prevented notice') 'clickButton'
        Wait-Regression $Context { (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-notice-done')") -eq 'yes' } 'notification click acknowledgement completes'
        Assert-Regression ((Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-notice-clicked')") -eq 'yes' -and $window.ViewModel.Browsers.Count -eq 1) 'preventDefault blocks the notification URL action'

        [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('userscriptNotifyDefault'))")
        Wait-Regression $Context { $null -ne (Find-UserScriptNotification $Context 'Default notice') } 'default-action notification is shown'
        Invoke-UserScriptNotificationButton (Find-UserScriptNotification $Context 'Default notice') 'clickButton'
        Wait-Regression $Context { $window.ViewModel.Browsers.Count -eq 2 -and
            (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-default-notice-done')") -eq 'yes' } 'notification default action opens a tab'
        $child = $window.ViewModel.CurrentBrowser
        Assert-Regression ($child -ne $model -and $child.Address -eq $fixture.BaseUrl + '/a') 'notification URL uses its real source window'
        $window.ViewModel.CloseBrowser($child)
        Wait-Regression $Context { $child.IsClosed -and $window.ViewModel.CurrentBrowser -eq $model } 'notification child closes and returns to source'

        [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('userscriptNotifyTag'))")
        Wait-Regression $Context { $null -ne (Find-UserScriptNotification $Context 'Tag second') -and
            (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-tag-first-done')") -eq 'yes' } 'tag replacement completes the first notification'
        Assert-Regression ($null -eq (Find-UserScriptNotification $Context 'Tag first')) 'script-scoped tags replace the previous notification'
        Invoke-UserScriptNotificationButton (Find-UserScriptNotification $Context 'Tag second') 'closeButton'
        Wait-Regression $Context { (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-tag-second-done')") -eq 'yes' } 'native close completes notification promise'
        [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('userscriptNotifyTimeout'))")
        Wait-Regression $Context { (Invoke-JavaScript $Context $model.Browser "document.documentElement.getAttribute('data-timeout-notice-done')") -eq 'yes' } 'notification timeout completes its promise'
        Assert-Regression ($null -eq (Find-UserScriptNotification $Context 'Timed notice')) 'notification timeout closes native UI'
        [void](Invoke-JavaScript $Context $model.Browser "document.dispatchEvent(new Event('userscriptNotifyPending'))")
        Wait-Regression $Context { $null -ne (Find-UserScriptNotification $Context 'Pending notice') } 'pending notification before close'
        Wait-Regression $Context { $fixture.SlowRequests -gt 0 } 'notification image fetch is pending before shutdown'
        $window.ForceCloseWindow()
        Assert-Regression (@($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.Dialogs.UserScriptNotificationWindow] }).Count -eq 0) 'source shutdown closes and drains notification UI'
    } finally {
        if ($null -ne $window) { $window.ForceCloseWindow() }
        if ($clipboardCaptured) { [UserScriptClipboardProbe]::Restore($clipboard) }
        $fixture.Dispose()
        Complete-UserScriptTask $Context ($repository.RemoveAsync($installation.ScriptKey, [Threading.CancellationToken]::None)) 'remove interaction fixture' | Out-Null
    }
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
