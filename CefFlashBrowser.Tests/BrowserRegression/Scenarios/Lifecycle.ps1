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

function Test-AuxiliaryBeforeShow($Context) {
    foreach ($auxType in $Context.AuxiliaryTypes) {
        foreach ($force in @($false, $true)) {
            $auxWindow = [Activator]::CreateInstance($auxType)
            $auxView = $auxWindow.FindName('browserView')
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
    $pendingWindow = New-RegressionWindow $Context -SkipWait
    [SimpleMvvm.Messaging.Messenger]::Global.Send([CefFlashBrowser.Data.MessageTokens]::CLOSE_ALL_BROWSERS, $null)
    Assert-Regression (!$readyWindow.IsVisible -and !$pendingWindow.IsVisible) 'close-all message completes before CEF shutdown'
    foreach ($auxWindow in @($readyAuxiliaryWindows) + @($pendingAuxiliaryWindows)) {
        Assert-AuxiliaryClosed $Context $auxWindow ($auxWindow.FindName('browserView'))
    }
}
