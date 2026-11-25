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
foreach ($file in @('CefFlashBrowser.exe', 'Binaries\SQLite\SQLite.Interop.dll',
    'Binaries\CmdProxy\CmdProxy.exe', 'Binaries\CefSharp\libcef.dll',
    'Binaries\CefSharp\CefSharp.Core.Runtime.dll', 'Binaries\CefSharp\v8_context_snapshot.bin',
    'Binaries\CefSharp\CefSharp.BrowserSubprocess.exe', 'Binaries\Plugins\pepflashplayer.dll',
    'Binaries\Plugins\readme.txt')) {
    if (!(Test-Path -LiteralPath (Join-Path $packagePath $file) -PathType Leaf)) {
        throw "Required package file is missing: $file"
    }
}
$runtimeFiles = @('msvcp140.dll', 'vcruntime140.dll')
$expectedMachine = 0x014c
if ($Platform -eq 'x64') {
    $runtimeFiles += 'vcruntime140_1.dll'
    $expectedMachine = 0x8664
}
$runtimePath = Join-Path $packagePath 'Binaries\VCRuntime'
$cefPath = Join-Path $packagePath 'Binaries\CefSharp'
foreach ($file in $runtimeFiles) {
    $runtimeFile = Join-Path $runtimePath $file
    $reader = New-Object IO.BinaryReader ([IO.File]::OpenRead($runtimeFile))
    try {
        $reader.BaseStream.Position = 0x3c
        $reader.BaseStream.Position = $reader.ReadInt32()
        if ($reader.ReadUInt32() -ne 0x4550 -or $reader.ReadUInt16() -ne $expectedMachine) {
            throw "Expected a $Platform PE image at $runtimeFile."
        }
    } finally {
        $reader.Dispose()
    }
    if ((Get-FileHash -LiteralPath $runtimeFile).Hash -ne (Get-FileHash -LiteralPath (Join-Path $cefPath $file)).Hash) {
        throw "The CefSharp copy of $file must match the bundled runtime."
    }
}
if ((Test-Path -LiteralPath (Join-Path $packagePath 'Assets')) -or
    @(Get-ChildItem -LiteralPath $packagePath -Recurse -File -Filter '*.tar.gz').Count -ne 0) {
    throw 'The package must deploy dependencies under Binaries without Assets or dependency archives.'
}
& (Join-Path $PSScriptRoot '..\CommandProxyRegression\Run.ps1') -Platform $Platform -Configuration Release `
    -ProxyPath (Join-Path $packagePath 'Binaries\CmdProxy\CmdProxy.exe')
$sqliteInteropPath = Join-Path $packagePath 'Binaries\SQLite\SQLite.Interop.dll'
$nativeFiles = @(Get-ChildItem -LiteralPath $packagePath -Recurse -File -Filter 'SQLite.Interop.dll')
if ($nativeFiles.Count -ne 1 -or $nativeFiles[0].FullName -ne $sqliteInteropPath) {
    throw 'The package must contain exactly one SQLite.Interop.dll under Binaries\SQLite.'
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
    MonkeySharpPath = (Join-Path $temporaryPath 'MonkeySharp')
    UserScriptCachePath = (Join-Path $temporaryPath 'Caches\UserScripts')
    LogsPath = (Join-Path $temporaryPath 'Logs')
    CefDllPath = $cefPath
    SqliteDllPath = (Join-Path $packagePath 'Binaries\SQLite')
    VcRuntimeDllPath = $runtimePath
}.GetEnumerator()) {
    $globalDataType.GetField('<' + $dataPath.Key + '>k__BackingField', $privateStatic).SetValue($null, $dataPath.Value)
}
$globalDataType.GetField('<Settings>k__BackingField', $privateStatic).SetValue($null, [CefFlashBrowser.Models.Settings]::Default)
[Threading.SynchronizationContext]::SetSynchronizationContext(
    (New-Object System.Windows.Threading.DispatcherSynchronizationContext))
$programType = $applicationAssembly.GetType('CefFlashBrowser.Program')
[void]$programType.GetMethod('InitializeDllDirectories', $privateStatic).Invoke($null, $null)
$modules = @(Get-Process -Id $PID -Module)
foreach ($file in $runtimeFiles) {
    if ($modules.FileName -notcontains (Join-Path $runtimePath $file)) {
        throw "The application must preload $file from the isolated package under Binaries\VCRuntime."
    }
}
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
    $scripts = $ioc.GetInstance([CefFlashBrowser.Services.UserScripts.UserScriptService], @())
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
    [void]$scripts.InitializeAsync().GetAwaiter().GetResult()
    Add-Type -Path (Join-Path $PSScriptRoot 'UserScriptHttpFixture.cs')
    $fixture = New-Object UserScriptHttpFixture
    $restoredStorage = $null
    try {
        $source = "// ==UserScript==`n// @name Packaged persistence`n// @match https://example.invalid/*`n// @grant none`n// @require $($fixture.BaseUrl)/dependency.js`n// @resource fixture $($fixture.BaseUrl)/resource.txt`n// ==/UserScript=="
        $installation = $scripts.Repository.InstallAsync($source, $null, $true, [Threading.CancellationToken]::None).GetAwaiter().GetResult()
        $cachePath = Join-Path $temporaryPath 'Caches\UserScripts'
        $objects = @(Get-ChildItem -LiteralPath (Join-Path $cachePath 'objects') -File)
        if ($objects.Count -ne 2 -or @($objects | Where-Object Name -NotMatch '^[0-9a-f]{64}$').Count -ne 0) {
            throw 'Packaged assets must use separate content-addressed object files.'
        }
        if ($database.ScalarAsync("PRAGMA user_version").GetAwaiter().GetResult() -ne 4 -or
            $database.ScalarAsync("SELECT COUNT(*) FROM downloads").GetAwaiter().GetResult() -ne 0 -or
            $database.ScalarAsync("SELECT COUNT(*) FROM monkeysharp_script_assets").GetAwaiter().GetResult() -ne 2 -or
            $database.ScalarAsync("SELECT COUNT(*) FROM pragma_table_info('monkeysharp_scripts') WHERE name IN ('source','prepared_revision_json','display_name')").GetAwaiter().GetResult() -ne 0) {
            throw 'Packaged persistence must have an empty downloads table and script descriptors without source payloads.'
        }
        $fixture.Offline = $true
        $restoredStorage = New-Object CefFlashBrowser.Services.UserScripts.Persistence.UserScriptStorage($databaseService)
        $persistence = New-Object CefFlashBrowser.Services.UserScripts.Persistence.SqliteUserScriptRepositoryPersistence(
            $restoredStorage, (Join-Path $temporaryPath 'MonkeySharp'), $cachePath, $null, $null)
        $restoredRepository = New-Object Mzying2001.MonkeySharp.Core.Repository.UserScriptRepository($persistence, $null, $null)
        [void]$restoredRepository.InitializeAsync([Threading.CancellationToken]::None).GetAwaiter().GetResult()
        $restored = $restoredRepository.GetAsync($installation.ScriptKey, [Threading.CancellationToken]::None).GetAwaiter().GetResult()
        if (!$restored.CanExecute -or $restored.PreparedRevision.Resources['fixture'].DecodeText() -ne 'resource-ready' -or
            $fixture.RequestsFor('/dependency.js') -ne 1 -or $fixture.RequestsFor('/resource.txt') -ne 1) {
            throw 'Packaged source and assets did not restore offline through the public persistence contract.'
        }
    } finally {
        if ($null -ne $restoredStorage) { [void]$restoredStorage.StopAsync().GetAwaiter().GetResult() }
        $fixture.Dispose()
    }
    [void]$scripts.ValueStore.SetAsync($installation.ScriptKey.ToString(), 'packaged',
        [Mzying2001.MonkeySharp.Core.Storage.UserScriptValue]::Undefined, [Threading.CancellationToken]::None).GetAwaiter().GetResult()
    $stored = $scripts.ValueStore.GetAsync($installation.ScriptKey.ToString(), 'packaged', [Threading.CancellationToken]::None).GetAwaiter().GetResult()
    if (!$stored.Exists -or $stored.Value.EncodedValue -ne 'u' -or
        !(Test-Path -LiteralPath (Join-Path $temporaryPath ('MonkeySharp\' + $installation.ScriptKey + '\userscript.js')))) {
        throw 'Packaged userscript services did not persist their source file and typed values.'
    }
    . (Join-Path $PSScriptRoot 'Helpers.ps1')
    . (Join-Path $PSScriptRoot 'PackageBrowserSmoke.ps1')
    Test-PackagedUserScripts $packagePath $temporaryPath $scripts $installation $runtimeFiles
    $nativeModules = @(Get-Process -Id $PID -Module | Where-Object ModuleName -eq 'SQLite.Interop.dll')
    if ($nativeModules.Count -ne 1 -or $nativeModules[0].FileName -ne $sqliteInteropPath) {
        throw 'SQLite must load its native library from the isolated package under Binaries\SQLite.'
    }
} finally {
    [void]$unregister.Invoke($null, $null)
}

$exclusiveFile = [IO.File]::Open($databasePath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
$exclusiveFile.Dispose()
Write-Host "Package smoke check passed ($Platform): native SQLite loading, lazy singletons, favicon/userscript persistence, real CEF injection and shutdown."
