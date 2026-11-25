#Requires -Version 5.1
#Requires -PSEdition Desktop
<#
Run in a fresh, architecture-matched Windows PowerShell process after building:
  powershell -NoProfile -STA -ExecutionPolicy Bypass -File CefFlashBrowser.Tests\BrowserRegression\Run.ps1
See README.md for x86 and Release commands.
#>
[CmdletBinding()]
param(
    [ValidateSet('x64', 'x86')][string]$Platform = 'x64',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [ValidateSet('All', 'BrowserIcon', 'DownloadRestart', 'BrowserDownloads', 'DownloadManager', 'Manager', 'UserScriptSettings', 'UserScriptSeams', 'BrowserDisposal', 'UserScripts', 'Loading', 'TabRouting', 'Lifecycle', 'ViewSource', 'Closing', 'Auxiliary', 'Menus', 'ScriptTabs', 'ContextMenu', 'Interactions', 'Downloads', 'Audio', 'Network', 'Frames', 'Recovery', 'TabClosing', 'TabHistory', 'PageSwfs', 'Polyfills', 'PolyfillsDisabled')][string]$Scenario = 'All'
)

$ErrorActionPreference = 'Stop'
if ([Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') {
    throw 'Run this script in an STA Windows PowerShell process (-STA).'
}
if (([IntPtr]::Size -eq 8) -ne ($Platform -eq 'x64')) {
    throw 'The PowerShell process architecture must match -Platform. Use SysWOW64\WindowsPowerShell\v1.0\powershell.exe for x86.'
}

foreach ($file in @(
    'Environment.ps1',
    'Helpers.ps1',
    'Scenarios\Language.ps1',
    'Scenarios\Downloads.ps1',
    'Scenarios\Favorites.ps1',
    'Scenarios\ProxySettings.ps1',
    'Scenarios\BrowserWindow.ps1',
    'Scenarios\BrowserTabs.ps1',
    'Scenarios\BrowserTabControl.ps1',
    'Scenarios\ContextMenu.ps1',
    'Scenarios\ViewSource.ps1',
    'Scenarios\AuxiliaryWindows.ps1',
    'Scenarios\UserScripts.ps1',
    'Scenarios\ListReorderBehavior.ps1',
    'Scenarios\UserScriptManager.ps1',
    'Scenarios\UserScriptSettings.ps1',
    'Scenarios\UserScriptNetwork.ps1',
    'Scenarios\Polyfills.ps1',
    'Scenarios\Lifecycle.ps1'
    'Scenarios\Favicons.ps1'
)) {
    . (Join-Path $PSScriptRoot $file)
}

$context = New-RegressionContext $PSScriptRoot $Platform $Configuration
$failure = $null
try {
    Initialize-RegressionEnvironment $context
    if ($Scenario -eq 'BrowserIcon') {
        Test-BrowserIconDataUrls $context
    } elseif ($Scenario -eq 'DownloadManager') {
        Test-DownloadManagerWindow $context
    } elseif ($Scenario -eq 'DownloadRestart') {
        Test-DownloadRestart $context
    } elseif ($Scenario -eq 'BrowserDownloads') {
        Start-RegressionCef $context
        Test-BrowserDownloads $context
    } elseif ($Scenario -eq 'BrowserDisposal') {
        Start-RegressionCef $context
        Test-BrowserFrameLoadStartAfterDisposal $context
    } elseif ($Scenario -eq 'TabClosing') {
        Start-RegressionCef $context
        Test-BrowserTabCloseKeyboardCancellation $context
        Test-BrowserTabOpenerClosePending $context
        Test-BrowserTabsReloadClose $context
    } elseif ($Scenario -eq 'Recovery') {
        Start-RegressionCef $context
        Test-UserScriptOfflineRecovery $context
    } elseif ($Scenario -eq 'Frames') {
        Start-RegressionCef $context
        Test-UserScriptFrames $context
    } elseif ($Scenario -eq 'Polyfills') {
        Start-PolyfillRegressionCef $context
        Test-BrowserPolyfills $context
    } elseif ($Scenario -eq 'PolyfillsDisabled') {
        Start-PolyfillRegressionCef $context -DisablePolyfills
        Test-BrowserPolyfillsDisabled $context
    } elseif ($Scenario -eq 'Network') {
        Start-RegressionCef $context
        Test-UserScriptNetwork $context
    } elseif ($Scenario -eq 'Audio') {
        Start-RegressionCef $context
        Test-BrowserMute $context
    } elseif ($Scenario -eq 'Downloads') {
        Start-RegressionCef $context
        Test-UserScriptDownloadsAndAudio $context
    } elseif ($Scenario -eq 'Interactions') {
        Start-RegressionCef $context
        Test-UserScriptInteractions $context
    } elseif ($Scenario -eq 'ContextMenu') {
        Start-RegressionCef $context
        Test-ContextMenu $context
    } elseif ($Scenario -eq 'ScriptTabs') {
        Start-RegressionCef $context
        Test-UserScriptTabs $context
    } elseif ($Scenario -eq 'Menus') {
        Start-RegressionCef $context
        Test-UserScriptMenus $context
    } elseif ($Scenario -eq 'Closing') {
        Start-RegressionCef $context
        Test-UserScriptCloseCancellation $context
    } elseif ($Scenario -eq 'Auxiliary') {
        Start-RegressionCef $context
        Test-AuxiliaryWindows $context
    } elseif ($Scenario -eq 'PageSwfs') {
        Start-RegressionCef $context -EnableFlash
        Test-StandaloneBrowserPageSwfs $context
        Test-BrowserPageSwfs $context
        Test-SwfPlayerPageContext $context
        Test-LocalSwfPlayerPageContext $context
    } elseif ($Scenario -eq 'ViewSource') {
        Start-RegressionCef $context
        Test-ViewSourceMinimal $context
    } elseif ($Scenario -eq 'Lifecycle') {
        Start-RegressionCef $context
        Test-BrowserCloseDuringInitialization $context
        Test-AuxiliaryBeforeShow $context
        Test-CloseAllBrowsers $context
    } elseif ($Scenario -eq 'TabRouting') {
        Start-RegressionCef $context
        Test-BrowserTabNewPageRouting $context
    } elseif ($Scenario -eq 'TabHistory') {
        Start-RegressionCef $context
        Test-BrowserTabHistory $context
    } elseif ($Scenario -eq 'Loading') {
        Start-RegressionCef $context
        Test-BrowserTabLoadingMenu $context
    } elseif ($Scenario -eq 'Manager') {
        Test-ListReorderBehavior $context
        Start-RegressionCef $context
        Test-UserScriptManager $context
    } elseif ($Scenario -eq 'UserScriptSettings') {
        Test-UserScriptSettings $context
    } elseif ($Scenario -eq 'UserScripts') {
        Start-RegressionCef $context
        Test-UserScripts $context
    } elseif ($Scenario -eq 'UserScriptSeams') {
        Start-RegressionCef $context
        Test-UserScriptSeams $context
    } else {
        Test-LanguageService $context
        Test-LanguageSelectors $context
        Test-BrowserIconDataUrls $context
        Test-FavoritesService $context
        Test-FavoritesManagerReorder $context
        Test-ProxySettings $context
        Test-UserScriptSettings $context
        Test-BrowserBeforeInitialization $context
        Test-DownloadManagerWindow $context
        Test-BrowserTabControl $context
        Test-ListReorderBehavior $context
        Test-BrowserTabsBeforeShow $context
        Start-RegressionCef $context -EnableFlash
        Test-BrowserPolyfills $context
        Test-UserScriptManager $context
        Test-UserScriptSeams $context
        Test-UserScripts $context
        Test-UserScriptCloseCancellation $context
        Test-UserScriptMenus $context
        Test-UserScriptTabs $context
        Test-UserScriptInteractions $context
        Test-UserScriptDownloadsAndAudio $context
        Test-BrowserDownloads $context
        Test-UserScriptNetwork $context
        Test-UserScriptFrames $context
        Test-UserScriptOfflineRecovery $context
        Test-NavigationBeforeHosting $context
        Test-BrowserTabHistory $context
        Test-AuxiliaryBeforeShow $context
        Test-BrowserWindow $context
        Test-Favicons $context
        Test-BrowserCloseDuringInitialization $context
        Test-BrowserTabs $context
        Test-ContextMenu $context
        Test-ViewSource $context
        Test-AuxiliaryWindows $context
        Test-CloseAllBrowsers $context
    }
} catch {
    $failure = $_
    throw
} finally {
    $cleanupErrors = @(Stop-RegressionEnvironment $context)
    if ($cleanupErrors.Count -gt 0) {
        if ($null -ne $failure) {
            foreach ($cleanupError in $cleanupErrors) {
                Write-Warning "Regression cleanup failed: $cleanupError" -WarningAction Continue
            }
        } else {
            throw $cleanupErrors[0]
        }
    }
}

Write-Host 'Browser regression checks passed.'
