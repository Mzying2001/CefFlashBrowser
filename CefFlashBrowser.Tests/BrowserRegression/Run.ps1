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
    [ValidateSet('All', 'UserScriptSeams', 'BrowserDisposal', 'UserScripts', 'Loading', 'TabRouting', 'Lifecycle', 'ViewSource')][string]$Scenario = 'All'
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
    'Scenarios\Favorites.ps1',
    'Scenarios\ProxySettings.ps1',
    'Scenarios\BrowserWindow.ps1',
    'Scenarios\BrowserTabs.ps1',
    'Scenarios\BrowserTabControl.ps1',
    'Scenarios\ContextMenu.ps1',
    'Scenarios\ViewSource.ps1',
    'Scenarios\AuxiliaryWindows.ps1',
    'Scenarios\UserScripts.ps1',
    'Scenarios\Lifecycle.ps1'
    'Scenarios\Favicons.ps1'
)) {
    . (Join-Path $PSScriptRoot $file)
}

$context = New-RegressionContext $PSScriptRoot $Platform $Configuration
$failure = $null
try {
    Initialize-RegressionEnvironment $context
    if ($Scenario -eq 'BrowserDisposal') {
        Start-RegressionCef $context
        Test-BrowserFrameLoadStartAfterDisposal $context
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
    } elseif ($Scenario -eq 'Loading') {
        Start-RegressionCef $context
        Test-BrowserTabLoadingMenu $context
    } elseif ($Scenario -eq 'UserScripts') {
        Start-RegressionCef $context
        Test-UserScripts $context
    } elseif ($Scenario -eq 'UserScriptSeams') {
        Start-RegressionCef $context
        Test-UserScriptSeams $context
    } else {
        Test-LanguageService $context
        Test-LanguageSelectors $context
        Test-FavoritesService $context
        Test-ProxySettings $context
        Test-BrowserBeforeInitialization $context
        Test-BrowserTabControl $context
        Test-BrowserTabsBeforeShow $context
        Start-RegressionCef $context
        Test-UserScriptSeams $context
        Test-UserScripts $context
        Test-NavigationBeforeHosting $context
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
