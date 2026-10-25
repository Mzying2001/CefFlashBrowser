function Test-BrowserBeforeInitialization($Context) {
    $window = New-Object CefFlashBrowser.Views.BrowserWindow
    Assert-Regression ($null -eq $window.ViewModel.CurrentBrowser.Browser) 'browser is initially deferred'
    Assert-Regression ($null -eq $window.ViewModel.CurrentBrowser.Address) 'browser address is initially unset'
    $window.Close()
    $window.ForceCloseWindow()
    Assert-Regression ($window.GetType().GetField('_isClosed', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($window)) 'close before Show is safe and idempotent'
    $window = New-Object CefFlashBrowser.Views.BrowserWindow
    $window.ForceCloseWindow()
    Assert-Regression ($window.GetType().GetField('_isClosed', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($window)) 'force close before Show is safe'

    $model = New-Object CefFlashBrowser.ViewModels.BrowserWindowViewModel
    $model.CreateShortcut($null)
    $model.AddFavorite($null)
    $model.ToggleLoadingState($null)
    $model.FindText($null, $true)
    foreach ($commandName in $Context.BrowserCommandNames) {
        Invoke-BrowserCommand $model.$commandName
        Invoke-BrowserCommand $model.$commandName $model.CurrentBrowser
    }
    $model.ToggleDevTools($model.CurrentBrowser)
    $model.ToggleFullscreen($model.CurrentBrowser)
    $model.CurrentBrowser.ExitFullScreen()
    Assert-Regression (!$model.Fullscreen) 'commands tolerate missing browsers'
}

function Test-NavigationBeforeHosting($Context) {
    $browser = New-Object CefFlashBrowser.WinformCefSharp4WPF.ChromiumWebBrowser
    $window = New-Object System.Windows.Window
    $window.Width = 800
    $window.Height = 600
    $window.Left = -10000
    $window.Top = -10000
    $window.ShowActivated = $false
    $window.ShowInTaskbar = $false
    $window.Content = $browser

    $navigationState = @{ FirstAddressLoaded = $false }
    $loading = [EventHandler[CefSharp.FrameLoadStartEventArgs]]({
        param($sender, $eventArgs)
        if ($eventArgs.Frame.IsMain -and $eventArgs.Frame.Url -eq $Context.AuxiliaryAddress) {
            $navigationState.FirstAddressLoaded = $true
        }
    }.GetNewClosure())
    $browser.add_FrameLoadStart($loading)
    try {
        $browser.Address = $Context.AuxiliaryAddress
        $browser.Load($Context.AuxiliarySecondAddress)
        Wait-Regression $Context { $browser.IsBrowserInitialized } 'unhosted browser initialization'
        Assert-Regression ($browser.Address -eq $Context.AuxiliarySecondAddress) 'unhosted browser retains the latest requested address'
        $nativeAddress = $browser.GetBrowser().MainFrame.Url
        Assert-Regression ($nativeAddress -ne $Context.AuxiliaryAddress -and $nativeAddress -ne $Context.AuxiliarySecondAddress) 'navigation waits for a native parent even after CEF initialization'

        $window.Show()
        Wait-Regression $Context { $browser.Title -eq 'Second auxiliary page' } 'hosting starts the pending navigation'
        Assert-Regression (!$navigationState.FirstAddressLoaded) 'hosting skips superseded navigation requests'
        Assert-Regression ($browser.Address -eq $Context.AuxiliarySecondAddress) 'pending navigation updates the public address'

        $browser.Load($Context.AuxiliaryAddress)
        Wait-Regression $Context { $browser.Title -eq 'Auxiliary regression' } 'hosted browser navigates immediately'
        Assert-Regression $navigationState.FirstAddressLoaded 'hosted navigation reaches the requested page'
    } finally {
        $browser.remove_FrameLoadStart($loading)
        $window.Close()
        $browser.Dispose()
    }

    $browser.Load($Context.AuxiliarySecondAddress)
    Assert-Regression $browser.IsDisposed 'late navigation does not recreate a disposed browser'
}

function Test-AuxiliaryBeforeShow($Context) {
    foreach ($auxType in $Context.AuxiliaryTypes) {
        foreach ($force in @($false, $true)) {
            $auxWindow = [Activator]::CreateInstance($auxType)
            $auxView = $auxWindow.FindName('browserView')
            if ($auxWindow -isnot [CefFlashBrowser.Views.SwfPlayerWindow]) {
                $auxWindow.Address = $Context.AuxiliaryAddress
                $expectedAddress = if ($auxWindow -is [CefFlashBrowser.Views.ViewSourceWindow]) {
                    'view-source:' + $Context.AuxiliaryAddress
                } else {
                    $Context.AuxiliaryAddress
                }
                Assert-Regression ($auxView.ViewModel.Address -eq $expectedAddress) "$($auxType.Name) accepts an address before Show"
            }
            if ($force) { $auxWindow.ForceCloseWindow() } else { $auxWindow.Close() }
            Assert-AuxiliaryClosed $Context $auxWindow $auxView
        }
    }
}

function Test-BrowserCloseDuringInitialization($Context) {
    foreach ($forceClose in @($false, $true) * 5) {
        $window = New-RegressionWindow $Context -SkipWait -Width 800
        if ($forceClose) {
            $window.ForceCloseWindow()
        } else {
            $window.Close()
        }

        Wait-Regression $Context { !$window.IsVisible } 'close during initialization'
        Assert-Regression (!$window.IsVisible) "close during initialization is safe (force=$forceClose)"
    }
}

function Test-CloseAllBrowsers($Context) {
    $readyAuxiliaryWindows = @($Context.AuxiliaryTypes | ForEach-Object { New-AuxiliaryWindow $Context $_ -WaitForLoad })
    $pendingAuxiliaryWindows = @($Context.AuxiliaryTypes | ForEach-Object { New-AuxiliaryWindow $Context $_ })
    $readyWindow = New-RegressionWindow $Context
    New-RegressionTab $Context $readyWindow $Context.AuxiliaryAddress | Out-Null
    New-RegressionTab $Context $readyWindow $Context.AuxiliarySecondAddress | Out-Null
    $pendingWindow = New-RegressionWindow $Context -SkipWait
    $pendingWindow.ViewModel.AddBrowser('about:blank', $false) | Out-Null
    $pendingWindow.ViewModel.AddBrowser('about:blank', $true) | Out-Null
    $pendingWindow.UpdateLayout()
    $browsers = @($readyWindow.ViewModel.Browsers) + @($pendingWindow.ViewModel.Browsers) |
        ForEach-Object { $_.Browser }
    [SimpleMvvm.Messaging.Messenger]::Global.Send([CefFlashBrowser.Data.MessageTokens]::CLOSE_ALL_BROWSERS, $null)
    Assert-Regression (!$readyWindow.IsVisible -and !$pendingWindow.IsVisible) 'close-all message completes before CEF shutdown'
    Assert-Regression (@($browsers | Where-Object { $null -ne $_ -and !$_.IsDisposed }).Count -eq 0) 'close-all message releases every ready and initializing tab before returning'
    foreach ($auxWindow in @($readyAuxiliaryWindows) + @($pendingAuxiliaryWindows)) {
        Assert-AuxiliaryClosed $Context $auxWindow ($auxWindow.FindName('browserView'))
    }
}
