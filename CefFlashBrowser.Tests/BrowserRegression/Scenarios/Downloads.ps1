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
    [IO.File]::WriteAllBytes($record.FilePath, [byte[]]@(1,2,3))
    $transfer = New-Object CefFlashBrowser.Services.Downloads.HttpDownloadTransfer($record, $null, $null)
    $manager.Accept($transfer)
    $window.DataContext.SelectedItem = $manager.Items[0]
    $manager.Refresh()
    Assert-Regression ($window.DataContext.SelectedItem.CanOpen) 'completed download enables opening its file'
    [IO.File]::Delete($record.FilePath)
    $manager.Refresh()
    Assert-Regression ($record.FileMissing -and !$window.DataContext.SelectedItem.CanOpen) 'externally deleted file updates its state and open action'
    $window.Close()
    [CefFlashBrowser.Services.WindowManager]::ShowDownloadManager()
    $window = @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.DownloadManagerWindow] })[0]
    Assert-Regression ($window.FindName('downloadList').Items.Count -eq 1) 'closing the download window retains application history'
    $window.Close()
}

function Test-BrowserDownloads($Context) {
    $fixture = New-Object DownloadHttpFixture
    $manager = [CefFlashBrowser.Services.Downloads.DownloadManagerService]::Current
    $window = New-RegressionWindow $Context
    $model = $window.ViewModel.CurrentBrowser
    $model.Address = $fixture.BaseUrl + '/page'
    $dialogState = @{ Seen = $false }
    $timer = New-Object Windows.Threading.DispatcherTimer
    $timer.Interval = [TimeSpan]::FromMilliseconds(30)
    $timer.Add_Tick({
        foreach ($dialog in @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.Dialogs.DownloadDialog] -and $_.IsVisible })) {
            $dialogState.Seen = $true
            $dialog.FindName('downloadButton').RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
        }
    }.GetNewClosure())
    $timer.Start()
    try {
        Wait-Regression $Context { $model.Browser.CanExecuteJavascriptInMainFrame -and $model.Browser.Title -eq 'Download fixture' } 'browser download source ready'
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
    } finally {
        $timer.Stop()
        if ($null -ne $window) { $window.ForceCloseWindow() }
        $fixture.Dispose()
    }
}
