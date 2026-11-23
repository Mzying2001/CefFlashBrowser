function Test-BrowserWindow($Context) {
    $window = New-RegressionWindow $Context
    $browser = $window.ViewModel.CurrentBrowser.Browser
    $view = Find-BrowserView $window

    Test-BrowserTemplate $Context $view $browser
    Test-BrowserFrameLoadStartAfterDisposal $Context
    Test-BrowserDevTools $Context $window $view $browser
    Test-BrowserNavigation $Context $window $view $browser
    Test-BrowserDisplay $Context $window $view $browser
    Test-StandaloneBrowserPageSwfs $Context
    Test-BrowserPageSwfs $Context
    Test-SwfPlayerPageContext $Context
    Test-LocalSwfPlayerPageContext $Context
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

function Test-StandaloneBrowserPageSwfs($Context) {
    $fixture = New-Object UserScriptHttpFixture
    $window = New-Object System.Windows.Window -Property @{ Width = 640; Height = 480 }
    $browser = New-Object CefFlashBrowser.FlashBrowser.ChromiumFlashBrowser($true)
    try {
        $window.Content = $browser
        $window.Show()
        $browser.Address = $fixture.BaseUrl + '/swf-empty'
        Wait-Regression $Context { $browser.Title -eq 'Empty SWF fixture' -and !$browser.IsLoading } 'standalone browser loads a page'
        Assert-Regression ($null -eq $browser.RequestHandler -and $null -eq $browser.ResourceRequestHandlerFactory) 'standalone browser starts with no caller-supplied handlers'
        [void](Invoke-JavaScript $Context $browser "fetch('/swf-response'); true;")
        Wait-Regression $Context {
            $browser.PageSwfs.Count -eq 1 -and $browser.PageSwfs[0].Url -eq ($fixture.BaseUrl + '/swf-response')
        } 'standalone browser discovers a SWF by MIME type'

        $handler = New-Object CefFlashBrowser.FlashBrowser.Handlers.RequestHandler
        $factory = [CefSharp.ResourceRequestHandlerFactory]::new([StringComparer]::Ordinal)
        $browser.RequestHandler = $handler
        $browser.ResourceRequestHandlerFactory = $factory
        Assert-Regression ([object]::ReferenceEquals($browser.RequestHandler, $handler) -and
            [object]::ReferenceEquals($browser.ResourceRequestHandlerFactory, $factory)) 'browser handler getters return the caller-supplied objects'
        $registeredUrl = $fixture.BaseUrl + '/swf-registered'
        $stream = [IO.MemoryStream]::new([Text.Encoding]::UTF8.GetBytes('registered SWF payload'))
        try {
            [CefSharp.WebBrowserExtensions]::RegisterResourceHandler($browser, $registeredUrl, $stream, 'application/x-shockwave-flash', $true)
        } finally {
            $stream.Dispose()
        }
        [void](Invoke-JavaScript $Context $browser "fetch('/swf-registered').then(r => r.text()).then(text => window.registeredSwfBody = text); true;")
        Wait-Regression $Context {
            ($browser.PageSwfs.Url -contains $registeredUrl) -and
                (Invoke-JavaScript $Context $browser 'window.registeredSwfBody') -ceq 'registered SWF payload'
        } 'registered SWFs are served and observed with a caller-supplied request handler'
        Assert-Regression ($fixture.RequestsFor('/swf-registered') -eq 0) 'the registered SWF response is served locally'

        $browser.RequestHandler = $null
        $browser.ResourceRequestHandlerFactory = $null
        [void](Invoke-JavaScript $Context $browser "fetch('/after-handler-clear.swf'); true;")
        Wait-Regression $Context {
            $browser.PageSwfs.Url -contains ($fixture.BaseUrl + '/after-handler-clear.swf')
        } 'SWF collection continues after clearing caller-supplied handlers'

        [void](Invoke-JavaScript $Context $browser "fetch('/AFTER-handler-clear.swf?variant=1'); true;")
        Wait-Regression $Context {
            $browser.PageSwfs.Url -ccontains ($fixture.BaseUrl + '/AFTER-handler-clear.swf?variant=1')
        } 'SWF collection keeps a second URL with an equivalent filename'
        Assert-Regression (($browser.PageSwfs.FileName -join '|') -ceq
            'after-handler-clear.swf|AFTER-handler-clear.swf|swf-registered|swf-response') 'later SWFs insert in filename order with stable case-insensitive ties'
    } finally {
        $window.Close()
        $browser.Dispose()
        $fixture.Dispose()
    }
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
        Assert-Regression ($button.Content.UriSource.OriginalString -eq '/Assets/Svgs/swf.svg') 'SWF toolbar uses the SWF icon'

        $buttonPeer = New-Object System.Windows.Automation.Peers.ButtonAutomationPeer($button)
        $buttonPeer.GetPattern([System.Windows.Automation.Peers.PatternInterface]::Invoke).Invoke()
        Wait-Regression $Context { $menu.IsOpen -and $menu.Items.Count -eq $expected.Count } 'clicking the SWF button displays the current page list'
        Assert-Regression ((($menu.Items | ForEach-Object { $_.FileName }) -join '|') -ceq
            'embedded.swf|frame.swf|GAME.SWF|swf-missing.swf|swf-response') 'SWF menu lists filenames in ascending case-insensitive order'
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
        Assert-Regression ((Invoke-JavaScript $Context $playerBrowser 'location.href') -ceq ($fixture.BaseUrl + '/swf-page')) 'a request without an embed still uses the source page context'

        [void](New-RegressionTab $Context $window ($fixture.BaseUrl + '/swf-empty'))
        Wait-Regression $Context { $button.Visibility -eq 'Collapsed' } 'a tab with no SWFs hides the button'
        $window.ViewModel.SelectBrowser(-1)
        Wait-Regression $Context { $window.ViewModel.CurrentBrowser -eq $model -and $button.IsVisible } 'returning to the SWF tab restores the button'

        [void](Invoke-JavaScript $Context $browser "location.hash = 'section'; fetch('/swf-delayed', { keepalive: true }); true;")
        Wait-Regression $Context { $fixture.RequestsFor('/swf-delayed') -eq 1 } 'the old document has an in-flight SWF response'
        Assert-Regression ($browser.PageSwfs.Count -eq $expected.Count) 'same-document navigation keeps the SWF list'
        $model.Address = $fixture.BaseUrl + '/swf-empty'
        Wait-Regression $Context { $browser.Title -eq 'Empty SWF fixture' -and $button.Visibility -eq 'Collapsed' } 'a new document clears the SWF list'
        $fixture.Release('/swf-delayed')
        [void](Invoke-JavaScript $Context $browser "fetch('/swf-delayed?document=current'); true;")
        $currentSwfUrl = $fixture.BaseUrl + '/swf-delayed?document=current'
        Wait-Regression $Context { $browser.PageSwfs.Url -contains $currentSwfUrl } 'the new document observes its SWF response'
        Assert-Regression ($browser.PageSwfs.Count -eq 1) 'the previous document does not contribute an in-flight SWF response'
    } finally {
        if ($null -ne $player) { $player.ForceCloseWindow() }
        if ($null -ne $window) { $window.ForceCloseWindow() }
        $fixture.Dispose()
    }
}

function Open-RegressionPageSwf($Context, $Window, [string]$Url) {
    $button = $Window.FindName('toolbarPageSwfsButton')
    $menu = $Window.Resources['pageSwfsMenu']
    Wait-Regression $Context { $button.IsVisible } 'page SWFs toolbar button available'
    $buttonPeer = New-Object System.Windows.Automation.Peers.ButtonAutomationPeer($button)
    $buttonPeer.GetPattern([System.Windows.Automation.Peers.PatternInterface]::Invoke).Invoke()
    $resource = $Window.ViewModel.CurrentBrowser.Browser.PageSwfs | Where-Object { $_.Url -ceq $Url }
    Wait-Regression $Context { $menu.IsOpen -and $null -ne $menu.ItemContainerGenerator.ContainerFromItem($resource) } 'SWF menu item available'
    $item = $menu.ItemContainerGenerator.ContainerFromItem($resource)
    $itemPeer = New-Object System.Windows.Automation.Peers.MenuItemAutomationPeer($item)
    $itemPeer.GetPattern([System.Windows.Automation.Peers.PatternInterface]::Invoke).Invoke()
    Wait-Regression $Context {
        @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.SwfPlayerWindow] -and $_.FileName -ceq $Url }).Count -eq 1
    } 'a player opens from the SWF menu'
    $Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.SwfPlayerWindow] -and $_.FileName -ceq $Url }
}

function Test-SwfPlayerPageContext($Context) {
    $fixture = New-Object UserScriptHttpFixture
    $peer = New-Object UserScriptHttpFixture
    $window = $null
    $player = $null
    $framePlayer = $null
    try {
        $window = New-RegressionWindow $Context
        $model = $window.ViewModel.CurrentBrowser
        $source = $fixture.BaseUrl + '/swf-context-page?session=fixture'
        $moviePath = '/movies/context.swf'
        $assetPath = '/swf-assets/relative-probe.swf'
        $movieUrl = $fixture.BaseUrl + $moviePath + '?token=a%2Fb'
        $model.Address = $source
        Wait-Regression $Context {
            $model.Browser.PageSwfs.Url -contains $movieUrl -and $fixture.RequestsFor($assetPath) -gt 0
        } 'the source page plays the SWF and loads its relative movie'

        $movieRequests = $fixture.RequestsFor($moviePath)
        $assetRequests = $fixture.RequestsFor($assetPath)
        $sourceCookie = $fixture.CookieFor($moviePath)
        $player = Open-RegressionPageSwf $Context $window $movieUrl
        $playerView = Find-BrowserView $player
        $playerBrowser = $playerView.ViewModel.Browser
        Wait-Regression $Context {
            $player.Title -ceq 'context.swf' -and $fixture.RequestsFor($moviePath) -gt $movieRequests -and
                $fixture.RequestsFor($assetPath) -gt $assetRequests
        } 'the player requests both the selected SWF and its relative movie'
        Assert-Regression ($fixture.CookieFor($moviePath) -match 'swf_context=yes' -and $fixture.CookieFor($moviePath) -ceq $sourceCookie) 'the player SWF request sends the same HttpOnly cookie as the original SWF request'
        [void](Invoke-JavaScript $Context $playerBrowser "fetch('/swf-cookie-probe'); true;")
        Wait-Regression $Context { $fixture.RequestsFor('/swf-cookie-probe') -gt 0 } 'a same-origin request uses the player context'
        Assert-Regression ($fixture.CookieFor('/swf-cookie-probe') -match 'swf_context_strict=yes') 'the playback document shares the HttpOnly Strict cookie'
        Assert-Regression ($fixture.RefererFor($moviePath) -ceq $source) 'the SWF request preserves its source Referer'
        Assert-Regression ($fixture.TargetFor($moviePath) -ceq ($moviePath + '?token=a%2Fb')) 'the SWF request preserves the escaped query'
        Assert-Regression ($fixture.RequestsFor('/swf-context-page') -eq 1) 'opening the player does not fetch the source page again'
        Assert-Regression ((Invoke-JavaScript $Context $playerBrowser 'document.baseURI') -ceq ($fixture.BaseUrl + '/swf-assets/')) 'the player uses the source HTML base'
        Assert-Regression ((Invoke-JavaScript $Context $playerBrowser "document.getElementById('flash-player').getAttribute('flashvars')") -ceq 'level=7&value=</script>"') 'object FlashVars are passed as data'
        Assert-Regression ((Invoke-JavaScript $Context $playerBrowser "!window.originalPage && document.getElementById('flash-player').getAttribute('allowscriptaccess') === 'never'") -and
            $playerView.ViewModel.UserScriptState -eq 'Disabled') 'the playback page has its own script context and excludes userscripts'

        $model.Address = $fixture.BaseUrl + '/swf-empty'
        Wait-Regression $Context { $model.Title -eq 'Empty SWF fixture' } 'the source tab navigates away'
        $movieRequests = $fixture.RequestsFor($moviePath)
        [CefSharp.WebBrowserExtensions]::Reload($playerBrowser, $true)
        Wait-Regression $Context { $fixture.RequestsFor($moviePath) -gt $movieRequests } 'the player reloads after source navigation'

        $player.FileName = $fixture.BaseUrl + '/independent.swf'
        Wait-Regression $Context { $player.Title -ceq 'independent.swf' -and $fixture.RequestsFor('/independent.swf') -gt 0 } 'changing FileName loads independently'
        Assert-Regression ($fixture.RefererFor('/independent.swf') -ceq '' -and
            (Invoke-JavaScript $Context $playerBrowser 'location.protocol') -eq 'file:' -and
            (Invoke-JavaScript $Context $playerBrowser "!document.getElementById('flash-player').hasAttribute('flashvars')")) 'a different file does not inherit the previous context'

        $fixture.PeerUrl = $peer.BaseUrl.Replace('127.0.0.1', 'localhost')
        $framePage = $fixture.PeerUrl + '/swf-context-frame'
        $frameMovie = $fixture.PeerUrl + '/frame-context.swf'
        $model.Address = $framePage
        Wait-Regression $Context { $model.Title -ceq 'SWF context frame' -and !$model.Browser.IsLoading } 'the frame document loads as a first-party page'
        [void](Invoke-JavaScript $Context $model.Browser "fetch('/swf-first-party'); true;")
        Wait-Regression $Context { $peer.CookieFor('/swf-first-party') -match 'swf_frame=yes' } 'the frame origin has a first-party Strict cookie'
        $frameRequests = $peer.RequestsFor('/frame-context.swf')
        $model.Address = $fixture.BaseUrl + '/swf-context-outer'
        Wait-Regression $Context {
            $model.Browser.PageSwfs.Url -contains $frameMovie -and $peer.RequestsFor('/frame-context.swf') -gt $frameRequests
        } 'the nested cross-site frame plays its own SWF'

        $frameRequests = $peer.RequestsFor('/frame-context.swf')
        $framePlayer = Open-RegressionPageSwf $Context $window $frameMovie
        $frameBrowser = (Find-BrowserView $framePlayer).ViewModel.Browser
        Wait-Regression $Context { $framePlayer.Title -ceq 'frame-context.swf' -and $peer.RequestsFor('/frame-context.swf') -gt $frameRequests -and !$frameBrowser.IsLoading } 'the player loads the frame SWF'
        Assert-Regression ($peer.CookieFor('/frame-context.swf') -notmatch 'swf_frame=yes') 'the player preserves cross-site cookie rules for a nested frame'
        Assert-Regression ($peer.RefererFor('/frame-context.swf') -ceq '') 'the player honors the frame request with no Referer'

        $native = $frameBrowser.GetBrowser()
        foreach ($id in $native.GetFrameIdentifiers()) {
            $frame = $native.GetFrame($id)
            if ($null -ne $frame -and $frame.Url -ceq $framePage) {
                try {
                    $evaluation = $frame.EvaluateScriptAsync("fetch('/swf-frame-cookie-probe'); document.getElementById('flash-player').getAttribute('flashvars')", $framePage, 1, [TimeSpan]::FromSeconds(5))
                    Wait-Regression $Context { $evaluation.IsCompleted } 'the player frame returns its Flash parameters'
                    Assert-Regression ($evaluation.Result.Result -ceq 'frame=child') 'FlashVars come from the initiating iframe'
                    Wait-Regression $Context { $peer.RequestsFor('/swf-frame-cookie-probe') -gt 0 } 'the embedded playback document sends a same-origin request'
                    Assert-Regression ($peer.CookieFor('/swf-frame-cookie-probe') -notmatch 'swf_frame=yes') 'the embed retains the cross-site ancestor for credentialed requests'
                    Assert-Regression ($peer.RefererFor('/swf-frame-cookie-probe') -ceq '') 'the playback document keeps the source no-referrer behavior'
                    break
                } finally { $frame.Dispose() }
            }
            if ($null -ne $frame) { $frame.Dispose() }
        }
        Assert-Regression ($null -ne $evaluation) 'the player has a document at the initiating frame URL'

        $window.ForceCloseWindow()
        $window = $null
        $frameRequests = $peer.RequestsFor('/frame-context.swf')
        [CefSharp.WebBrowserExtensions]::Reload($frameBrowser, $true)
        Wait-Regression $Context { $peer.RequestsFor('/frame-context.swf') -gt $frameRequests } 'iframe playback reloads after the source window closes'
    } finally {
        if ($null -ne $framePlayer) { $framePlayer.ForceCloseWindow() }
        if ($null -ne $player) { $player.ForceCloseWindow() }
        if ($null -ne $window) { $window.ForceCloseWindow() }
        $peer.Dispose()
        $fixture.Dispose()
    }
}

function Test-LocalSwfPlayerPageContext($Context) {
    $fixture = New-Object UserScriptHttpFixture
    $window = $null
    $player = $null
    try {
        $path = Join-Path $Context.TemporaryPath 'swf-context.html'
        $movie = $fixture.BaseUrl + '/local-page.swf'
        [IO.File]::WriteAllText($path, "<base href='local-assets/'><embed src='$movie' flashvars='mode=local' width='160' height='120'>")
        $window = New-RegressionWindow $Context
        $model = $window.ViewModel.CurrentBrowser
        $model.Address = [Uri]::new($path).AbsoluteUri
        Wait-Regression $Context { $model.Browser.PageSwfs.Url -contains $movie } 'the local HTML page discovers its SWF'

        $player = Open-RegressionPageSwf $Context $window $movie
        $playerBrowser = (Find-BrowserView $player).ViewModel.Browser
        Wait-Regression $Context { $player.Title -ceq 'local-page.swf' } 'the local HTML source opens in the player'
        $base = [Uri]::new([Uri]::new($path), 'local-assets/').AbsoluteUri
        Assert-Regression ((Invoke-JavaScript $Context $playerBrowser 'document.baseURI') -ceq $base -and
            (Invoke-JavaScript $Context $playerBrowser "document.getElementById('flash-player').getAttribute('flashvars')") -ceq 'mode=local') 'local HTML passes its base and FlashVars to the player'
        $player.FileName = $fixture.BaseUrl + '/local-next.swf'
        Wait-Regression $Context { $player.Title -ceq 'local-next.swf' } 'the contextual local player can switch to a separate SWF'
        Assert-Regression ((Invoke-JavaScript $Context $playerBrowser "!document.getElementById('flash-player').hasAttribute('flashvars')")) 'a switched local player clears the FlashVars'
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
