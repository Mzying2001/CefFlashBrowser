<#
Build the solution with VS MSBuild first, then run in a fresh Windows PowerShell process:
  powershell -NoProfile -STA -ExecutionPolicy Bypass -File CefFlashBrowser.Tests\RunBrowserRegression.ps1
Use 32-bit Windows PowerShell with -Platform x86 to test the x86 build.
The test uses temporary CEF data and in-memory application settings, not the user's profile.
#>
[CmdletBinding()]
param(
    [ValidateSet('x64', 'x86')][string]$Platform = 'x64',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
if ([Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') {
    throw 'Run this script in an STA Windows PowerShell process.'
}
if (([IntPtr]::Size -eq 8) -ne ($Platform -eq 'x64')) {
    throw 'The PowerShell process architecture must match -Platform.'
}

$repositoryPath = Split-Path $PSScriptRoot -Parent
$buildPath = if ($Platform -eq 'x64') { "bin\x64\$Configuration" } else { "bin\$Configuration" }
$applicationPath = Join-Path $repositoryPath $buildPath
$cefPath = Join-Path $applicationPath 'Assets\CefSharp'
$temporaryPath = Join-Path ([IO.Path]::GetTempPath()) ('CefFlashBrowser-regression-' + [Guid]::NewGuid())
[void][IO.Directory]::CreateDirectory($temporaryPath)

Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml,System.Windows.Forms
foreach ($dependency in @('SimpleMvvm.dll', 'Microsoft.Xaml.Behaviors.dll', 'HandyControl.dll',
    'Newtonsoft.Json.dll', 'SharpVectors.Core.dll', 'SharpVectors.Runtime.Wpf.dll', 'SharpVectors.Converters.Wpf.dll',
    'CefFlashBrowser.Log.dll', 'CefFlashBrowser.WinformCefSharp4WPF.dll', 'CefFlashBrowser.FlashBrowser.dll')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $applicationPath $dependency))
}
[void][Reflection.Assembly]::LoadFrom((Join-Path $cefPath 'CefSharp.dll'))
$applicationAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $applicationPath 'CefFlashBrowser.exe'))
[void][CefFlashBrowser.Utils.Win32]::SetDllDirectory($cefPath)
foreach ($dependency in @('CefSharp.Core.dll', 'CefSharp.WinForms.dll')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $cefPath $dependency))
}

$references = @(
    (Join-Path $cefPath 'CefSharp.dll'),
    (Join-Path $applicationPath 'CefFlashBrowser.FlashBrowser.dll')
)
Add-Type -ReferencedAssemblies $references -TypeDefinition @'
using System;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Threading;
using CefSharp;

public sealed class DisposedBrowserProxy : RealProxy
{
    public DisposedBrowserProxy(Type type) : base(type) { }
    public override IMessage Invoke(IMessage message)
    {
        var call = (IMethodCallMessage)message;
        object result;
        switch (call.MethodName)
        {
            case "get_IsDisposed": result = true; break;
            case "GetType": result = GetProxiedType(); break;
            case "ToString": result = "Disposed browser proxy"; break;
            case "GetHashCode": result = GetHashCode(); break;
            case "Equals": result = ReferenceEquals(GetTransparentProxy(), call.Args[0]); break;
            default: return new ReturnMessage(new ObjectDisposedException("test browser"), call);
        }
        return new ReturnMessage(result, null, 0, call.LogicalCallContext, call);
    }
}

public sealed class RegressionJsDialogHandler : CefFlashBrowser.FlashBrowser.Handlers.JsDialogHandler
{
    public volatile bool Accept;
    public int Count;
    public override bool OnBeforeUnloadDialog(IWebBrowser control, IBrowser browser,
        string message, bool isReload, IJsDialogCallback callback)
    {
        Interlocked.Increment(ref Count);
        using (callback) { callback.Continue(Accept); }
        return true;
    }
}
'@

[System.Windows.Application]::ResourceAssembly = $applicationAssembly
$application = New-Object System.Windows.Application
$application.ShutdownMode = [System.Windows.ShutdownMode]::OnExplicitShutdown
$globalDataType = $applicationAssembly.GetType('CefFlashBrowser.Data.GlobalData')
$privateStatic = [Reflection.BindingFlags]'Static,NonPublic'
$globalDataType.GetField('<Settings>k__BackingField', $privateStatic).SetValue($null, [CefFlashBrowser.Models.Settings]::Default)
$globalDataType.GetField('<Favorites>k__BackingField', $privateStatic).SetValue($null,
    (New-Object 'System.Collections.ObjectModel.ObservableCollection[CefFlashBrowser.Models.Website]'))
$locator = New-Object CefFlashBrowser.ViewModels.ViewModelLocator
$logger = New-Object CefFlashBrowser.Log.FileLogger((Join-Path $temporaryPath 'app.log'))
$registerInstance = [SimpleMvvm.Ioc.SimpleIoc].GetMethods() | Where-Object {
    $_.Name -eq 'Register' -and $_.IsGenericMethodDefinition -and
    $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.IsGenericParameter
}
[void]$registerInstance.MakeGenericMethod([CefFlashBrowser.Log.ILogger]).Invoke([SimpleMvvm.Ioc.SimpleIoc]::Global, @($logger.PSObject.BaseObject))

foreach ($dictionaryUri in @(
    'pack://application:,,,/CefFlashBrowser;component/Assets/Language/zh-CN.xaml',
    'pack://application:,,,/HandyControl;component/Themes/SkinDefault.xaml',
    'pack://application:,,,/HandyControl;component/Themes/Theme.xaml',
    'pack://application:,,,/CefFlashBrowser;component/Views/Converters/Converters.xaml')) {
    $dictionary = New-Object System.Windows.ResourceDictionary
    $dictionary.set_Source([Uri]$dictionaryUri)
    $application.Resources.MergedDictionaries.Add($dictionary)
}
foreach ($icon in @('Add', 'Sub', 'Back', 'Forward', 'Reload', 'Stop', 'Warning', 'Search', 'More', 'Up', 'Down', 'Page')) {
    $application.Resources.Add($icon + 'IconUri', [Uri]('/Assets/Svgs/' + $icon.ToLowerInvariant() + '.svg', [UriKind]::Relative))
}

function Assert-Regression([bool]$Condition, [string]$Message) {
    if (!$Condition) { throw $Message }
    Write-Host "PASS $Message"
}

function Wait-Regression([scriptblock]$Condition, [string]$Message, [int]$TimeoutSeconds = 15) {
    $timeout = [Diagnostics.Stopwatch]::StartNew()
    while (!(& $Condition)) {
        if ($timeout.Elapsed.TotalSeconds -ge $TimeoutSeconds) { throw "Timed out: $Message" }
        $frame = New-Object System.Windows.Threading.DispatcherFrame
        $stopFrame = [Action]({ $frame.Continue = $false }.GetNewClosure())
        [void]$application.Dispatcher.BeginInvoke([System.Windows.Threading.DispatcherPriority]::Background, $stopFrame)
        [System.Windows.Threading.Dispatcher]::PushFrame($frame)
        [Threading.Thread]::Sleep(10)
    }
}

function Invoke-JavaScript($Browser, [string]$Code) {
    $evaluation = [CefSharp.WebBrowserExtensions]::EvaluateScriptAsync($Browser, $Code, [TimeSpan]::FromSeconds(5))
    Wait-Regression { $evaluation.IsCompleted } 'JavaScript result'
    $response = $evaluation.GetAwaiter().GetResult()
    if (!$response.Success) { throw $response.Message }
    return $response.Result
}

function Invoke-BrowserCommand($Command, $Parameter = $null) {
    if ($null -ne $Parameter) { $Parameter = $Parameter.PSObject.BaseObject }
    [void][System.Windows.Input.ICommand].GetMethod('Execute').Invoke(
        $Command.PSObject.BaseObject, [object[]]@($Parameter))
}

function Find-BrowserView($Element) {
    if ($Element -is [CefFlashBrowser.Views.BrowserView]) { return $Element }
    for ($index = 0; $index -lt [System.Windows.Media.VisualTreeHelper]::GetChildrenCount($Element); $index++) {
        $view = Find-BrowserView ([System.Windows.Media.VisualTreeHelper]::GetChild($Element, $index))
        if ($null -ne $view) { return $view }
    }
}

function New-RegressionWindow {
    $window = New-Object CefFlashBrowser.Views.BrowserWindow
    $window.Width = 900
    $window.Height = 600
    $window.Left = -10000
    $window.Top = -10000
    $window.ShowActivated = $false
    $window.ShowInTaskbar = $false
    $window.Show()
    Wait-Regression { $null -ne $window.ViewModel.CurrentBrowser.Browser -and
        $window.ViewModel.CurrentBrowser.Browser.IsBrowserInitialized -and
        $window.ViewModel.CurrentBrowser.Browser.CanExecuteJavascriptInMainFrame } 'browser initialization'
    return $window
}

$privateInstance = [Reflection.BindingFlags]'Instance,NonPublic'
function Invoke-ViewCallback($View, [string]$Method, [object[]]$Arguments) {
    for ($index = 0; $index -lt $Arguments.Count; $index++) {
        if ($null -ne $Arguments[$index]) { $Arguments[$index] = $Arguments[$index].PSObject.BaseObject }
    }
    [void]$View.GetType().GetMethod($Method, $privateInstance).Invoke($View, $Arguments)
}

$cefStarted = $false
try {
    $window = New-Object CefFlashBrowser.Views.BrowserWindow
    Assert-Regression ($null -eq $window.ViewModel.CurrentBrowser.Browser) 'browser is initially deferred'
    $window.Close()
    $window.ForceCloseWindow()
    Assert-Regression ($window.GetType().GetField('_isClosed', $privateInstance).GetValue($window)) 'close before Show is safe and idempotent'
    $window = New-Object CefFlashBrowser.Views.BrowserWindow
    $window.ForceCloseWindow()
    Assert-Regression ($window.GetType().GetField('_isClosed', $privateInstance).GetValue($window)) 'force close before Show is safe'

    $model = New-Object CefFlashBrowser.ViewModels.BrowserWindowViewModel
    $model.CreateShortcut($null)
    $model.AddFavorite($null)
    $model.ToggleLoadingState($null)
    $model.FindText($null, $true)
    $model.ToggleDevTools($model.CurrentBrowser)
    $model.ToggleFullscreen($model.CurrentBrowser)
    $model.CurrentBrowser.ExitFullScreen()
    Assert-Regression (!$model.Fullscreen) 'commands tolerate missing browsers'

    $cefSettings = New-Object CefFlashBrowser.FlashBrowser.CefFlashSettings
    $cefSettings.BrowserSubprocessPath = Join-Path $cefPath 'CefSharp.BrowserSubprocess.exe'
    $cefSettings.ResourcesDirPath = $cefPath
    $cefSettings.LocalesDirPath = Join-Path $cefPath 'locales'
    $cefSettings.CachePath = Join-Path $temporaryPath 'Cache'
    $cefSettings.LogFile = Join-Path $temporaryPath 'cef.log'
    $cefSettings.CefCommandLineArgs['disable-gpu'] = '1'
    $cefStarted = [CefSharp.Cef]::Initialize($cefSettings)
    if (!$cefStarted) { throw 'CEF initialization failed' }

    $window = New-RegressionWindow
    $browser = $window.ViewModel.CurrentBrowser.Browser
    $view = Find-BrowserView $window
    Assert-Regression ([object]::ReferenceEquals($view.ViewModel.Browser, $browser)) 'template synchronously associates the browser'
    $view.ViewModel.Browser = $browser
    $view.OnApplyTemplate()
    $view.OnApplyTemplate()
    Invoke-ViewCallback $view 'BrowserViewLoaded' @($view, (New-Object System.Windows.RoutedEventArgs))
    Assert-Regression ([object]::ReferenceEquals($view.ViewModel.Browser, $browser)) 'reapplying the template and Loaded is safe'

    $disposedBrowser = (New-Object DisposedBrowserProxy([CefSharp.IBrowser])).GetTransparentProxy()
    $capture = @{ Called = $false }
    $interceptClose = [CefFlashBrowser.Views.BrowserDoCloseEventHandler]({
        param($sender, $eventArgs)
        $capture.Called = $true
        $eventArgs.Handled = $true
    }.GetNewClosure())
    $view.add_BrowserDoClose($interceptClose)
    Invoke-ViewCallback $view 'OnBrowserDoClose' @($browser, $disposedBrowser)
    $view.remove_BrowserDoClose($interceptClose)
    Assert-Regression $capture.Called 'disposed callback browser does not access popup or host properties'

    [CefFlashBrowser.Data.GlobalData]::Settings.EnableIntegratedDevTools = $false
    for ($cycle = 0; $cycle -lt 2; $cycle++) {
        $view.ViewModel.ToggleDevTools()
        Wait-Regression { [CefFlashBrowser.Utils.HwndHelper]::FindNotIntegratedDevTools($browser) -ne [IntPtr]::Zero } 'detached DevTools opens through the template'
        if ($cycle -eq 0) {
            $firstTools = [CefFlashBrowser.Utils.HwndHelper]::FindNotIntegratedDevTools($browser)
            $otherWindow = New-RegressionWindow
            $otherBrowser = $otherWindow.ViewModel.CurrentBrowser.Browser
            Assert-Regression ([CefFlashBrowser.Utils.HwndHelper]::FindNotIntegratedDevTools($otherBrowser) -eq [IntPtr]::Zero) 'DevTools lookup is isolated between browser windows'
            $otherWindow.ViewModel.CurrentBrowser.ToggleDevTools()
            Wait-Regression { [CefFlashBrowser.Utils.HwndHelper]::FindNotIntegratedDevTools($otherBrowser) -ne [IntPtr]::Zero } 'second window DevTools opens'
            Assert-Regression ([CefFlashBrowser.Utils.HwndHelper]::FindNotIntegratedDevTools($otherBrowser) -ne $firstTools) 'each browser owns its own DevTools window'
            $otherWindow.ViewModel.CurrentBrowser.ToggleDevTools()
            Wait-Regression { [CefFlashBrowser.Utils.HwndHelper]::FindNotIntegratedDevTools($otherBrowser) -eq [IntPtr]::Zero } 'second window DevTools closes'
            $otherWindow.ForceCloseWindow()
        }
        $view.ViewModel.ToggleDevTools()
        Wait-Regression { [CefFlashBrowser.Utils.HwndHelper]::FindNotIntegratedDevTools($browser) -eq [IntPtr]::Zero } 'detached DevTools closes through the toggle'
    }
    Assert-Regression $window.IsVisible 'closing DevTools does not close its browser window'

    [CefFlashBrowser.Data.GlobalData]::Settings.EnableIntegratedDevTools = $true
    [CefFlashBrowser.Data.GlobalData]::Settings.IntegratedDevToolsWidth = 340
    $view.ViewModel.ToggleDevTools()
    Wait-Regression { $view.ViewModel.IntegratedDevToolsHandle -ne [IntPtr]::Zero } 'integrated DevTools opens'
    $devtoolsColumn = $view.FindName('devtoolsColumn')
    $devtoolsColumn.Width = New-Object System.Windows.GridLength(380)
    $view.ViewModel.ToggleDevTools()
    Wait-Regression { $view.ViewModel.IntegratedDevToolsHandle -eq [IntPtr]::Zero } 'integrated DevTools closes'
    Assert-Regression ([CefFlashBrowser.Data.GlobalData]::Settings.IntegratedDevToolsWidth -eq 380) 'DevTools width persists'

    $firstAddress = 'data:text/html,' + [Uri]::EscapeDataString('<title>First page</title><p>first</p>')
    $secondAddress = 'data:text/html,' + [Uri]::EscapeDataString('<title>Browser regression</title><p>find this text</p>')
    $view.ViewModel.Address = $firstAddress
    Wait-Regression { $window.Title -eq 'First page' } 'address binding navigates to the first page'
    $view.ViewModel.Address = $secondAddress
    Wait-Regression { $window.Title -eq 'Browser regression' } 'browser title binding'
    Invoke-BrowserCommand $browser.BackCommand
    Wait-Regression { $window.Title -eq 'First page' } 'back command'
    Invoke-BrowserCommand $browser.ForwardCommand
    Wait-Regression { $window.Title -eq 'Browser regression' } 'forward command'
    Assert-Regression ($view.ViewModel.Address -eq $secondAddress) 'history navigation updates the address binding'
    Invoke-BrowserCommand $browser.ReloadCommand
    Wait-Regression { !$browser.IsLoading -and $browser.CanExecuteJavascriptInMainFrame } 'reload command'

    $favoriteTimer = New-Object System.Windows.Threading.DispatcherTimer
    $favoriteTimer.Interval = [TimeSpan]::FromMilliseconds(20)
    $favoriteTimer.add_Tick({
        $dialog = $application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.Dialogs.AddFavoriteDialog] } | Select-Object -First 1
        if ($null -ne $dialog -and $dialog.IsLoaded) {
            $favoriteTimer.Stop()
            Invoke-BrowserCommand $dialog.DataContext.ConfirmCommand
        }
    })
    $favoriteTimer.Start()
    try { $window.ViewModel.AddFavorite($browser) }
    finally { $favoriteTimer.Stop() }
    $favorite = [CefFlashBrowser.Data.GlobalData]::Favorites | Select-Object -Last 1
    Assert-Regression ($favorite.Name -eq 'Browser regression' -and $favorite.Url -eq $secondAddress) 'favorite dialog preserves the current title and address'

    [void](Invoke-JavaScript $browser "console.info('Cross-origin plugin content from https://example.invalid/game.swf');")
    Wait-Regression { $browser.HasBlockedSwfs } 'Flash blocking notification'
    $blockedItems = $window.Resources['blockedSwfsMenu'].Items[1]
    Assert-Regression ($blockedItems.Items.Contains('https://example.invalid/game.swf')) 'blocked Flash menu retains its browser binding'
    $statusMethod = [CefFlashBrowser.WinformCefSharp4WPF.ChromiumWebBrowser].GetMethod(
        'OnStatusMessage', $privateInstance, $null, [Type[]]@([CefSharp.StatusMessageEventArgs]), $null)
    $statusEvent = New-Object CefSharp.StatusMessageEventArgs($browser.GetBrowser(), 'regression status')
    [void]$statusMethod.Invoke($browser.PSObject.BaseObject, [object[]]@($statusEvent.PSObject.BaseObject))
    Assert-Regression ($browser.StatusText -eq 'regression status') 'status callback updates the browser property'
    Assert-Regression ([object]::ReferenceEquals($window.FindName('statusPopupContent').Content, $view.ViewModel)) 'status popup retains its browser binding'

    $window.ViewModel.TextToFind = 'find this'
    $window.ViewModel.FindText($browser, $true)
    $window.ViewModel.ShowFindPopup = $true
    $window.ViewModel.ShowFindPopup = $false
    $browser.ZoomLevel = 0.5
    Wait-Regression { $view.ViewModel.ZoomLevel -eq 0.5 } 'zoom binding'
    Assert-Regression ([CefFlashBrowser.Data.GlobalData]::Settings.BrowserZoomLevel -eq 0.5) 'zoom persists through BrowserViewModel'
    $window.ViewModel.ToggleFullscreen($view.ViewModel)
    Assert-Regression ($window.WindowStyle -eq [System.Windows.WindowStyle]::None) 'F11 enters window fullscreen'
    $window.ViewModel.ToggleFullscreen($view.ViewModel)
    Assert-Regression ($window.WindowStyle -eq [System.Windows.WindowStyle]::SingleBorderWindow) 'F11 restores window chrome'

    $dialogHandler = New-Object RegressionJsDialogHandler
    $browser.JsDialogHandler = $dialogHandler
    [void](Invoke-JavaScript $browser "window.onbeforeunload=function(e){e.preventDefault();e.returnValue='leave';return 'leave';};")
    $hostBrowser = $browser.GetBrowser().GetHost()
    $mouse = New-Object CefSharp.MouseEvent(40, 40, [CefSharp.CefEventFlags]::None)
    $hostBrowser.SendMouseClickEvent($mouse, [CefSharp.MouseButtonType]::Left, $false, 1)
    $hostBrowser.SendMouseClickEvent($mouse, [CefSharp.MouseButtonType]::Left, $true, 1)
    [void](Invoke-JavaScript $browser 'document.hasFocus()')
    $window.Close()
    Wait-Regression { $dialogHandler.Count -gt 0 } 'beforeunload dialog on normal close'
    Assert-Regression $window.IsVisible 'cancelling beforeunload keeps the window open'
    $dialogHandler.Accept = $true
    $closeBinding = $window.InputBindings | Where-Object { $_.Key -eq [System.Windows.Input.Key]::W -and $_.Modifiers -eq [System.Windows.Input.ModifierKeys]::Control }
    Invoke-BrowserCommand $closeBinding.Command ([bool]$closeBinding.CommandParameter)
    Wait-Regression { !$window.IsVisible } 'Ctrl+W closes after beforeunload approval'
    Assert-Regression ($view.GetType().GetField('_isClosed', $privateInstance).GetValue($view)) 'host close cleans up BrowserView'
    Invoke-ViewCallback $view 'OnBrowserDoClose' @($browser, $disposedBrowser)
    $window.ViewModel.CreateShortcut($browser)
    $window.ViewModel.AddFavorite($browser)
    $window.ViewModel.ToggleLoadingState($browser)
    $window.ViewModel.FindText($browser, $true)
    $view.ViewModel.ToggleDevTools()
    $view.ViewModel.ExitFullScreen()
    Assert-Regression $browser.IsDisposed 'commands tolerate disposed browsers'

    $window = New-RegressionWindow
    $view = Find-BrowserView $window
    Assert-Regression ($view.ViewModel.ZoomLevel -eq 0.5) 'a new browser restores the saved zoom level'
    $rejected = $false
    try { $view.ViewModel.Browser = $browser }
    catch { $rejected = $true }
    Assert-Regression $rejected 'a different browser instance is rejected'
    $view.ViewModel.ToggleDevTools()
    Wait-Regression { $view.ViewModel.IntegratedDevToolsHandle -ne [IntPtr]::Zero } 'DevTools opens before force close'
    Assert-Regression ($view.FindName('devtoolsColumn').Width.Value -eq 380) 'integrated DevTools restores its saved width'
    $view.FindName('devtoolsColumn').Width = New-Object System.Windows.GridLength(400)
    $window.ForceCloseWindow()
    Assert-Regression ([CefFlashBrowser.Data.GlobalData]::Settings.IntegratedDevToolsWidth -eq 400) 'force close preserves DevTools width without DoClose'
    Assert-Regression ($view.GetType().GetField('_isClosed', $privateInstance).GetValue($view)) 'force close cleans up BrowserView'
    Assert-Regression $view.ViewModel.Browser.IsDisposed 'force close releases the hosted browser'
    $window.ForceCloseWindow()

    foreach ($forceClose in @($false, $true) * 5) {
        $window = New-Object CefFlashBrowser.Views.BrowserWindow
        $window.Width = 800
        $window.Height = 600
        $window.ShowActivated = $false
        $window.ShowInTaskbar = $false
        $window.Left = -10000
        $window.Top = -10000
        $window.Show()
        if ($forceClose) { $window.ForceCloseWindow() } else { $window.Close() }
        Wait-Regression { !$window.IsVisible } 'close during initialization'
        Assert-Regression (!$window.IsVisible) "close during initialization is safe (force=$forceClose)"
    }

    $readyWindow = New-RegressionWindow
    $pendingWindow = New-Object CefFlashBrowser.Views.BrowserWindow
    $pendingWindow.ShowActivated = $false
    $pendingWindow.ShowInTaskbar = $false
    $pendingWindow.Left = -10000
    $pendingWindow.Top = -10000
    $pendingWindow.Show()
    [SimpleMvvm.Messaging.Messenger]::Global.Send([CefFlashBrowser.Data.MessageTokens]::CLOSE_ALL_BROWSERS, $null)
    Assert-Regression (!$readyWindow.IsVisible -and !$pendingWindow.IsVisible) 'close-all message completes before CEF shutdown'
    Write-Host 'Browser regression checks passed.'
}
finally {
    foreach ($remainingWindow in @($application.Windows)) {
        if ($remainingWindow -is [CefFlashBrowser.Views.BrowserWindow]) { $remainingWindow.ForceCloseWindow() }
        else { $remainingWindow.Close() }
    }
    if ($cefStarted) { [CefSharp.Cef]::Shutdown() }
    $logger.Dispose()
    Write-Host "Isolated test data: $temporaryPath"
}
