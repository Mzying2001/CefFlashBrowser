function Test-AuxiliaryRouting($Context, $AuxWindow, $AuxView) {
    $auxBrowser = $AuxView.ViewModel.Browser
    $originalAddress = $AuxView.ViewModel.Address
    $features = (New-Object RegressionPopupFeaturesProxy).GetTransparentProxy()
    foreach ($behavior in @(
        [CefFlashBrowser.Models.NewPageBehavior]::NewWindow,
        [CefFlashBrowser.Models.NewPageBehavior]::OriginalWindow
    )) {
        [CefFlashBrowser.Data.GlobalData]::Settings.NewPageBehavior = $behavior
        foreach ($isPopup in @($false, $true)) {
            $beforeWindows = @($Context.Application.Windows)
            $disposition = if ($isPopup) {
                [CefSharp.WindowOpenDisposition]::NewPopup
            } else {
                [CefSharp.WindowOpenDisposition]::NewForegroundTab
            }
            Invoke-ViewCallback $AuxView 'OnBrowserBeforePopup' @(
                $auxBrowser, $auxBrowser.GetBrowser(), $null, $Context.AuxiliaryAddress, '',
                $disposition, $true, $features, $null, $null, $false)
            Wait-Regression $Context { @($Context.Application.Windows | Where-Object { !$beforeWindows.Contains($_) }).Count -gt 0 } 'routed new window'
            $childWindow = $Context.Application.Windows | Where-Object { !$beforeWindows.Contains($_) } | Select-Object -First 1
            if ($isPopup) {
                Assert-Regression ($childWindow -is [CefFlashBrowser.Views.PopupWebPage]) "$($AuxWindow.GetType().Name) routes popups to PopupWebPage ($behavior)"
                Assert-Regression ($childWindow.Left -eq 20 -and $childWindow.Top -eq 30 -and $childWindow.Width -eq 500 -and $childWindow.Height -eq 360) 'popup request position and size are applied'
            } else {
                Assert-Regression ($childWindow -is [CefFlashBrowser.Views.BrowserWindow]) "$($AuxWindow.GetType().Name) always opens ordinary pages in BrowserWindow ($behavior)"
            }
            Assert-Regression ($AuxView.ViewModel.Address -eq $originalAddress) 'opening a new page preserves the originating page'
            $childWindow.ForceCloseWindow()
        }
    }
}

function Test-AuxiliaryWindows($Context) {
    foreach ($auxType in $Context.AuxiliaryTypes) {
        $auxWindow = New-AuxiliaryWindow $Context $auxType -WaitForLoad
        $auxView = $auxWindow.FindName('browserView')
        $auxBrowser = $auxView.ViewModel.Browser
        Assert-Regression ($auxView -is [CefFlashBrowser.Views.BrowserView] -and $auxBrowser.IsBrowserInitialized) "$($auxType.Name) hosts BrowserView with its own initialized model"
        $expectedTitle = $auxView.ViewModel.Title
        if ($null -eq $expectedTitle) { $expectedTitle = 'view-source' }
        Assert-Regression ($auxWindow.Title -eq $expectedTitle) "$($auxType.Name) binds its title to BrowserViewModel"
        $closeBinding = $auxWindow.InputBindings | Where-Object {
            $_.Key -eq [System.Windows.Input.Key]::W -and
            $_.Modifiers -eq [System.Windows.Input.ModifierKeys]::Control
        }
        Assert-Regression ([object]::ReferenceEquals($closeBinding.Command, $auxBrowser.CloseBrowserCommand)) "$($auxType.Name) binds Ctrl+W to its hosted browser"
        if ($auxWindow -is [CefFlashBrowser.Views.PopupWebPage]) {
            Assert-Regression ($auxView.ViewModel.Address -eq $Context.AuxiliaryAddress) 'popup Address initializes BrowserViewModel'
            $auxWindow.Address = $Context.AuxiliarySecondAddress
            Wait-Regression $Context { $auxWindow.Title -eq 'Second auxiliary page' } 'popup Address updates navigate the hosted browser'
        } elseif ($auxWindow -is [CefFlashBrowser.Views.SwfPlayerWindow]) {
            Wait-Regression $Context { $auxWindow.Title -eq 'first.swf' } 'initial SWF filename reaches loadSwf'
            Assert-Regression ((Invoke-JavaScript $Context $auxBrowser "document.getElementById('flash-player').getAttribute('src')") -eq 'first.swf') 'initial SWF embed source is set'
            $auxWindow.FileName = 'second.swf'
            Wait-Regression $Context { $auxWindow.Title -eq 'second.swf' } 'SWF FileName updates reach loadSwf'
            Assert-Regression ((Invoke-JavaScript $Context $auxBrowser "document.getElementById('flash-player').getAttribute('src')") -eq 'second.swf') 'changed SWF embed source is set'
            $auxWindow.Width = 910
            $auxWindow.Height = 610
        } else {
            # CEF reports source-view addresses without their prefix after navigation.
            Assert-Regression ($auxWindow.Address -eq $Context.AuxiliaryAddress) 'source window retains its requested URL'
            Assert-Regression ((Get-RegressionText $Context $auxBrowser).Contains('<title>Auxiliary regression</title>')) 'source window renders source markup'
            Assert-Regression ((Get-RegressionSource $Context $auxBrowser).Contains('auxiliary-source-token')) 'source window displays the requested source'
            $auxWindow.Address = $Context.AuxiliarySecondAddress
            Wait-Regression $Context { (Get-RegressionSource $Context $auxBrowser).Contains('second-source-token') } 'source Address updates navigate to the requested source'
            Assert-Regression ((Get-RegressionText $Context $auxBrowser).Contains('<title>Second auxiliary page</title>')) 'updated source address continues to render markup'
        }
        if ($auxWindow -isnot [CefFlashBrowser.Views.PopupWebPage]) {
            Assert-Regression ($auxBrowser.DragHandler -is [CefFlashBrowser.Browser.Handlers.DisableDragHandler]) "$($auxType.Name) keeps drag disabled"
        }

        Test-AuxiliaryRouting $Context $auxWindow $auxView
        [CefFlashBrowser.Data.GlobalData]::Settings.EnableIntegratedDevTools = $false
        Open-RegressionDevTools $Context $auxView
        Wait-Regression $Context { [CefFlashBrowser.Utils.HwndHelper]::FindNotIntegratedDevTools($auxBrowser) -ne [IntPtr]::Zero } "$($auxType.Name) detached DevTools opens"
        $toolsHandle = [CefFlashBrowser.Utils.HwndHelper]::FindNotIntegratedDevTools($auxBrowser)
        $ownerHandle = (New-Object System.Windows.Interop.WindowInteropHelper($auxWindow)).Handle
        Assert-Regression ([CefFlashBrowser.Utils.HwndHelper]::GetOwnerWindow($toolsHandle) -eq $ownerHandle) "$($auxType.Name) owns its detached DevTools"
        $auxView.ViewModel.ToggleDevTools()
        Wait-Regression $Context { [CefFlashBrowser.Utils.HwndHelper]::FindNotIntegratedDevTools($auxBrowser) -eq [IntPtr]::Zero } 'detached DevTools closes'
        Assert-Regression $auxWindow.IsVisible 'closing detached DevTools keeps its auxiliary window open'

        [CefFlashBrowser.Data.GlobalData]::Settings.EnableIntegratedDevTools = $true
        [CefFlashBrowser.Data.GlobalData]::Settings.IntegratedDevToolsWidth = 340
        Open-RegressionDevTools $Context $auxView
        Wait-Regression $Context { $auxView.ViewModel.IntegratedDevToolsHandle -ne [IntPtr]::Zero } "$($auxType.Name) integrated DevTools opens"
        Assert-Regression ($auxView.FindName('devtoolsColumn').Width.Value -eq 340) 'auxiliary DevTools restores its saved width'
        $auxView.FindName('devtoolsColumn').Width = New-Object System.Windows.GridLength(350)
        $auxView.ViewModel.ToggleDevTools()
        Wait-Regression $Context { $auxView.ViewModel.IntegratedDevToolsHandle -eq [IntPtr]::Zero } 'integrated DevTools closes'
        Assert-Regression ($auxWindow.IsVisible -and [CefFlashBrowser.Data.GlobalData]::Settings.IntegratedDevToolsWidth -eq 350) 'auxiliary DevTools closes independently and saves its width'

        # Use an HTML document to exercise the source window's forced-close policy too.
        $auxView.ViewModel.Address = $Context.AuxiliaryAddress
        Wait-Regression $Context { $auxWindow.Title -eq 'Auxiliary regression' -and $auxBrowser.CanExecuteJavascriptInMainFrame } 'beforeunload fixture ready'
        $dialogHandler = Set-RegressionBeforeUnload $Context $auxBrowser
        $auxWindow.Close()
        if ($auxWindow -is [CefFlashBrowser.Views.ViewSourceWindow]) {
            Wait-Regression $Context { !$auxWindow.IsVisible } 'source window force closes normally'
            Assert-Regression ($dialogHandler.Count -eq 0) 'source window skips beforeunload confirmation'
        } else {
            Wait-Regression $Context { $dialogHandler.Count -gt 0 } "$($auxType.Name) requests beforeunload confirmation"
            Assert-Regression $auxWindow.IsVisible "$($auxType.Name) stays open when beforeunload is cancelled"
            $dialogHandler.Accept = $true
            Invoke-BrowserCommand $closeBinding.Command ([bool]$closeBinding.CommandParameter)
            Wait-Regression $Context { !$auxWindow.IsVisible } "$($auxType.Name) Ctrl+W closes after confirmation"
        }
        Assert-AuxiliaryClosed $Context $auxWindow $auxView
        if ($auxWindow -is [CefFlashBrowser.Views.SwfPlayerWindow]) {
            $size = [CefFlashBrowser.Data.GlobalData]::Settings.SwfWindowSizeInfo
            Assert-Regression ($size.Width -eq 910 -and $size.Height -eq 610) 'SWF window dimensions are preserved'
        }
        Invoke-ViewCallback $auxView 'OnBrowserDoClose' @($auxBrowser, $Context.DisposedBrowser)

        $auxWindow = New-AuxiliaryWindow $Context $auxType -WaitForLoad
        $auxView = $auxWindow.FindName('browserView')
        $auxView.ViewModel.Address = $Context.AuxiliarySecondAddress
        Wait-Regression $Context { $auxWindow.Title -eq 'Second auxiliary page' -and $auxView.ViewModel.Browser.CanExecuteJavascriptInMainFrame } 'force-close beforeunload fixture ready'
        $forceDialogHandler = Set-RegressionBeforeUnload $Context $auxView.ViewModel.Browser
        Open-RegressionDevTools $Context $auxView
        Wait-Regression $Context { $auxView.ViewModel.IntegratedDevToolsHandle -ne [IntPtr]::Zero } 'DevTools opens before auxiliary force close'
        $auxView.FindName('devtoolsColumn').Width = New-Object System.Windows.GridLength(365)
        $auxWindow.ForceCloseWindow()
        Assert-AuxiliaryClosed $Context $auxWindow $auxView
        Assert-Regression ([CefFlashBrowser.Data.GlobalData]::Settings.IntegratedDevToolsWidth -eq 365) 'auxiliary force close saves DevTools width'
        Assert-Regression ($forceDialogHandler.Count -eq 0) 'force close skips beforeunload confirmation'

        foreach ($force in @($false, $true) * 2) {
            $auxWindow = New-AuxiliaryWindow $Context $auxType
            $auxView = $auxWindow.FindName('browserView')
            if ($force) { $auxWindow.ForceCloseWindow() } else { $auxWindow.Close() }
            Wait-Regression $Context { !$auxWindow.IsVisible } "$($auxType.Name) closes during initialization (force=$force)"
            Assert-AuxiliaryClosed $Context $auxWindow $auxView
        }
    }
}
