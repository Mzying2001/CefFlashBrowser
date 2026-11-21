function Test-DownloadManagerWindow($Context) {
    [CefFlashBrowser.Services.WindowManager]::ShowDownloadManager()
    $window = @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.DownloadManagerWindow] })[0]
    Assert-Regression ($null -ne $window -and $window.IsVisible) 'download manager opens through the global command'
    Invoke-BrowserCommand ([CefFlashBrowser.Services.WindowManager]::OpenDownloadsCommand)
    Assert-Regression (@($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.DownloadManagerWindow] }).Count -eq 1) 'download manager entry reuses its single window'
    $manager = [CefFlashBrowser.Services.Downloads.DownloadManagerService]::Current
    $model = $window.DataContext
    $canExecute = [Windows.Input.ICommand].GetMethod('CanExecute')
    $list = $window.FindName('downloadList')
    $filter = $window.FindName('downloadFilter')
    $details = $window.FindName('downloadDetails')
    $errorField = $window.FindName('detailError')
    $emptyMessage = $window.FindName('emptyDownloadMessage')
    Assert-Regression ($details.Visibility -eq 'Collapsed' -and $emptyMessage.Visibility -eq 'Visible' -and
        $emptyMessage.Text -eq $Context.LanguageService.GetString('downloads_empty')) 'empty download history leaves the details panel collapsed'
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
    $completedItem = $manager.Items[0]

    $failure = New-Object CefFlashBrowser.Models.Downloads.DownloadRecord
    $failure.Source = 'browser'
    $failure.Url = 'https://example.test/failed-request'
    $failure.FileName = 'failed.bin'
    $failure.FilePath = Join-Path $Context.TemporaryPath 'alternate\failed.bin'
    $failure.State = [CefFlashBrowser.Models.Downloads.DownloadState]::Failed
    $failure.Error = 'The server closed the connection.'
    $manager.Accept((New-Object CefFlashBrowser.Services.Downloads.HttpDownloadTransfer($failure, $null, $null)))
    $failedItem = $manager.Items[0]

    Assert-Regression ($details.Visibility -eq 'Collapsed') 'populated history hides details until a download is selected'
    $list.SelectedItem = $completedItem
    $manager.Refresh()
    Assert-Regression ($canExecute.Invoke($model.OpenCommand, [object[]]@($null)) -and
        $window.FindName('openFolderButton').IsEnabled) 'completed download enables file and folder actions'
    Save-DownloadManagerCapture $window (Join-Path $Context.TemporaryPath 'DownloadManager.png')

    Assert-Regression ($details.Visibility -eq 'Visible' -and $errorField.Visibility -eq 'Collapsed') 'selecting a completed download automatically shows details without an error row'
    $filter.Text = '  EXAMPLE.BIN  '
    Assert-Regression ($list.Items.Count -eq 1 -and $list.SelectedItem -eq $completedItem -and $details.Visibility -eq 'Visible') 'name filter trims whitespace and keeps a matching selection and its details'
    $filter.Text = 'ALTERNATE'
    Assert-Regression ($list.Items.Count -eq 1 -and $list.Items[0] -eq $failedItem) 'filter matches the saved path'
    Assert-Regression ($null -eq $model.SelectedItem -and $null -eq $list.SelectedItem -and $details.Visibility -eq 'Collapsed') 'filtering out the selected download clears selection and closes details'
    Assert-Regression (!$canExecute.Invoke($model.OpenCommand, [object[]]@($null)) -and
        !$window.FindName('openFolderButton').IsEnabled) 'row actions are disabled when filtering out the selected download'
    $filter.Text = 'failed-request'
    Assert-Regression ($list.Items.Count -eq 1 -and $list.Items[0] -eq $failedItem) 'filter matches the download URL'
    $failure.Url = 'https://example.test/redirected'
    $manager.Refresh($false)
    Assert-Regression ($list.Items.Count -eq 0 -and $emptyMessage.Visibility -eq 'Visible' -and
        $emptyMessage.Text -eq $Context.LanguageService.GetString('downloads_noMatches')) 'a URL change updates filter results and the empty-results message'
    $filter.Text = ''
    Assert-Regression ($list.Items.Count -eq 2 -and $emptyMessage.Visibility -eq 'Collapsed') 'clearing the filter restores all history'

    $list.SelectedItem = $completedItem
    $list.ScrollIntoView($failedItem)
    $list.UpdateLayout()
    $row = $list.ItemContainerGenerator.ContainerFromItem($failedItem)
    $right = New-Object Windows.Input.MouseButtonEventArgs([Windows.Input.Mouse]::PrimaryDevice, 0, [Windows.Input.MouseButton]::Right)
    $right.RoutedEvent = [Windows.UIElement]::PreviewMouseRightButtonDownEvent
    $row.RaiseEvent($right)
    Assert-Regression ($model.SelectedItem -eq $failedItem -and
        !$canExecute.Invoke($model.OpenCommand, [object[]]@($null))) 'right click selects the clicked download and updates available actions'
    $menu = $row.ContextMenu
    $menu.PlacementTarget = $row
    $menu.IsOpen = $true
    try {
        Wait-Regression $Context { $menu.Items[0].Command -eq $model.OpenCommand } 'row context menu binds to the download commands'
        Assert-Regression (@($menu.Items | Where-Object { $_.Header -eq $Context.LanguageService.GetString('downloads_details') }).Count -eq 0) 'row context menu has no details action'
    } finally {
        $menu.IsOpen = $false
    }
    Assert-Regression ($details.Visibility -eq 'Visible' -and $errorField.Visibility -eq 'Visible' -and
        $errorField.Text -eq $failure.Error) 'failed downloads show the actual error in details'

    $window.Width = $window.MinWidth
    $window.Height = $window.MinHeight
    Set-ManagerRegressionTheme $Context ([CefFlashBrowser.Models.Theme]::Dark)
    try {
        Save-DownloadManagerCapture $window (Join-Path $Context.TemporaryPath 'DownloadManager-Dark.png')
        Assert-Regression ($list.ActualHeight -gt 80 -and $details.ActualHeight -gt 0) 'the minimum window size leaves space for the list and details'
    } finally {
        Set-ManagerRegressionTheme $Context ([CefFlashBrowser.Models.Theme]::Light)
    }
    $list.SelectedItem = $completedItem
    Assert-Regression ($details.Visibility -eq 'Visible' -and $errorField.Visibility -eq 'Collapsed') 'details follow selection and hide cleared errors'
    $list.SelectedItem = $null
    Assert-Regression ($details.Visibility -eq 'Collapsed') 'clearing selection returns the details space to the list'
    Set-TabWindowForeground $Context $window
    $filter.Text = 'failed.bin'
    $list.SelectedItem = $failedItem
    [void]$filter.Focus()
    $filter.Select($filter.Text.Length - 1, 0)
    [RegressionKeyboard]::PressKey(0x2E)
    Wait-Regression $Context { $filter.Text -eq 'failed.bi' } 'Delete edits the filter text'
    Assert-Regression ($manager.Items.Contains($failedItem) -and $list.SelectedItem -eq $failedItem) 'Delete in the filter leaves the selected record intact'
    $list.UpdateLayout()
    [void]$list.ItemContainerGenerator.ContainerFromItem($failedItem).Focus()
    [RegressionKeyboard]::PressKey(0x2E)
    Wait-Regression $Context { !$manager.Items.Contains($failedItem) } 'Delete in the list removes the selected record'
    Assert-Regression ($details.Visibility -eq 'Collapsed') 'removing the only visible record hides details'
    $filter.Text = ''
    $list.SelectedItem = $completedItem

    [IO.File]::Delete($record.FilePath)
    $manager.Refresh()
    Wait-Regression $Context { $record.FileMissing } 'refresh detects the deleted file asynchronously'
    Assert-Regression ($record.FileMissing -and !$canExecute.Invoke($model.OpenCommand, [object[]]@($null))) 'externally deleted file updates its state and open action'
    $window.Close()
    [CefFlashBrowser.Services.WindowManager]::ShowDownloadManager()
    $window = @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.DownloadManagerWindow] })[0]
    Assert-Regression ($window.FindName('downloadList').Items.Count -eq 1) 'closing the download window retains application history'
    $window.Close()
    [IO.File]::WriteAllBytes($record.FilePath, [byte[]]@(1,2,3))
    $recordCountSql = "SELECT COUNT(*) FROM downloads WHERE id = '$($record.Id)'"
    Wait-Regression $Context {
        $Context.Database.ScalarAsync($recordCountSql).GetAwaiter().GetResult() -eq 1
    } 'completed download appears in the database'

    $blockRemovalSql = "CREATE TRIGGER block_download_removal BEFORE DELETE ON downloads " +
        "WHEN OLD.id = '$($record.Id)' BEGIN SELECT RAISE(ABORT, 'Simulated download history write failure'); END"
    [void](Complete-UserScriptTask $Context ($Context.Database.ExecuteAsync($blockRemovalSql)) 'block download history deletion temporarily')
    try {
        Complete-UserScriptTask $Context ($manager.RemoveAsync($completedItem)) 'remove completed download while the database rejects deletion'
        Assert-Regression (!$manager.Items.Contains($completedItem)) 'failed database deletion leaves the record removed from the window'
        $remaining = Complete-UserScriptTask $Context ($Context.Database.ScalarAsync($recordCountSql)) 'read record after failed deletion'
        Assert-Regression ($remaining -eq 1) 'failed deletion leaves the database row intact until retry'
    } finally {
        [void](Complete-UserScriptTask $Context ($Context.Database.ExecuteAsync('DROP TRIGGER block_download_removal')) 'allow download history deletion again')
    }
    Wait-Regression $Context {
        $Context.Database.ScalarAsync($recordCountSql).GetAwaiter().GetResult() -eq 0
    } 'timer retries the pending download history deletion'
    Assert-Regression ([IO.File]::Exists($record.FilePath)) 'removing download history preserves the completed file'

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

function Save-DownloadManagerCapture($Window, [string]$Path) {
    $Window.UpdateLayout()
    $bitmap = New-Object Windows.Media.Imaging.RenderTargetBitmap([int]$Window.Content.ActualWidth, [int]$Window.Content.ActualHeight, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($Window.Content)
    $encoder = New-Object Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.File]::Create($Path)
    try {
        $encoder.Save($stream)
    } finally {
        $stream.Dispose()
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
    $dialogState = @{ Seen = $false; CancelNext = $false; ConfirmFileAction = $false; ManagerVisibleBeforeConfirmation = $false }
    $timer = New-Object Windows.Threading.DispatcherTimer
    $timer.Interval = [TimeSpan]::FromMilliseconds(30)
    $timer.Add_Tick({
        foreach ($dialog in @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.Dialogs.DownloadDialog] -and $_.IsVisible })) {
            $dialogState.Seen = $true
            $dialogState.ManagerVisibleBeforeConfirmation = @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.DownloadManagerWindow] -and $_.IsVisible }).Count -gt 0
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

        $dialogState.CancelNext = $true
        $model.Browser.GetBrowser().GetHost().StartDownload($fixture.BaseUrl + '/slow-dismiss')
        Wait-Regression $Context {
            $manager.Items.Count -gt 0 -and $manager.Items[0].FileName -eq 'slow-dismiss.bin' -and
                $manager.Items[0].Record.State -eq [CefFlashBrowser.Models.Downloads.DownloadState]::Cancelled
        } 'native download confirmation is cancelled'
        Assert-Regression (!$dialogState.ManagerVisibleBeforeConfirmation -and
            @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.DownloadManagerWindow] }).Count -eq 0) 'cancelling confirmation keeps the download manager closed'

        $dialogState.Seen = $false
        $model.Browser.GetBrowser().GetHost().StartDownload($fixture.BaseUrl + '/slow')
        Wait-Regression $Context { $manager.Items[0].FileName -eq 'slow.bin' -and $manager.Items[0].Record.Received -gt 16384 } 'native browser download receives bytes'
        $downloadItem = $manager.Items[0]
        $record = $downloadItem.Record
        Assert-Regression ($dialogState.Seen -and $record.Source -eq 'browser' -and $record.Method -eq 'GET') 'native download uses shared confirmation and preserves request metadata'
        Assert-Regression (!$dialogState.ManagerVisibleBeforeConfirmation) 'download manager stays closed while confirmation is pending'
        Wait-Regression $Context {
            $downloads = @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.DownloadManagerWindow] -and $_.IsVisible })
            $downloads.Count -eq 1 -and $downloads[0].FindName('downloadList').Items.Contains($downloadItem)
        } 'native download opens the download manager with the new task'
        $managerWindow = @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.DownloadManagerWindow] })[0]
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
        $managerWindow.WindowState = [Windows.WindowState]::Minimized
        [void](Invoke-JavaScript $Context $model.Browser "document.getElementById('post').submit()")
        Wait-Regression $Context { $manager.Items[0].FileName -eq 'post.bin' -and $manager.Items[0].Record.State -eq [CefFlashBrowser.Models.Downloads.DownloadState]::Completed } 'POST response downloads through native transport'
        Assert-Regression ($fixture.PostCount -eq 1 -and $manager.Items[0].Record.Method -eq 'POST') 'browser download preserves POST without repeating it'
        Wait-Regression $Context { $managerWindow.WindowState -eq [Windows.WindowState]::Normal } 'another native download restores the minimized download manager'
        $downloadWindows = @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.DownloadManagerWindow] })
        Assert-Regression ($downloadWindows.Count -eq 1 -and [object]::ReferenceEquals($downloadWindows[0], $managerWindow)) 'native downloads reuse the same download manager window'

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
