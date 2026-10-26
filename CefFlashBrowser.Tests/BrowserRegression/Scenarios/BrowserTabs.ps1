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
    $model.MoveBrowser($first, 1)
    Assert-Regression ($model.Browsers[1] -eq $first -and $model.CurrentBrowser -eq $first) 'reordering unrealized tabs preserves selection'
    $model.MoveBrowser($first, 0)
    foreach ($index in @(-1, 2)) { $model.MoveBrowser($first, $index) }
    $model.MoveBrowser((New-Object CefFlashBrowser.ViewModels.BrowserViewModel), 0)
    $model.MoveBrowser($null, 0)
    Assert-Regression ($model.Browsers[0] -eq $first -and $model.Browsers[1] -eq $second) 'invalid reorder requests leave tabs unchanged'
    foreach ($invalid in @($null, (New-Object CefFlashBrowser.ViewModels.BrowserViewModel))) {
        $rejected = $false
        try { $model.CurrentBrowser = $invalid } catch { $rejected = $true }
        Assert-Regression $rejected 'selection rejects a browser outside the window'
    }
    $model.CloseBrowser($second)
    Assert-Regression ($model.Browsers.Count -eq 1) 'unrealized background tab closes without creating CEF'
    foreach ($address in @($null, '')) {
        $first.Address = $address
        Invoke-BrowserCommand $model.DuplicateBrowserCommand $first
        $duplicate = $model.CurrentBrowser
        Assert-Regression ($model.Browsers[1] -eq $duplicate -and $duplicate.Address -eq 'about:blank' -and
            $null -eq $duplicate.Browser) 'duplicating an unrealized empty-address tab creates a blank tab without CEF'
        $model.CloseBrowser($duplicate)
    }
    foreach ($invalid in @($null, (New-Object CefFlashBrowser.ViewModels.BrowserViewModel), $second)) {
        $model.DuplicateBrowser($invalid)
        $model.CloseOtherBrowsers($invalid)
    }
    Assert-Regression ($model.Browsers.Count -eq 1 -and $model.CurrentBrowser -eq $first) 'new tab commands ignore missing and removed source tabs'
    $model.AddBrowser('about:blank', $true) | Out-Null
    $window.Close()
    Assert-Regression ($model.Browsers.Count -eq 0) 'closing before Show removes all unrealized tabs'
    $window.ForceCloseWindow()
}

function Test-BrowserTabs($Context) {
    Test-BrowserTabStrip $Context
    Test-BrowserTabStripSettings $Context
    Test-BrowserTabContextMenu $Context
    Test-BrowserTabCloseOthers $Context
    Test-BrowserTabCloseOthersInterrupted $Context
    Test-BrowserTabNewPageRouting $Context
    Test-BrowserTabSelection $Context
    Test-BrowserTabReordering $Context
    Test-BrowserTabDragPreview $Context
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

function Open-TabContextMenu($Context, $Window, $Browser) {
    $Window.UpdateLayout()
    $tabs = $Window.FindName('tabList')
    $container = $tabs.ItemContainerGenerator.ContainerFromItem($Browser)
    Assert-Regression ($null -ne $container.ContextMenu) 'tab header has a context menu'
    $menu = $container.ContextMenu
    $menu.PlacementTarget = $container
    $menu.IsOpen = $true
    Wait-Regression $Context {
        $menu.Items[0].CommandParameter -eq $Browser -and $null -ne $menu.Items[1].Command
    } 'tab menu binds to its placement target'
    return $menu
}

function Invoke-TabMenuItem($Menu, [int]$Index) {
    $item = $Menu.Items[$Index]
    $command = $item.Command
    $parameter = $item.CommandParameter
    $Menu.IsOpen = $false
    Invoke-BrowserCommand $command $parameter
}

function Test-BrowserTabContextMenu($Context) {
    $settings = [CefFlashBrowser.Data.GlobalData]::Settings
    $savedBehavior = $settings.NewPageBehavior
    $savedShortcuts = $settings.DisableBrowserShortcuts
    $savedTabStrip = $settings.AlwaysShowTabStrip
    $savedLanguage = $Context.Application.Resources.MergedDictionaries[0]
    $beforeWindows = @($Context.Application.Windows)
    try {
        $settings.AlwaysShowTabStrip = $true
        $window = New-RegressionWindow $Context
        $model = $window.ViewModel
        $source = $model.CurrentBrowser
        $menu = Open-TabContextMenu $Context $window $source
        Assert-Regression ($menu.Items.Count -eq 5 -and $menu.Items[2] -is [System.Windows.Controls.Separator] -and
            $menu.Items[0].InputGestureText -eq 'Ctrl+W' -and $menu.Items[3].InputGestureText -eq 'Ctrl+Shift+K') 'tab menu has the requested order, separator and shortcut hints'
        Assert-Regression (!$menu.Items[1].IsEnabled) 'close other tabs is disabled for a single tab'
        $keys = @('browser_closeTab', 'browser_closeOtherTabs', 'browser_duplicateTab', 'browser_openTabInNewWindow')
        foreach ($language in [CefFlashBrowser.Services.LanguageManager]::GetSupportedLanguage()) {
            $dictionary = New-Object System.Windows.ResourceDictionary
            $dictionary.set_Source([Uri]("pack://application:,,,/CefFlashBrowser;component/Assets/Language/$language.xaml"))
            $Context.Application.Resources.MergedDictionaries[0] = $dictionary
            foreach ($index in 0..3) {
                $itemIndex = @(0, 1, 3, 4)[$index]
                $label = [CefFlashBrowser.Services.LanguageManager]::GetString($language, $keys[$index])
                Assert-Regression (![string]::IsNullOrWhiteSpace($label)) "tab menu label exists: $($keys[$index]) ($language)"
                Wait-Regression $Context { $menu.Items[$itemIndex].Header -eq $label } "tab menu language updates ($language)"
            }
        }
        $menu.IsOpen = $false
        $Context.Application.Resources.MergedDictionaries[0] = $savedLanguage
        $last = New-RegressionTab $Context $window $Context.AuxiliarySecondAddress
        $source.Address = $Context.AuxiliaryAddress
        Wait-Regression $Context {
            $source.Browser.Address -eq $Context.AuxiliaryAddress -and !$source.Browser.IsLoading -and
            $source.Browser.CanExecuteJavascriptInMainFrame
        } 'source page loads'
        [void](Invoke-JavaScript $Context $source.Browser 'window.tabDuplicateToken = 42;')
        foreach ($behavior in [Enum]::GetValues([CefFlashBrowser.Models.NewPageBehavior])) {
            $settings.NewPageBehavior = $behavior
            $model.CurrentBrowser = $last
            $menu = Open-TabContextMenu $Context $window $source
            Assert-Regression ($menu.Items[1].IsEnabled -and $menu.Items[4].CommandParameter -eq $source.Address) 'tab menu enables close others and targets the source address'
            Invoke-TabMenuItem $menu 3
            $duplicate = $model.CurrentBrowser
            Wait-Regression $Context {
                $duplicate.IsBrowserReady() -and !$duplicate.Browser.IsLoading -and $duplicate.Browser.CanExecuteJavascriptInMainFrame
            } 'duplicated tab initialization'
            Assert-Regression ($model.Browsers.Count -eq 3 -and $model.Browsers[1] -eq $duplicate -and
                $duplicate.Address -eq $source.Address -and $model.Browsers[2] -eq $last) "menu duplication inserts and selects a copy beside its source ($behavior)"
            Assert-Regression (Invoke-JavaScript $Context $duplicate.Browser "typeof window.tabDuplicateToken === 'undefined'") 'duplicated page starts with fresh runtime state'
            $menu = Open-TabContextMenu $Context $window $last
            Assert-Regression ($menu.Items[4].CommandParameter -eq $last.Address -and $menu.Items[3].CommandParameter -eq $last) 'reopening a shared menu updates its target'
            $menu.IsOpen = $false
            $menu = Open-TabContextMenu $Context $window $duplicate
            Invoke-TabMenuItem $menu 0
            Wait-Regression $Context { $model.Browsers.Count -eq 2 } 'tab menu closes its target'
            Assert-Regression ($duplicate.Browser.IsDisposed -and $model.Browsers.Contains($last)) 'menu close releases only the target browser'
        }
        $model.CurrentBrowser = $source
        Invoke-TabShortcut $window K ([System.Windows.Input.ModifierKeys]::Control -bor [System.Windows.Input.ModifierKeys]::Shift)
        $duplicate = $model.CurrentBrowser
        Assert-Regression ($model.Browsers[1] -eq $duplicate -and $duplicate.Address -eq $source.Address) 'Ctrl+Shift+K duplicates the current tab'
        $model.CloseBrowser($duplicate)
        Wait-Regression $Context { $model.Browsers.Count -eq 2 } 'shortcut-created tab closes'

        $menu = Open-TabContextMenu $Context $window $source
        $existing = @($Context.Application.Windows)
        Invoke-TabMenuItem $menu 4
        $child = @($Context.Application.Windows | Where-Object { !$existing.Contains($_) })
        Assert-Regression ($child.Count -eq 1 -and $child[0] -is [CefFlashBrowser.Views.BrowserWindow] -and
            $child[0].ViewModel.CurrentBrowser.Address -eq $source.Address -and $model.Browsers.Count -eq 2 -and
            !$source.Browser.IsDisposed) 'open tab in new window preserves the source tab'
        $child[0].ForceCloseWindow()
        foreach ($address in @($null, '')) {
            $existing = @($Context.Application.Windows)
            Invoke-BrowserCommand $model.NewBrowserWindowCommand $address
            $child = @($Context.Application.Windows | Where-Object { !$existing.Contains($_) })
            Assert-Regression ($child.Count -eq 1 -and $child[0].ViewModel.CurrentBrowser.Address -eq 'about:blank') 'new window command defaults empty addresses to a blank page'
            $child[0].ForceCloseWindow()
        }

        $settings.DisableBrowserShortcuts = $true
        $disabledWindow = New-RegressionWindow $Context
        Assert-Regression ($disabledWindow.InputBindings.Count -eq 0) 'disabling browser shortcuts removes tab shortcut bindings'
        $menu = Open-TabContextMenu $Context $disabledWindow $disabledWindow.ViewModel.CurrentBrowser
        Invoke-TabMenuItem $menu 3
        Assert-Regression ($disabledWindow.ViewModel.Browsers.Count -eq 2) 'tab menu duplication works when shortcuts are disabled'
        $menu = Open-TabContextMenu $Context $disabledWindow $disabledWindow.ViewModel.CurrentBrowser
        Invoke-TabMenuItem $menu 1
        Wait-Regression $Context { $disabledWindow.ViewModel.Browsers.Count -eq 1 } 'tab menu closes other tabs with shortcuts disabled'
        $menu = Open-TabContextMenu $Context $disabledWindow $disabledWindow.ViewModel.CurrentBrowser
        Invoke-TabMenuItem $menu 0
        Wait-Regression $Context { !$disabledWindow.IsVisible } 'tab menu closes the window with its final tab'
    } finally {
        if ($null -ne $menu) { $menu.IsOpen = $false }
        $Context.Application.Resources.MergedDictionaries[0] = $savedLanguage
        $settings.NewPageBehavior = $savedBehavior
        $settings.DisableBrowserShortcuts = $savedShortcuts
        $settings.AlwaysShowTabStrip = $savedTabStrip
        foreach ($created in @($Context.Application.Windows | Where-Object { !$beforeWindows.Contains($_) })) {
            $created.ForceCloseWindow()
        }
    }
}

function Test-BrowserTabCloseOthers($Context) {
    $window = New-RegressionWindow $Context
    $handler = $null
    try {
        $model = $window.ViewModel
        $first = $model.CurrentBrowser
        $blocked = New-RegressionTab $Context $window $Context.AuxiliaryAddress
        $later = New-RegressionTab $Context $window $Context.AuxiliarySecondAddress
        $retained = New-RegressionTab $Context $window
        $handler = Set-RegressionBeforeUnload $Context $blocked.Browser
        $handler.Hold = $true
        $model.CurrentBrowser = $later
        $menu = Open-TabContextMenu $Context $window $retained
        Invoke-TabMenuItem $menu 1
        Wait-Regression $Context { $handler.Count -eq 1 -and !$model.Browsers.Contains($first) } 'close others waits for each preceding tab to close'
        $model.CloseOtherBrowsers($later)
        Assert-Regression ($model.CurrentBrowser -eq $retained -and !$later.Browser.IsDisposed -and $handler.Count -eq 1) 'duplicate batches are ignored and later tabs await confirmation'
        $handler.CompletePending($false)
        Wait-Regression $Context {
            $null -eq $window.GetType().GetField('_closeOtherTabsOperation', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($window)
        } 'cancelled close-others batch finishes'
        Assert-Regression ($first.Browser.IsDisposed -and $model.Browsers.Count -eq 3 -and
            !$blocked.Browser.IsDisposed -and !$later.Browser.IsDisposed) 'cancelling close others preserves the rejected and later tabs'
        $handler.Hold = $true
        $model.CloseOtherBrowsers($retained)
        Wait-Regression $Context { $handler.Count -eq 2 } 'close others can be retried after cancellation'
        $added = New-RegressionTab $Context $window
        $handler.CompletePending($true)
        Wait-Regression $Context { $model.Browsers.Count -eq 2 } 'approved close-others snapshot completes'
        Assert-Regression ($model.Browsers.Contains($retained) -and $model.Browsers.Contains($added) -and
            $blocked.Browser.IsDisposed -and $later.Browser.IsDisposed) 'close others disposes snapshot tabs and preserves tabs added during confirmation'
        $model.CloseOtherBrowsers($retained)
        Wait-Regression $Context { $model.Browsers.Count -eq 1 } 'another batch closes the newly added tab'
        Assert-Regression ($window.IsVisible -and $model.CurrentBrowser -eq $retained -and !$retained.Browser.IsDisposed) 'close others leaves its selected target and window alive'
    } finally {
        if ($null -ne $handler) { $handler.CompletePending($true) }
        $window.ForceCloseWindow()
    }
}

function Test-BrowserTabCloseOthersInterrupted($Context) {
    foreach ($interruption in @('retained-tab', 'window-close', 'force-close')) {
        $window = New-RegressionWindow $Context
        $handler = $null
        try {
            $model = $window.ViewModel
            $blocked = $model.CurrentBrowser
            $blocked.Address = $Context.AuxiliaryAddress
            Wait-Regression $Context {
                $blocked.Browser.Address -eq $Context.AuxiliaryAddress -and !$blocked.Browser.IsLoading -and
                $blocked.Browser.CanExecuteJavascriptInMainFrame
            } 'interruption source loads'
            $retained = New-RegressionTab $Context $window
            $later = New-RegressionTab $Context $window
            $handler = Set-RegressionBeforeUnload $Context $blocked.Browser
            $handler.Hold = $true
            $model.CloseOtherBrowsers($retained)
            Wait-Regression $Context { $handler.Count -eq 1 } 'close others awaits interruption'
            switch ($interruption) {
                'retained-tab' {
                    $model.CloseBrowser($retained)
                    Wait-Regression $Context { !$model.Browsers.Contains($retained) } 'retained tab closes independently'
                    $handler.CompletePending($true)
                    Wait-Regression $Context { $model.Browsers.Count -eq 1 } 'batch stops when retained tab disappears'
                    Assert-Regression ($model.CurrentBrowser -eq $later -and !$later.Browser.IsDisposed) 'later tab survives removal of the retained tab'
                }
                'window-close' {
                    $window.Close()
                    Wait-Regression $Context {
                        !$model.Browsers.Contains($retained) -and $model.CurrentBrowser -eq $blocked
                    } 'window close reaches the pending batch confirmation'
                    $handler.CompletePending($false)
                    Wait-Regression $Context { !$model.IsClosing } 'window close cancellation finishes'
                    Assert-Regression ($model.Browsers.Count -eq 2 -and $model.Browsers.Contains($later) -and
                        !$later.Browser.IsDisposed) 'window close takes over the batch and cancellation preserves later tabs'
                }
                'force-close' {
                    $window.ForceCloseWindow()
                    Wait-Regression $Context { !$window.IsVisible -and $model.Browsers.Count -eq 0 } 'force close interrupts a pending close-others batch'
                    Assert-Regression ($blocked.Browser.IsDisposed -and $retained.Browser.IsDisposed -and $later.Browser.IsDisposed) 'force close releases all tabs during a batch'
                }
            }
        } finally {
            if ($null -ne $handler) { $handler.CompletePending($true) }
            $window.ForceCloseWindow()
        }
    }
}

function Assert-TabStripLayout($Context, $Window, [bool]$ShowTabStrip) {
    $address = $Window.FindName('addressTextBox')
    $icon = $address.Parent.Children | Where-Object { $_ -is [System.Windows.Controls.StackPanel] }
    $padding = if ($ShowTabStrip) { 8 } else { 35 }
    Wait-Regression $Context {
        $Window.ViewModel.ShowTabStrip -eq $ShowTabStrip -and
        $Window.FindName('tabStrip').IsVisible -eq $ShowTabStrip -and
        $Window.FindName('toolbarNewTabButton').IsVisible -eq !$ShowTabStrip -and
        $icon.IsVisible -eq !$ShowTabStrip -and $address.Padding.Left -eq $padding
    } "tab strip and toolbar layout match visibility $ShowTabStrip"
}

function Test-BrowserTabStrip($Context) {
    $window = New-RegressionWindow $Context -Width 600
    try {
        $model = $window.ViewModel
        $tabs = $window.FindName('tabList')
        $first = $model.CurrentBrowser
        Assert-Regression ($model.Browsers.Count -eq 1 -and $tabs.SelectedItem -eq $first) 'window starts with a single selected tab'
        Assert-TabStripLayout $Context $window $false
        $newButton = $window.FindName('newTabButton')
        $toolbarNewButton = $window.FindName('toolbarNewTabButton')
        $peer = New-Object System.Windows.Automation.Peers.ButtonAutomationPeer($toolbarNewButton)
        $peer.GetPattern([System.Windows.Automation.Peers.PatternInterface]::Invoke).Invoke()
        Wait-Regression $Context { $model.Browsers.Count -eq 2 -and $model.CurrentBrowser.IsBrowserReady() } 'new tab button creates and selects a browser'
        $second = $model.CurrentBrowser
        Assert-TabStripLayout $Context $window $true
        Assert-Regression ($second.Address -eq 'about:blank' -and $tabs.SelectedItem -eq $second) 'new tab has a blank address and selected header'
        $tabs.SetCurrentValue([System.Windows.Controls.Primitives.Selector]::SelectedItemProperty, $first)
        Assert-Regression ($model.CurrentBrowser -eq $first) 'selecting a tab header changes the current browser'
        Invoke-TabShortcut $window T Control
        Wait-Regression $Context { $model.Browsers.Count -eq 3 -and $model.CurrentBrowser.IsBrowserReady() } 'Ctrl+T creates a tab'
        $third = $model.CurrentBrowser
        Assert-Regression ($model.Browsers[2] -eq $third) 'Ctrl+T appends a tab after all existing tabs'
        $model.CurrentBrowser = $first
        $peer.GetPattern([System.Windows.Automation.Peers.PatternInterface]::Invoke).Invoke()
        Wait-Regression $Context { $model.Browsers.Count -eq 4 -and $model.CurrentBrowser.IsBrowserReady() } 'new tab button creates another tab'
        $fourth = $model.CurrentBrowser
        Assert-Regression ($model.Browsers[3] -eq $fourth) 'new tab button appends a tab after all existing tabs'
        $model.CloseBrowser($fourth)
        Wait-Regression $Context { $model.Browsers.Count -eq 3 } 'extra toolbar tab closes'
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
        Assert-TabStripLayout $Context $window $false
        $model.ToggleFullscreen($third)
        Assert-Regression (!$window.FindName('tabStrip').IsVisible) 'fullscreen hides the tab strip'
        $model.ToggleFullscreen($third)
        Assert-Regression (!$window.FindName('tabStrip').IsVisible) 'leaving fullscreen keeps the tab strip hidden for a single tab'
    } finally {
        $window.ForceCloseWindow()
    }
}

function Test-BrowserTabStripSettings($Context) {
    $settings = $Context.Application.Resources['Locator'].SettingsWindowViewModel
    $savedAlwaysShow = $settings.AlwaysShowTabStrip
    $windows = @()
    try {
        $legacy = [Newtonsoft.Json.JsonConvert]::DeserializeObject('{"SaveZoomLevel":false}', [CefFlashBrowser.Models.Settings])
        Assert-Regression (!$legacy.AlwaysShowTabStrip -and !$legacy.SaveZoomLevel) 'older settings default to hiding a single tab without changing other values'
        foreach ($language in [CefFlashBrowser.Services.LanguageManager]::GetSupportedLanguage()) {
            $label = [CefFlashBrowser.Services.LanguageManager]::GetString($language, 'settings_alwaysShowTabStrip')
            Assert-Regression (![string]::IsNullOrWhiteSpace($label)) "always-show tab strip setting has a localized label ($language)"
        }

        $settings.AlwaysShowTabStrip = $false
        $first = New-RegressionWindow $Context
        $windows += $first
        $second = New-RegressionWindow $Context
        $windows += $second
        foreach ($window in $windows) { Assert-TabStripLayout $Context $window $false }

        $settings.AlwaysShowTabStrip = $true
        foreach ($window in $windows) { Assert-TabStripLayout $Context $window $true }
        $json = [Newtonsoft.Json.JsonConvert]::SerializeObject([CefFlashBrowser.Data.GlobalData]::Settings)
        $restored = [Newtonsoft.Json.JsonConvert]::DeserializeObject($json, [CefFlashBrowser.Models.Settings])
        Assert-Regression $restored.AlwaysShowTabStrip 'JSON round trip preserves the always-show tab strip setting'

        $third = New-RegressionWindow $Context
        $windows += $third
        Assert-TabStripLayout $Context $third $true
        $extra = New-RegressionTab $Context $third
        Assert-TabStripLayout $Context $third $true
        $third.ViewModel.CloseBrowser($extra)
        Wait-Regression $Context { $third.ViewModel.Browsers.Count -eq 1 } 'closing to a single tab preserves the always-visible tab strip'
        Assert-TabStripLayout $Context $third $true

        $first.ViewModel.ToggleFullscreen($first.ViewModel.CurrentBrowser)
        Assert-Regression (!$first.FindName('tabStrip').IsVisible) 'fullscreen hides the always-visible tab strip'
        $settings.AlwaysShowTabStrip = $false
        Assert-Regression (!$first.FindName('tabStrip').IsVisible) 'disabling always-show during fullscreen keeps the tab strip hidden'
        $settings.AlwaysShowTabStrip = $true
        Assert-Regression (!$first.FindName('tabStrip').IsVisible) 'enabling always-show during fullscreen keeps the tab strip hidden'
        $first.ViewModel.ToggleFullscreen($first.ViewModel.CurrentBrowser)
        Assert-TabStripLayout $Context $first $true

        $extra = New-RegressionTab $Context $third
        $settings.AlwaysShowTabStrip = $false
        Assert-TabStripLayout $Context $first $false
        Assert-TabStripLayout $Context $second $false
        Assert-TabStripLayout $Context $third $true
        $third.ViewModel.CloseBrowser($extra)
        Wait-Regression $Context { $third.ViewModel.Browsers.Count -eq 1 } 'disabling always-show restores hiding after closing to a single tab'
        Assert-TabStripLayout $Context $third $false
    } finally {
        foreach ($window in $windows) { $window.ForceCloseWindow() }
        $settings.AlwaysShowTabStrip = $savedAlwaysShow
    }
}

function Test-BrowserTabNewPageRouting($Context) {
    $window = New-RegressionWindow $Context
    $savedBehavior = [CefFlashBrowser.Data.GlobalData]::Settings.NewPageBehavior
    try {
        $model = $window.ViewModel
        $first = $model.CurrentBrowser
        $last = New-RegressionTab $Context $window
        $model.CurrentBrowser = $first
        $view = Find-BrowserView $window $first
        [CefFlashBrowser.Data.GlobalData]::Settings.NewPageBehavior = [CefFlashBrowser.Models.NewPageBehavior]::NewTab
        $model.ToggleFullscreen($first)
        Invoke-ViewCallback $view 'OnBrowserBeforePopup' @(
            $first.Browser, $first.Browser.GetBrowser(), $null, $Context.AuxiliaryAddress, '',
            [CefSharp.WindowOpenDisposition]::NewBackgroundTab, $true, $null, $null, $null, $false)
        Wait-Regression $Context { $model.Browsers.Count -eq 3 } 'background page request opens a tab'
        $background = $model.Browsers[1]
        Assert-Regression ($model.Browsers[0] -eq $first -and $background.Address -eq $Context.AuxiliaryAddress -and $model.Browsers[2] -eq $last) 'background page opens immediately after its source before existing tabs'
        Assert-Regression ($model.CurrentBrowser -eq $first -and $model.Fullscreen) 'background page request preserves selection and fullscreen'
        Invoke-ViewCallback $view 'OnBrowserBeforePopup' @(
            $first.Browser, $first.Browser.GetBrowser(), $null, $Context.AuxiliarySecondAddress, '',
            [CefSharp.WindowOpenDisposition]::NewForegroundTab, $true, $null, $null, $null, $false)
        Wait-Regression $Context { $model.Browsers.Count -eq 4 -and $window.Title -eq 'Second auxiliary page' } 'foreground page request selects the new tab'
        $foreground = $model.CurrentBrowser
        Assert-Regression (!$model.Fullscreen -and $first.Address -eq 'about:blank') 'foreground tab exits fullscreen and preserves the source page'
        Assert-Regression ($model.Browsers[0] -eq $first -and $model.Browsers[1] -eq $foreground -and $model.Browsers[2] -eq $background -and $model.Browsers[3] -eq $last) 'repeated page requests insert the newest tab immediately after their source'
        Assert-Regression ($foreground.Address -eq $Context.AuxiliarySecondAddress -and $background.Address -eq $Context.AuxiliaryAddress) 'new page requests retain their target addresses'

        Wait-Regression $Context { $background.IsBrowserReady() } 'background source tab initialization'
        $backgroundView = Find-BrowserView $window $background
        $model.ToggleFullscreen($foreground)
        Invoke-ViewCallback $backgroundView 'OnBrowserBeforePopup' @(
            $background.Browser, $background.Browser.GetBrowser(), $null, 'about:blank', '',
            [CefSharp.WindowOpenDisposition]::NewBackgroundTab, $true, $null, $null, $null, $false)
        Wait-Regression $Context { $model.Browsers.Count -eq 5 } 'background source opens a new tab'
        Assert-Regression ($model.Browsers[1] -eq $foreground -and $model.Browsers[2] -eq $background -and $model.Browsers[3].Address -eq 'about:blank' -and $model.Browsers[4] -eq $last) 'background source inserts after itself instead of the selected tab'
        Assert-Regression ($model.CurrentBrowser -eq $foreground -and $model.Fullscreen) 'background source request preserves selection and fullscreen'
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

function Test-BrowserTabReordering($Context) {
    $window = New-RegressionWindow $Context
    try {
        $model = $window.ViewModel
        $first = $model.CurrentBrowser
        $first.Address = $Context.AuxiliaryAddress
        Wait-Regression $Context { $window.Title -eq 'Auxiliary regression' } 'reorder source page loaded'
        $second = New-RegressionTab $Context $window
        $third = New-RegressionTab $Context $window
        $model.CurrentBrowser = $first
        $window.UpdateLayout()
        $tabs = $window.FindName('tabList')
        $firstView = Find-BrowserView $window $first
        $firstBrowser = $first.Browser
        [void](Invoke-JavaScript $Context $firstBrowser 'window.preservedReorderValue = 73')
        [CefFlashBrowser.Data.GlobalData]::Settings.EnableIntegratedDevTools = $true
        Open-RegressionDevTools $Context $firstView
        $toolsHandle = $first.IntegratedDevToolsHandle

        $model.MoveBrowser($first, 2)
        $window.UpdateLayout()
        Assert-Regression ($model.Browsers[0] -eq $second -and $model.Browsers[1] -eq $third -and $model.Browsers[2] -eq $first) 'moving a tab right updates its position'
        Assert-Regression ($model.CurrentBrowser -eq $first -and $tabs.SelectedItem -eq $first) 'moving the selected tab preserves selection'
        Assert-Regression ([object]::ReferenceEquals((Find-BrowserView $window $first), $firstView)) 'reordering retains the BrowserView instance'
        Assert-Regression ([object]::ReferenceEquals($first.Browser, $firstBrowser) -and !$firstBrowser.IsDisposed) 'reordering retains the CEF browser instance'
        Assert-Regression ((Invoke-JavaScript $Context $firstBrowser 'window.preservedReorderValue') -eq 73) 'reordering preserves JavaScript state'
        Assert-Regression ($first.IntegratedDevToolsHandle -eq $toolsHandle) 'reordering preserves integrated DevTools'

        $model.MoveBrowser($first, 0)
        $model.MoveBrowser($third, 1)
        $window.UpdateLayout()
        Assert-Regression ($model.Browsers[0] -eq $first -and $model.Browsers[1] -eq $third -and $model.Browsers[2] -eq $second) 'moving selected and background tabs left updates the order'
        Assert-Regression ($model.CurrentBrowser -eq $first -and $tabs.SelectedItem -eq $first) 'moving a background tab preserves selection'
        Invoke-TabShortcut $window Tab Control
        Assert-Regression ($model.CurrentBrowser -eq $third) 'keyboard cycling follows the reordered tabs'

        # Exercise the same midpoint and viewport logic used by DragOver without moving the desktop cursor.
        $draggedField = $window.GetType().GetField('_draggedTab', [Reflection.BindingFlags]'Instance,NonPublic')
        $draggedField.SetValue($window, $third)
        $firstHeader = $tabs.ItemContainerGenerator.ContainerFromItem($first)
        $point = $firstHeader.TranslatePoint([System.Windows.Point]::new(1, 10), $tabs)
        Invoke-ViewCallback $window 'UpdateTabDrag' @($point, $false)
        Assert-Regression ($model.Browsers[0] -eq $third -and $model.CurrentBrowser -eq $third) 'dragging past the left midpoint moves the tab left'
        $window.UpdateLayout()
        $secondHeader = $tabs.ItemContainerGenerator.ContainerFromItem($second)
        $point = $secondHeader.TranslatePoint([System.Windows.Point]::new($secondHeader.ActualWidth - 1, 10), $tabs)
        Invoke-ViewCallback $window 'UpdateTabDrag' @($point, $false)
        Assert-Regression ($model.Browsers[2] -eq $third) 'dragging past the right midpoint moves the tab right'
        Invoke-ViewCallback $window 'UpdateTabDrag' @([System.Windows.Point]::new(0, -10), $false)
        Assert-Regression ($model.Browsers[2] -eq $third) 'dragging outside the tab strip leaves the order unchanged'

        $window.Width = 400
        $window.UpdateLayout()
        $scroll = $tabs.Template.FindName('tabScrollViewer', $tabs)
        $scroll.ScrollToLeftEnd()
        $window.UpdateLayout()
        $offset = $scroll.HorizontalOffset
        $point = $scroll.TranslatePoint([System.Windows.Point]::new($scroll.ActualWidth - 1, 10), $tabs)
        Invoke-ViewCallback $window 'UpdateTabDrag' @($point, $true)
        Assert-Regression ($scroll.HorizontalOffset -gt $offset) 'dragging at the right edge scrolls overflowing tabs'
        $point = $scroll.TranslatePoint([System.Windows.Point]::new(1, 10), $tabs)
        Invoke-ViewCallback $window 'UpdateTabDrag' @($point, $true)
        Assert-Regression ($scroll.HorizontalOffset -eq 0) 'dragging at the left edge scrolls back'
        $draggedField.SetValue($window, $null)

        $model.MoveBrowser($third, 2)
        $model.CloseBrowser($third)
        Wait-Regression $Context { $model.Browsers.Count -eq 2 } 'reordered selected tab closes'
        Assert-Regression ($model.CurrentBrowser -eq $second) 'closing a reordered tab selects its current neighbor'
    } finally {
        $window.ForceCloseWindow()
    }
}

function Assert-TabDragPreview($Window, $Browser, [string]$Scenario) {
    $draggedField = $Window.GetType().GetField('_draggedTab', [Reflection.BindingFlags]'Instance,NonPublic')
    $draggedField.SetValue($Window, $Browser)
    try {
        Invoke-ViewCallback $Window 'ShowTabDragPreview' @()
        $snapshot = $Window.FindName('tabDragPreviewImage').Source
        Assert-Regression ($null -ne $snapshot) "$Scenario creates a preview image"
        $stride = $snapshot.PixelWidth * 4
        $pixels = New-Object byte[] ($stride * $snapshot.PixelHeight)
        $snapshot.CopyPixels($pixels, $stride, 0)
        # The selected header background has low alpha; opaque pixels come from its text and icons.
        $visiblePixels = 0
        for ($index = 3; $index -lt $pixels.Length; $index += 4) {
            if ($pixels[$index] -gt 128) { $visiblePixels++ }
        }
        Assert-Regression ($visiblePixels -gt 20) "$Scenario preview contains header content ($visiblePixels visible pixels)"
    } finally {
        Invoke-ViewCallback $Window 'HideTabDragPreview' @()
        $draggedField.SetValue($Window, $null)
    }
}

function Test-BrowserTabDragPreview($Context) {
    $window = New-RegressionWindow $Context -Width 1000
    try {
        $model = $window.ViewModel
        $first = $model.CurrentBrowser
        $second = New-RegressionTab $Context $window
        $third = New-RegressionTab $Context $window
        $browsers = @($first, $second, $third)
        for ($index = 0; $index -lt $browsers.Count; $index++) {
            $browser = $browsers[$index]
            $browser.Title = "Preview tab $($index + 1)"
            $model.CurrentBrowser = $browser
            $window.UpdateLayout()
            Assert-TabDragPreview $window $browser "tab $($index + 1)"
        }

        $model.MoveBrowser($third, 1)
        $window.UpdateLayout()
        Assert-TabDragPreview $window $third 'reordered second tab'
        $window.Width = 400
        $window.UpdateLayout()
        $tabs = $window.FindName('tabList')
        $tabs.ScrollIntoView($third)
        $window.UpdateLayout()
        $scroll = $tabs.Template.FindName('tabScrollViewer', $tabs)
        Assert-Regression ($scroll.HorizontalOffset -gt 0) 'preview check covers a scrolled tab strip'
        Assert-TabDragPreview $window $third 'second tab after horizontal scrolling'
        Assert-Regression (!$window.FindName('tabDragPreview').IsOpen -and $null -eq $tabs.Tag) 'preview cleanup restores the tab strip'
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
