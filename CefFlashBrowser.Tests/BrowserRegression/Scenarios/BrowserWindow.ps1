function Test-BrowserWindow($Context) {
    $window = New-RegressionWindow $Context
    $browser = $window.ViewModel.CurrentBrowser.Browser
    $view = Find-BrowserView $window

    Test-BrowserTemplate $Context $view $browser
    Test-BrowserFrameLoadStartAfterDisposal $Context
    Test-BrowserDevTools $Context $window $view $browser
    Test-BrowserNavigation $Context $window $view $browser
    Test-BrowserDisplay $Context $window $view $browser
    Test-BrowserPageSwfs $Context
    Test-BrowserClose $Context $window $view $browser
    Test-BrowserStateRestoration $Context $browser
}

function Test-BrowserFrameLoadStartAfterDisposal($Context) {
    $window = New-RegressionWindow $Context
    $browser = $window.ViewModel.CurrentBrowser.Browser
    $callback = @{ Disposed = $false }
    $handler = [EventHandler[CefSharp.FrameLoadStartEventArgs]]({
        param($sender, $eventArgs)
        $callback.Disposed = $true
        $sender.Dispose()
    }.GetNewClosure())
    $browser.add_FrameLoadStart($handler)
    $eventArgs = New-Object CefSharp.FrameLoadStartEventArgs(
        $browser.GetBrowser(),
        (New-Object RegressionMainFrameProxy).GetTransparentProxy(),
        [CefSharp.TransitionType]::LinkClicked)
    $method = [CefFlashBrowser.WinformCefSharp4WPF.ChromiumWebBrowser].GetMethod(
        'OnFrameLoadStart', [Reflection.BindingFlags]'Instance,NonPublic', $null,
        [Type[]]@([object], [CefSharp.FrameLoadStartEventArgs]), $null)
    Assert-Regression ($null -ne $method) 'frame-load handler is discoverable'
    $threw = $false

    try {
        [void]$method.Invoke($browser.PSObject.BaseObject, [object[]]@($null, $eventArgs.PSObject.BaseObject))
    } catch {
        $threw = $true
    } finally {
        $browser.remove_FrameLoadStart($handler)
        $window.ForceCloseWindow()
    }

    Assert-Regression $callback.Disposed 'main-frame callback can dispose the hosted browser'
    Assert-Regression (!$threw) 'frame-load zoom update tolerates disposal during the callback'
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

    $capture = @{ Called = $false }
    $interceptClose = [CefFlashBrowser.Views.BrowserDoCloseEventHandler](
        {
            param($sender, $eventArgs)
            $capture.Called = $true
            $eventArgs.Handled = $true
        }.GetNewClosure())
    $view.add_BrowserDoClose($interceptClose)
    try {
        $racingBrowser = (New-Object DisposedDuringCallbackBrowserProxy).GetTransparentProxy()
        Invoke-ViewCallback $view 'OnBrowserDoClose' @($browser, $racingBrowser)
    } finally {
        $view.remove_BrowserDoClose($interceptClose)
    }
    Assert-Regression $capture.Called 'browser disposal between IsDisposed and GetHost does not escape the close callback'
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

    $favorite = $Context.FavoritesService.Favorites | Select-Object -Last 1
    Assert-Regression ($favorite.Name -eq 'Browser regression' -and $favorite.Url -eq $secondAddress) 'favorite dialog preserves the current title and address'
}

function Test-BrowserDisplay($Context, $window, $view, $browser) {
    $statusMethod = [CefFlashBrowser.WinformCefSharp4WPF.ChromiumWebBrowser].GetMethod(
        'OnStatusMessage', [Reflection.BindingFlags]'Instance,NonPublic', $null, [Type[]]@([CefSharp.StatusMessageEventArgs]), $null)
    $statusEvent = New-Object CefSharp.StatusMessageEventArgs($browser.GetBrowser(), 'regression status')
    [void]$statusMethod.Invoke($browser.PSObject.BaseObject, [object[]]@($statusEvent.PSObject.BaseObject))
    Assert-Regression ($browser.StatusText -eq 'regression status') 'status callback updates the browser property'
    Assert-Regression ([object]::ReferenceEquals($window.FindName('statusPopupContent').Content, $view.ViewModel)) 'status popup retains its browser binding'

    Test-BrowserStatusPopupOffset $Context $window

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

function Test-BrowserPageSwfs($Context) {
    $fixture = New-Object UserScriptHttpFixture
    $window = $null
    $player = $null
    try {
        $window = New-RegressionWindow $Context
        $model = $window.ViewModel.CurrentBrowser
        $browser = $model.Browser
        $button = $window.FindName('toolbarPageSwfsButton')
        $menu = $window.Resources['pageSwfsMenu']
        Assert-Regression ($button.Visibility -eq 'Collapsed') 'SWF toolbar button is initially hidden'

        $model.Address = $fixture.BaseUrl + '/swf-page'
        Wait-Regression $Context { $browser.Title -eq 'SWF fixture' -and !$browser.IsLoading } 'SWF fixture page loads'
        $gameUrl = $fixture.BaseUrl + '/GAME.SWF?token=a%2Fb'
        $expected = @(
            $fixture.BaseUrl + '/embedded.swf'
            $fixture.BaseUrl + '/frame.swf'
            $gameUrl
            $fixture.BaseUrl + '/swf-response'
            $fixture.BaseUrl + '/swf-missing.swf'
        )
        [void](Invoke-JavaScript $Context $browser @'
console.info('Cross-origin plugin content from https://example.invalid/fake.swf');
fetch('/GAME.SWF?token=a%2Fb').then(() => fetch('/GAME.SWF?token=a%2Fb'));
fetch('/swf-redirect');
fetch('/swf-missing.swf');
true;
'@)
        Wait-Regression $Context {
            $browser.PageSwfs.Count -ge $expected.Count -and $fixture.RequestsFor('/GAME.SWF') -eq 2
        } 'CEF observes SWFs from the page and its iframe'
        $actual = @($browser.PageSwfs | ForEach-Object { $_.Url })
        Assert-Regression ($actual.Count -eq $expected.Count -and !(Compare-Object $expected $actual)) 'SWF list contains only requested resources and deduplicates URLs'
        $game = $browser.PageSwfs | Where-Object { $_.Url -ceq $gameUrl }
        Assert-Regression ($game -is [CefFlashBrowser.FlashBrowser.SwfResourceRecord] -and
            $game.FileName -ceq 'GAME.SWF') 'SWF item exposes its filename and full URL'
        Wait-Regression $Context { $button.IsVisible } 'SWF toolbar button appears'
        Assert-Regression ([Windows.Automation.AutomationProperties]::GetName($button) -ceq 'PageSwfs') 'SWF toolbar has a fixed automation name'
        Assert-Regression ($button.Content.UriSource.OriginalString -eq '/Assets/Svgs/flash-player.svg') 'SWF toolbar uses the Flash Player icon'

        $buttonPeer = New-Object System.Windows.Automation.Peers.ButtonAutomationPeer($button)
        $buttonPeer.GetPattern([System.Windows.Automation.Peers.PatternInterface]::Invoke).Invoke()
        Wait-Regression $Context { $menu.IsOpen -and $menu.Items.Count -eq $expected.Count } 'clicking the SWF button displays the current page list'
        Wait-Regression $Context {
            $null -ne $menu.ItemContainerGenerator.ContainerFromItem($game)
        } 'SWF menu generates the player item'
        $item = $menu.ItemContainerGenerator.ContainerFromItem($game)
        Assert-Regression ($item.ToolTip -ceq $gameUrl -and $null -ne $item.Command) 'SWF menu item exposes the complete URL and player action'
        $itemPeer = New-Object System.Windows.Automation.Peers.MenuItemAutomationPeer($item)
        $itemPeer.GetPattern([System.Windows.Automation.Peers.PatternInterface]::Invoke).Invoke()
        Wait-Regression $Context {
            @($Context.Application.Windows | Where-Object {
                $_ -is [CefFlashBrowser.Views.SwfPlayerWindow] -and $_.FileName -ceq $gameUrl
            }).Count -eq 1
        } 'SWF menu selection opens the player with the full URL'
        $player = $Context.Application.Windows | Where-Object {
            $_ -is [CefFlashBrowser.Views.SwfPlayerWindow] -and $_.FileName -ceq $gameUrl
        }
        $playerBrowser = (Find-BrowserView $player).ViewModel.Browser
        Wait-Regression $Context { $player.Title -ceq 'GAME.SWF' } 'SWF player applies the selected file'
        Assert-Regression ((Invoke-JavaScript $Context $playerBrowser "document.getElementById('flash-player').getAttribute('src')") -ceq $gameUrl) 'SWF player preserves the query string'

        [void](New-RegressionTab $Context $window ($fixture.BaseUrl + '/swf-empty'))
        Wait-Regression $Context { $button.Visibility -eq 'Collapsed' } 'a tab with no SWFs hides the button'
        $window.ViewModel.SelectBrowser(-1)
        Wait-Regression $Context { $window.ViewModel.CurrentBrowser -eq $model -and $button.IsVisible } 'returning to the SWF tab restores the button'

        $version = $browser.SwfPageVersion
        [void](Invoke-JavaScript $Context $browser "location.hash = 'section'; fetch('/swf-delayed'); true;")
        Wait-Regression $Context { $fixture.RequestsFor('/swf-delayed') -eq 1 } 'the old document has an in-flight SWF response'
        Assert-Regression ($browser.PageSwfs.Count -eq $expected.Count -and $browser.SwfPageVersion -eq $version) 'same-document navigation keeps the SWF list'
        $model.Address = $fixture.BaseUrl + '/swf-empty'
        Wait-Regression $Context { $browser.Title -eq 'Empty SWF fixture' -and $button.Visibility -eq 'Collapsed' } 'a new document clears the SWF list'
        $fixture.Release('/swf-delayed')
        $browser.ObserveSwfRequest($version, ($fixture.BaseUrl + '/swf-delayed'), 'application/x-shockwave-flash')
        [void](Invoke-JavaScript $Context $browser 'true')
        Assert-Regression ($browser.PageSwfs.Count -eq 0) 'an old document response cannot populate the new page'
    } finally {
        if ($null -ne $player) { $player.ForceCloseWindow() }
        if ($null -ne $window) { $window.ForceCloseWindow() }
        $fixture.Dispose()
    }
}

function Test-BrowserStatusPopupOffset($Context, $window) {
    $popup = $window.FindName('statusPopup')
    $content = $window.FindName('statusPopupContent')
    $wasOpen = $popup.IsOpen
    $placement = $popup.PlacementRectangle
    $cursor = [System.Windows.Forms.Cursor]::Position
    $flushLayout = {
        [void]$Context.Application.Dispatcher.Invoke(
            [System.Windows.Threading.DispatcherPriority]::Background, [Action]{})
    }

    try {
        $area = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
        $outside = New-Object System.Drawing.Point(($area.Right - 10), ($area.Top + 10))
        [System.Windows.Forms.Cursor]::Position = $outside
        $popup.PlacementRectangle = New-Object System.Windows.Rect(($area.Left + 100), ($area.Top + 100), 0, 0)
        $content.Visibility = [System.Windows.Visibility]::Visible
        $popup.IsOpen = $true
        Wait-Regression $Context { $content.IsVisible -and $content.IsLoaded -and $content.ActualHeight -gt 0 } 'status popup layout'
        & $flushLayout
        $origin = $content.PointToScreen((New-Object System.Windows.Point(0, 0)))
        [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point(
            ($area.Right - 10), ([int]($origin.Y + $content.ActualHeight / 2)))
        Invoke-ViewCallback $window 'StatusPopupMouseEnter' @($popup, $null)
        Assert-Regression ($popup.VerticalOffset -eq -30) 'status popup avoids the cursor using the vertical interval'

        for ($update = 0; $update -lt 3; $update++) {
            Wait-Regression $Context {
                $content.PointToScreen((New-Object System.Windows.Point(0, 0))).Y -lt $origin.Y
            } 'status popup has moved above the cursor'
            Invoke-ViewCallback $window 'StatusPopupSizeChanged' @($popup, $null)
            Assert-Regression ($popup.VerticalOffset -eq -30) 'repeated status popup updates preserve cursor avoidance'
        }

        $content.Visibility = [System.Windows.Visibility]::Collapsed
        $content.Visibility = [System.Windows.Visibility]::Visible
        & $flushLayout
        Assert-Regression ($popup.VerticalOffset -eq -30) 'redisplaying shifted status content keeps avoiding the cursor'

        $content.Visibility = [System.Windows.Visibility]::Collapsed
        [System.Windows.Forms.Cursor]::Position = $outside
        $content.Visibility = [System.Windows.Visibility]::Visible
        & $flushLayout
        Assert-Regression ($popup.VerticalOffset -eq 0) 'redisplaying status content outside the interval restores its position'
    } finally {
        $popup.IsOpen = $false
        $popup.VerticalOffset = 0
        $popup.PlacementRectangle = $placement
        [System.Windows.Forms.Cursor]::Position = $cursor
        $content.ClearValue([System.Windows.UIElement]::VisibilityProperty)
        $popup.IsOpen = $wasOpen
    }
}

function Test-BrowserClose($Context, $window, $view, $browser) {
    $browserModel = $view.ViewModel
    $dialogHandler = Set-RegressionBeforeUnload $Context $browser
    $window.Close()
    Wait-Regression $Context { $dialogHandler.Count -gt 0 } 'beforeunload dialog on normal close'
    Wait-Regression $Context { !$window.ViewModel.IsClosing } 'cancelled close returns the window to its open state'
    Assert-Regression $window.IsVisible 'cancelling beforeunload keeps the window open'
    $dialogHandler.Accept = $true
    $closeBinding = $window.InputBindings | Where-Object {
        $_.Key -eq [System.Windows.Input.Key]::W -and
        $_.Modifiers -eq [System.Windows.Input.ModifierKeys]::Control
    }
    Invoke-BrowserCommand $closeBinding.Command $closeBinding.CommandParameter
    Wait-Regression $Context { !$window.IsVisible } 'Ctrl+W closes after beforeunload approval'
    Assert-Regression ($view.GetType().GetField('_isClosed', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($view)) 'host close cleans up BrowserView'
    Invoke-ViewCallback $view 'OnBrowserDoClose' @($browser, $Context.DisposedBrowser)
    $window.ViewModel.CreateShortcut($browserModel)
    $window.ViewModel.AddFavorite($browserModel)
    $window.ViewModel.ToggleLoadingState($browserModel)
    $window.ViewModel.FindText($browserModel, $true)
    foreach ($commandName in $Context.BrowserCommandNames) {
        Invoke-BrowserCommand $window.ViewModel.$commandName $browserModel
    }
    $browserModel.ToggleDevTools()
    $browserModel.ExitFullScreen()
    Assert-Regression $browser.IsDisposed 'commands tolerate disposed browsers'
}

function Test-BrowserStateRestoration($Context, $PreviousBrowser) {
    $window = New-RegressionWindow $Context
    $view = Find-BrowserView $window
    $browserModel = $view.ViewModel
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
    Assert-Regression $browserModel.Browser.IsDisposed 'force close releases the hosted browser'
    $window.ForceCloseWindow()
}
