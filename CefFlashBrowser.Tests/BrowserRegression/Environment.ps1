function New-RegressionContext([string]$ScriptPath, [string]$Platform, [string]$Configuration) {
    $repositoryPath = Split-Path (Split-Path $ScriptPath -Parent) -Parent
    $buildPath = if ($Platform -eq 'x64') { "bin\x64\$Configuration" } else { "bin\$Configuration" }
    $applicationPath = Join-Path $repositoryPath $buildPath

    return [pscustomobject]@{
        ScriptPath = $ScriptPath
        RepositoryPath = $repositoryPath
        Platform = $Platform
        Configuration = $Configuration
        ApplicationPath = $applicationPath
        CefPath = Join-Path $applicationPath 'Binaries\CefSharp'
        TemporaryPath = Join-Path ([IO.Path]::GetTempPath()) ('CefFlashBrowser-regression-' + [Guid]::NewGuid())
        EmbeddedAssemblyPath = $null
        Application = $null
        Logger = $null
        LanguageService = $null
        FavoritesService = $null
        Database = $null
        DatabaseService = $null
        DownloadManagerService = $null
        FaviconService = $null
        UserScriptService = $null
        CefStarted = $false
        OriginalComSpec = [Environment]::GetEnvironmentVariable('ComSpec')
        AuxiliaryTypes = @()
        AuxiliaryAddress = $null
        AuxiliarySecondAddress = $null
        DisposedBrowser = $null
        BrowserCommandNames = @(
            'CreateShortcutCommand', 'AddFavoriteCommand', 'ToggleLoadingStateCommand',
            'FindNextTextCommand', 'FindPrevTextCommand', 'ToggleMuteCommand', 'ViewSourceCommand'
        )
    }
}

function Get-RegressionDependencyPath($Context, [string]$Dependency) {
    $applicationPath = Join-Path $Context.ApplicationPath $Dependency
    if ($Context.Configuration -eq 'Release') {
        if ($null -ne $Context.EmbeddedAssemblyPath) {
            $embeddedPath = Join-Path $Context.EmbeddedAssemblyPath $Dependency
            if (Test-Path -LiteralPath $embeddedPath -PathType Leaf) { return $embeddedPath }
        }
        return $applicationPath
    }
    $projectOutputPath = Join-Path $Context.RepositoryPath (
        "$([IO.Path]::GetFileNameWithoutExtension($Dependency))\bin\$($Context.Platform)\$($Context.Configuration)\net462\$Dependency")

    if (Test-Path -LiteralPath $projectOutputPath -PathType Leaf) {
        if (!(Test-Path -LiteralPath $applicationPath -PathType Leaf) -or
            (Get-Item -LiteralPath $projectOutputPath).LastWriteTimeUtc -gt (Get-Item -LiteralPath $applicationPath).LastWriteTimeUtc) {
            return $projectOutputPath
        }
    }

    return $applicationPath
}

function Expand-RegressionEmbeddedAssemblies($Context) {
    if ($Context.Configuration -ne 'Release') { return }
    # Add-Type needs physical compiler references. Use the built executable's bytes,
    # never another project's output or the developer's package cache for Release.
    $assembly = [Reflection.Assembly]::ReflectionOnlyLoadFrom((Join-Path $Context.ApplicationPath 'CefFlashBrowser.exe'))
    $Context.EmbeddedAssemblyPath = Join-Path $Context.TemporaryPath 'Assemblies'
    [void][IO.Directory]::CreateDirectory($Context.EmbeddedAssemblyPath)
    foreach ($name in $assembly.GetManifestResourceNames()) {
        if (!$name.StartsWith('costura.') -or !$name.EndsWith('.dll.compressed')) { continue }
        $fileName = $name.Substring(8, $name.Length - 8 - '.compressed'.Length)
        if ([IO.Path]::GetFileName($fileName) -ne $fileName) { throw 'Invalid embedded dependency name.' }
        $resource = $assembly.GetManifestResourceStream($name)
        try {
            $decoded = New-Object IO.Compression.DeflateStream($resource, [IO.Compression.CompressionMode]::Decompress)
            try {
                $file = [IO.File]::Create((Join-Path $Context.EmbeddedAssemblyPath $fileName))
                try { $decoded.CopyTo($file) } finally { $file.Dispose() }
            } finally { $decoded.Dispose() }
        } finally { $resource.Dispose() }
    }
}

function Initialize-RegressionEnvironment($Context, [string]$DatabasePath) {
    $dependencies = @(
        'ICSharpCode.AvalonEdit.dll',
        'Mzying2001.MonkeySharp.Core.dll',
        'Mzying2001.MonkeySharp.CefSharp.dll',
        'SimpleMvvm.dll',
        'Microsoft.Xaml.Behaviors.dll',
        'HandyControl.dll',
        'System.Text.Json.dll',
        'SharpVectors.Core.dll',
        'SharpVectors.Runtime.Wpf.dll',
        'SharpVectors.Converters.Wpf.dll',
        'CefFlashBrowser.Log.dll',
        'System.Data.SQLite.dll',
        'CefFlashBrowser.Database.dll',
        'CefFlashBrowser.WinformCefSharp4WPF.dll',
        'CefFlashBrowser.FlashBrowser.dll',
        'CefSharp.WinForms.dll'
    )
    $cefDependencies = @('CefSharp.dll', 'CefSharp.Core.dll', 'CefSharp.Core.Runtime.dll')
    $requiredFiles = @('CefFlashBrowser.exe', 'Binaries\SQLite\SQLite.Interop.dll',
        'Binaries\CmdProxy\CmdProxy.exe', 'Binaries\Plugins\pepflashplayer.dll',
        'Binaries\Plugins\readme.txt') + @(
        ($cefDependencies + @('CefSharp.BrowserSubprocess.exe', 'libcef.dll', 'v8_context_snapshot.bin')) | ForEach-Object { "Binaries\CefSharp\$_" }
    )
    foreach ($file in $requiredFiles) {
        $path = Join-Path $Context.ApplicationPath $file
        if (!(Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Build output not found: $path. Build the solution with VS MSBuild for $($Context.Configuration)|$($Context.Platform) first."
        }
    }
    Expand-RegressionEmbeddedAssemblies $Context
    foreach ($dependency in $dependencies) {
        $path = Get-RegressionDependencyPath $Context $dependency
        if (!(Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Build output not found: $path. Build the solution with VS MSBuild for $($Context.Configuration)|$($Context.Platform) first."
        }
    }

    Assert-Regression (!(Test-Path -LiteralPath (Join-Path $Context.ApplicationPath 'Assets'))) 'build output has no external Assets directory'
    Assert-Regression (@(Get-ChildItem -LiteralPath $Context.ApplicationPath -Recurse -File -Filter '*.tar.gz').Count -eq 0) 'build output has no dependency archives'

    [void][IO.Directory]::CreateDirectory($Context.TemporaryPath)
    Write-Host "Isolated test data: $($Context.TemporaryPath)"
    Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Xaml, System.Windows.Forms
    if ($null -ne [System.Windows.Application]::Current) {
        throw 'Run the regression in a fresh Windows PowerShell process.'
    }
    Add-Type -Path (Join-Path $Context.ScriptPath 'AssemblyResolver.cs')
    [RegressionAssemblyResolver]::Install($Context.ApplicationPath)
    if ($null -ne $Context.EmbeddedAssemblyPath) {
        [RegressionAssemblyResolver]::Install($Context.EmbeddedAssemblyPath)
    }
    foreach ($dependency in $dependencies) {
        [void][Reflection.Assembly]::LoadFrom((Get-RegressionDependencyPath $Context $dependency))
    }
    [void][Reflection.Assembly]::LoadFrom((Join-Path $Context.CefPath 'CefSharp.dll'))
    [CefSharp.CefSharpSettings]::ConcurrentTaskExecution = $true
    [CefSharp.CefSharpSettings]::WcfEnabled = $false
    $applicationAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $Context.ApplicationPath 'CefFlashBrowser.exe'))
    Assert-Regression ($applicationAssembly.GetManifestResourceNames() -contains 'CefFlashBrowser.Assets.SwfPlayer.swfplayer.html') 'SWF player page is embedded in the application'
    $privateStatic = [Reflection.BindingFlags]'Static,NonPublic'
    $globalDataType = $applicationAssembly.GetType('CefFlashBrowser.Data.GlobalData')
    foreach ($nativePath in @{
        CefDllPath = $Context.CefPath
        SqliteDllPath = (Join-Path $Context.ApplicationPath 'Binaries\SQLite')
        VcRuntimeDllPath = (Join-Path $Context.ApplicationPath 'Binaries\VCRuntime')
    }.GetEnumerator()) {
        $globalDataType.GetField('<' + $nativePath.Key + '>k__BackingField', $privateStatic).SetValue($null, $nativePath.Value)
    }
    [void]$applicationAssembly.GetType('CefFlashBrowser.Program').GetMethod('InitializeDllDirectories', $privateStatic).Invoke($null, $null)
    [void][Reflection.Assembly]::LoadFrom((Join-Path $Context.CefPath 'CefSharp.Core.dll'))

    $references = @(
        'System.Net.Http',
        (Get-RegressionDependencyPath $Context 'CefFlashBrowser.Database.dll'),
        [System.Xaml.XamlReader].Assembly.Location,
        [System.Windows.Interop.HwndHost].Assembly.Location,
        [System.Windows.DependencyObject].Assembly.Location,
        [System.Windows.Media.Visual].Assembly.Location,
        (Join-Path $Context.CefPath 'CefSharp.dll'),
        (Join-Path $Context.CefPath 'CefSharp.Core.dll'),
        $applicationAssembly.FullName,
        (Get-RegressionDependencyPath $Context 'CefFlashBrowser.FlashBrowser.dll'),
        (Get-RegressionDependencyPath $Context 'SimpleMvvm.dll'),
        (Get-RegressionDependencyPath $Context 'CefFlashBrowser.WinformCefSharp4WPF.dll'),
        (Get-RegressionDependencyPath $Context 'Mzying2001.MonkeySharp.Core.dll'),
        (Get-RegressionDependencyPath $Context 'Mzying2001.MonkeySharp.CefSharp.dll')
    )
    Add-Type -ReferencedAssemblies $references -Path @(
        (Join-Path $Context.ScriptPath 'TestDoubles.cs'),
        (Join-Path $Context.ScriptPath 'FaviconHttpFixture.cs'),
        (Join-Path $Context.ScriptPath 'UserScriptSeamProbe.cs'),
        (Join-Path $Context.ScriptPath 'UserScriptMenuProbe.cs'),
        (Join-Path $Context.ScriptPath 'UserScriptInteractionProbe.cs'),
        (Join-Path $Context.ScriptPath 'UserScriptHttpFixture.cs'),
        (Join-Path $Context.ScriptPath 'DownloadHttpFixture.cs'),
        (Join-Path $Context.ScriptPath 'UserScriptFrameProbe.cs'),
        (Join-Path $Context.ScriptPath 'UserScriptRuntimeProbe.cs')
    )
    $Context.DisposedBrowser = (New-Object DisposedBrowserProxy([CefSharp.IBrowser])).GetTransparentProxy()
    $Context.AuxiliaryTypes = @(
        [CefFlashBrowser.Views.PopupWebPage],
        [CefFlashBrowser.Views.SwfPlayerWindow]
    )

    [System.Windows.Application]::ResourceAssembly = $applicationAssembly
    $Context.Application = New-Object System.Windows.Application
    $Context.Application.ShutdownMode = [System.Windows.ShutdownMode]::OnExplicitShutdown
    if (!$DatabasePath) { $DatabasePath = Join-Path $Context.TemporaryPath 'browser.db' }
    foreach ($dataPath in @{
        DataPath = $Context.TemporaryPath
        FavoritesPath = (Join-Path $Context.TemporaryPath 'favorites.json')
        SettingsPath = (Join-Path $Context.TemporaryPath 'settings.json')
        DatabasePath = $DatabasePath
        MonkeySharpPath = (Join-Path $Context.TemporaryPath 'MonkeySharp')
        UserScriptCachePath = (Join-Path $Context.TemporaryPath 'Caches\UserScripts')
    }.GetEnumerator()) {
        $globalDataType.GetField('<' + $dataPath.Key + '>k__BackingField', $privateStatic).SetValue($null, $dataPath.Value)
    }
    $globalDataType.GetField('<Settings>k__BackingField', $privateStatic).SetValue($null, [CefFlashBrowser.Models.Settings]::Default)
    # Keep the embedded page's virtual file address consistent with the deployed executable.
    $globalDataType.GetField('<SwfPlayerPath>k__BackingField', $privateStatic).SetValue(
        $null, (Join-Path $Context.ApplicationPath 'Assets\SwfPlayer\swfplayer.html'))
    [CefFlashBrowser.Data.GlobalData]::Settings.UserScriptDownloadDirectory = Join-Path $Context.TemporaryPath 'Downloads'
    [CefFlashBrowser.Data.GlobalData]::Settings.DownloadDirectory = Join-Path $Context.TemporaryPath 'Downloads'
    [CefFlashBrowser.Data.GlobalData]::Settings.NewTabAddress = 'about:blank'
    [CefFlashBrowser.Data.GlobalData]::IsStartWithoutMainWindow = $true
    $locator = New-Object CefFlashBrowser.ViewModels.ViewModelLocator
    $Context.Application.Resources.Add('Locator', $locator.PSObject.BaseObject)
    $Context.Application.Resources.Add('AppInfo', (New-Object CefFlashBrowser.Infrastructure.Wpf.AppInfo))
    $Context.Logger = New-Object CefFlashBrowser.Log.FileLogger((Join-Path $Context.TemporaryPath 'app.log'))
    $registerInstance = [SimpleMvvm.Ioc.SimpleIoc].GetMethods() | Where-Object {
        $_.Name -eq 'Register' -and $_.IsGenericMethodDefinition -and
        $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.IsGenericParameter
    }
    [void]$registerInstance.MakeGenericMethod([CefFlashBrowser.Log.ILogger]).Invoke(
        [SimpleMvvm.Ioc.SimpleIoc]::Global, @($Context.Logger.PSObject.BaseObject))

    $Context.FavoritesService = New-Object CefFlashBrowser.Services.FavoritesService(
        [CefFlashBrowser.Data.GlobalData]::FavoritesPath, $Context.Logger)
    [void]$registerInstance.MakeGenericMethod([CefFlashBrowser.Services.FavoritesService]).Invoke(
        [SimpleMvvm.Ioc.SimpleIoc]::Global, @($Context.FavoritesService.PSObject.BaseObject))

    [Threading.SynchronizationContext]::SetSynchronizationContext(
        (New-Object System.Windows.Threading.DispatcherSynchronizationContext($Context.Application.Dispatcher)))
    $Context.Database = New-Object CefFlashBrowser.Database.SqliteDatabase($DatabasePath)
    $Context.DatabaseService = New-Object CefFlashBrowser.Services.DatabaseService($Context.Database)
    $Context.FaviconService = New-Object CefFlashBrowser.Services.FaviconService($Context.DatabaseService, $Context.Logger)
    [void]$registerInstance.MakeGenericMethod([CefFlashBrowser.Database.IDatabase]).Invoke(
        [SimpleMvvm.Ioc.SimpleIoc]::Global, @($Context.Database.PSObject.BaseObject))
    [void]$registerInstance.MakeGenericMethod([CefFlashBrowser.Services.DatabaseService]).Invoke(
        [SimpleMvvm.Ioc.SimpleIoc]::Global, @($Context.DatabaseService.PSObject.BaseObject))
    [void]$registerInstance.MakeGenericMethod([CefFlashBrowser.Services.FaviconService]).Invoke(
        [SimpleMvvm.Ioc.SimpleIoc]::Global, @($Context.FaviconService.PSObject.BaseObject))
    $scriptStorage = New-Object CefFlashBrowser.Services.UserScripts.Persistence.UserScriptStorage($Context.DatabaseService)
    $scriptContent = New-Object CefFlashBrowser.Services.UserScripts.UserScriptContentService($null, $null, 10485760, $null)
    $scriptPrompt = New-Object CefFlashBrowser.Browser.UserScripts.UserScriptPermissionPrompt
    $Context.UserScriptService = New-Object CefFlashBrowser.Services.UserScripts.UserScriptService(
        $scriptStorage, (Join-Path $Context.TemporaryPath 'MonkeySharp'), (Join-Path $Context.TemporaryPath 'Caches\UserScripts'), $scriptContent, $scriptPrompt, $null)
    [void]$registerInstance.MakeGenericMethod([CefFlashBrowser.Services.UserScripts.UserScriptService]).Invoke(
        [SimpleMvvm.Ioc.SimpleIoc]::Global, @($Context.UserScriptService.PSObject.BaseObject))
    $faviconInitialization = $Context.FaviconService.InitializeAsync()
    Wait-Regression $Context { $faviconInitialization.IsCompleted } 'isolated favicon database initialization'
    [void]$faviconInitialization.GetAwaiter().GetResult()

    $languageDictionary = New-Object CefFlashBrowser.Infrastructure.Wpf.NamedResourceDictionary
    $languageDictionary.set_Name('Language')
    $languageDictionary.set_Source([Uri]'pack://application:,,,/CefFlashBrowser;component/Assets/Language/zh-CN.xaml')
    $Context.Application.Resources.MergedDictionaries.Add($languageDictionary)
    foreach ($dictionaryUri in @(
        'pack://application:,,,/HandyControl;component/Themes/SkinDefault.xaml',
        'pack://application:,,,/HandyControl;component/Themes/Theme.xaml',
        'pack://application:,,,/CefFlashBrowser;component/Themes/ListItems.xaml',
        'pack://application:,,,/CefFlashBrowser;component/Themes/MenuItems.xaml'
    )) {
        $dictionary = New-Object System.Windows.ResourceDictionary
        $dictionary.set_Source([Uri]$dictionaryUri)
        $Context.Application.Resources.MergedDictionaries.Add($dictionary)
    }

    # Load shared UI resources from App.xaml into the isolated test application.
    $appXamlPath = Join-Path $Context.RepositoryPath 'CefFlashBrowser\App.xaml'
    [xml]$appXaml = [IO.File]::ReadAllText($appXamlPath)
    $namespaces = [Xml.XmlNamespaceManager]::new($appXaml.NameTable)
    $namespaces.AddNamespace('wpf', $appXaml.DocumentElement.NamespaceURI)
    $namespaces.AddNamespace('x', 'http://schemas.microsoft.com/winfx/2006/xaml')
    $namespaces.AddNamespace('sys', $appXaml.DocumentElement.GetNamespaceOfPrefix('sys'))
    $svgTemplate = $appXaml.SelectSingleNode("//wpf:DataTemplate[@x:Key='SharedSvgIconTemplate']", $namespaces)
    $Context.Application.Resources.Add('SharedSvgIconTemplate', [System.Windows.Markup.XamlReader]::Parse($svgTemplate.OuterXml))
    foreach ($icon in $appXaml.SelectNodes('//sys:Uri[@x:Key]', $namespaces)) {
        $key = $icon.GetAttribute('Key', $namespaces.LookupNamespace('x'))
        $Context.Application.Resources.Add($key, [Uri]::new($icon.InnerText, [UriKind]::Relative))
    }

    # Match App.xaml's implicit SVG style so local icon styles are tested with theme colors.
    $svgStyle = [System.Windows.Markup.XamlReader]::Parse(@'
<Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
       xmlns:svgc="http://sharpvectors.codeplex.com/svgc/" TargetType="svgc:SvgIcon">
    <Setter Property="Fill" Value="{DynamicResource PrimaryTextBrush}"/>
</Style>
'@)
    $Context.Application.Resources.Add([SharpVectors.Converters.SvgIcon], $svgStyle)

    foreach ($converter in @(
        'BinaryToSizeStringConverter', 'IntToDoubleConverter', 'LanguageNameConverter', 'MonospaceFontWeightConverter',
        'MultiEqualityConverter', 'ObjectGetTypeConverter', 'ObjectToStringConverter',
        'WebsiteIconUrlConverter', 'ZoomLevelToScaleConverter'
    )) {
        $Context.Application.Resources.Add($converter, (New-Object "CefFlashBrowser.Views.Converters.$converter"))
    }
    $Context.Application.Resources.Add('BooleanToVisibilityConverter', (New-Object System.Windows.Controls.BooleanToVisibilityConverter))
    $readableSizeConverter = New-Object CefFlashBrowser.Views.Converters.BinaryToSizeStringConverter
    $readableSizeConverter.UseReadableSize = $true
    $Context.Application.Resources.Add('BinaryToReadableSizeStringConverter', $readableSizeConverter)
    $twoThirdConverter = New-Object CefFlashBrowser.Views.Converters.MultiplyConverter
    $twoThirdConverter.Multiplier = 0.6666666666666667
    $Context.Application.Resources.Add('TwoThirdConverter', $twoThirdConverter)

    # Exercise automatic initialization with a saved language different from App's default.
    $savedLanguage = [CefFlashBrowser.Data.GlobalData]::Settings.Language
    [CefFlashBrowser.Data.GlobalData]::Settings.Language = 'en-US'
    $Context.LanguageService = New-Object CefFlashBrowser.Services.LanguageService($Context.Logger)
    [void]$registerInstance.MakeGenericMethod([CefFlashBrowser.Services.LanguageService]).Invoke(
        [SimpleMvvm.Ioc.SimpleIoc]::Global, @($Context.LanguageService.PSObject.BaseObject))
    $languageService = [SimpleMvvm.Ioc.SimpleIoc]::Global.GetInstance([CefFlashBrowser.Services.LanguageService], @())
    Assert-Regression ([object]::ReferenceEquals($Context.LanguageService, $languageService) -and
        [object]::ReferenceEquals($languageService, [SimpleMvvm.Ioc.SimpleIoc]::Global.GetInstance(
            [CefFlashBrowser.Services.LanguageService], @()))) 'language lookups share the registered service instance'
    Assert-Regression ($languageService.CurrentLanguage -eq 'en-US' -and
        $languageService.GetLocale('en-US') -eq 'en-US' -and
        $Context.Application.FindResource('language_name') -eq 'English') 'language construction applies the saved language before CEF starts'
    $languageService.SetCurrentLanguage($savedLanguage)

    $registerFactory = [SimpleMvvm.Ioc.SimpleIoc].GetMethods() | Where-Object {
        $parameters = $_.GetParameters()
        $_.Name -eq 'Register' -and $_.IsGenericMethodDefinition -and $parameters.Count -eq 2 -and
        $parameters[0].ParameterType.IsGenericType -and
        $parameters[0].ParameterType.GetGenericTypeDefinition().FullName -eq 'System.Func`1'
    }
    $downloadManagerType = [CefFlashBrowser.Services.Downloads.DownloadManagerService]
    $downloadManagerFactory = [Func[CefFlashBrowser.Services.Downloads.DownloadManagerService]]({
        $Context.DownloadManagerService = New-Object CefFlashBrowser.Services.Downloads.DownloadManagerService(
            $Context.DatabaseService, $Context.Logger, $Context.LanguageService, $Context.Application)
        return $Context.DownloadManagerService
    }.GetNewClosure())
    [void]$registerFactory.MakeGenericMethod($downloadManagerType).Invoke(
        [SimpleMvvm.Ioc.SimpleIoc]::Global, @($downloadManagerFactory, [SimpleMvvm.Ioc.Lifetime]::Singleton))

    $auxiliaryFile = Join-Path $Context.TemporaryPath 'auxiliary.html'
    $auxiliarySecondFile = Join-Path $Context.TemporaryPath 'auxiliary-second.html'
    [IO.File]::WriteAllText($auxiliaryFile, '<title>Auxiliary regression</title><p>auxiliary-source-token</p>')
    [IO.File]::WriteAllText($auxiliarySecondFile, '<title>Second auxiliary page</title><p>second-source-token</p>')
    $Context.AuxiliaryAddress = ([Uri]$auxiliaryFile).AbsoluteUri
    $Context.AuxiliarySecondAddress = ([Uri]$auxiliarySecondFile).AbsoluteUri
}

function Start-RegressionCef($Context, [string]$CachePath = '', [switch]$EnableFlash) {
    [Environment]::SetEnvironmentVariable('ComSpec',
        (Join-Path $Context.ApplicationPath 'Binaries\CmdProxy\CmdProxy.exe'))
    $cefSettings = New-Object CefFlashBrowser.FlashBrowser.CefFlashSettings
    $cefSettings.BrowserSubprocessPath = Join-Path $Context.CefPath 'CefSharp.BrowserSubprocess.exe'
    $cefSettings.ResourcesDirPath = $Context.CefPath
    $cefSettings.LocalesDirPath = Join-Path $Context.CefPath 'locales'
    $cefSettings.CachePath = if ($CachePath) { $CachePath } else { Join-Path $Context.TemporaryPath 'Cache' }
    $cefSettings.LogFile = Join-Path $Context.TemporaryPath 'cef.log'
    $cefSettings.CefCommandLineArgs['disable-gpu'] = '1'
    if ($EnableFlash) {
        $cefSettings.PpapiFlashPath = Join-Path $Context.ApplicationPath 'Binaries\Plugins\pepflashplayer.dll'
        $cefSettings.PpapiFlashVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($cefSettings.PpapiFlashPath).FileVersion.Replace(',', '.')
        $cefSettings.EnableSystemFlash = $true
        $cefSettings.CefCommandLineArgs['override-plugin-power-saver-for-testing'] = 'never'
    }
    $Context.CefStarted = [CefSharp.Cef]::Initialize($cefSettings)
    if (!$Context.CefStarted) {
        throw 'CEF initialization failed'
    }
}

function Stop-RegressionEnvironment($Context) {
    $cleanupErrors = New-Object 'System.Collections.Generic.List[object]'
    if ($null -ne $Context.Application) {
        try {
            $windows = @($Context.Application.Windows)
            foreach ($window in $windows) {
                try {
                    if ($window -is [CefFlashBrowser.Views.BrowserWindow] -or $Context.AuxiliaryTypes.Contains($window.GetType())) {
                        $window.ForceCloseWindow()
                    } else {
                        $window.Close()
                    }
                } catch {
                    $cleanupErrors.Add($_)
                }
            }
        } catch {
            $cleanupErrors.Add($_)
        }
    }
    if ($null -ne $Context.DownloadManagerService) {
        try {
            $downloads = $Context.DownloadManagerService.StopAsync()
            Wait-Regression $Context { $downloads.IsCompleted } 'download manager shutdown'
            [void]$downloads.GetAwaiter().GetResult()
        } catch {
            $cleanupErrors.Add($_)
        }
    }
    if ($null -ne $Context.UserScriptService) {
        try {
            $scripts = $Context.UserScriptService.StopAsync()
            Wait-Regression $Context { $scripts.IsCompleted } 'userscript runtime shutdown'
            [void]$scripts.GetAwaiter().GetResult()
        } catch {
            $cleanupErrors.Add($_)
        }
    }
    if ($Context.CefStarted) {
        try {
            [CefSharp.Cef]::Shutdown()
        } catch {
            $cleanupErrors.Add($_)
        } finally {
            $Context.CefStarted = $false
        }
    }
    if ($null -ne $Context.FaviconService) {
        try {
            [void]$Context.FaviconService.StopAsync().GetAwaiter().GetResult()
        } catch {
            $cleanupErrors.Add($_)
        }
    }
    if ($null -ne $Context.Database) {
        try {
            $Context.Database.Dispose()
        } catch {
            $cleanupErrors.Add($_)
        }
    }
    if ($null -ne $Context.Logger) {
        try {
            $Context.Logger.Dispose()
        } catch {
            $cleanupErrors.Add($_)
        }
    }
    [Environment]::SetEnvironmentVariable('ComSpec', $Context.OriginalComSpec)
    return $cleanupErrors.ToArray()
}
