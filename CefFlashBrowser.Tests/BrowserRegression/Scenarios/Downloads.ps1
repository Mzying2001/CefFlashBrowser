function Test-DownloadManagerWindow($Context) {
    [CefFlashBrowser.Services.WindowManager]::ShowDownloadManager()
    $window = @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.DownloadManagerWindow] })[0]
    Assert-Regression ($null -ne $window -and $window.IsVisible) 'download manager opens through the global command'
    Invoke-BrowserCommand ([CefFlashBrowser.Services.WindowManager]::OpenDownloadsCommand)
    Assert-Regression (@($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.DownloadManagerWindow] }).Count -eq 1) 'download manager entry reuses its single window'
    $ioc = [SimpleMvvm.Ioc.SimpleIoc]::Global
    $manager = $ioc.GetInstance([CefFlashBrowser.Services.Downloads.DownloadManagerService], @())
    $model = $window.DataContext
    Assert-Regression ([object]::ReferenceEquals($manager, $ioc.GetInstance(
        [CefFlashBrowser.Services.Downloads.DownloadManagerService], @())) -and
        [object]::ReferenceEquals($model.Items, $manager.Items)) 'the download window and container share one download manager and history list'
    $canExecute = [Windows.Input.ICommand].GetMethod('CanExecute')
    $list = $window.FindName('downloadList')
    $filter = $window.FindName('downloadFilter')
    Assert-Regression ([Windows.Automation.AutomationProperties]::GetName($filter) -ceq 'DownloadFilter') 'download filter has a fixed automation name'
    $details = $window.FindName('downloadDetails')
    $sourceField = $window.FindName('detailSource')
    $errorField = $window.FindName('detailError')
    $emptyMessage = $window.FindName('emptyDownloadMessage')
    $resumeText = $Context.LanguageService.GetString('downloads_resume')
    $retryText = $Context.LanguageService.GetString('downloads_retry')
    $taskMenu = $window.Content.Children[0].Items | Where-Object { $_.Header -eq $Context.LanguageService.GetString('downloads_taskMenu') }
    $taskResume = $taskMenu.Items | Where-Object { $_.Command -eq $model.ResumeCommand }
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
    $list.UpdateLayout()
    $completedRow = $list.ItemContainerGenerator.ContainerFromItem($completedItem)
    $completedTransfer = Find-CommandButton $completedRow $model.ToggleTransferCommand
    Assert-Regression (!$completedTransfer.IsEnabled -and [object]::ReferenceEquals($completedTransfer.Content, $window.Resources['PlayIcon'])) 'the completed row shows a disabled play icon'
    Assert-Regression ($canExecute.Invoke($model.OpenCommand, [object[]]@($null)) -and
        $window.FindName('openFolderButton').IsEnabled) 'completed download enables file and folder actions'
    Test-DownloadFileIcons $Context $list $completedRow $failedItem
    Save-DownloadManagerCapture $window (Join-Path $Context.TemporaryPath 'DownloadManager.png')

    Assert-Regression ($details.Visibility -eq 'Visible' -and $errorField.Visibility -eq 'Collapsed') 'selecting a completed download automatically shows details without an error row'
    Assert-Regression ($sourceField.Text -eq $Context.LanguageService.GetString('downloads_manual') -and
        $sourceField.IsReadOnly -and $list.View.Columns[5].Header -eq $Context.LanguageService.GetString('downloads_operations')) 'download source appears in details and the list has an operations column'
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
    $rowTransfer = Find-CommandButton $row $model.ToggleTransferCommand
    $rowRemove = Find-CommandButton $row $model.RemoveRowCommand
    Assert-Regression ($rowTransfer.IsEnabled -and $rowTransfer.ToolTip -eq $retryText -and
        [object]::ReferenceEquals($rowTransfer.CommandParameter, $failedItem) -and
        [object]::ReferenceEquals($rowTransfer.Content, $window.Resources['PlayIcon'])) 'an unselected failed row offers Retry using the shared play icon'
    Assert-Regression ($rowRemove.IsEnabled -and [object]::ReferenceEquals($rowRemove.CommandParameter, $failedItem) -and
        [object]::ReferenceEquals($rowRemove.Content.Content, $window.Resources['TrashIcon'])) 'the remove button refers to its own row and uses the shared trash icon'
    $right = New-Object Windows.Input.MouseButtonEventArgs([Windows.Input.Mouse]::PrimaryDevice, 0, [Windows.Input.MouseButton]::Right)
    $right.RoutedEvent = [Windows.UIElement]::PreviewMouseRightButtonDownEvent
    $row.RaiseEvent($right)
    Assert-Regression ($model.SelectedItem -eq $failedItem -and
        !$canExecute.Invoke($model.OpenCommand, [object[]]@($null))) 'right click selects the clicked download and updates available actions'
    Assert-Regression ($sourceField.Text -eq $Context.LanguageService.GetString('downloads_browser')) 'source details follow selection'
    $menu = $row.ContextMenu
    $menu.PlacementTarget = $row
    $menu.IsOpen = $true
    try {
        Wait-Regression $Context { $menu.Items[0].Command -eq $model.OpenCommand } 'row context menu binds to the download commands'
        Assert-Regression (@($menu.Items | Where-Object { $_.Header -eq $Context.LanguageService.GetString('downloads_details') }).Count -eq 0) 'row context menu has no details action'
        $rowResume = $menu.Items | Where-Object { $_.Command -eq $model.ResumeCommand }
        Assert-Regression ($taskResume.Header -eq $retryText -and $rowResume.Header -eq $retryText -and $rowResume.IsEnabled) 'selecting a failed download shows Retry in both menus'

        $actionTextByState = [ordered]@{
            Paused = $resumeText
            Interrupted = $resumeText
            Cancelled = $retryText
            TimedOut = $retryText
            Failed = $retryText
        }
        foreach ($state in $actionTextByState.GetEnumerator()) {
            $failure.State = [CefFlashBrowser.Models.Downloads.DownloadState]$state.Key
            $manager.Refresh($false)
            Assert-Regression ($taskResume.Header -eq $state.Value -and $rowResume.Header -eq $state.Value -and
                $rowTransfer.ToolTip -eq $state.Value -and
                [Windows.Automation.AutomationProperties]::GetName($rowTransfer) -eq $state.Value) "menus and row button update the action text for a $($state.Key) download"
        }
    } finally {
        $menu.IsOpen = $false
    }
    Assert-Regression ($details.Visibility -eq 'Visible' -and $errorField.Visibility -eq 'Visible' -and
        $errorField.Text -eq $failure.Error) 'failed downloads show the actual error in details'
    $window.UpdateLayout()
    $detailGrid = $sourceField.Parent
    $detailFields = @($detailGrid.Children | Where-Object { $_ -is [Windows.Controls.TextBox] })
    $gaps = for ($index = 1; $index -lt $detailFields.Count; $index++) {
        $previous = $detailFields[$index - 1]
        $bottom = $previous.TranslatePoint([Windows.Point]::new(0, $previous.ActualHeight), $detailGrid).Y
        $top = $detailFields[$index].TranslatePoint([Windows.Point]::new(0, 0), $detailGrid).Y
        $top - $bottom
    }
    $unevenGaps = @($gaps | Where-Object { [Math]::Abs($_ - $gaps[0]) -gt 1 })
    Assert-Regression ($gaps.Count -eq 3 -and $unevenGaps.Count -eq 0) "download details have even row gaps (measured: $($gaps -join ', ') DIP)"
    $failure.Method = 'POST'
    $manager.Refresh($false)
    Assert-Regression (!$rowTransfer.IsEnabled) 'an unrecoverable failed download disables Retry'
    $failure.Method = 'GET'

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
    Assert-Regression ($taskResume.Header -eq $resumeText) 'selecting a completed download restores the default Resume label'
    $list.SelectedItem = $null
    Assert-Regression ($details.Visibility -eq 'Collapsed') 'clearing selection returns the details space to the list'
    $taskMenu.IsSubmenuOpen = $true
    try {
        Wait-Regression $Context { $taskMenu.IsSubmenuOpen -and $taskResume.Header -eq $resumeText -and !$taskResume.IsEnabled } 'the task menu shows a disabled Resume action without a selection'
    } finally {
        $taskMenu.IsSubmenuOpen = $false
    }
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
    Assert-Regression ($taskResume.Header -eq $retryText -and $canExecute.Invoke($model.ResumeCommand, [object[]]@($null))) 'a missing file changes the action to Retry'
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
        Assert-Regression ($dialog.FindName('systemDownloadButton').Visibility -eq [Windows.Visibility]::Collapsed) 'manual download hides the system download button'
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

        $unknownRecord = New-Object CefFlashBrowser.Models.Downloads.DownloadRecord
        $unknownRecord.Source = 'browser'
        $unknownRecord.Url = $dialogRecord.Url
        $unknownRecord.FileName = $dialogRecord.FileName
        $unknownRecord.Method = ''
        $unknownDialog = New-Object CefFlashBrowser.Views.Dialogs.DownloadDialog
        $initialize = $unknownDialog.GetType().GetMethod('Initialize', [Reflection.BindingFlags]'Instance,NonPublic')
        [void]$initialize.Invoke($unknownDialog, [object[]]@($unknownRecord.PSObject.BaseObject, $true))
        $systemButton = $unknownDialog.FindName('systemDownloadButton')
        Assert-Regression ($systemButton.Visibility -eq [Windows.Visibility]::Visible -and !$systemButton.IsEnabled) 'unknown browser request method disables system download'
        $unknownDialog.Close()
    } finally {
        [CefFlashBrowser.Data.GlobalData]::Settings.DownloadDirectory = $directory
        [void][CefFlashBrowser.Data.GlobalData]::SaveSettings()
    }
}

function Test-DownloadFileIcons($Context, $List, $CompletedRow, $MissingItem) {
    $missingRow = $List.ItemContainerGenerator.ContainerFromItem($MissingItem)
    $completedIcon = Find-TabElement $CompletedRow 'fileIcon'
    $missingIcon = Find-TabElement $missingRow 'fileIcon'
    Wait-Regression $Context { $null -ne $completedIcon.Source -and $null -ne $missingIcon.Source } 'resolve file type icons in the download list'
    $cached = $completedIcon.Source
    Assert-Regression (![IO.File]::Exists($MissingItem.FilePath) -and $cached.IsFrozen -and
        [object]::ReferenceEquals($missingIcon.Source, $cached)) 'a missing download shares the same file type bitmap as the completed download'

    $fileName = $missingIcon.Parent.Children[1]
    $iconRight = $missingIcon.TranslatePoint([Windows.Point]::new($missingIcon.ActualWidth, $missingIcon.ActualHeight / 2), $missingRow)
    $textLeft = $fileName.TranslatePoint([Windows.Point]::new(0, $fileName.ActualHeight / 2), $missingRow)
    Assert-Regression ($iconRight.X -lt $textLeft.X -and [Math]::Abs($iconRight.Y - $textLeft.Y) -lt 1 -and
        !$missingIcon.IsHitTestVisible -and $fileName.ToolTip -eq $MissingItem.FileName -and
        $fileName.TextTrimming -eq [Windows.TextTrimming]::CharacterEllipsis) 'the row icon aligns with the filename and preserves trimming and the tooltip'

    $probe = New-Object CefFlashBrowser.Views.Custom.FileIcon
    $probe.FilePath = [IO.Path]::ChangeExtension($MissingItem.FilePath, '.BIN')
    Wait-Regression $Context { [object]::ReferenceEquals($probe.Source, $cached) } 'a full missing path with different extension casing uses the same cache'

    $originalName = $MissingItem.FileName
    $MissingItem.Record.FileName = 'renamed.txt'
    $MissingItem.Refresh()
    Wait-Regression $Context { $null -ne $missingIcon.Source -and ![object]::ReferenceEquals($missingIcon.Source, $cached) } 'a changed download extension updates the row icon'

    $probe.FilePath = 'file.cfb-icon-regression'
    $MissingItem.Record.FileName = $probe.FilePath
    $MissingItem.Refresh()
    $MissingItem.Record.FileName = $originalName
    $MissingItem.Refresh()
    Wait-Regression $Context { $null -ne $probe.Source -and [object]::ReferenceEquals($missingIcon.Source, $cached) } 'a late icon lookup cannot overwrite the current row icon'

    $probe.FilePath = 'file-without-extension'
    Wait-Regression $Context { $null -ne $probe.Source } 'an extensionless name gets a generic file icon'
    $probe.FilePath = ''
    Assert-Regression ($null -eq $probe.Source) 'clearing the input clears the file icon'
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

function Invoke-DownloadRowButton($Window, $Item, $Command) {
    $list = $Window.FindName('downloadList')
    $list.ScrollIntoView($Item)
    $list.UpdateLayout()
    $row = $list.ItemContainerGenerator.ContainerFromItem($Item)
    $button = Find-CommandButton $row $Command
    Assert-Regression ($null -ne $button -and $button.IsEnabled) 'download row action is available'
    $peer = New-Object Windows.Automation.Peers.ButtonAutomationPeer($button)
    $peer.GetPattern([Windows.Automation.Peers.PatternInterface]::Invoke).Invoke()
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

function Test-ExplorerSelection($Shell, [string]$Path) {
    foreach ($window in $Shell.Windows()) {
        if ([IO.Path]::GetFileName($window.FullName) -ne 'explorer.exe') { continue }
        foreach ($item in $window.Document.SelectedItems()) {
            if ($item.Path -eq $Path) { return $true }
        }
    }
    return $false
}

function Test-BrowserDownloads($Context) {
    $fixture = New-Object DownloadHttpFixture
    $manager = [SimpleMvvm.Ioc.SimpleIoc]::Global.GetInstance([CefFlashBrowser.Services.Downloads.DownloadManagerService], @())
    $window = New-RegressionWindow $Context
    $model = $window.ViewModel.CurrentBrowser
    $model.Address = $fixture.BaseUrl + '/page'
    $dialogState = @{ Seen = $false; CancelNext = $false; BrowserDismissal = $null; ConfirmFileAction = $false; ManagerVisibleBeforeConfirmation = $false; SystemDownload = $null; ConfirmAfterReceiving = $null }
    $shell = New-Object -ComObject Shell.Application
    $timer = New-Object Windows.Threading.DispatcherTimer
    $timer.Interval = [TimeSpan]::FromMilliseconds(30)
    $timer.Add_Tick({
        foreach ($dialog in @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.Dialogs.DownloadDialog] -and $_.IsVisible })) {
            $dialogState.Seen = $true
            $dialogState.ManagerVisibleBeforeConfirmation = @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.DownloadManagerWindow] -and $_.IsVisible }).Count -gt 0
            $systemButton = $dialog.FindName('systemDownloadButton')
            $dialogState.SystemButtonDisabled = $systemButton.Visibility -eq [Windows.Visibility]::Visible -and !$systemButton.IsEnabled
            $handoff = $dialogState.SystemDownload
            $dismissal = $dialogState.BrowserDismissal
            $pending = if ($null -ne $dismissal) { $dismissal } elseif ($null -ne $handoff -and $handoff.Result -ge 0) { $handoff }
            if ($null -ne $pending) {
                $pending.Record = $manager.Items[0].Record
                $pending.Transfer = $manager.Find($pending.Record)
                $pending.Sql = "SELECT COUNT(*) FROM downloads WHERE id = '$($pending.Record.Id)'"
                $pending.RowPersisted = $Context.Database.ScalarAsync($pending.Sql).GetAwaiter().GetResult() -eq 1
                if (!$pending.RowPersisted -and $pending.Wait.Elapsed.TotalSeconds -lt 8) { continue }
            }
            if ($null -ne $handoff) {
                $dialogState.SystemDownload = $null
                $systemPosition = $systemButton.TranslatePoint([Windows.Point]::new(0, 0), $dialog)
                $downloadPosition = $dialog.FindName('downloadButton').TranslatePoint([Windows.Point]::new(0, 0), $dialog)
                $handoff.ButtonOnLeft = $systemPosition.X -lt $downloadPosition.X -and [Math]::Abs($systemPosition.Y - $downloadPosition.Y) -lt 1
                $start = [Func[string,int]]({
                    param($url)
                    $handoff.RequestedUrl = $url
                    $handoff.DialogDisabled = !$dialog.IsEnabled
                    return $handoff.Result
                }.GetNewClosure())
                $dialog.GetType().GetProperty('StartSystemDownload', [Reflection.BindingFlags]'Instance,NonPublic').SetValue($dialog, $start.PSObject.BaseObject)
                if ($handoff.Result -lt 0) {
                    $application = $Context.Application
                    $failureMessage = $Context.LanguageService.GetString('downloads_systemDownloadFailed')
                    [void]$application.Dispatcher.BeginInvoke([Windows.Threading.DispatcherPriority]::Background, [Action]({
                        foreach ($alert in @($application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.Dialogs.JsAlertDialog] -and $_.IsVisible })) {
                            $handoff.FailureShown = $alert.Owner -eq $dialog -and $alert.Message -eq $failureMessage
                            $alert.FindName('okButton').RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
                        }
                    }.GetNewClosure()))
                }
                $systemButton.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
                if ($handoff.Result -ge 0) {
                    if ($dialog.IsVisible) { $dialog.FindName('cancelButton').RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent))) }
                    continue
                }
                $handoff.FallbackAvailable = $dialog.IsVisible -and $dialog.IsEnabled
            }
            if ($dialogState.CancelNext -or $null -ne $dismissal) {
                $dialogState.CancelNext = $false
                $dialogState.BrowserDismissal = $null
                if ($null -ne $dismissal -and $dismissal.CloseWindow) {
                    $dialog.Close()
                } else {
                    $dialog.FindName('cancelButton').RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
                }
                continue
            }
            if ($null -ne $dialogState.ConfirmAfterReceiving) {
                $record = $manager.Items[0].Record
                if ($record.Received -ne $dialogState.ConfirmAfterReceiving -or $record.Total -ne $record.Received) { continue }
                $dialogState.ConfirmAfterReceiving = $null
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

        $dialogState.ConfirmAfterReceiving = 5
        [void](Invoke-JavaScript $Context $model.Browser "var a=document.createElement('a');a.download='native-blob.bin';a.href=URL.createObjectURL(new Blob([new Uint8Array([0,1,2,255,127])]));a.click()")
        Wait-Regression $Context { $manager.Items[0].FileName -eq 'native-blob.bin' -and $manager.Items[0].Record.State -eq [CefFlashBrowser.Models.Downloads.DownloadState]::Completed } 'native Blob download receives all bytes before confirmation and commits'
        $blobRecord = $manager.Items[0].Record
        Assert-Regression $dialogState.SystemButtonDisabled 'Blob download keeps the system button visible and disabled'
        Assert-Regression ([Convert]::ToBase64String([IO.File]::ReadAllBytes($blobRecord.FilePath)) -eq [Convert]::ToBase64String([byte[]]@(0,1,2,255,127))) 'browser Blob download keeps the original bytes'
        Wait-Regression $Context { Test-ExplorerSelection $shell $blobRecord.FilePath } 'File Explorer selects the saved Blob file'
        Assert-Regression (!$dialogState.ManagerVisibleBeforeConfirmation -and
            @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.DownloadManagerWindow] }).Count -eq 0) 'pre-received browser download leaves the download manager closed'
        $blobSql = "SELECT COUNT(*) FROM downloads WHERE id = '$($blobRecord.Id)' AND state = $([int]$blobRecord.State)"
        Wait-Regression $Context { $Context.Database.ScalarAsync($blobSql).GetAwaiter().GetResult() -eq 1 } 'pre-received browser download saves its completed history'

        $systemUrl = $fixture.BaseUrl + '/slow-system'
        $handoff = @{ Result = 0; Wait = [Diagnostics.Stopwatch]::StartNew() }
        $dialogState.SystemDownload = $handoff
        $model.Browser.GetBrowser().GetHost().StartDownload($systemUrl)
        Wait-Regression $Context { $null -ne $handoff.Transfer -and $handoff.Transfer.Released.IsCompleted } 'system handoff releases the original CEF transfer'
        Assert-Regression ($handoff.ButtonOnLeft -and $handoff.DialogDisabled -and $handoff.RequestedUrl -ceq $systemUrl) 'system button is left of Download and hands Windows the original URL'
        Assert-Regression ($handoff.RowPersisted -and $null -eq $handoff.Record.FilePath -and $null -eq $handoff.Record.PartialPath -and
            @($manager.Items | Where-Object { $_.Record.Id -eq $handoff.Record.Id }).Count -eq 0) 'system handoff removes the original task without choosing a destination in the app'
        Wait-Regression $Context { $Context.Database.ScalarAsync($handoff.Sql).GetAwaiter().GetResult() -eq 0 } 'system handoff deletes the persisted download row'
        Assert-Regression (@($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.DownloadManagerWindow] }).Count -eq 0) 'system handoff leaves the download manager closed'

        foreach ($action in @('Cancel', 'Close')) {
            $dismissal = @{ CloseWindow = $action -eq 'Close'; Wait = [Diagnostics.Stopwatch]::StartNew() }
            $dialogState.BrowserDismissal = $dismissal
            $model.Browser.GetBrowser().GetHost().StartDownload($fixture.BaseUrl + '/slow-dismiss')
            Wait-Regression $Context { $null -ne $dismissal.Transfer -and $dismissal.Transfer.Released.IsCompleted } "$action releases the original CEF transfer"
            Assert-Regression ($dismissal.RowPersisted -and $dismissal.Record.State -eq [CefFlashBrowser.Models.Downloads.DownloadState]::Cancelled -and
                @($manager.Items | Where-Object { $_.Record.Id -eq $dismissal.Record.Id }).Count -eq 0) "$action removes the cancelled task from the download list"
            Wait-Regression $Context { $Context.Database.ScalarAsync($dismissal.Sql).GetAwaiter().GetResult() -eq 0 } "$action deletes the persisted download row"
            Assert-Regression (!$dialogState.ManagerVisibleBeforeConfirmation -and
                @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.DownloadManagerWindow] }).Count -eq 0) "$action keeps the download manager closed"
        }

        $dialogState.Seen = $false
        $failedHandoff = @{ Result = -2147467259 }
        $dialogState.SystemDownload = $failedHandoff
        $model.Browser.GetBrowser().GetHost().StartDownload($fixture.BaseUrl + '/slow')
        Wait-Regression $Context { $manager.Items[0].FileName -eq 'slow.bin' -and $manager.Items[0].Record.Received -gt 16384 } 'native browser download receives bytes'
        $downloadItem = $manager.Items[0]
        $record = $downloadItem.Record
        Assert-Regression ($failedHandoff.FailureShown -and $failedHandoff.FallbackAvailable) 'system download failure leaves the same dialog usable for CEF download'
        Assert-Regression ($dialogState.Seen -and $record.Source -eq 'browser' -and $record.Method -eq 'GET') 'native download uses shared confirmation and preserves request metadata'
        Assert-Regression (!$dialogState.ManagerVisibleBeforeConfirmation) 'download manager stays closed while confirmation is pending'
        Wait-Regression $Context {
            $downloads = @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.DownloadManagerWindow] -and $_.IsVisible })
            $downloads.Count -eq 1 -and $downloads[0].FindName('downloadList').Items.Contains($downloadItem)
        } 'native download opens the download manager with the new task'
        $managerWindow = @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.DownloadManagerWindow] })[0]
        $list = $managerWindow.FindName('downloadList')
        $list.UpdateLayout()
        $row = $list.ItemContainerGenerator.ContainerFromItem($downloadItem)
        $transferButton = Find-CommandButton $row $managerWindow.DataContext.ToggleTransferCommand
        Assert-Regression ($transferButton.ToolTip -eq $Context.LanguageService.GetString('downloads_pause') -and
            [object]::ReferenceEquals($transferButton.Content, $managerWindow.Resources['StopIcon'])) 'the running row shows the shared stop icon and Pause'
        Invoke-DownloadRowButton $managerWindow $downloadItem $managerWindow.DataContext.ToggleTransferCommand
        Wait-Regression $Context {
            $record.State -eq [CefFlashBrowser.Models.Downloads.DownloadState]::Paused -and
            $transferButton.ToolTip -eq $Context.LanguageService.GetString('downloads_resume') -and $transferButton.IsEnabled
        } 'the row button pauses the native download and switches to Resume'
        Invoke-DownloadRowButton $managerWindow $downloadItem $managerWindow.DataContext.ToggleTransferCommand
        Wait-Regression $Context { $record.State -eq [CefFlashBrowser.Models.Downloads.DownloadState]::Downloading } 'the row button resumes the native download'
        $nativeBrowser = $model.Browser
        $window.ForceCloseWindow()
        Wait-Regression $Context { $record.State -eq [CefFlashBrowser.Models.Downloads.DownloadState]::Completed } 'native download continues after source close'
        Assert-Regression ([Convert]::ToBase64String([IO.File]::ReadAllBytes($record.FilePath)) -eq [Convert]::ToBase64String($fixture.Payload)) 'native background download commits exact bytes'
        Wait-Regression $Context { $nativeBrowser.IsDisposed } 'retained native browser releases after transfer completion'
        Assert-Regression (!(Test-ExplorerSelection $shell $record.FilePath)) 'download confirmed while receiving stays in the download manager when complete'

        $window = New-RegressionWindow $Context
        $model = $window.ViewModel.CurrentBrowser
        $model.Address = $fixture.BaseUrl + '/page'
        Wait-Regression $Context { $model.Browser.CanExecuteJavascriptInMainFrame -and $model.Browser.Title -eq 'Download fixture' } 'POST download source ready'
        $managerWindow.WindowState = [Windows.WindowState]::Minimized
        [void](Invoke-JavaScript $Context $model.Browser "document.getElementById('post').submit()")
        Wait-Regression $Context { $manager.Items[0].FileName -eq 'slow-post.bin' -and $manager.Items[0].Record.State -eq [CefFlashBrowser.Models.Downloads.DownloadState]::Completed } 'POST response downloads through native transport'
        Assert-Regression $dialogState.SystemButtonDisabled 'POST download keeps the system button visible and disabled'
        Assert-Regression ($fixture.PostCount -eq 1 -and $manager.Items[0].Record.Method -eq 'POST') 'browser download preserves POST without repeating it'
        Wait-Regression $Context { $managerWindow.WindowState -eq [Windows.WindowState]::Normal } 'another native download restores the minimized download manager'
        $downloadWindows = @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.DownloadManagerWindow] })
        Assert-Regression ($downloadWindows.Count -eq 1 -and [object]::ReferenceEquals($downloadWindows[0], $managerWindow)) 'native downloads reuse the same download manager window'

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
        Assert-Regression ($manager.Items[0].Record -eq $record) 'cancelling an active transfer keeps its history entry'
        $window.ForceCloseWindow()

        [CefFlashBrowser.Services.WindowManager]::ShowDownloadManager()
        $managerWindow = @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.DownloadManagerWindow] })[0]
        try {
            Complete-UserScriptTask $Context ($manager.NewAsync()) 'create manual download through the shared dialog'
            Wait-Regression $Context { $manager.Items[0].Record.Source -eq 'manual' -and $manager.Items[0].Record.State -eq [CefFlashBrowser.Models.Downloads.DownloadState]::Completed } 'manual download entry commits a file'
            $manualItem = $manager.Items[0]
            $manualPath = $manualItem.FilePath
            Assert-Regression ([Convert]::ToBase64String([IO.File]::ReadAllBytes($manualPath)) -eq [Convert]::ToBase64String($fixture.Payload)) 'manual dialog download preserves response bytes'
            $managerWindow.DataContext.SelectedItem = $null
            Invoke-DownloadRowButton $managerWindow $manualItem $managerWindow.DataContext.RemoveRowCommand
            Wait-Regression $Context { !$manager.Items.Contains($manualItem) } 'the row button removes the unselected completed record'
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
            Invoke-DownloadRowButton $managerWindow $manager.Items[0] $managerWindow.DataContext.ToggleTransferCommand
            Wait-Regression $Context { !$manualRecord.FileMissing -and [IO.File]::Exists($manualRecord.FilePath) } 'Retry in the row restores the missing file'
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
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)
        if ($null -ne $window) {
            $window.ForceCloseWindow()
        }
        $fixture.Dispose()
    }
}
