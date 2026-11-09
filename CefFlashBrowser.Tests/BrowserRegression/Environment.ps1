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
        Application = $null
        Logger = $null
        LanguageService = $null
        FavoritesService = $null
        Database = $null
        DatabaseService = $null
        FaviconService = $null
        CefStarted = $false
        OriginalComSpec = [Environment]::GetEnvironmentVariable('ComSpec')
        AuxiliaryTypes = @()
        AuxiliaryAddress = $null
        AuxiliarySecondAddress = $null
        DisposedBrowser = $null
        BrowserCommandNames = @(
            'CreateShortcutCommand', 'AddFavoriteCommand', 'ToggleLoadingStateCommand',
            'FindNextTextCommand', 'FindPrevTextCommand'
        )
    }
}

function Get-RegressionDependencyPath($Context, [string]$Dependency) {
    $applicationPath = Join-Path $Context.ApplicationPath $Dependency
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

function Initialize-RegressionEnvironment($Context) {
    $dependencies = @(
        'SimpleMvvm.dll',
        'Microsoft.Xaml.Behaviors.dll',
        'HandyControl.dll',
        'Newtonsoft.Json.dll',
        'SharpVectors.Core.dll',
        'SharpVectors.Runtime.Wpf.dll',
        'SharpVectors.Converters.Wpf.dll',
        'CefFlashBrowser.Log.dll',
        'System.Data.SQLite.dll',
        'CefFlashBrowser.Database.dll',
        'CefFlashBrowser.WinformCefSharp4WPF.dll',
        'CefFlashBrowser.FlashBrowser.dll'
    )
    $cefDependencies = @('CefSharp.dll', 'CefSharp.Core.dll', 'CefSharp.WinForms.dll')
    $requiredFiles = @('CefFlashBrowser.exe', 'Binaries\SQLite\SQLite.Interop.dll',
        'Binaries\CmdProxy\CmdProxy.exe', 'Binaries\Plugins\pepflashplayer.dll',
        'Binaries\Plugins\readme.txt') + @(
        ($cefDependencies + @('CefSharp.BrowserSubprocess.exe', 'libcef.dll')) | ForEach-Object { "Binaries\CefSharp\$_" }
    )
    foreach ($file in $requiredFiles) {
        $path = Join-Path $Context.ApplicationPath $file
        if (!(Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Build output not found: $path. Build the solution with VS MSBuild for $($Context.Configuration)|$($Context.Platform) first."
        }
    }
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
    foreach ($dependency in $dependencies) {
        [void][Reflection.Assembly]::LoadFrom((Get-RegressionDependencyPath $Context $dependency))
    }
    [void][Reflection.Assembly]::LoadFrom((Join-Path $Context.CefPath 'CefSharp.dll'))
    $applicationAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $Context.ApplicationPath 'CefFlashBrowser.exe'))
    Assert-Regression ($applicationAssembly.GetManifestResourceNames() -contains 'CefFlashBrowser.Assets.SwfPlayer.swfplayer.html') 'SWF player page is embedded in the application'
    [void][CefFlashBrowser.Utils.Win32]::SetDllDirectory($Context.CefPath)
    foreach ($dependency in @('CefSharp.Core.dll', 'CefSharp.WinForms.dll')) {
        [void][Reflection.Assembly]::LoadFrom((Join-Path $Context.CefPath $dependency))
    }

    $references = @(
        (Join-Path $Context.CefPath 'CefSharp.dll'),
        $applicationAssembly.FullName,
        (Get-RegressionDependencyPath $Context 'CefFlashBrowser.FlashBrowser.dll')
    )
    Add-Type -ReferencedAssemblies $references -Path @(
        (Join-Path $Context.ScriptPath 'TestDoubles.cs'),
        (Join-Path $Context.ScriptPath 'FaviconHttpFixture.cs')
    )
    $Context.DisposedBrowser = (New-Object DisposedBrowserProxy([CefSharp.IBrowser])).GetTransparentProxy()
    $Context.AuxiliaryTypes = @(
        [CefFlashBrowser.Views.PopupWebPage],
        [CefFlashBrowser.Views.SwfPlayerWindow],
        [CefFlashBrowser.Views.ViewSourceWindow]
    )

    [System.Windows.Application]::ResourceAssembly = $applicationAssembly
    $Context.Application = New-Object System.Windows.Application
    $Context.Application.ShutdownMode = [System.Windows.ShutdownMode]::OnExplicitShutdown
    $globalDataType = $applicationAssembly.GetType('CefFlashBrowser.Data.GlobalData')
    $privateStatic = [Reflection.BindingFlags]'Static,NonPublic'
    foreach ($dataPath in @{
        DataPath = $Context.TemporaryPath
        FavoritesPath = (Join-Path $Context.TemporaryPath 'favorites.json')
        SettingsPath = (Join-Path $Context.TemporaryPath 'settings.json')
        DatabasePath = (Join-Path $Context.TemporaryPath 'browser.db')
        SqliteDllPath = (Join-Path $Context.ApplicationPath 'Binaries\SQLite')
    }.GetEnumerator()) {
        $globalDataType.GetField('<' + $dataPath.Key + '>k__BackingField', $privateStatic).SetValue($null, $dataPath.Value)
    }
    $globalDataType.GetField('<Settings>k__BackingField', $privateStatic).SetValue($null, [CefFlashBrowser.Models.Settings]::Default)
    # Keep the embedded page's virtual file address consistent with the deployed executable.
    $globalDataType.GetField('<SwfPlayerPath>k__BackingField', $privateStatic).SetValue(
        $null, (Join-Path $Context.ApplicationPath 'Assets\SwfPlayer\swfplayer.html'))
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
    [void]$applicationAssembly.GetType('CefFlashBrowser.Program').GetMethod('InitializeSQLite', $privateStatic).Invoke($null, $null)
    $Context.Database = New-Object CefFlashBrowser.Database.SqliteDatabase((Join-Path $Context.TemporaryPath 'browser.db'))
    $Context.DatabaseService = New-Object CefFlashBrowser.Services.DatabaseService($Context.Database)
    $Context.FaviconService = New-Object CefFlashBrowser.Services.FaviconService($Context.DatabaseService, $Context.Logger)
    [void]$registerInstance.MakeGenericMethod([CefFlashBrowser.Database.IDatabase]).Invoke(
        [SimpleMvvm.Ioc.SimpleIoc]::Global, @($Context.Database.PSObject.BaseObject))
    [void]$registerInstance.MakeGenericMethod([CefFlashBrowser.Services.DatabaseService]).Invoke(
        [SimpleMvvm.Ioc.SimpleIoc]::Global, @($Context.DatabaseService.PSObject.BaseObject))
    [void]$registerInstance.MakeGenericMethod([CefFlashBrowser.Services.FaviconService]).Invoke(
        [SimpleMvvm.Ioc.SimpleIoc]::Global, @($Context.FaviconService.PSObject.BaseObject))
    $faviconInitialization = $Context.FaviconService.InitializeAsync()
    Wait-Regression $Context { $faviconInitialization.IsCompleted } 'isolated favicon database initialization'
    [void]$faviconInitialization.GetAwaiter().GetResult()

    $languageDictionary = New-Object CefFlashBrowser.Infrastructure.Wpf.NamedResourceDictionary
    $languageDictionary.set_Name('Language')
    $languageDictionary.set_Source([Uri]'pack://application:,,,/CefFlashBrowser;component/Assets/Language/zh-CN.xaml')
    $Context.Application.Resources.MergedDictionaries.Add($languageDictionary)
    foreach ($dictionaryUri in @(
        'pack://application:,,,/HandyControl;component/Themes/SkinDefault.xaml',
        'pack://application:,,,/HandyControl;component/Themes/Theme.xaml'
    )) {
        $dictionary = New-Object System.Windows.ResourceDictionary
        $dictionary.set_Source([Uri]$dictionaryUri)
        $Context.Application.Resources.MergedDictionaries.Add($dictionary)
    }

    foreach ($converter in @(
        'BinaryToSizeStringConverter', 'IntToDoubleConverter', 'LanguageNameConverter', 'MultiEqualityConverter',
        'ObjectGetTypeConverter', 'ObjectToStringConverter', 'UrlGetFileNameConverter',
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

    # Keep this list in sync with the <sys:Uri> icon resources declared in App.xaml;
    # a missing icon here crashes template instantiation with XamlParseException.
    foreach ($icon in @('Add', 'Sub', 'Back', 'Forward', 'Reload', 'Stop', 'Warning', 'Search', 'More', 'Mute', 'Volume', 'Up', 'Down', 'Page', 'Close', 'Language', 'Settings')) {
        $Context.Application.Resources.Add($icon + 'IconUri', [Uri]('/Assets/Svgs/' + $icon.ToLowerInvariant() + '.svg', [UriKind]::Relative))
    }
    $Context.Application.Resources.Add('NewTabIconUri', [Uri]::new('/Assets/Svgs/new-tab.svg', [UriKind]::Relative))

    $auxiliaryFile = Join-Path $Context.TemporaryPath 'auxiliary.html'
    $auxiliarySecondFile = Join-Path $Context.TemporaryPath 'auxiliary-second.html'
    [IO.File]::WriteAllText($auxiliaryFile, '<title>Auxiliary regression</title><p>auxiliary-source-token</p>')
    [IO.File]::WriteAllText($auxiliarySecondFile, '<title>Second auxiliary page</title><p>second-source-token</p>')
    $Context.AuxiliaryAddress = ([Uri]$auxiliaryFile).AbsoluteUri
    $Context.AuxiliarySecondAddress = ([Uri]$auxiliarySecondFile).AbsoluteUri
}

function Start-RegressionCef($Context) {
    [Environment]::SetEnvironmentVariable('ComSpec',
        (Join-Path $Context.ApplicationPath 'Binaries\CmdProxy\CmdProxy.exe'))
    $cefSettings = New-Object CefFlashBrowser.FlashBrowser.CefFlashSettings
    $cefSettings.BrowserSubprocessPath = Join-Path $Context.CefPath 'CefSharp.BrowserSubprocess.exe'
    $cefSettings.ResourcesDirPath = $Context.CefPath
    $cefSettings.LocalesDirPath = Join-Path $Context.CefPath 'locales'
    $cefSettings.CachePath = Join-Path $Context.TemporaryPath 'Cache'
    $cefSettings.LogFile = Join-Path $Context.TemporaryPath 'cef.log'
    $cefSettings.CefCommandLineArgs['disable-gpu'] = '1'
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
