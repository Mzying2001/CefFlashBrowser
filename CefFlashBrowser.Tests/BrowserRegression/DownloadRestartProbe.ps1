#Requires -Version 5.1
#Requires -PSEdition Desktop
[CmdletBinding()]
param(
    [ValidateSet('Save', 'Resume')][string]$Phase,
    [string]$ProfilePath,
    [string]$BaseUrl,
    [ValidateSet('x64', 'x86')][string]$Platform = 'x64',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Environment.ps1')
. (Join-Path $PSScriptRoot 'Helpers.ps1')
. (Join-Path $PSScriptRoot 'Scenarios\BrowserWindow.ps1')
. (Join-Path $PSScriptRoot 'Scenarios\UserScripts.ps1')

$context = New-RegressionContext $PSScriptRoot $Platform $Configuration
$timer = $null

try {
    Initialize-RegressionEnvironment $context
    $privateStatic = [Reflection.BindingFlags]'Static,NonPublic'
    [CefFlashBrowser.Data.GlobalData].GetField('<DataPath>k__BackingField', $privateStatic).SetValue($null, $ProfilePath)
    [CefFlashBrowser.Data.GlobalData]::Settings.DownloadDirectory = Join-Path $ProfilePath 'Files'
    Start-RegressionCef $context (Join-Path $ProfilePath 'Cache')
    $manager = [CefFlashBrowser.Services.Downloads.DownloadManagerService]::Current
    if ($Phase -eq 'Save') {
        $window = New-RegressionWindow $context
        $model = $window.ViewModel.CurrentBrowser
        $model.Address = $BaseUrl + '/page'
        Wait-Regression $context { $model.Browser.CanExecuteJavascriptInMainFrame -and $model.Browser.Title -eq 'Download fixture' } 'restart source ready'

        $timer = New-Object Windows.Threading.DispatcherTimer
        $timer.Interval = [TimeSpan]::FromMilliseconds(30)
        $timer.Add_Tick({
            foreach ($dialog in @($context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.Dialogs.DownloadDialog] -and $_.IsVisible })) {
                $dialog.FindName('downloadButton').RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
            }
        }.GetNewClosure())
        $timer.Start()
        $model.Browser.GetBrowser().GetHost().StartDownload($BaseUrl + '/slow-restart-native')
        Wait-Regression $context { $manager.Items.Count -eq 1 -and $manager.Items[0].Record.Received -gt 16384 } 'native restart task has durable bytes'

        $record = New-Object CefFlashBrowser.Models.Downloads.DownloadRecord
        $record.Source = 'manual'
        $record.Url = $BaseUrl + '/slow-restart-manual'
        $record.FileName = 'manual.bin'
        $record.FilePath = Join-Path $ProfilePath 'Files\manual.bin'
        $transfer = New-Object CefFlashBrowser.Services.Downloads.HttpDownloadTransfer($record, $manager.Http, $null)
        $manager.Accept($transfer)
        [void]$transfer.StartAsync()
        Wait-Regression $context { $record.Received -gt 16384 } 'manual restart task has durable bytes'
        Complete-UserScriptTask $context ($manager.StopAsync()) 'persist unfinished tasks before CEF shutdown'
        $manager.Items | ForEach-Object { @{ Url = $_.Record.Url; Offset = $_.Record.Received } } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ProfilePath 'offsets.json') -Encoding UTF8
    } else {
        Assert-Regression ($manager.Items.Count -eq 2) 'new process loads both download records'
        foreach ($item in $manager.Items) {
            Assert-Regression ($item.Record.State -eq [CefFlashBrowser.Models.Downloads.DownloadState]::Interrupted -and $item.Record.Received -gt 0) 'new process restores a non-running task with its actual offset'
            Complete-UserScriptTask $context ($manager.ResumeAsync($item.Record)) 'resume persisted task'
        }
        Wait-Regression $context { @($manager.Items | Where-Object { $_.Record.State -ne [CefFlashBrowser.Models.Downloads.DownloadState]::Completed }).Count -eq 0 } 'all restored tasks complete'
        $expected = [Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $ProfilePath 'payload.bin')))
        foreach ($item in $manager.Items) {
            Assert-Regression ([Convert]::ToBase64String([IO.File]::ReadAllBytes($item.FilePath)) -eq $expected) 'restarted download preserves the complete file contents'
        }
    }
} finally {
    if ($null -ne $timer) {
        $timer.Stop()
    }

    $errors = @(Stop-RegressionEnvironment $context)
    if ($errors.Count -gt 0) {
        throw $errors[0]
    }
}
