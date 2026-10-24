function New-RegressionContext([string]$ScriptPath, [string]$Platform, [string]$Configuration) {
    $repositoryPath = Split-Path (Split-Path $ScriptPath -Parent) -Parent
    $buildPath = if ($Platform -eq 'x64') { "bin\x64\$Configuration" } else { "bin\$Configuration" }
    $applicationPath = Join-Path $repositoryPath $buildPath

    return [pscustomobject]@{
        ScriptPath = $ScriptPath
        Platform = $Platform
        Configuration = $Configuration
        ApplicationPath = $applicationPath
        CefPath = Join-Path $applicationPath 'Assets\CefSharp'
        TemporaryPath = Join-Path ([IO.Path]::GetTempPath()) ('CefFlashBrowser-regression-' + [Guid]::NewGuid())
        Application = $null
        Logger = $null
        CefStarted = $false
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
        'CefFlashBrowser.WinformCefSharp4WPF.dll',
        'CefFlashBrowser.FlashBrowser.dll'
    )
    $cefDependencies = @('CefSharp.dll', 'CefSharp.Core.dll', 'CefSharp.WinForms.dll')
    $requiredFiles = @('CefFlashBrowser.exe', 'Assets\SwfPlayer\swfplayer.html') + $dependencies + @(
        ($cefDependencies + @('CefSharp.BrowserSubprocess.exe', 'libcef.dll')) | ForEach-Object { "Assets\CefSharp\$_" }
    )
    foreach ($file in $requiredFiles) {
        $path = Join-Path $Context.ApplicationPath $file
        if (!(Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Build output not found: $path. Build the solution with VS MSBuild for $($Context.Configuration)|$($Context.Platform) first."
        }
    }

    [void][IO.Directory]::CreateDirectory($Context.TemporaryPath)
    Write-Host "Isolated test data: $($Context.TemporaryPath)"
    Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Xaml, System.Windows.Forms
    if ($null -ne [System.Windows.Application]::Current) {
        throw 'Run the regression in a fresh Windows PowerShell process.'
    }
    foreach ($dependency in $dependencies) {
        [void][Reflection.Assembly]::LoadFrom((Join-Path $Context.ApplicationPath $dependency))
    }
    [void][Reflection.Assembly]::LoadFrom((Join-Path $Context.CefPath 'CefSharp.dll'))
    $applicationAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $Context.ApplicationPath 'CefFlashBrowser.exe'))
    [void][CefFlashBrowser.Utils.Win32]::SetDllDirectory($Context.CefPath)
    foreach ($dependency in @('CefSharp.Core.dll', 'CefSharp.WinForms.dll')) {
        [void][Reflection.Assembly]::LoadFrom((Join-Path $Context.CefPath $dependency))
    }

    $references = @(
        (Join-Path $Context.CefPath 'CefSharp.dll'),
        (Join-Path $Context.ApplicationPath 'CefFlashBrowser.FlashBrowser.dll')
    )
    Add-Type -ReferencedAssemblies $references -Path (Join-Path $Context.ScriptPath 'TestDoubles.cs')
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
    $globalDataType.GetField('<Settings>k__BackingField', $privateStatic).SetValue($null, [CefFlashBrowser.Models.Settings]::Default)
    $globalDataType.GetField('<Favorites>k__BackingField', $privateStatic).SetValue($null,
        (New-Object 'System.Collections.ObjectModel.ObservableCollection[CefFlashBrowser.Models.Website]'))
    # PowerShell has a different application base directory than the deployed executable.
    $globalDataType.GetField('<SwfPlayerPath>k__BackingField', $privateStatic).SetValue(
        $null, (Join-Path $Context.ApplicationPath 'Assets\SwfPlayer\swfplayer.html'))
    [CefFlashBrowser.Data.GlobalData]::IsStartWithoutMainWindow = $true
    $locator = New-Object CefFlashBrowser.ViewModels.ViewModelLocator
    $Context.Application.Resources.Add('Locator', $locator.PSObject.BaseObject)
    $Context.Logger = New-Object CefFlashBrowser.Log.FileLogger((Join-Path $Context.TemporaryPath 'app.log'))
    $registerInstance = [SimpleMvvm.Ioc.SimpleIoc].GetMethods() | Where-Object {
        $_.Name -eq 'Register' -and $_.IsGenericMethodDefinition -and
        $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.IsGenericParameter
    }
    [void]$registerInstance.MakeGenericMethod([CefFlashBrowser.Log.ILogger]).Invoke(
        [SimpleMvvm.Ioc.SimpleIoc]::Global, @($Context.Logger.PSObject.BaseObject))

    foreach ($dictionaryUri in @(
        'pack://application:,,,/CefFlashBrowser;component/Assets/Language/zh-CN.xaml',
        'pack://application:,,,/HandyControl;component/Themes/SkinDefault.xaml',
        'pack://application:,,,/HandyControl;component/Themes/Theme.xaml',
        'pack://application:,,,/CefFlashBrowser;component/Views/Converters/Converters.xaml'
    )) {
        $dictionary = New-Object System.Windows.ResourceDictionary
        $dictionary.set_Source([Uri]$dictionaryUri)
        $Context.Application.Resources.MergedDictionaries.Add($dictionary)
    }
    foreach ($icon in @('Add', 'Sub', 'Back', 'Forward', 'Reload', 'Stop', 'Warning', 'Search', 'More', 'Up', 'Down', 'Page')) {
        $Context.Application.Resources.Add($icon + 'IconUri', [Uri]('/Assets/Svgs/' + $icon.ToLowerInvariant() + '.svg', [UriKind]::Relative))
    }

    $auxiliaryFile = Join-Path $Context.TemporaryPath 'auxiliary.html'
    $auxiliarySecondFile = Join-Path $Context.TemporaryPath 'auxiliary-second.html'
    [IO.File]::WriteAllText($auxiliaryFile, '<title>Auxiliary regression</title><p>auxiliary-source-token</p>')
    [IO.File]::WriteAllText($auxiliarySecondFile, '<title>Second auxiliary page</title><p>second-source-token</p>')
    $Context.AuxiliaryAddress = ([Uri]$auxiliaryFile).AbsoluteUri
    $Context.AuxiliarySecondAddress = ([Uri]$auxiliarySecondFile).AbsoluteUri
}

function Start-RegressionCef($Context) {
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
    if ($null -ne $Context.Logger) {
        try {
            $Context.Logger.Dispose()
        } catch {
            $cleanupErrors.Add($_)
        }
    }
    return $cleanupErrors.ToArray()
}
