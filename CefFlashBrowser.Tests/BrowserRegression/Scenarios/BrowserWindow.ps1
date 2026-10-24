function Test-BrowserWindow($Context) {
    $window = New-RegressionWindow $Context
    $browser = $window.ViewModel.CurrentBrowser.Browser
    $view = Find-BrowserView $window

    Test-BrowserTemplate $Context $view $browser
    Test-BrowserDevTools $Context $window $view $browser
    Test-BrowserNavigation $Context $window $view $browser
    Test-BrowserDisplay $Context $window $view $browser
    Test-BrowserClose $Context $window $view $browser
    Test-BrowserStateRestoration $Context $browser
}

function Test-BrowserTemplate($Context, $view, $browser) {
    Assert-Regression ([object]::ReferenceEquals($view.ViewModel.Browser, $browser)) 'template synchronously associates the browser'
    $view.ViewModel.Browser = $browser
    $view.OnApplyTemplate()
    $view.OnApplyTemplate()
    Invoke-ViewCallback $view 'BrowserViewLoaded' @($view, (New-Object System.Windows.RoutedEventArgs))
    Assert-Regression ([object]::ReferenceEquals($view.ViewModel.Browser, $browser)) 'reapplying the template and Loaded is safe'

    $capture = @{ Called = $false }
    $interceptClose = [CefFlashBrowser.Views.BrowserDoCloseEventHandler]({
        param($sender, $eventArgs)
        $capture.Called = $true
        $eventArgs.Handled = $true
    }.GetNewClosure())
    $view.add_BrowserDoClose($interceptClose)
    try {
        Invoke-ViewCallback $view 'OnBrowserDoClose' @($browser, $Context.DisposedBrowser)
    } finally {
        $view.remove_BrowserDoClose($interceptClose)
    }
    Assert-Regression $capture.Called 'disposed callback browser does not access popup or host properties'
}

function Test-BrowserDevTools($Context, $window, $view, $browser) {
    [CefFlashBrowser.Data.GlobalData]::Settings.EnableIntegratedDevTools = $false
    for ($cycle = 0; $cycle -lt 2; $cycle++) {
        $view.ViewModel.ToggleDevTools()
        Wait-Regression $Context { [CefFlashBrowser.Utils.HwndHelper]::FindNotIntegratedDevTools($browser) -ne [IntPtr]::Zero } 'detached DevTools opens through the template'
        if ($cycle -eq 0) {
            $firstTools = [CefFlashBrowser.Utils.HwndHelper]::FindNotIntegratedDevTools($browser)
            $otherWindow = New-RegressionWindow $Context
            $otherBrowser = $otherWindow.ViewModel.CurrentBrowser.Browser
            Assert-Regression ([CefFlashBrowser.Utils.HwndHelper]::FindNotIntegratedDevTools($otherBrowser) -eq [IntPtr]::Zero) 'DevTools lookup is isolated between browser windows'
            $otherWindow.ViewModel.CurrentBrowser.ToggleDevTools()
            Wait-Regression $Context { [CefFlashBrowser.Utils.HwndHelper]::FindNotIntegratedDevTools($otherBrowser) -ne [IntPtr]::Zero } 'second window DevTools opens'
            Assert-Regression ([CefFlashBrowser.Utils.HwndHelper]::FindNotIntegratedDevTools($otherBrowser) -ne $firstTools) 'each browser owns its own DevTools window'
            $otherWindow.ViewModel.CurrentBrowser.ToggleDevTools()
            Wait-Regression $Context { [CefFlashBrowser.Utils.HwndHelper]::FindNotIntegratedDevTools($otherBrowser) -eq [IntPtr]::Zero } 'second window DevTools closes'
            $otherWindow.ForceCloseWindow()
        }
        $view.ViewModel.ToggleDevTools()
        Wait-Regression $Context { [CefFlashBrowser.Utils.HwndHelper]::FindNotIntegratedDevTools($browser) -eq [IntPtr]::Zero } 'detached DevTools closes through the toggle'
    }
    Assert-Regression $window.IsVisible 'closing DevTools does not close its browser window'

    [CefFlashBrowser.Data.GlobalData]::Settings.EnableIntegratedDevTools = $true
    [CefFlashBrowser.Data.GlobalData]::Settings.IntegratedDevToolsWidth = 340
    Open-RegressionDevTools $Context $view
    Wait-Regression $Context { $view.ViewModel.IntegratedDevToolsHandle -ne [IntPtr]::Zero } 'integrated DevTools opens'
    $devtoolsColumn = $view.FindName('devtoolsColumn')
    $devtoolsColumn.Width = New-Object System.Windows.GridLength(380)
    $view.ViewModel.ToggleDevTools()
    Wait-Regression $Context { $view.ViewModel.IntegratedDevToolsHandle -eq [IntPtr]::Zero } 'integrated DevTools closes'
    Assert-Regression ([CefFlashBrowser.Data.GlobalData]::Settings.IntegratedDevToolsWidth -eq 380) 'DevTools width persists'
}

function Test-BrowserNavigation($Context, $window, $view, $browser) {
    $firstAddress = 'data:text/html,' + [Uri]::EscapeDataString('<title>First page</title><p>first</p>')
    $secondAddress = 'data:text/html,' + [Uri]::EscapeDataString('<title>Browser regression</title><p>find this text</p>')
    $view.ViewModel.Address = $firstAddress
    Wait-Regression $Context { $window.Title -eq 'First page' } 'address binding navigates to the first page'
    $view.ViewModel.Address = $secondAddress
    Wait-Regression $Context { $window.Title -eq 'Browser regression' } 'browser title binding'
    Invoke-BrowserCommand $browser.BackCommand
    Wait-Regression $Context { $window.Title -eq 'First page' } 'back command'
    Invoke-BrowserCommand $browser.ForwardCommand
    Wait-Regression $Context { $window.Title -eq 'Browser regression' } 'forward command'
    Assert-Regression ($view.ViewModel.Address -eq $secondAddress) 'history navigation updates the address binding'
    Invoke-BrowserCommand $browser.ReloadCommand
    Wait-Regression $Context { !$browser.IsLoading -and $browser.CanExecuteJavascriptInMainFrame } 'reload command'
    Invoke-BrowserCommand $window.ViewModel.ToggleLoadingStateCommand $view.ViewModel
    Wait-Regression $Context { !$browser.IsLoading -and $browser.CanExecuteJavascriptInMainFrame } 'reload through BrowserViewModel command'

    $favoriteTimer = New-Object System.Windows.Threading.DispatcherTimer
    $favoriteTimer.Interval = [TimeSpan]::FromMilliseconds(20)
    $favoriteTimer.add_Tick({
        $dialog = $Context.Application.Windows | Where-Object {
            $_ -is [CefFlashBrowser.Views.Dialogs.AddFavoriteDialog]
        } | Select-Object -First 1

        if ($null -ne $dialog -and $dialog.IsLoaded) {
            $favoriteTimer.Stop()
            Invoke-BrowserCommand $dialog.DataContext.ConfirmCommand
        }
    })
    $favoriteTimer.Start()

    try {
        Invoke-BrowserCommand $window.ViewModel.AddFavoriteCommand $view.ViewModel
    } finally {
        $favoriteTimer.Stop()
    }

    $favorite = [CefFlashBrowser.Data.GlobalData]::Favorites | Select-Object -Last 1
    Assert-Regression ($favorite.Name -eq 'Browser regression' -and $favorite.Url -eq $secondAddress) 'favorite dialog preserves the current title and address'
}

function Test-BrowserDisplay($Context, $window, $view, $browser) {
    [void](Invoke-JavaScript $Context $browser "console.info('Cross-origin plugin content from https://example.invalid/game.swf');")
    Wait-Regression $Context { $browser.HasBlockedSwfs } 'Flash blocking notification'
    $blockedItems = $window.Resources['blockedSwfsMenu'].Items[1]
    Assert-Regression ($blockedItems.Items.Contains('https://example.invalid/game.swf')) 'blocked Flash menu retains its browser binding'
    $statusMethod = [CefFlashBrowser.WinformCefSharp4WPF.ChromiumWebBrowser].GetMethod(
        'OnStatusMessage', [Reflection.BindingFlags]'Instance,NonPublic', $null, [Type[]]@([CefSharp.StatusMessageEventArgs]), $null)
    $statusEvent = New-Object CefSharp.StatusMessageEventArgs($browser.GetBrowser(), 'regression status')
    [void]$statusMethod.Invoke($browser.PSObject.BaseObject, [object[]]@($statusEvent.PSObject.BaseObject))
    Assert-Regression ($browser.StatusText -eq 'regression status') 'status callback updates the browser property'
    Assert-Regression ([object]::ReferenceEquals($window.FindName('statusPopupContent').Content, $view.ViewModel)) 'status popup retains its browser binding'

    $window.ViewModel.TextToFind = 'find this'
    Invoke-BrowserCommand $window.ViewModel.FindNextTextCommand $view.ViewModel
    Invoke-BrowserCommand $window.ViewModel.FindPrevTextCommand $view.ViewModel
    $window.ViewModel.TextToFind = ''
    Invoke-BrowserCommand $window.ViewModel.FindNextTextCommand $view.ViewModel
    $window.ViewModel.ShowFindPopup = $true
    $window.ViewModel.ShowFindPopup = $false
    $browser.ZoomLevel = 0.5
    Wait-Regression $Context { $view.ViewModel.ZoomLevel -eq 0.5 } 'zoom binding'
    Assert-Regression ([CefFlashBrowser.Data.GlobalData]::Settings.BrowserZoomLevel -eq 0.5) 'zoom persists through BrowserViewModel'
    $window.ViewModel.ToggleFullscreen($view.ViewModel)
    Assert-Regression ($window.WindowStyle -eq [System.Windows.WindowStyle]::None) 'F11 enters window fullscreen'
    $window.ViewModel.ToggleFullscreen($view.ViewModel)
    Assert-Regression ($window.WindowStyle -eq [System.Windows.WindowStyle]::SingleBorderWindow) 'F11 restores window chrome'
}

function Test-BrowserClose($Context, $window, $view, $browser) {
    $dialogHandler = Set-RegressionBeforeUnload $Context $browser
    $window.Close()
    Wait-Regression $Context { $dialogHandler.Count -gt 0 } 'beforeunload dialog on normal close'
    Assert-Regression $window.IsVisible 'cancelling beforeunload keeps the window open'
    $dialogHandler.Accept = $true
    $closeBinding = $window.InputBindings | Where-Object {
        $_.Key -eq [System.Windows.Input.Key]::W -and
        $_.Modifiers -eq [System.Windows.Input.ModifierKeys]::Control
    }
    Invoke-BrowserCommand $closeBinding.Command ([bool]$closeBinding.CommandParameter)
    Wait-Regression $Context { !$window.IsVisible } 'Ctrl+W closes after beforeunload approval'
    Assert-Regression ($view.GetType().GetField('_isClosed', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($view)) 'host close cleans up BrowserView'
    Invoke-ViewCallback $view 'OnBrowserDoClose' @($browser, $Context.DisposedBrowser)
    $window.ViewModel.CreateShortcut($view.ViewModel)
    $window.ViewModel.AddFavorite($view.ViewModel)
    $window.ViewModel.ToggleLoadingState($view.ViewModel)
    $window.ViewModel.FindText($view.ViewModel, $true)
    foreach ($commandName in $Context.BrowserCommandNames) {
        Invoke-BrowserCommand $window.ViewModel.$commandName $view.ViewModel
    }
    $view.ViewModel.ToggleDevTools()
    $view.ViewModel.ExitFullScreen()
    Assert-Regression $browser.IsDisposed 'commands tolerate disposed browsers'
}

function Test-BrowserStateRestoration($Context, $PreviousBrowser) {
    $window = New-RegressionWindow $Context
    $view = Find-BrowserView $window
    Assert-Regression ($view.ViewModel.ZoomLevel -eq 0.5) 'a new browser restores the saved zoom level'
    $rejected = $false
    try {
        $view.ViewModel.Browser = $PreviousBrowser
    } catch {
        $rejected = $true
    }

    Assert-Regression $rejected 'a different browser instance is rejected'
    Open-RegressionDevTools $Context $view
    Wait-Regression $Context { $view.ViewModel.IntegratedDevToolsHandle -ne [IntPtr]::Zero } 'DevTools opens before force close'
    Assert-Regression ($view.FindName('devtoolsColumn').Width.Value -eq 380) 'integrated DevTools restores its saved width'
    $view.FindName('devtoolsColumn').Width = New-Object System.Windows.GridLength(400)
    $window.ForceCloseWindow()
    Assert-Regression ([CefFlashBrowser.Data.GlobalData]::Settings.IntegratedDevToolsWidth -eq 400) 'force close preserves DevTools width without DoClose'
    Assert-Regression ($view.GetType().GetField('_isClosed', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($view)) 'force close cleans up BrowserView'
    Assert-Regression $view.ViewModel.Browser.IsDisposed 'force close releases the hosted browser'
    $window.ForceCloseWindow()
}
