function Assert-ViewSourcePage($Context, $Model, [string]$Address, [string]$Title = 'Auxiliary regression') {
    Wait-Regression $Context {
        $Model.IsBrowserReady() -and !$Model.Browser.IsLoading -and
        $Model.Address -eq $Address -and
        (Get-RegressionText $Context $Model.Browser) -like "*<title>$Title</title>*"
    } 'source page retains its view-source address and renders markup'
}

function Test-ViewSourceRouting($Context) {
    $settings = [CefFlashBrowser.Data.GlobalData]::Settings
    $savedBehavior = $settings.NewPageBehavior
    $savedSize = $settings.BrowserWindowSizeInfo
    try {
        foreach ($behavior in [Enum]::GetValues([CefFlashBrowser.Models.NewPageBehavior])) {
            $settings.NewPageBehavior = $behavior
            foreach ($entryPoint in @('Menu', 'Shortcut', 'ContextMenu')) {
                $beforeWindows = @($Context.Application.Windows)
                $window = New-RegressionWindow $Context
                try {
                    $model = $window.ViewModel
                    $source = $model.CurrentBrowser
                    $source.Address = $Context.AuxiliaryAddress
                    Wait-Regression $Context { $source.Title -eq 'Auxiliary regression' -and $source.Browser.CanExecuteJavascriptInMainFrame } 'original page loads'
                    $last = New-RegressionTab $Context $window
                    $model.CurrentBrowser = $source
                    $view = Find-BrowserView $window $source
                    if ($entryPoint -eq 'Menu') {
                        $menu = Open-BrowserActionsMenu $Context $window
                        $item = $menu.Items | Where-Object { $_.Command -eq $model.ViewSourceCommand }
                        Assert-Regression ($item.CommandParameter -eq $source) 'source menu targets the selected browser model'
                        $menu.IsOpen = $false
                    }

                    # A native CEF click grants the user activation required by requestFullscreen.
                    [void](Invoke-JavaScript $Context $source.Browser "window.sourcePageToken=42; document.onclick=function(){document.documentElement.requestFullscreen();};")
                    $hostBrowser = $source.Browser.GetBrowser().GetHost()
                    $mouse = New-Object CefSharp.MouseEvent(40, 40, [CefSharp.CefEventFlags]::None)
                    $hostBrowser.SendMouseClickEvent($mouse, [CefSharp.MouseButtonType]::Left, $false, 1)
                    $hostBrowser.SendMouseClickEvent($mouse, [CefSharp.MouseButtonType]::Left, $true, 1)
                    Wait-Regression $Context { $model.Fullscreen -and (Invoke-JavaScript $Context $source.Browser '!!document.fullscreenElement') } 'original page enters native HTML fullscreen'

                    switch ($entryPoint) {
                        'Menu' { Invoke-BrowserCommand $item.Command $item.CommandParameter }
                        'Shortcut' { Invoke-TabShortcut $window U Control }
                        'ContextMenu' {
                            Assert-Regression (Invoke-ContextMenuCommand $source.Browser ([CefSharp.CefMenuCommand]::ViewSource)) 'source context command is handled'
                        }
                    }
                    $address = 'view-source:' + $Context.AuxiliaryAddress
                    if ($behavior -eq [CefFlashBrowser.Models.NewPageBehavior]::NewWindow) {
                        $children = @($Context.Application.Windows | Where-Object { $_ -ne $window -and !$beforeWindows.Contains($_) })
                        Assert-Regression ($children.Count -eq 1 -and $children[0] -is [CefFlashBrowser.Views.BrowserWindow]) "$entryPoint opens a regular source window ($behavior)"
                        Assert-ViewSourcePage $Context $children[0].ViewModel.CurrentBrowser $address
                        Assert-Regression ($model.Browsers.Count -eq 2 -and $model.CurrentBrowser -eq $source -and
                            !$model.Fullscreen -and $window.WindowStyle -eq [System.Windows.WindowStyle]::SingleBorderWindow) 'opening a source window preserves selection and exits window fullscreen'
                    } else {
                        Assert-Regression ($model.Browsers.Count -eq 3 -and $model.Browsers[0] -eq $source -and
                            $model.Browsers[1] -eq $model.CurrentBrowser -and $model.Browsers[2] -eq $last -and
                            $Context.Application.Windows.Count -eq $beforeWindows.Count + 1) "$entryPoint inserts and selects a source tab after its origin ($behavior)"
                        Assert-ViewSourcePage $Context $model.CurrentBrowser $address
                        Assert-TabFullscreen $window "$entryPoint source tab ($behavior)"
                        Invoke-ViewCallback $view 'OnFullScreenChanged' @($false)
                        Assert-TabFullscreen $window 'late fullscreen exit from the originating page'
                    }
                    Wait-Regression $Context { !(Invoke-JavaScript $Context $source.Browser '!!document.fullscreenElement') } 'source command exits original HTML fullscreen'
                    Assert-Regression ($source.Address -eq $Context.AuxiliaryAddress -and
                        (Invoke-JavaScript $Context $source.Browser 'window.sourcePageToken') -eq 42) 'viewing source preserves the original page and its JavaScript state'
                    if ($behavior -ne [CefFlashBrowser.Models.NewPageBehavior]::NewWindow) {
                        $model.CloseBrowser($model.CurrentBrowser)
                        Wait-Regression $Context { $model.Browsers.Count -eq 2 } 'source tab closes'
                        Assert-Regression ($model.CurrentBrowser -eq $source) "$entryPoint source tab returns to its opener instead of its right neighbor ($behavior)"
                    }
                } finally {
                    foreach ($created in @($Context.Application.Windows | Where-Object { !$beforeWindows.Contains($_) })) {
                        $created.ForceCloseWindow()
                    }
                }
            }
        }
    } finally {
        $settings.NewPageBehavior = $savedBehavior
        $settings.BrowserWindowSizeInfo = $savedSize
    }
}

function Test-ViewSourceNavigation($Context) {
    $settings = [CefFlashBrowser.Data.GlobalData]::Settings
    $savedBehavior = $settings.NewPageBehavior
    $savedTools = $settings.EnableIntegratedDevTools
    $window = New-RegressionWindow $Context
    try {
        $settings.NewPageBehavior = [CefFlashBrowser.Models.NewPageBehavior]::NewTab
        $model = $window.ViewModel
        $model.CurrentBrowser.Address = $Context.AuxiliaryAddress
        Wait-Regression $Context { $model.CurrentBrowser.Title -eq 'Auxiliary regression' } 'navigation origin loads'
        $model.ViewSource($model.CurrentBrowser)
        $source = $model.CurrentBrowser
        $address = 'view-source:' + $Context.AuxiliaryAddress
        Assert-ViewSourcePage $Context $source $address
        Assert-Regression ($window.FindName('addressTextBox').Text -eq $address) 'address bar displays the source prefix'

        $loadState = @{ Count = 0 }
        $loaded = [EventHandler[CefSharp.FrameLoadEndEventArgs]]({
            param($sender, $eventArgs)
            if ($eventArgs.Frame.IsMain) { $loadState.Count++ }
        }.GetNewClosure())
        $source.Browser.add_FrameLoadEnd($loaded)
        try {
            [CefSharp.WebBrowserExtensions]::Reload($source.Browser)
            Wait-Regression $Context { $loadState.Count -gt 0 } 'source page reload completes'
        } finally {
            $source.Browser.remove_FrameLoadEnd($loaded)
        }
        Assert-ViewSourcePage $Context $source $address
        $model.DuplicateBrowser($source)
        Assert-ViewSourcePage $Context $model.CurrentBrowser $address
        $model.ViewSource($model.CurrentBrowser)
        $repeated = $model.CurrentBrowser
        Assert-ViewSourcePage $Context $repeated $address
        Assert-Regression ($model.Browsers.Count -eq 4) 'duplicating and viewing source again create ordinary source tabs without a nested prefix'

        $settings.EnableIntegratedDevTools = $true
        $view = Find-BrowserView $window $repeated
        Open-RegressionDevTools $Context $view
        Wait-Regression $Context { $repeated.IntegratedDevToolsHandle -ne [IntPtr]::Zero } 'source tab supports ordinary integrated DevTools'

        $repeated.Address = $Context.AuxiliarySecondAddress
        Wait-Regression $Context { $repeated.Title -eq 'Second auxiliary page' -and $repeated.Browser.CanExecuteJavascriptInMainFrame } 'source tab navigates to a regular page'
        Assert-Regression ($repeated.Address -eq $Context.AuxiliarySecondAddress) 'regular navigation clears the source prefix'
        [CefSharp.WebBrowserExtensions]::Back($repeated.Browser)
        Assert-ViewSourcePage $Context $repeated $address
        [CefSharp.WebBrowserExtensions]::Forward($repeated.Browser)
        Wait-Regression $Context { $repeated.Title -eq 'Second auxiliary page' -and $repeated.Browser.CanExecuteJavascriptInMainFrame } 'forward navigation returns to the regular page'

        $dialog = Set-RegressionBeforeUnload $Context $repeated.Browser
        $model.CloseBrowser($repeated)
        Wait-Regression $Context {
            $dialog.Count -gt 0 -and $null -eq $view.GetType().GetField('_closeCompletion', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($view)
        } 'source tab uses ordinary beforeunload after navigation'
        Assert-Regression ($model.Browsers.Contains($repeated) -and !$repeated.Browser.IsDisposed) 'cancelled close keeps the source tab alive'
        $dialog.Accept = $true
        $model.CloseBrowser($repeated)
        Wait-Regression $Context { $repeated.Browser.IsDisposed } 'confirmed source tab close releases CEF'
        Assert-Regression ($repeated.IntegratedDevToolsHandle -eq [IntPtr]::Zero) 'closing the source tab releases DevTools'
        $model.CurrentBrowser = $source
        Invoke-TabShortcut $window W Control
        Wait-Regression $Context { $source.Browser.IsDisposed } 'Ctrl+W closes a source tab through the ordinary browser lifecycle'
        $count = $model.Browsers.Count
        $model.ViewSource($repeated)
        $model.ViewSource($null)
        Assert-Regression ($model.Browsers.Count -eq $count) 'removed and missing source commands cannot add tabs'
        $window.ForceCloseWindow()
        $windowCount = $Context.Application.Windows.Count
        $model.ViewSource($source)
        Assert-Regression ($model.Browsers.Count -le $count -and $Context.Application.Windows.Count -eq $windowCount -and
            !(Invoke-ContextMenuCommand $source.Browser ([CefSharp.CefMenuCommand]::ViewSource))) 'removed, missing and closed source commands cannot create browsers'
    } finally {
        $window.ForceCloseWindow()
        $settings.NewPageBehavior = $savedBehavior
        $settings.EnableIntegratedDevTools = $savedTools
    }
}

function Test-AuxiliaryViewSource($Context) {
    $settings = [CefFlashBrowser.Data.GlobalData]::Settings
    $savedBehavior = $settings.NewPageBehavior
    try {
        foreach ($auxType in $Context.AuxiliaryTypes) {
            foreach ($behavior in [Enum]::GetValues([CefFlashBrowser.Models.NewPageBehavior])) {
                $settings.NewPageBehavior = $behavior
                $beforeWindows = @($Context.Application.Windows)
                $window = New-AuxiliaryWindow $Context $auxType -WaitForLoad
                try {
                    $source = $window.FindName('browserView').ViewModel
                    $originalAddress = $source.Address
                    Assert-Regression (Invoke-ContextMenuCommand $source.Browser ([CefSharp.CefMenuCommand]::ViewSource)) 'auxiliary source command is handled'
                    $children = @($Context.Application.Windows | Where-Object { $_ -ne $window -and !$beforeWindows.Contains($_) })
                    Assert-Regression ($children.Count -eq 1 -and $children[0] -is [CefFlashBrowser.Views.BrowserWindow] -and
                        $children[0].ViewModel.CurrentBrowser.Address -eq ('view-source:' + $originalAddress) -and
                        $source.Address -eq $originalAddress) "$($auxType.Name) opens source in a regular browser window ($behavior)"
                } finally {
                    foreach ($created in @($Context.Application.Windows | Where-Object { !$beforeWindows.Contains($_) })) {
                        $created.ForceCloseWindow()
                    }
                }
            }
        }
    } finally {
        $settings.NewPageBehavior = $savedBehavior
    }
}

function Test-ViewSource($Context) {
    Test-ViewSourceRouting $Context
    Test-ViewSourceNavigation $Context
    Test-AuxiliaryViewSource $Context
}
