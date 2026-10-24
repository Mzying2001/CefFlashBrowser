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
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug'
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
    'Scenarios\BrowserWindow.ps1',
    'Scenarios\AuxiliaryWindows.ps1',
    'Scenarios\Lifecycle.ps1'
)) {
    . (Join-Path $PSScriptRoot $file)
}

$context = New-RegressionContext $PSScriptRoot $Platform $Configuration
$failure = $null
try {
    Initialize-RegressionEnvironment $context
    Test-BrowserBeforeInitialization $context
    Start-RegressionCef $context
    Test-NavigationBeforeHosting $context
    Test-AuxiliaryBeforeShow $context
    Test-BrowserWindow $context
    Test-BrowserCloseDuringInitialization $context
    Test-AuxiliaryWindows $context
    Test-CloseAllBrowsers $context
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
