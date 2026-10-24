function Assert-Regression([bool]$Condition, [string]$Message) {
    if (!$Condition) {
        throw $Message
    }

    Write-Host "PASS $Message"
}

function Wait-Regression($Context, [scriptblock]$Condition, [string]$Message, [int]$TimeoutSeconds = 15) {
    $timeout = [Diagnostics.Stopwatch]::StartNew()

    while (!(& $Condition)) {
        if ($timeout.Elapsed.TotalSeconds -ge $TimeoutSeconds) {
            throw "Timed out: $Message"
        }

        $frame = New-Object System.Windows.Threading.DispatcherFrame
        $stopFrame = [Action]({ $frame.Continue = $false }.GetNewClosure())
        [void]$Context.Application.Dispatcher.BeginInvoke([System.Windows.Threading.DispatcherPriority]::Background, $stopFrame)
        [System.Windows.Threading.Dispatcher]::PushFrame($frame)
        [Threading.Thread]::Sleep(10)
    }
}

function Invoke-JavaScript($Context, $Browser, [string]$Code) {
    $evaluation = [CefSharp.WebBrowserExtensions]::EvaluateScriptAsync($Browser, $Code, [TimeSpan]::FromSeconds(5))
    Wait-Regression $Context { $evaluation.IsCompleted } 'JavaScript result'
    $response = $evaluation.GetAwaiter().GetResult()

    if (!$response.Success) {
        throw $response.Message
    }

    return $response.Result
}

function Invoke-BrowserCommand($Command, $Parameter = $null) {
    if ($null -ne $Parameter) {
        $Parameter = $Parameter.PSObject.BaseObject
    }

    [void][System.Windows.Input.ICommand].GetMethod('Execute').Invoke(
        $Command.PSObject.BaseObject, [object[]]@($Parameter))
}

function Find-BrowserView($Element) {
    if ($Element -is [CefFlashBrowser.Views.BrowserView]) {
        return $Element
    }

    for ($index = 0; $index -lt [System.Windows.Media.VisualTreeHelper]::GetChildrenCount($Element); $index++) {
        $view = Find-BrowserView ([System.Windows.Media.VisualTreeHelper]::GetChild($Element, $index))
        if ($null -ne $view) {
            return $view
        }
    }
}

function New-RegressionWindow($Context, [switch]$SkipWait, [double]$Width = 900, [double]$Height = 600) {
    $window = New-Object CefFlashBrowser.Views.BrowserWindow
    $window.Width = $Width
    $window.Height = $Height
    $window.Left = -10000
    $window.Top = -10000
    $window.ShowActivated = $false
    $window.ShowInTaskbar = $false
    $window.ViewModel.CurrentBrowser.Address = 'about:blank'
    $window.Show()
    if (!$SkipWait) {
        Wait-Regression $Context {
            $null -ne $window.ViewModel.CurrentBrowser.Browser -and
            $window.ViewModel.CurrentBrowser.Browser.IsBrowserInitialized -and
            $window.ViewModel.CurrentBrowser.Browser.CanExecuteJavascriptInMainFrame
        } 'browser initialization'
    }
    return $window
}

function Invoke-ViewCallback($View, [string]$Method, [object[]]$Arguments) {
    for ($index = 0; $index -lt $Arguments.Count; $index++) {
        if ($null -ne $Arguments[$index]) {
            $Arguments[$index] = $Arguments[$index].PSObject.BaseObject
        }
    }

    [void]$View.GetType().GetMethod($Method, [Reflection.BindingFlags]'Instance,NonPublic').Invoke($View, $Arguments)
}

function New-AuxiliaryWindow($Context, [Type]$WindowType, [switch]$WaitForLoad) {
    $auxWindow = [Activator]::CreateInstance($WindowType)
    $auxWindow.Width = 900
    $auxWindow.Height = 600
    $auxWindow.Left = -10000
    $auxWindow.Top = -10000
    $auxWindow.ShowActivated = $false
    $auxWindow.ShowInTaskbar = $false
    if ($auxWindow -is [CefFlashBrowser.Views.SwfPlayerWindow]) {
        $auxWindow.FileName = 'first.swf'
    } else {
        $auxWindow.Address = $Context.AuxiliaryAddress
    }

    $auxView = $auxWindow.FindName('browserView')
    $loadState = @{ MainFrameLoaded = $false }
    $loaded = [EventHandler[CefSharp.FrameLoadEndEventArgs]]({
        param($sender, $eventArgs)
        try {
            if ($sender.IsDisposed) { return }
            if ($eventArgs.Frame.IsMain) { $loadState.MainFrameLoaded = $true }
        } catch {
            # CEF can deliver a late FrameLoadEnd callback after the window starts closing.
        }
    }.GetNewClosure())
    $auxView.ViewModel.Browser.add_FrameLoadEnd($loaded)
    try {
        $auxWindow.Show()
        if ($WaitForLoad) {
            Wait-Regression $Context { $loadState.MainFrameLoaded } "$($WindowType.Name) main frame load"
        }
    } finally {
        $auxView.ViewModel.Browser.remove_FrameLoadEnd($loaded)
    }
    return $auxWindow
}

function Assert-AuxiliaryClosed($Context, $AuxWindow, $AuxView) {
    Wait-Regression $Context { $AuxView.ViewModel.Browser.IsDisposed } "$($AuxWindow.GetType().Name) completes browser cleanup"
    Assert-Regression (!$AuxWindow.IsVisible -and $AuxView.ViewModel.Browser.IsDisposed) "$($AuxWindow.GetType().Name) releases its browser"
    Assert-Regression ($AuxView.GetType().GetField('_isClosed', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($AuxView)) "$($AuxWindow.GetType().Name) cleans up BrowserView"
    $AuxWindow.ForceCloseWindow()
}

function Get-RegressionSource($Context, $Browser) {
    $sourceTask = $Browser.GetBrowser().MainFrame.GetSourceAsync()
    Wait-Regression $Context { $sourceTask.IsCompleted } 'page source result'
    return $sourceTask.GetAwaiter().GetResult()
}

function Get-RegressionText($Context, $Browser) {
    $textTask = $Browser.GetBrowser().MainFrame.GetTextAsync()
    Wait-Regression $Context { $textTask.IsCompleted } 'page text result'
    return $textTask.GetAwaiter().GetResult()
}

function Open-RegressionDevTools($Context, $View) {
    $toolsState = @{ Browser = $null }
    $created = [CefFlashBrowser.Views.BrowserAfterCreatedEventHandler]({
        param($sender, $eventArgs)
        if ([CefFlashBrowser.Utils.HwndHelper]::IsDevToolsWindow($eventArgs.HostHandle)) {
            $toolsState.Browser = $eventArgs.Browser
        }
    }.GetNewClosure())
    $View.add_BrowserAfterCreated($created)
    try {
        $View.ViewModel.ToggleDevTools()
        Wait-Regression $Context { $null -ne $toolsState.Browser } 'DevTools browser created'
        Wait-Regression $Context {
            $evaluation = $toolsState.Browser.MainFrame.EvaluateScriptAsync(
                "document.readyState === 'complete' && !!document.querySelector('.root-view')",
                'about:blank', 1, [TimeSpan]::FromSeconds(3))
            Wait-Regression $Context { $evaluation.IsCompleted } 'DevTools frontend evaluation'
            $response = $evaluation.GetAwaiter().GetResult()
            $response.Success -and $response.Result -eq $true
        } 'DevTools frontend ready'
    } finally {
        $View.remove_BrowserAfterCreated($created)
    }
}

function Set-RegressionBeforeUnload($Context, $Browser) {
    $dialogHandler = New-Object RegressionJsDialogHandler
    $Browser.JsDialogHandler = $dialogHandler
    [CefFlashBrowser.Data.GlobalData]::Settings.DisableOnBeforeUnloadDialog = $false
    [void](Invoke-JavaScript $Context $Browser "window.onbeforeunload=function(e){e.preventDefault();e.returnValue='leave';return 'leave';};")
    $hostBrowser = $Browser.GetBrowser().GetHost()
    $mouse = New-Object CefSharp.MouseEvent(40, 40, [CefSharp.CefEventFlags]::None)
    $hostBrowser.SendMouseClickEvent($mouse, [CefSharp.MouseButtonType]::Left, $false, 1)
    $hostBrowser.SendMouseClickEvent($mouse, [CefSharp.MouseButtonType]::Left, $true, 1)
    [void](Invoke-JavaScript $Context $Browser 'document.hasFocus()')
    return $dialogHandler
}
