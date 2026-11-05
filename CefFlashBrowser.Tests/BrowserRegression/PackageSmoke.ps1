#Requires -Version 5.1
#Requires -PSEdition Desktop
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ArchivePath,
    [ValidateSet('x64', 'x86')][string]$Platform = 'x64'
)

$ErrorActionPreference = 'Stop'
if ([Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') {
    throw 'Run the package smoke check in a fresh STA Windows PowerShell process.'
}
if (([IntPtr]::Size -eq 8) -ne ($Platform -eq 'x64')) {
    throw 'The PowerShell process architecture must match -Platform.'
}

$temporaryPath = Join-Path ([IO.Path]::GetTempPath()) ('CefFlashBrowser-package-' + [Guid]::NewGuid())
$packagePath = Join-Path $temporaryPath 'package'
$databasePath = Join-Path $temporaryPath 'browser.db'
Write-Host "Isolated package check: $temporaryPath"
Expand-Archive -LiteralPath $ArchivePath -DestinationPath $packagePath
foreach ($file in @('CefFlashBrowser.exe', 'Assets\SQLite\SQLite.Interop.dll',
    'Assets\CommandProxy\CefFlashBrowser.CommandProxy.exe')) {
    if (!(Test-Path -LiteralPath (Join-Path $packagePath $file) -PathType Leaf)) {
        throw "Required package file is missing: $file"
    }
}
& (Join-Path $PSScriptRoot '..\CommandProxyRegression\Run.ps1') -Platform $Platform -Configuration Release `
    -ProxyPath (Join-Path $packagePath 'Assets\CommandProxy\CefFlashBrowser.CommandProxy.exe')
$sqliteInteropPath = Join-Path $packagePath 'Assets\SQLite\SQLite.Interop.dll'
$nativeFiles = @(Get-ChildItem -LiteralPath $packagePath -Recurse -File -Filter 'SQLite.Interop.dll')
if ($nativeFiles.Count -ne 1 -or $nativeFiles[0].FullName -ne $sqliteInteropPath) {
    throw 'The package must contain exactly one SQLite.Interop.dll under Assets\SQLite.'
}

Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase
$applicationAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $packagePath 'CefFlashBrowser.exe'))
if ((Test-Path -LiteralPath (Join-Path $packagePath 'Assets\SwfPlayer')) -or
    $applicationAssembly.GetManifestResourceNames() -notcontains 'CefFlashBrowser.Assets.SwfPlayer.swfplayer.html') {
    throw 'The package must embed the SWF player page without an Assets\SwfPlayer directory.'
}
# Start Costura's embedded assembly resolver without running the application's entry point.
[Runtime.CompilerServices.RuntimeHelpers]::RunModuleConstructor($applicationAssembly.ManifestModule.ModuleHandle)
$privateStatic = [Reflection.BindingFlags]'Static,NonPublic'
$globalDataType = $applicationAssembly.GetType('CefFlashBrowser.Data.GlobalData')
foreach ($dataPath in @{
    DatabasePath = $databasePath
    LogsPath = (Join-Path $temporaryPath 'Logs')
    SqliteDllPath = (Join-Path $packagePath 'Assets\SQLite')
}.GetEnumerator()) {
    $globalDataType.GetField('<' + $dataPath.Key + '>k__BackingField', $privateStatic).SetValue($null, $dataPath.Value)
}
[void][CefFlashBrowser.Utils.Win32]::SetDllDirectory((Join-Path $packagePath 'Assets\CefSharp'))
[Threading.SynchronizationContext]::SetSynchronizationContext(
    (New-Object System.Windows.Threading.DispatcherSynchronizationContext))
$programType = $applicationAssembly.GetType('CefFlashBrowser.Program')
$register = $programType.GetMethod('RegisterServices', $privateStatic)
$unregister = $programType.GetMethod('UnregisterServices', $privateStatic)

# Cleanup of a forwarding or failed startup must not instantiate its services.
[void]$register.Invoke($null, $null)
[void]$unregister.Invoke($null, $null)
if (Test-Path -LiteralPath $databasePath) {
    throw 'Unregistering unused services opened the database.'
}
if (@(Get-Process -Id $PID -Module | Where-Object ModuleName -eq 'SQLite.Interop.dll').Count -ne 0) {
    throw 'Unregistering unused services loaded the native SQLite library.'
}

[void]$register.Invoke($null, $null)
try {
    $ioc = [SimpleMvvm.Ioc.SimpleIoc]::Global
    $database = $ioc.GetInstance([CefFlashBrowser.Database.IDatabase], @())
    $databaseService = $ioc.GetInstance([CefFlashBrowser.Services.DatabaseService], @())
    $service = $ioc.GetInstance([CefFlashBrowser.Services.FaviconService], @())
    if (![object]::ReferenceEquals($database, $ioc.GetInstance([CefFlashBrowser.Database.IDatabase], @())) -or
        ![object]::ReferenceEquals($databaseService, $ioc.GetInstance([CefFlashBrowser.Services.DatabaseService], @())) -or
        ![object]::ReferenceEquals($service, $ioc.GetInstance([CefFlashBrowser.Services.FaviconService], @()))) {
        throw 'Database, database service and favicon factories must return singleton instances.'
    }
    if (Test-Path -LiteralPath $databasePath) {
        throw 'Resolving services performed database I/O before initialization.'
    }

    $initialization = $service.InitializeAsync()
    $timeout = [Diagnostics.Stopwatch]::StartNew()
    while (!$initialization.IsCompleted) {
        if ($timeout.Elapsed.TotalSeconds -gt 10) { throw 'Favicon initialization timed out.' }
        $frame = New-Object System.Windows.Threading.DispatcherFrame
        $stopFrame = [Action]({ $frame.Continue = $false }.GetNewClosure())
        [void][System.Windows.Threading.Dispatcher]::CurrentDispatcher.BeginInvoke(
            [System.Windows.Threading.DispatcherPriority]::Background, $stopFrame)
        [System.Windows.Threading.Dispatcher]::PushFrame($frame)
        [Threading.Thread]::Sleep(5)
    }
    [void]$initialization.GetAwaiter().GetResult()
    [void]$service.RecordAsync('https://example.invalid/a', '/icon.png').GetAwaiter().GetResult()
    $records = $databaseService.GetWebsiteIconsAsync().GetAwaiter().GetResult()
    if ($records.Count -ne 1 -or $records[0].IconUrl -ne 'https://example.invalid/icon.png') {
        throw 'The packaged service did not persist its mapping through the shared database.'
    }
    $nativeModules = @(Get-Process -Id $PID -Module | Where-Object ModuleName -eq 'SQLite.Interop.dll')
    if ($nativeModules.Count -ne 1 -or $nativeModules[0].FileName -ne $sqliteInteropPath) {
        throw 'SQLite must load its native library from the isolated package under Assets\SQLite.'
    }
} finally {
    [void]$unregister.Invoke($null, $null)
}

$exclusiveFile = [IO.File]::Open($databasePath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
$exclusiveFile.Dispose()
Write-Host "Package smoke check passed ($Platform): native SQLite loading, lazy singletons, persistence and shutdown."
