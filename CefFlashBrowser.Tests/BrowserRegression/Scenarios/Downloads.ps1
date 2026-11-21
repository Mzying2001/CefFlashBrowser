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
