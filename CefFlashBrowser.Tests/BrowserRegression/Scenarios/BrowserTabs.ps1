function New-RegressionTab($Context, $Window, [string]$Address = 'about:blank') {
    $model = $Window.ViewModel.AddBrowser($Address, $true)
    Wait-Regression $Context {
        $null -ne $model.Browser -and $model.Browser.IsBrowserInitialized -and
        $model.Browser.CanExecuteJavascriptInMainFrame -and $model.Browser.Address -eq $Address
    } 'new tab initialization'
    return $model
}

function Test-BrowserTabsBeforeShow($Context) {
    $window = New-Object CefFlashBrowser.Views.BrowserWindow
    $model = $window.ViewModel
    $first = $model.CurrentBrowser
    $second = $model.AddBrowser('about:blank', $false)
    Assert-Regression ([object]::ReferenceEquals($model.CurrentBrowser, $first)) 'background creation preserves selection'
    Assert-Regression (([Collections.IList]$model.Browsers).IsReadOnly) 'browser collection is read-only to callers'
    foreach ($invalid in @($null, (New-Object CefFlashBrowser.ViewModels.BrowserViewModel))) {
        $rejected = $false
        try { $model.CurrentBrowser = $invalid } catch { $rejected = $true }
        Assert-Regression $rejected 'selection rejects a browser outside the window'
    }
    $model.CloseBrowser($second)
    Assert-Regression ($model.Browsers.Count -eq 1) 'unrealized background tab closes without creating CEF'
    $model.AddBrowser('about:blank', $true) | Out-Null
    $window.Close()
    Assert-Regression ($model.Browsers.Count -eq 0) 'closing before Show removes all unrealized tabs'
    $window.ForceCloseWindow()
}

function Test-BrowserTabs($Context) {
    Test-BrowserTabStrip $Context
    Test-BrowserTabNewPageRouting $Context
    Test-BrowserTabSelection $Context
    Test-BrowserTabCallbacks $Context
    Test-BrowserTabCloseCancellation $Context
    Test-BrowserTabsReloadClose $Context
    Test-BrowserTabsWindowClose $Context
    Test-BrowserTabsForceClose $Context
    Test-BrowserTabsForceClosePending $Context
}

function Find-TabElement($Element, [string]$Name) {
    if ($Element -is [System.Windows.FrameworkElement] -and $Element.Name -eq $Name) {
        return $Element
    }
    for ($index = 0; $index -lt [System.Windows.Media.VisualTreeHelper]::GetChildrenCount($Element); $index++) {
        $found = Find-TabElement ([System.Windows.Media.VisualTreeHelper]::GetChild($Element, $index)) $Name
        if ($null -ne $found) { return $found }
    }
}

function Invoke-TabShortcut($Window, [System.Windows.Input.Key]$Key, [System.Windows.Input.ModifierKeys]$Modifiers) {
    $binding = $Window.InputBindings | Where-Object { $_.Key -eq $Key -and $_.Modifiers -eq $Modifiers }
    Assert-Regression ($null -ne $binding) "tab shortcut exists: $Modifiers+$Key"
    Invoke-BrowserCommand $binding.Command $binding.CommandParameter
}

function Test-BrowserTabStrip($Context) {
    $window = New-RegressionWindow $Context -Width 600
    try {
        $model = $window.ViewModel
        $tabs = $window.FindName('tabList')
        $first = $model.CurrentBrowser
        Assert-Regression ($model.Browsers.Count -eq 1 -and $tabs.SelectedItem -eq $first) 'window starts with a single selected tab'
        $newButton = $window.FindName('newTabButton')
        $toolbarNewButton = $window.FindName('toolbarNewTabButton')
        $peer = New-Object System.Windows.Automation.Peers.ButtonAutomationPeer($toolbarNewButton)
        $peer.GetPattern([System.Windows.Automation.Peers.PatternInterface]::Invoke).Invoke()
        Wait-Regression $Context { $model.Browsers.Count -eq 2 -and $model.CurrentBrowser.IsBrowserReady() } 'new tab button creates and selects a browser'
        $second = $model.CurrentBrowser
        Assert-Regression ($second.Address -eq 'about:blank' -and $tabs.SelectedItem -eq $second) 'new tab has a blank address and selected header'
        $tabs.SetCurrentValue([System.Windows.Controls.Primitives.Selector]::SelectedItemProperty, $first)
        Assert-Regression ($model.CurrentBrowser -eq $first) 'selecting a tab header changes the current browser'
        Invoke-TabShortcut $window T Control
        Wait-Regression $Context { $model.Browsers.Count -eq 3 -and $model.CurrentBrowser.IsBrowserReady() } 'Ctrl+T creates a tab'
        $third = $model.CurrentBrowser
        Invoke-TabShortcut $window Tab Control
        Assert-Regression ($model.CurrentBrowser -eq $first) 'Ctrl+Tab wraps to the first tab'
        Invoke-TabShortcut $window Tab ([System.Windows.Input.ModifierKeys]::Control -bor [System.Windows.Input.ModifierKeys]::Shift)
        Assert-Regression ($model.CurrentBrowser -eq $third) 'Ctrl+Shift+Tab wraps to the last tab'
        Invoke-TabShortcut $window Next Control
        Invoke-TabShortcut $window Prior Control
        Assert-Regression ($model.CurrentBrowser -eq $third) 'Ctrl+PageDown and Ctrl+PageUp cycle tabs'
        $window.UpdateLayout()
        $scroll = $tabs.Template.FindName('tabScrollViewer', $tabs)
        Wait-Regression $Context { $scroll.HorizontalOffset -gt 0 } 'selecting an overflowing tab brings it into view'
        Assert-Regression ($newButton.IsVisible -and $scroll.ScrollableWidth -gt 0) 'overflow keeps the new tab button accessible'

        $container = $tabs.ItemContainerGenerator.ContainerFromItem($second)
        $title = Find-TabElement $container 'tabTitle'
        $second.Title = ''
        Assert-Regression ($title.Text -eq $window.FindResource('browser_newTab')) 'empty tab title uses localized fallback'
        $second.Title = 'Updated tab title'
        Assert-Regression ($title.Text -eq 'Updated tab title') 'tab header follows title changes'
        $closeButton = Find-TabElement $container 'closeTabButton'
        $peer = New-Object System.Windows.Automation.Peers.ButtonAutomationPeer($closeButton)
        $peer.GetPattern([System.Windows.Automation.Peers.PatternInterface]::Invoke).Invoke()
        Wait-Regression $Context { $model.Browsers.Count -eq 2 } 'tab close button closes its own browser'
        Assert-Regression ($model.CurrentBrowser -eq $third -and $second.Browser.IsDisposed) 'closing a background header preserves selection and disposes its browser'
        $container = $tabs.ItemContainerGenerator.ContainerFromItem($first)
        $middle = New-Object System.Windows.Input.MouseButtonEventArgs([System.Windows.Input.Mouse]::PrimaryDevice, 0, [System.Windows.Input.MouseButton]::Middle)
        $middle.RoutedEvent = [System.Windows.Input.Mouse]::PreviewMouseDownEvent
        $container.RaiseEvent($middle)
        Wait-Regression $Context { $model.Browsers.Count -eq 1 } 'middle click closes a tab'
        Assert-Regression ($middle.Handled -and $model.CurrentBrowser -eq $third) 'middle click preserves selection'
        $model.ToggleFullscreen($third)
        Assert-Regression (!$window.FindName('tabStrip').IsVisible) 'fullscreen hides the tab strip'
        $model.ToggleFullscreen($third)
        Assert-Regression (!$window.FindName('tabStrip').IsVisible) 'leaving fullscreen keeps the tab strip hidden for a single tab'
    } finally {
        $window.ForceCloseWindow()
    }
}

function Test-BrowserTabNewPageRouting($Context) {
    $window = New-RegressionWindow $Context
    $savedBehavior = [CefFlashBrowser.Data.GlobalData]::Settings.NewPageBehavior
    try {
        $model = $window.ViewModel
        $first = $model.CurrentBrowser
        $view = Find-BrowserView $window $first
        [CefFlashBrowser.Data.GlobalData]::Settings.NewPageBehavior = [CefFlashBrowser.Models.NewPageBehavior]::NewTab
        $model.ToggleFullscreen($first)
        Invoke-ViewCallback $view 'OnBrowserBeforePopup' @(
            $first.Browser, $first.Browser.GetBrowser(), $null, $Context.AuxiliaryAddress, '',
            [CefSharp.WindowOpenDisposition]::NewBackgroundTab, $true, $null, $null, $null, $false)
        Wait-Regression $Context { $model.Browsers.Count -eq 2 } 'background page request opens a tab'
        Assert-Regression ($model.CurrentBrowser -eq $first -and $model.Fullscreen) 'background page request preserves selection and fullscreen'
        Invoke-ViewCallback $view 'OnBrowserBeforePopup' @(
            $first.Browser, $first.Browser.GetBrowser(), $null, $Context.AuxiliarySecondAddress, '',
            [CefSharp.WindowOpenDisposition]::NewForegroundTab, $true, $null, $null, $null, $false)
        Wait-Regression $Context { $model.Browsers.Count -eq 3 -and $window.Title -eq 'Second auxiliary page' } 'foreground page request selects the new tab'
        Assert-Regression (!$model.Fullscreen -and $first.Address -eq 'about:blank') 'foreground tab exits fullscreen and preserves the source page'
        Assert-Regression ($model.Browsers[1].Address -eq $Context.AuxiliaryAddress -and $model.CurrentBrowser -eq $model.Browsers[2]) 'new page requests retain their target addresses'
    } finally {
        [CefFlashBrowser.Data.GlobalData]::Settings.NewPageBehavior = $savedBehavior
        $window.ForceCloseWindow()
    }
}

function Test-BrowserTabSelection($Context) {
    $window = New-RegressionWindow $Context
    try {
        $model = $window.ViewModel
        $first = $model.CurrentBrowser
        $first.Address = $Context.AuxiliaryAddress
        Wait-Regression $Context { $window.Title -eq 'Auxiliary regression' } 'first tab title'
        $firstBrowser = $first.Browser
        $firstView = Find-BrowserView $window $first
        [void](Invoke-JavaScript $Context $firstBrowser 'window.preservedTabValue = 42')
        [CefFlashBrowser.Data.GlobalData]::Settings.EnableIntegratedDevTools = $true
        Open-RegressionDevTools $Context $firstView
        $toolsHandle = $first.IntegratedDevToolsHandle
        $model.ShowFindPopup = $true
        $model.ToggleFullscreen($first)
        $second = New-RegressionTab $Context $window $Context.AuxiliarySecondAddress
        $secondView = Find-BrowserView $window $second
        Assert-Regression (!$model.ShowFindPopup -and !$model.Fullscreen) 'switching tabs resets find and fullscreen'
        Assert-Regression (!$firstView.IsVisible -and $secondView.IsVisible) 'only the selected tab is visible'
        Assert-Regression (!$firstBrowser.IsDisposed -and $first.IntegratedDevToolsHandle -eq $toolsHandle) 'hidden tab retains browser and DevTools'
        $model.CurrentBrowser = $first
        Wait-Regression $Context { $window.Title -eq 'Auxiliary regression' } 'title follows selected tab'
        Assert-Regression ([object]::ReferenceEquals((Find-BrowserView $window $first), $firstView)) 'switching preserves the BrowserView instance'
        Assert-Regression ((Invoke-JavaScript $Context $firstBrowser 'window.preservedTabValue') -eq 42) 'switching preserves JavaScript state'
        Assert-Regression ($firstView.IsVisible -and !$secondView.IsVisible) 'switching restores the original view'
        $third = New-RegressionTab $Context $window
        $model.CloseBrowser($second)
        Wait-Regression $Context { $model.Browsers.Count -eq 2 } 'background tab closes'
        Assert-Regression ([object]::ReferenceEquals($model.CurrentBrowser, $third)) 'closing background tab preserves selection'
        Assert-Regression $second.Browser.IsDisposed 'background tab releases CEF'
        $model.CurrentBrowser = $first
        $model.CloseBrowser($first)
        Wait-Regression $Context { $model.Browsers.Count -eq 1 } 'selected tab closes'
        Assert-Regression ([object]::ReferenceEquals($model.CurrentBrowser, $third)) 'closing selected tab selects its right neighbor'
        Assert-Regression ($firstBrowser.IsDisposed -and $first.IntegratedDevToolsHandle -eq [IntPtr]::Zero) 'tab close releases its browser and DevTools'
        $fourth = New-RegressionTab $Context $window
        $model.CloseBrowser($fourth)
        Wait-Regression $Context { $model.Browsers.Count -eq 1 } 'last-position tab closes'
        Assert-Regression ([object]::ReferenceEquals($model.CurrentBrowser, $third)) 'closing final-position tab selects its left neighbor'
        $closeBinding = $window.InputBindings | Where-Object {
            $_.Key -eq [System.Windows.Input.Key]::W -and $_.Modifiers -eq [System.Windows.Input.ModifierKeys]::Control
        }
        Invoke-BrowserCommand $closeBinding.Command $closeBinding.CommandParameter
        Wait-Regression $Context { !$window.IsVisible } 'Ctrl+W on the final tab closes its window'
        Assert-Regression ($null -eq $model.CurrentBrowser -and $third.Browser.IsDisposed) 'final tab close clears selection and releases CEF'
    } finally {
        $window.ForceCloseWindow()
    }
}

function Test-BrowserTabCallbacks($Context) {
    $window = New-RegressionWindow $Context
    $savedBehavior = [CefFlashBrowser.Data.GlobalData]::Settings.NewPageBehavior
    try {
        $model = $window.ViewModel
        $first = $model.CurrentBrowser
        $firstView = Find-BrowserView $window $first
        $second = New-RegressionTab $Context $window $Context.AuxiliarySecondAddress
        Invoke-ViewCallback $firstView 'OnFullScreenChanged' @($true)
        Assert-Regression (!$model.Fullscreen) 'background fullscreen callback cannot enter window fullscreen'
        $model.ToggleFullscreen($second)
        Invoke-ViewCallback $firstView 'OnFullScreenChanged' @($false)
        Assert-Regression $model.Fullscreen 'background callback cannot exit current-tab fullscreen'
        [CefFlashBrowser.Data.GlobalData]::Settings.NewPageBehavior = [CefFlashBrowser.Models.NewPageBehavior]::OriginalWindow
        Invoke-ViewCallback $firstView 'OnBrowserBeforePopup' @(
            $first.Browser, $first.Browser.GetBrowser(), $null, $Context.AuxiliaryAddress, '',
            [CefSharp.WindowOpenDisposition]::NewForegroundTab, $true, $null, $null, $null, $false)
        Wait-Regression $Context { $first.Address -eq $Context.AuxiliaryAddress } 'background new-page request navigates its source tab'
        Assert-Regression ($model.CurrentBrowser -eq $second -and $model.Fullscreen) 'background new-page routing preserves current tab and fullscreen'
        Assert-Regression ($second.Address -eq $Context.AuxiliarySecondAddress -and $window.Title -eq 'Second auxiliary page') 'background navigation does not replace current page or title'
        $model.ToggleFullscreen($second)
        $first.Browser.GetBrowser().CloseBrowser($false)
        Wait-Regression $Context { $model.Browsers.Count -eq 1 } 'browser-initiated close removes only its source tab'
        Assert-Regression ($window.IsVisible -and $model.CurrentBrowser -eq $second) 'background browser close keeps its window and selected tab'
        Invoke-ViewCallback $firstView 'OnFullScreenChanged' @($true)
        Invoke-ViewCallback $firstView 'OnBrowserDoClose' @($first.Browser, $Context.DisposedBrowser)
        Assert-Regression (!$model.Fullscreen -and $model.Browsers.Count -eq 1) 'late callbacks from a removed tab are ignored'
        $firstView.Dispose()
    } finally {
        [CefFlashBrowser.Data.GlobalData]::Settings.NewPageBehavior = $savedBehavior
        $window.ForceCloseWindow()
    }
}

function Test-BrowserTabCloseCancellation($Context) {
    $window = New-RegressionWindow $Context
    try {
        $model = $window.ViewModel
        $first = $model.CurrentBrowser
        $second = New-RegressionTab $Context $window $Context.AuxiliaryAddress
        $view = Find-BrowserView $window $second
        $handler = Set-RegressionBeforeUnload $Context $second.Browser
        $model.CloseBrowser($second)
        $model.CloseBrowser($second)
        Wait-Regression $Context {
            $handler.Count -eq 1 -and $null -eq $view.GetType().GetField('_closeCompletion', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($view)
        } 'single-tab close cancellation finishes'
        Assert-Regression ($model.Browsers.Count -eq 2 -and !$second.Browser.IsDisposed) 'cancelled tab close preserves the tab'
        Assert-Regression ($handler.Count -eq 1) 'repeated tab close requests share one confirmation'
        $handler.Accept = $true
        $model.CloseBrowser($second)
        Wait-Regression $Context { $model.Browsers.Count -eq 1 } 'cancelled tab can be closed again'
        Assert-Regression ($model.CurrentBrowser -eq $first) 'retry selects the surviving tab'
    } finally {
        $window.ForceCloseWindow()
    }
}

function Test-BrowserTabsReloadClose($Context) {
    $window = New-RegressionWindow $Context
    try {
        $second = New-RegressionTab $Context $window $Context.AuxiliaryAddress
        $handler = Set-RegressionBeforeUnload $Context $second.Browser
        $handler.Hold = $true
        [CefSharp.WebBrowserExtensions]::Reload($second.Browser, $false)
        Wait-Regression $Context { $handler.Count -eq 1 } 'reload awaits confirmation'
        $window.Close()
        $view = Find-BrowserView $window $second
        Wait-Regression $Context {
            $view.GetType().GetField('_closeSent', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($view)
        } 'window close reaches the reloading tab'
        $settled = [Diagnostics.Stopwatch]::StartNew()
        Wait-Regression $Context { $settled.ElapsedMilliseconds -gt 150 } 'CEF processes close while reload confirmation is held'
        $handler.Accept = $true
        $handler.Hold = $false
        $handler.CompletePending($false)
        Wait-Regression $Context { !$window.IsVisible } 'window close continues after cancelling a reload'
        Assert-Regression ($handler.Count -eq 2 -and $second.Browser.IsDisposed) 'reload cancellation preserves the window close request and requests separate leave confirmation'
    } finally {
        if ($null -ne $handler) { $handler.CompletePending($true) }
        $window.ForceCloseWindow()
    }
}

function Test-BrowserTabsWindowClose($Context) {
    $window = New-RegressionWindow $Context
    try {
        $model = $window.ViewModel
        $first = $model.CurrentBrowser
        $second = New-RegressionTab $Context $window $Context.AuxiliaryAddress
        $third = New-RegressionTab $Context $window $Context.AuxiliarySecondAddress
        $model.CurrentBrowser = $second
        $handler = Set-RegressionBeforeUnload $Context $second.Browser
        $fourth = New-RegressionTab $Context $window
        $window.Close()
        $window.Close()
        Wait-Regression $Context { $handler.Count -eq 1 -and !$model.IsClosing } 'beforeunload cancellation stops window close'
        Assert-Regression ($fourth.Browser.IsDisposed -and $first.Browser.IsDisposed -and $model.Browsers.Count -eq 2) 'window close starts at the current tab then removes approved tabs in collection order'
        Assert-Regression ($model.CurrentBrowser -eq $second -and !$third.Browser.IsDisposed) 'cancelled page is selected and later tabs remain open'
        Assert-Regression ($handler.Count -eq 1) 'repeated window close does not duplicate confirmation'
        $handler.Accept = $true
        $window.Close()
        Wait-Regression $Context { !$window.IsVisible } 'window close retries all remaining tabs after approval'
        Assert-Regression ($handler.Count -eq 2 -and $second.Browser.IsDisposed -and $third.Browser.IsDisposed) 'window close releases every approved tab'
    } finally {
        $window.ForceCloseWindow()
    }
}
function Test-BrowserTabsForceClose($Context) {
    foreach ($force in @($false, $true)) {
        $window = New-RegressionWindow $Context -SkipWait
        $window.ViewModel.AddBrowser('about:blank', $false) | Out-Null
        $window.ViewModel.AddBrowser('about:blank', $true) | Out-Null
        $window.UpdateLayout()
        $browsers = @($window.ViewModel.Browsers | ForEach-Object { $_.Browser })
        if ($force) { $window.ForceCloseWindow() } else { $window.Close() }
        Wait-Regression $Context { !$window.IsVisible } "multi-tab close during initialization (force=$force)"
        Assert-Regression (@($browsers | Where-Object { $null -ne $_ -and !$_.IsDisposed }).Count -eq 0) 'initializing tab browsers are all released'
        $window.ForceCloseWindow()
    }
    $window = New-RegressionWindow $Context
    try {
        $first = $window.ViewModel.CurrentBrowser
        $second = New-RegressionTab $Context $window $Context.AuxiliaryAddress
        $handler = Set-RegressionBeforeUnload $Context $second.Browser
        $window.ViewModel.AddBrowser('about:blank', $false) | Out-Null
        $window.UpdateLayout()
        $browsers = @($window.ViewModel.Browsers | ForEach-Object { $_.Browser })
        $window.ForceCloseWindow()
        Assert-Regression (!$window.IsVisible -and $handler.Count -eq 0) 'force close bypasses every tab confirmation'
        Assert-Regression (@($browsers | Where-Object { $null -ne $_ -and !$_.IsDisposed }).Count -eq 0) 'force close returns after every tab is disposed'
    } finally {
        $window.ForceCloseWindow()
    }
}

function Test-BrowserTabsForceClosePending($Context) {
    $window = New-RegressionWindow $Context
    $watchdog = New-Object System.Windows.Threading.DispatcherTimer
    $state = @{ TimedOut = $false }
    try {
        $second = New-RegressionTab $Context $window $Context.AuxiliaryAddress
        $handler = Set-RegressionBeforeUnload $Context $second.Browser
        $handler.Hold = $true
        $window.Close()
        Wait-Regression $Context { $handler.Count -eq 1 } 'window close awaits beforeunload approval'
        $model = $window.ViewModel
        $current = $model.CurrentBrowser
        $count = $model.Browsers.Count
        Invoke-BrowserCommand $model.AddBrowserCommand
        Invoke-BrowserCommand $model.SelectNextBrowserCommand
        $model.OnNewPage($current, 'about:blank')
        Assert-Regression ($model.Browsers.Count -eq $count -and $model.CurrentBrowser -eq $current) 'closing window ignores new tab and selection commands'
        $browsers = @($window.ViewModel.Browsers | ForEach-Object { $_.Browser })
        $watchdog.Interval = [TimeSpan]::FromSeconds(5)
        $watchdog.add_Tick({
            $state.TimedOut = $true
            $watchdog.Stop()
            $handler.CompletePending($true)
        }.GetNewClosure())
        $watchdog.Start()
        $window.ForceCloseWindow()
        Assert-Regression (!$state.TimedOut -and !$window.IsVisible) 'force close completes while a normal close awaits confirmation'
        Assert-Regression (@($browsers | Where-Object { !$_.IsDisposed }).Count -eq 0) 'escalated close releases all tabs before returning'
        $handler.CompletePending($false)
        Assert-Regression ($window.ViewModel.Browsers.Count -eq 0) 'late beforeunload cancellation cannot restore closed tabs'
    } finally {
        $watchdog.Stop()
        if ($null -ne $handler) { $handler.CompletePending($true) }
        $window.ForceCloseWindow()
    }
}
