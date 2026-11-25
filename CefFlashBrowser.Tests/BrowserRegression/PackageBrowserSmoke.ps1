function Test-PackagedUserScripts($PackagePath, $TemporaryPath, $Scripts, $Installation, [string[]]$RuntimeFiles) {
    # Resolve only the supplied archive and its Costura resources, without build-output fallbacks.
    $cefPath = Join-Path $PackagePath 'Binaries\CefSharp'
    foreach ($dependency in @('CefSharp.dll', 'CefSharp.Core.dll')) {
        [void][Reflection.Assembly]::LoadFrom((Join-Path $cefPath $dependency))
    }
    foreach ($assemblyName in @('CefSharp.WinForms', 'CefFlashBrowser.WinformCefSharp4WPF', 'CefFlashBrowser.FlashBrowser',
        'Mzying2001.MonkeySharp.CefSharp')) {
        [void][Reflection.Assembly]::Load($assemblyName)
    }
    [CefSharp.CefSharpSettings]::ConcurrentTaskExecution = $true
    [CefSharp.CefSharpSettings]::WcfEnabled = $false
    $application = New-Object System.Windows.Application
    $application.ShutdownMode = [System.Windows.ShutdownMode]::OnExplicitShutdown
    $context = [pscustomobject]@{ Application = $application }
    $cefStarted = $false
    $browser = $null
    $scriptHost = $null
    $window = $null
    $originalComSpec = [Environment]::GetEnvironmentVariable('ComSpec')
    try {
        $source = @'
// ==UserScript==
// @name Packaged runtime
// @match file:///*
// @sandbox DOM
// @grant GM_getValue
// @grant GM.setValue
// @run-at document-end
// ==/UserScript==
var restored = GM_getValue('packaged', 42);
GM.setValue('packaged-commit', 42).then(function () {
    document.documentElement.setAttribute('data-packaged', restored === undefined ? 'ready' : 'missing');
});
'@
        [void]$Scripts.Repository.UpdateAsync($Installation.ScriptKey, $source, $null,
            [Threading.CancellationToken]::None).GetAwaiter().GetResult()
        $pagePath = Join-Path $TemporaryPath 'packaged.html'
        [IO.File]::WriteAllText($pagePath, '<html><body>Packaged userscript</body></html>',
            (New-Object Text.UTF8Encoding($false)))

        [Environment]::SetEnvironmentVariable('ComSpec', (Join-Path $PackagePath 'Binaries\CmdProxy\CmdProxy.exe'))
        $settings = New-Object CefFlashBrowser.FlashBrowser.CefFlashSettings
        $subprocessPath = Join-Path $cefPath 'CefSharp.BrowserSubprocess.exe'
        $settings.BrowserSubprocessPath = $subprocessPath
        $settings.ResourcesDirPath = $cefPath
        $settings.LocalesDirPath = Join-Path $cefPath 'locales'
        $settings.CachePath = Join-Path $TemporaryPath 'Cache'
        $settings.LogFile = Join-Path $TemporaryPath 'cef.log'
        $settings.PpapiFlashPath = Join-Path $PackagePath 'Binaries\Plugins\pepflashplayer.dll'
        $settings.CefCommandLineArgs['disable-gpu'] = '1'
        $cefStarted = [CefSharp.Cef]::Initialize($settings)
        Assert-Regression $cefStarted 'packaged CEF initializes from the extracted archive'

        $browser = New-Object CefFlashBrowser.FlashBrowser.ChromiumFlashBrowser($true)
        $builder = New-Object Mzying2001.MonkeySharp.CefSharp.CefSharpUserScriptHostBuilder($Scripts.Repository)
        $scriptHost = $builder.UseValueStore($Scripts.ValueStore).Build()
        $scriptHost.Attach($browser)
        Assert-Regression (!$scriptHost.IsLateAttach) 'packaged host attaches before native creation'
        $window = New-Object System.Windows.Window
        $window.Width = 800
        $window.Height = 600
        $window.Left = -10000
        $window.Top = -10000
        $window.ShowActivated = $false
        $window.ShowInTaskbar = $false
        $window.Content = $browser
        $window.Show()
        Wait-Regression $context { $browser.IsBrowserInitialized } 'packaged native browser creation'
        $browser.LoadPrepared('about:blank')
        $preparation = $scriptHost.PrepareAsync([Threading.CancellationToken]::None)
        Wait-Regression $context { $preparation.IsCompleted } 'packaged isolated-world preparation'
        [void]$preparation.GetAwaiter().GetResult()
        $navigation = $scriptHost.NavigateAsync(([Uri]$pagePath).AbsoluteUri, [Threading.CancellationToken]::None)
        Wait-Regression $context { $navigation.IsCompleted } 'packaged target navigation'
        [void]$navigation.GetAwaiter().GetResult()
        Wait-Regression $context {
            $browser.CanExecuteJavascriptInMainFrame -and
            (Invoke-JavaScript $context $browser "document.documentElement && document.documentElement.getAttribute('data-packaged')") -eq 'ready'
        } 'packaged userscript injection and synchronous typed-value bootstrap'
        $committed = $Scripts.ValueStore.GetAsync($Installation.ScriptKey.ToString(), 'packaged-commit',
            [Threading.CancellationToken]::None).GetAwaiter().GetResult()
        Assert-Regression ($committed.Exists -and $committed.Value.EncodedValue -eq 'n42') 'packaged asynchronous GM write reaches SQLite before acknowledgement'
        $nativeCef = @(Get-Process -Id $PID -Module | Where-Object ModuleName -eq 'libcef.dll')
        Assert-Regression ($nativeCef.Count -eq 1 -and $nativeCef[0].FileName -eq (Join-Path $cefPath 'libcef.dll')) 'native CEF loads from the supplied package'
        Wait-Regression $context {
            foreach ($subprocess in [Diagnostics.Process]::GetProcessesByName('CefSharp.BrowserSubprocess')) {
                try {
                    if ($subprocess.MainModule.FileName -ne $subprocessPath) { continue }
                    $subprocessModules = @($subprocess.Modules | ForEach-Object FileName)
                    $missingRuntimeFiles = @($RuntimeFiles | Where-Object {
                        $subprocessModules -notcontains (Join-Path $cefPath $_)
                    })
                    if ($missingRuntimeFiles.Count -eq 0) { return $true }
                } catch [ComponentModel.Win32Exception], [InvalidOperationException] {
                    # CEF can exit a subprocess between enumeration and module inspection.
                    continue
                } finally {
                    $subprocess.Dispose()
                }
            }
            return $false
        } 'CEF subprocess loads the bundled VC++ runtime'
        Assert-Regression $true 'CEF subprocess loads the bundled VC++ runtime'
    } finally {
        try {
            if ($null -ne $scriptHost) {
                try {
                    $detach = $scriptHost.DetachAsync([Threading.CancellationToken]::None)
                    Wait-Regression $context { $detach.IsCompleted } 'packaged host detach before native disposal'
                    [void]$detach.GetAwaiter().GetResult()
                } finally {
                    $scriptHost.Dispose()
                }
            }
        } finally {
            if ($null -ne $browser) { $browser.Dispose() }
            if ($null -ne $window) { $window.Close() }
            if ($cefStarted) { [CefSharp.Cef]::Shutdown() }
            [Environment]::SetEnvironmentVariable('ComSpec', $originalComSpec)
            $application.Shutdown()
        }
    }
}
