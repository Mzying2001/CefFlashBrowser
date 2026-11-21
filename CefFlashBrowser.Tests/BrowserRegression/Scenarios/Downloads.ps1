function Test-DownloadManagerWindow($Context) {
    [CefFlashBrowser.Services.WindowManager]::ShowDownloadManager()
    $window = @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.DownloadManagerWindow] })[0]
    Assert-Regression ($null -ne $window -and $window.IsVisible) 'download manager opens through the global command'
    Invoke-BrowserCommand ([CefFlashBrowser.Services.WindowManager]::OpenDownloadsCommand)
    Assert-Regression (@($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.DownloadManagerWindow] }).Count -eq 1) 'download manager entry reuses its single window'
    $manager = [CefFlashBrowser.Services.Downloads.DownloadManagerService]::Current
    $record = New-Object CefFlashBrowser.Models.Downloads.DownloadRecord
    $record.Source = 'manual'
    $record.Url = 'https://example.test/download'
    $record.FileName = 'example.bin'
    $record.FilePath = Join-Path $Context.TemporaryPath 'example.bin'
    $record.State = [CefFlashBrowser.Models.Downloads.DownloadState]::Completed
    $record.Received = 3
    $record.Total = 3
    [IO.File]::WriteAllBytes($record.FilePath, [byte[]]@(1,2,3))
    $transfer = New-Object CefFlashBrowser.Services.Downloads.HttpDownloadTransfer($record, $null, $null)
    $manager.Accept($transfer)
    $window.DataContext.SelectedItem = $manager.Items[0]
    $manager.Refresh()
    Assert-Regression ($window.DataContext.SelectedItem.CanOpen) 'completed download enables opening its file'
    $window.UpdateLayout()
    $bitmap = New-Object Windows.Media.Imaging.RenderTargetBitmap([int]$window.Content.ActualWidth, [int]$window.Content.ActualHeight, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($window.Content)
    $encoder = New-Object Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.File]::Create((Join-Path $Context.TemporaryPath 'DownloadManager.png'))
    try {
        $encoder.Save($stream)
    } finally {
        $stream.Dispose()
    }

    [IO.File]::Delete($record.FilePath)
    $manager.Refresh()
    Wait-Regression $Context { $record.FileMissing } 'refresh detects the deleted file asynchronously'
    Assert-Regression ($record.FileMissing -and !$window.DataContext.SelectedItem.CanOpen) 'externally deleted file updates its state and open action'
    $window.Close()
    [CefFlashBrowser.Services.WindowManager]::ShowDownloadManager()
    $window = @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.DownloadManagerWindow] })[0]
    Assert-Regression ($window.FindName('downloadList').Items.Count -eq 1) 'closing the download window retains application history'
    $window.Close()
    Complete-UserScriptTask $Context ($manager.RemoveAsync($manager.Items[0])) 'remove shell regression record'

    $directory = [CefFlashBrowser.Data.GlobalData]::Settings.DownloadDirectory
    try {
        [CefFlashBrowser.Data.GlobalData]::Settings.DownloadDirectory = 'relative\invalid'
        $dialogRecord = New-Object CefFlashBrowser.Models.Downloads.DownloadRecord
        $dialogRecord.Source = 'manual'
        $dialogRecord.Url = 'https://example.test/test.bin'
        $dialogRecord.FileName = 'test.bin'
        $dialog = New-Object CefFlashBrowser.Views.Dialogs.DownloadDialog
        $dialog.Initialize($dialogRecord)
        Assert-Regression ([IO.Path]::GetDirectoryName($dialog.FindName('pathBox').Text) -eq [CefFlashBrowser.Utils.UserScriptDownloadPaths]::DefaultDirectory()) 'invalid configured folder falls back to system Downloads'
        $chosen = Join-Path $Context.TemporaryPath 'Remembered\chosen.bin'
        $dialog.FindName('pathBox').Text = $chosen
        $dialog.FindName('rememberBox').IsChecked = $true
        $dialog.FindName('downloadButton').RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
        Assert-Regression ($dialogRecord.FilePath -eq $chosen -and [CefFlashBrowser.Data.GlobalData]::Settings.DownloadDirectory -eq [IO.Path]::GetDirectoryName($chosen)) 'confirmation remembers only the selected folder'
        $saved = Get-Content -LiteralPath ([CefFlashBrowser.Data.GlobalData]::SettingsPath) -Raw | ConvertFrom-Json
        Assert-Regression ($saved.DownloadDirectory -eq [IO.Path]::GetDirectoryName($chosen)) 'remembered folder is persisted in settings'
        $nextDialog = New-Object CefFlashBrowser.Views.Dialogs.DownloadDialog
        $nextDialog.Initialize($dialogRecord)
        Assert-Regression ([IO.Path]::GetDirectoryName($nextDialog.FindName('pathBox').Text) -eq [IO.Path]::GetDirectoryName($chosen)) 'next confirmation uses the remembered folder'
        $nextDialog.Close()
    } finally {
        [CefFlashBrowser.Data.GlobalData]::Settings.DownloadDirectory = $directory
        [void][CefFlashBrowser.Data.GlobalData]::SaveSettings()
    }
}

function Test-DownloadRestart($Context) {
    $fixture = New-Object DownloadHttpFixture
    $profile = Join-Path $Context.TemporaryPath 'RestartProfile'
    [void][IO.Directory]::CreateDirectory($profile)
    [IO.File]::WriteAllBytes((Join-Path $profile 'payload.bin'), $fixture.Payload)
    try {
        foreach ($phase in @('Save', 'Resume')) {
            $arguments = @('-NoProfile', '-STA', '-ExecutionPolicy', 'Bypass', '-File', ('"' + (Join-Path $Context.ScriptPath 'DownloadRestartProbe.ps1') + '"'),
                '-Phase', $phase, '-ProfilePath', ('"' + $profile + '"'), '-BaseUrl', $fixture.BaseUrl, '-Platform', $Context.Platform, '-Configuration', $Context.Configuration)
            $log = Join-Path $profile ($phase + '.log')
            $errorLog = Join-Path $profile ($phase + '-error.log')
            $process = Start-Process -FilePath (Join-Path $PSHOME 'powershell.exe') -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput $log -RedirectStandardError $errorLog
            $null = $process.Handle
            if (!$process.WaitForExit(60000)) {
                $process.Kill()
                throw "Restart probe timed out during $phase"
            }
            $process.Refresh()
            Get-Content -LiteralPath $log | Write-Host
            if ($process.ExitCode -ne 0) { throw ("Restart probe exit code " + $process.ExitCode + ": " + [IO.File]::ReadAllText($errorLog)) }
        }
        $offsets = Get-Content -LiteralPath (Join-Path $profile 'offsets.json') -Raw | ConvertFrom-Json
        foreach ($offset in $offsets) {
            $path = ([Uri]$offset.Url).AbsolutePath
            Assert-Regression ($fixture.RangeFor($path) -eq ('bytes=' + $offset.Offset + '-')) 'new process requests the persisted durable offset'
        }
        Assert-Regression ($fixture.CookieFor('/slow-restart-native').Contains('download-session=present')) 'restarted native download uses persisted browser cookies'
    } finally {
        $fixture.Dispose()
    }
}

function Test-BrowserDownloads($Context) {
    $fixture = New-Object DownloadHttpFixture
    $manager = [CefFlashBrowser.Services.Downloads.DownloadManagerService]::Current
    $window = New-RegressionWindow $Context
    $model = $window.ViewModel.CurrentBrowser
    $model.Address = $fixture.BaseUrl + '/page'
    $dialogState = @{ Seen = $false; CancelNext = $false; ConfirmFileAction = $false }
    $timer = New-Object Windows.Threading.DispatcherTimer
    $timer.Interval = [TimeSpan]::FromMilliseconds(30)
    $timer.Add_Tick({
        foreach ($dialog in @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.Dialogs.DownloadDialog] -and $_.IsVisible })) {
            $dialogState.Seen = $true
            if ($dialogState.CancelNext) {
                $dialogState.CancelNext = $false
                $dialog.FindName('cancelButton').RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
                continue
            }
            if (!$dialog.FindName('urlBox').Text) { $dialog.FindName('urlBox').Text = $fixture.BaseUrl + '/file' }
            $dialog.FindName('downloadButton').RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
        }
        if ($dialogState.ConfirmFileAction) {
            foreach ($dialog in @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.Dialogs.JsConfirmDialog] -and $_.IsVisible })) {
                $dialog.FindName('yesButton').RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
            }
        }
    }.GetNewClosure())
    $timer.Start()
    try {
        Wait-Regression $Context { $model.Browser.CanExecuteJavascriptInMainFrame -and $model.Browser.Title -eq 'Download fixture' } 'browser download source ready'
        [DownloadHttpFixture]::RegisterUnrelatedResource($model.Browser, ($fixture.BaseUrl + '/registered-resource'))
        foreach ($addressFocus in @($false, $true)) {
            Set-TabKeyboardFocus $Context $window -Address:$addressFocus
            try {
                [RegressionKeyboard]::SetKey(0x11, $false)
                Wait-Regression $Context { [Windows.Input.Keyboard]::Modifiers -eq [Windows.Input.ModifierKeys]::Control } 'Ctrl is held for the downloads shortcut'
                [RegressionKeyboard]::PressKey(0x4A)
                $lastKey = [Diagnostics.Stopwatch]::StartNew()
                Wait-Regression $Context {
                    if (@($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.DownloadManagerWindow] -and $_.IsVisible }).Count -eq 1) { return $true }
                    if ($lastKey.ElapsedMilliseconds -ge 300) {
                        Set-TabKeyboardFocus $Context $window -Address:$addressFocus
                        [RegressionKeyboard]::PressKey(0x4A)
                        $lastKey.Restart()
                    }
                    return $false
                } "real Ctrl+J opens downloads (address focus=$addressFocus)"
            } finally {
                [RegressionKeyboard]::SetKey(0x11, $true)
            }
            @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.DownloadManagerWindow] })[0].Close()
        }
        $model.Browser.GetBrowser().GetHost().StartDownload($fixture.BaseUrl + '/slow')
        Wait-Regression $Context { $manager.Items.Count -gt 0 -and $manager.Items[0].Record.Received -gt 16384 } 'native browser download receives bytes'
        $record = $manager.Items[0].Record
        Assert-Regression ($dialogState.Seen -and $record.Source -eq 'browser' -and $record.Method -eq 'GET') 'native download uses shared confirmation and preserves request metadata'
        Complete-UserScriptTask $Context ($manager.PauseAsync($record)) 'native download pause'
        Assert-Regression ($record.State -eq [CefFlashBrowser.Models.Downloads.DownloadState]::Paused) 'native pause reaches the shared task state'
        Complete-UserScriptTask $Context ($manager.ResumeAsync($record)) 'native download resume'
        $nativeBrowser = $model.Browser
        $window.ForceCloseWindow()
        Wait-Regression $Context { $record.State -eq [CefFlashBrowser.Models.Downloads.DownloadState]::Completed } 'native download continues after source close'
        Assert-Regression ([Convert]::ToBase64String([IO.File]::ReadAllBytes($record.FilePath)) -eq [Convert]::ToBase64String($fixture.Payload)) 'native background download commits exact bytes'
        Wait-Regression $Context { $nativeBrowser.IsDisposed } 'retained native browser releases after transfer completion'

        $window = New-RegressionWindow $Context
        $model = $window.ViewModel.CurrentBrowser
        $model.Address = $fixture.BaseUrl + '/page'
        Wait-Regression $Context { $model.Browser.CanExecuteJavascriptInMainFrame -and $model.Browser.Title -eq 'Download fixture' } 'POST download source ready'
        [void](Invoke-JavaScript $Context $model.Browser "document.getElementById('post').submit()")
        Wait-Regression $Context { $manager.Items[0].FileName -eq 'post.bin' -and $manager.Items[0].Record.State -eq [CefFlashBrowser.Models.Downloads.DownloadState]::Completed } 'POST response downloads through native transport'
        Assert-Regression ($fixture.PostCount -eq 1 -and $manager.Items[0].Record.Method -eq 'POST') 'browser download preserves POST without repeating it'

        [void](Invoke-JavaScript $Context $model.Browser "var a=document.createElement('a');a.download='native-blob.bin';a.href=URL.createObjectURL(new Blob([new Uint8Array([0,1,2,255,127])]));a.click()")
        Wait-Regression $Context { $manager.Items[0].FileName -eq 'native-blob.bin' -and $manager.Items[0].Record.State -eq [CefFlashBrowser.Models.Downloads.DownloadState]::Completed } 'native Blob download completes'
        Assert-Regression ([Convert]::ToBase64String([IO.File]::ReadAllBytes($manager.Items[0].FilePath)) -eq [Convert]::ToBase64String([byte[]]@(0,1,2,255,127))) 'browser Blob download keeps the original bytes'

        $model.Browser.GetBrowser().GetHost().StartDownload($fixture.BaseUrl + '/slow-snapshot')
        Wait-Regression $Context { $manager.Items[0].FileName -eq 'slow-snapshot.bin' -and $manager.Items[0].Record.Received -gt 16384 } 'native snapshot receives a partial file'
        $record = $manager.Items[0].Record
        $transfer = $manager.Find($record)
        Complete-UserScriptTask $Context ($transfer.ShutdownAsync()) 'native durable snapshot'
        $offset = $record.Received
        Assert-Regression ($record.State -eq [CefFlashBrowser.Models.Downloads.DownloadState]::Interrupted -and [IO.File]::Exists($record.PartialPath)) 'native shutdown retains a durable partial file'
        Complete-UserScriptTask $Context ($manager.ResumeAsync($record)) 'resume native snapshot through the shared HTTP transport'
        Wait-Regression $Context { $record.State -eq [CefFlashBrowser.Models.Downloads.DownloadState]::Completed } 'native snapshot finishes through the resumed HTTP request'
        Assert-Regression ($fixture.RangeFor('/slow-snapshot') -eq ('bytes=' + $offset + '-')) 'native snapshot recovery requests the durable byte offset'
        Assert-Regression ($fixture.CookieFor('/slow-snapshot').Contains('download-session=present')) 'native snapshot recovery uses the browser cookie jar'
        Assert-Regression ([Convert]::ToBase64String([IO.File]::ReadAllBytes($record.FilePath)) -eq [Convert]::ToBase64String($fixture.Payload)) 'native snapshot recovery preserves file contents'

        $model.Browser.GetBrowser().GetHost().StartDownload($fixture.BaseUrl + '/slow-cancel')
        Wait-Regression $Context { $manager.Items[0].FileName -eq 'slow-cancel.bin' -and $manager.Items[0].Record.Received -gt 16384 } 'native cancellation receives a partial file'
        $record = $manager.Items[0].Record
        Complete-UserScriptTask $Context ($manager.CancelAsync($record)) 'cancel native download'
        Wait-Regression $Context { ![IO.File]::Exists($record.PartialPath) } 'cancel removes native partial file'
        Assert-Regression ($record.State -eq [CefFlashBrowser.Models.Downloads.DownloadState]::Cancelled -and ![IO.File]::Exists($record.FilePath)) 'native cancellation keeps the final destination absent'
        $window.ForceCloseWindow()

        [CefFlashBrowser.Services.WindowManager]::ShowDownloadManager()
        $managerWindow = @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.DownloadManagerWindow] })[0]
        try {
            Complete-UserScriptTask $Context ($manager.NewAsync()) 'create manual download through the shared dialog'
            Wait-Regression $Context { $manager.Items[0].Record.Source -eq 'manual' -and $manager.Items[0].Record.State -eq [CefFlashBrowser.Models.Downloads.DownloadState]::Completed } 'manual download entry commits a file'
            $manualItem = $manager.Items[0]
            $manualPath = $manualItem.FilePath
            Assert-Regression ([Convert]::ToBase64String([IO.File]::ReadAllBytes($manualPath)) -eq [Convert]::ToBase64String($fixture.Payload)) 'manual dialog download preserves response bytes'
            Complete-UserScriptTask $Context ($manager.RemoveAsync($manualItem)) 'remove completed manual record'
            Assert-Regression ([IO.File]::Exists($manualPath) -and !($manager.Items -contains $manualItem)) 'removing a completed record preserves its file'
            $count = $manager.Items.Count
            $dialogState.CancelNext = $true
            Complete-UserScriptTask $Context ($manager.NewAsync()) 'cancel manual download confirmation'
            Assert-Regression ($manager.Items.Count -eq $count) 'cancelled manual confirmation creates no task'
            Complete-UserScriptTask $Context ($manager.NewAsync()) 'create another manual download'
            Wait-Regression $Context { $manager.Items[0].Record.Source -eq 'manual' -and $manager.Items[0].Record.State -eq [CefFlashBrowser.Models.Downloads.DownloadState]::Completed } 'second manual download completes'
            $manualRecord = $manager.Items[0].Record
            $dialogState.ConfirmFileAction = $true
            Complete-UserScriptTask $Context ($manager.DeleteFileAsync($manualRecord)) 'delete downloaded file through confirmation'
            Wait-Regression $Context { $manualRecord.FileMissing } 'deleting a file updates its retained record'
            Assert-Regression (![IO.File]::Exists($manualRecord.FilePath) -and $manager.Items[0].Record -eq $manualRecord) 'delete-file action retains download history'
        } finally {
            $managerWindow.Close()
        }

        $player = New-AuxiliaryWindow $Context ([CefFlashBrowser.Views.SwfPlayerWindow]) -WaitForLoad
        try {
            $playerView = $player.FindName('browserView')
            $savedToolsMode = [CefFlashBrowser.Data.GlobalData]::Settings.EnableIntegratedDevTools
            [CefFlashBrowser.Data.GlobalData]::Settings.EnableIntegratedDevTools = $false
            Open-RegressionDevTools $Context $playerView
            Wait-Regression $Context { [CefFlashBrowser.Utils.HwndHelper]::FindNotIntegratedDevTools($playerView.ViewModel.Browser) -ne [IntPtr]::Zero } 'download observer leaves native DevTools requests intact'
            Assert-Regression $player.IsVisible 'embedded SWF page and detached DevTools remain available with download handling enabled'
        } finally {
            [CefFlashBrowser.Data.GlobalData]::Settings.EnableIntegratedDevTools = $savedToolsMode
            $player.ForceCloseWindow()
        }
    } finally {
        $timer.Stop()
        if ($null -ne $window) {
            $window.ForceCloseWindow()
        }
        $fixture.Dispose()
    }
}
