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
    Test-BrowserTabAdaptiveWidth $Context
    Test-BrowserTabStripSettings $Context
    Test-BrowserTabContextMenu $Context
    Test-BrowserTabLoadingMenu $Context
    Test-BrowserMute $Context
    Test-BrowserTabCloseOthers $Context
    Test-BrowserTabCloseOthersInterrupted $Context
    Test-BrowserTabFullscreen $Context
    Test-BrowserTabNewPageRouting $Context
    Test-BrowserTabSelection $Context
    Test-BrowserTabCloseKeyboard $Context
    Test-BrowserTabCloseKeyboardCancellation $Context
    Test-BrowserTabCloseFocusChanges $Context
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

function Set-TabWindowForeground($Context, $Window) {
    $Window.Left = 100
    $Window.Top = 100
    $Window.Topmost = $true
    $handle = (New-Object System.Windows.Interop.WindowInteropHelper($Window)).Handle
    Wait-Regression $Context {
        # Another window can interrupt the initial activation request.
        if (!$Window.IsActive -or ![RegressionKeyboard]::IsForegroundWindow($handle)) {
            [void]$Window.Activate()
            if (![RegressionKeyboard]::IsForegroundWindow($handle)) {
                [void][RegressionKeyboard]::TryActivateWindow($handle)
            }
        }
        $Window.IsActive -and [RegressionKeyboard]::IsForegroundWindow($handle)
    } 'keyboard test window is in the foreground'
}

function Set-TabKeyboardFocus($Context, $Window, [switch]$Address) {
    Set-TabWindowForeground $Context $Window
    if ($Address) {
        [void]$Window.FindName('addressTextBox').Focus()
    } else {
        $browser = $Window.ViewModel.CurrentBrowser.Browser
        [void]$browser.Focus()
        $hostBrowser = $browser.GetBrowser().GetHost()
        $hostBrowser.SetFocus($true)
        Wait-Regression $Context {
            [RegressionKeyboard]::HasFocus($hostBrowser.GetWindowHandle()) -and
            (Invoke-JavaScript $Context $browser 'document.hasFocus()')
        } 'keyboard test page has native focus'
    }
}

function Send-TabCloseKey($Window) {
    $handle = (New-Object System.Windows.Interop.WindowInteropHelper($Window)).Handle
    Assert-Regression ([RegressionKeyboard]::IsForegroundWindow($handle)) 'Ctrl+W targets the test window'
    [RegressionKeyboard]::PressKey(0x57)
}

function Test-BrowserTabKeyboardFocusRecovery($Context, $Window) {
    $otherWindow = $null
    $onActivated = $null
    try {
        $otherWindow = New-Object System.Windows.Window
        $otherWindow.Title = 'Keyboard focus recovery regression'
        $otherWindow.Width = 400
        $otherWindow.Height = 100
        $otherWindow.ShowInTaskbar = $false
        $otherWindow.Show()
        Set-TabWindowForeground $Context $otherWindow
        $state = @{ InterruptOnce = $true }
        $onActivated = [EventHandler]({
            param($sender, $eventArgs)
            if ($state.InterruptOnce) {
                $state.InterruptOnce = $false
                [void]$otherWindow.Activate()
            }
        }.GetNewClosure())
        $Window.add_Activated($onActivated)
        Set-TabWindowForeground $Context $Window
        Assert-Regression (!$state.InterruptOnce -and $Window.IsActive) 'keyboard setup recovers from a one-time focus interruption'
    } finally {
        if ($null -ne $onActivated) { $Window.remove_Activated($onActivated) }
        if ($null -ne $otherWindow) { $otherWindow.Close() }
    }
}

function Test-BrowserTabCloseKeyboard($Context) {
    foreach ($scenario in @('last', 'middle', 'address', 'blank')) {
        $window = New-RegressionWindow $Context
        try {
            $model = $window.ViewModel
            $first = $model.CurrentBrowser
            if ($scenario -ne 'blank') {
                $first.Address = $Context.AuxiliaryAddress
                Wait-Regression $Context { $window.Title -eq 'Auxiliary regression' } 'first keyboard test page loads'
            }
            $second = New-RegressionTab $Context $window $Context.AuxiliarySecondAddress
            $remaining = @($first)
            if ($scenario -ne 'blank') {
                $third = New-RegressionTab $Context $window $Context.AuxiliaryAddress
                $remaining = @($second, $first)
                if ($scenario -eq 'middle') {
                    $model.CurrentBrowser = $second
                    $remaining = @($third, $first)
                }
            }
            if ($scenario -eq 'last') {
                Test-BrowserTabKeyboardFocusRecovery $Context $window
            }
            Set-TabKeyboardFocus $Context $window -Address:($scenario -eq 'address')
            [RegressionKeyboard]::SetKey(0x11, $false)
            Wait-Regression $Context {
                [System.Windows.Input.Keyboard]::Modifiers -eq [System.Windows.Input.ModifierKeys]::Control
            } 'Ctrl remains held between tab close keys'

            foreach ($next in $remaining) {
                $expectedCount = $model.Browsers.Count - 1
                Send-TabCloseKey $window
                Wait-Regression $Context { $model.Browsers.Count -eq $expectedCount } "real Ctrl+W closes a tab ($scenario, remaining=$expectedCount)" 5
                Assert-Regression ($model.CurrentBrowser -eq $next) "Ctrl+W selects the expected neighbor ($scenario)"
            }
            if ($scenario -eq 'address' -or $scenario -eq 'blank') {
                $address = $window.FindName('addressTextBox')
                Assert-Regression ([object]::ReferenceEquals([System.Windows.Input.Keyboard]::FocusedElement, $address)) "address focus survives tab close ($scenario)"
                if ($scenario -eq 'blank') {
                    Assert-Regression ($address.SelectionLength -eq $address.Text.Length) 'blank successor selects the whole address'
                }
            } else {
                Assert-Regression (Invoke-JavaScript $Context $first.Browser 'document.hasFocus()') 'surviving page receives native focus'
            }
            Send-TabCloseKey $window
            Wait-Regression $Context { !$window.IsVisible } "real Ctrl+W closes the final tab and window ($scenario)" 5
        } finally {
            [RegressionKeyboard]::SetKey(0x11, $true)
            $window.ForceCloseWindow()
        }
    }
}

function Test-BrowserTabCloseKeyboardCancellation($Context) {
    $window = New-RegressionWindow $Context
    try {
        $model = $window.ViewModel
        $first = $model.CurrentBrowser
        $first.Address = $Context.AuxiliaryAddress
        Wait-Regression $Context { $window.Title -eq 'Auxiliary regression' } 'first cancellation test page loads'
        $second = New-RegressionTab $Context $window $Context.AuxiliarySecondAddress
        $view = Find-BrowserView $window $second
        $handler = Set-RegressionBeforeUnload $Context $second.Browser
        Set-TabKeyboardFocus $Context $window
        [RegressionKeyboard]::SetKey(0x11, $false)
        Wait-Regression $Context {
            [System.Windows.Input.Keyboard]::Modifiers -eq [System.Windows.Input.ModifierKeys]::Control
        } 'Ctrl is held for cancellation and retry'
        Send-TabCloseKey $window
        Wait-Regression $Context {
            $handler.Count -eq 1 -and $null -eq $view.GetType().GetField('_closeCompletion', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($view)
        } 'keyboard close cancellation completes'
        Assert-Regression ($model.Browsers.Count -eq 2 -and $model.CurrentBrowser -eq $second -and
            [RegressionKeyboard]::HasFocus($second.Browser.GetBrowser().GetHost().GetWindowHandle())) 'cancelled keyboard close retains its tab and native focus'
        $handler.Accept = $true
        Send-TabCloseKey $window
        Wait-Regression $Context { $model.Browsers.Count -eq 1 } 'keyboard close retries after cancellation' 5
        Assert-Regression ($model.CurrentBrowser -eq $first -and $handler.Count -eq 2) 'retry selects the successor after approval'
        Send-TabCloseKey $window
        Wait-Regression $Context { !$window.IsVisible } 'keyboard close continues on the surviving page after retry' 5
    } finally {
        [RegressionKeyboard]::SetKey(0x11, $true)
        $window.ForceCloseWindow()
    }
}

function Test-BrowserTabCloseFocusChanges($Context) {
    foreach ($scenario in @('background', 'tab', 'address', 'window')) {
        $window = New-RegressionWindow $Context
        $otherWindow = $null
        $handler = $null
        try {
            $model = $window.ViewModel
            $first = $model.CurrentBrowser
            $first.Address = $Context.AuxiliaryAddress
            Wait-Regression $Context { $window.Title -eq 'Auxiliary regression' } 'first focus change test page loads'
            $second = New-RegressionTab $Context $window $Context.AuxiliarySecondAddress
            if ($scenario -eq 'background') {
                $third = New-RegressionTab $Context $window $Context.AuxiliaryAddress
                Set-TabKeyboardFocus $Context $window
                $model.CloseBrowser($second)
                Wait-Regression $Context { $model.Browsers.Count -eq 2 } 'background tab closes while another page has focus'
                Assert-Regression ($model.CurrentBrowser -eq $third -and
                    [RegressionKeyboard]::HasFocus($third.Browser.GetBrowser().GetHost().GetWindowHandle())) 'background close preserves the current page and native focus'
                continue
            }

            $handler = Set-RegressionBeforeUnload $Context $second.Browser
            $handler.Hold = $true
            Set-TabKeyboardFocus $Context $window
            [RegressionKeyboard]::SetKey(0x11, $false)
            Wait-Regression $Context {
                [System.Windows.Input.Keyboard]::Modifiers -eq [System.Windows.Input.ModifierKeys]::Control
            } 'Ctrl is held for the pending close'
            Send-TabCloseKey $window
            Wait-Regression $Context { $handler.Count -eq 1 } 'keyboard close awaits confirmation'
            [RegressionKeyboard]::SetKey(0x11, $true)
            switch ($scenario) {
                'tab' {
                    $model.CurrentBrowser = $first
                    Set-TabKeyboardFocus $Context $window
                }
                'address' { Set-TabKeyboardFocus $Context $window -Address }
                'window' {
                    $otherWindow = New-RegressionWindow $Context
                    Set-TabKeyboardFocus $Context $otherWindow -Address
                }
            }
            $handler.CompletePending($true)
            Wait-Regression $Context { $model.Browsers.Count -eq 1 } "pending close completes after focus changes ($scenario)"
            Assert-Regression ($model.CurrentBrowser -eq $first) 'pending close preserves the surviving selection'
            switch ($scenario) {
                'tab' {
                    Assert-Regression ([RegressionKeyboard]::HasFocus($first.Browser.GetBrowser().GetHost().GetWindowHandle())) 'pending background close preserves the newly focused page'
                }
                'address' {
                    Assert-Regression ([object]::ReferenceEquals([System.Windows.Input.Keyboard]::FocusedElement,
                        $window.FindName('addressTextBox'))) 'pending close preserves focus moved to the address bar'
                }
                'window' {
                    $handle = (New-Object System.Windows.Interop.WindowInteropHelper($otherWindow)).Handle
                    Assert-Regression ($otherWindow.IsActive -and [RegressionKeyboard]::IsForegroundWindow($handle) -and
                        [object]::ReferenceEquals([System.Windows.Input.Keyboard]::FocusedElement,
                            $otherWindow.FindName('addressTextBox'))) 'pending close does not take focus from another window'
                }
            }
        } finally {
            [RegressionKeyboard]::SetKey(0x11, $true)
            if ($null -ne $handler) { $handler.CompletePending($true) }
            if ($null -ne $otherWindow) { $otherWindow.ForceCloseWindow() }
            $window.ForceCloseWindow()
        }
    }
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
    $savedLanguage = $Context.LanguageService.CurrentLanguage
    $beforeWindows = @($Context.Application.Windows)
    try {
        $settings.AlwaysShowTabStrip = $true
        $window = New-RegressionWindow $Context
        $model = $window.ViewModel
        $source = $model.CurrentBrowser
        $menu = Open-TabContextMenu $Context $window $source
        Assert-Regression ($menu.Items.Count -eq 8 -and $menu.Items[5] -is [System.Windows.Controls.Separator] -and
            $menu.Items[2] -is [System.Windows.Controls.Separator] -and $menu.Items[1].InputGestureText -eq 'Ctrl+M' -and
            $menu.Items[3].InputGestureText -eq 'Ctrl+W' -and $menu.Items[6].InputGestureText -eq 'Ctrl+Shift+K') 'tab menu has the requested order, separator and shortcut hints'
        Assert-Regression (!$menu.Items[4].IsEnabled) 'close other tabs is disabled for a single tab'
        Assert-Regression ($menu.Items[1].Command -eq $model.ToggleMuteCommand -and
            $menu.Items[1].CommandParameter -eq $source -and
            $menu.Items[1].Header -eq $Context.LanguageService.GetString('browser_muteSite')) 'tab menu mute item binds its command and target tab'
        $keys = @('browser_reload', 'browser_muteSite', 'browser_closeTab', 'browser_closeOtherTabs', 'browser_duplicateTab', 'browser_openTabInNewWindow')
        foreach ($language in $Context.LanguageService.GetSupportedLanguage()) {
            $Context.LanguageService.SetCurrentLanguage($language)
            foreach ($index in 0..5) {
                $itemIndex = @(0, 1, 3, 4, 6, 7)[$index]
                $label = $Context.LanguageService.GetString($language, $keys[$index])
                Assert-Regression (![string]::IsNullOrWhiteSpace($label)) "tab menu label exists: $($keys[$index]) ($language)"
                Wait-Regression $Context { $menu.Items[$itemIndex].Header -eq $label } "tab menu language updates ($language)"
            }
        }
        $menu.IsOpen = $false
        $Context.LanguageService.SetCurrentLanguage($savedLanguage)
        $last = New-RegressionTab $Context $window $Context.AuxiliarySecondAddress
        Assert-Regression (!$source.IsMuted -and !$last.IsMuted) 'both tabs start independently unmuted'
        $menu = Open-TabContextMenu $Context $window $source
        Assert-Regression ($menu.Items[1].CommandParameter -eq $source -and
            $menu.Items[1].Header -eq $Context.LanguageService.GetString('browser_muteSite')) 'background tab mute menu targets its source tab'
        Invoke-TabMenuItem $menu 1
        Wait-Regression $Context { $source.IsMuted } 'tab menu mutes its background source tab'
        Assert-BrowserAudioMuted $Context $source.Browser $true 'background tab menu mutes its native CEF browser'
        Assert-BrowserAudioMuted $Context $last.Browser $false 'background tab muting leaves the selected native browser unmuted'
        Assert-Regression ($model.CurrentBrowser -eq $last -and !$last.IsMuted) 'muting a background tab preserves selection and the selected tab audio'
        $menu = Open-TabContextMenu $Context $window $source
        Assert-Regression ($menu.Items[1].CommandParameter -eq $source -and
            $menu.Items[1].Header -eq $Context.LanguageService.GetString('browser_unmuteSite')) 'muted background tab offers its unmute action'
        $menu.IsOpen = $false
        $menu = Open-TabContextMenu $Context $window $last
        Assert-Regression ($menu.Items[1].CommandParameter -eq $last -and
            $menu.Items[1].Header -eq $Context.LanguageService.GetString('browser_muteSite')) 'reopening the shared mute menu updates its target and mute state'
        $menu.IsOpen = $false
        $menu = Open-TabContextMenu $Context $window $source
        Assert-Regression ($menu.Items[1].CommandParameter -eq $source -and
            $menu.Items[1].Header -eq $Context.LanguageService.GetString('browser_unmuteSite')) 'reopening the muted background tab restores its unmute action'
        Invoke-TabMenuItem $menu 1
        Wait-Regression $Context { !$source.IsMuted } 'tab menu unmutes its background source tab'
        Assert-BrowserAudioMuted $Context $source.Browser $false 'background tab menu restores its native CEF audio'
        Assert-BrowserAudioMuted $Context $last.Browser $false 'background tab unmuting leaves the selected native browser unchanged'
        Assert-Regression ($model.CurrentBrowser -eq $last -and !$last.IsMuted) 'unmuting a background tab preserves selection and the selected tab audio'
        $source.Address = $Context.AuxiliaryAddress
        Wait-Regression $Context {
            $source.Browser.Address -eq $Context.AuxiliaryAddress -and $source.Browser.Title -eq 'Auxiliary regression' -and
            !$source.Browser.IsLoading -and
            $source.Browser.CanExecuteJavascriptInMainFrame
        } 'source page loads'
        [void](Invoke-JavaScript $Context $source.Browser 'window.tabDuplicateToken = 42;')
        foreach ($behavior in [Enum]::GetValues([CefFlashBrowser.Models.NewPageBehavior])) {
            $settings.NewPageBehavior = $behavior
            $model.CurrentBrowser = $last
            $menu = Open-TabContextMenu $Context $window $source
            Assert-Regression ($menu.Items[4].IsEnabled -and $menu.Items[7].CommandParameter -eq $source.Address) 'tab menu enables close others and targets the source address'
            Invoke-TabMenuItem $menu 6
            $duplicate = $model.CurrentBrowser
            Wait-Regression $Context {
                $duplicate.IsBrowserReady() -and !$duplicate.Browser.IsLoading -and $duplicate.Browser.CanExecuteJavascriptInMainFrame
            } 'duplicated tab initialization'
            Assert-Regression ($model.Browsers.Count -eq 3 -and $model.Browsers[1] -eq $duplicate -and
                $duplicate.Address -eq $source.Address -and $model.Browsers[2] -eq $last) "menu duplication inserts and selects a copy beside its source ($behavior)"
            Assert-Regression (Invoke-JavaScript $Context $duplicate.Browser "typeof window.tabDuplicateToken === 'undefined'") 'duplicated page starts with fresh runtime state'
            $menu = Open-TabContextMenu $Context $window $last
            Assert-Regression ($menu.Items[7].CommandParameter -eq $last.Address -and $menu.Items[6].CommandParameter -eq $last) 'reopening a shared menu updates its target'
            $menu.IsOpen = $false
            $menu = Open-TabContextMenu $Context $window $duplicate
            Invoke-TabMenuItem $menu 3
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
        Invoke-TabMenuItem $menu 7
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
        Invoke-TabMenuItem $menu 6
        Assert-Regression ($disabledWindow.ViewModel.Browsers.Count -eq 2) 'tab menu duplication works when shortcuts are disabled'
        $menu = Open-TabContextMenu $Context $disabledWindow $disabledWindow.ViewModel.CurrentBrowser
        Invoke-TabMenuItem $menu 4
        Wait-Regression $Context { $disabledWindow.ViewModel.Browsers.Count -eq 1 } 'tab menu closes other tabs with shortcuts disabled'
        $menu = Open-TabContextMenu $Context $disabledWindow $disabledWindow.ViewModel.CurrentBrowser
        Invoke-TabMenuItem $menu 3
        Wait-Regression $Context { !$disabledWindow.IsVisible } 'tab menu closes the window with its final tab'
    } finally {
        if ($null -ne $menu) { $menu.IsOpen = $false }
        $Context.LanguageService.SetCurrentLanguage($savedLanguage)
        $settings.NewPageBehavior = $savedBehavior
        $settings.DisableBrowserShortcuts = $savedShortcuts
        $settings.AlwaysShowTabStrip = $savedTabStrip
        foreach ($created in @($Context.Application.Windows | Where-Object { !$beforeWindows.Contains($_) })) {
            $created.ForceCloseWindow()
        }
    }
}

function Test-BrowserTabLoadingMenu($Context) {
    $settings = [CefFlashBrowser.Data.GlobalData]::Settings
    $savedTabStrip = $settings.AlwaysShowTabStrip
    $savedLanguage = $Context.LanguageService.CurrentLanguage
    $beforeWindows = @($Context.Application.Windows)
    $fixture = $null
    $menu = $null
    try {
        $settings.AlwaysShowTabStrip = $true
        $fixture = New-Object FaviconHttpFixture
        $window = New-RegressionWindow $Context
        $model = $window.ViewModel
        $source = $model.CurrentBrowser
        Invoke-BrowserCommand $source.Browser.LoadUrlCommand $Context.AuxiliaryAddress
        Wait-Regression $Context {
            $source.Browser.Title -eq 'Auxiliary regression' -and !$source.Browser.IsLoading
        } 'loading-menu source page loads'
        $selected = New-RegressionTab $Context $window $Context.AuxiliarySecondAddress
        foreach ($target in @($selected, $source)) {
            Wait-Regression $Context {
                !$target.Browser.IsLoading -and $target.Browser.CanExecuteJavascriptInMainFrame
            } 'target tab is ready for a menu reload'
            [void](Invoke-JavaScript $Context $target.Browser 'window.tabReloadToken = 42;')
            $menu = Open-TabContextMenu $Context $window $target
            Assert-Regression ($menu.Items[0].Command -eq $model.ToggleLoadingStateCommand -and
                $menu.Items[0].CommandParameter -eq $target -and $menu.Items[0].InputGestureText -eq 'Ctrl+R') 'reload menu targets the clicked tab and shows its shortcut'
            Invoke-TabMenuItem $menu 0
            Wait-Regression $Context {
                !$target.Browser.IsLoading -and $target.Browser.CanExecuteJavascriptInMainFrame -and
                (Invoke-JavaScript $Context $target.Browser "typeof window.tabReloadToken === 'undefined'")
            } 'tab menu reloads the target page'
            Assert-Regression ($model.CurrentBrowser -eq $selected) 'menu reload preserves the selected tab'

            $menu = Open-TabContextMenu $Context $window $target
            $reloadIcon = $menu.Items[0].Icon
            $requests = $fixture.SlowRequests
            Invoke-BrowserCommand $target.Browser.LoadUrlCommand ($fixture.BaseUrl + '/slow-bad.png?tab=' + $requests)
            Wait-Regression $Context {
                $fixture.SlowRequests -gt $requests -and $target.Browser.IsLoading -and
                $menu.Items[0].Header -eq $Context.LanguageService.GetString('browser_stop') -and
                $menu.Items[0].Icon -ne $reloadIcon -and $menu.Items[0].InputGestureText -eq 'Esc'
            } 'open tab menu switches to stop while its target is loading'
            foreach ($language in $Context.LanguageService.GetSupportedLanguage()) {
                $Context.LanguageService.SetCurrentLanguage($language)
                Wait-Regression $Context {
                    $menu.Items[0].Header -eq $Context.LanguageService.GetString($language, 'browser_stop')
                } "stop menu language updates ($language)"
            }
            $Context.LanguageService.SetCurrentLanguage($savedLanguage)
            if ($target -eq $source) {
                $menu.IsOpen = $false
                $menu = Open-TabContextMenu $Context $window $selected
                Assert-Regression ($menu.Items[0].Header -eq $Context.LanguageService.GetString('browser_reload') -and
                    $menu.Items[0].CommandParameter -eq $selected) 'shared menu shows reload for the idle selected tab'
                $menu.IsOpen = $false
                $menu = Open-TabContextMenu $Context $window $target
                Assert-Regression ($menu.Items[0].Header -eq $Context.LanguageService.GetString('browser_stop') -and
                    $menu.Items[0].CommandParameter -eq $target) 'shared menu restores stop for the loading background tab'
            }
            Invoke-TabMenuItem $menu 0
            Wait-Regression $Context { !$target.Browser.IsLoading } 'tab menu stops the pending request'
            $menu = Open-TabContextMenu $Context $window $target
            Assert-Regression ($menu.Items[0].Header -eq $Context.LanguageService.GetString('browser_reload') -and
                $menu.Items[0].Icon -eq $reloadIcon -and $menu.Items[0].InputGestureText -eq 'Ctrl+R' -and
                $model.CurrentBrowser -eq $selected) 'stopped tab restores reload and preserves selection'
            $menu.IsOpen = $false
        }
    } finally {
        if ($null -ne $menu) { $menu.IsOpen = $false }
        $Context.LanguageService.SetCurrentLanguage($savedLanguage)
        $settings.AlwaysShowTabStrip = $savedTabStrip
        foreach ($created in @($Context.Application.Windows | Where-Object { !$beforeWindows.Contains($_) })) {
            $created.ForceCloseWindow()
        }
        if ($null -ne $fixture) { $fixture.Dispose() }
    }
}

function Test-BrowserMute($Context) {
    $settings = [CefFlashBrowser.Data.GlobalData]::Settings
    $savedTabStrip = $settings.AlwaysShowTabStrip
    $savedShortcuts = $settings.DisableBrowserShortcuts
    $savedLanguage = $Context.LanguageService.CurrentLanguage
    $window = $null
    $actionsMenu = $null
    $tabMenu = $null
    $controlHeld = $false
    try {
        $settings.AlwaysShowTabStrip = $true
        $settings.DisableBrowserShortcuts = $false
        $window = New-RegressionWindow $Context
        $model = $window.ViewModel
        $source = $model.CurrentBrowser
        $source.Address = $Context.AuxiliaryAddress
        Wait-Regression $Context {
            $source.Title -eq 'Auxiliary regression' -and $source.Browser.CanExecuteJavascriptInMainFrame
        } 'mute shortcut page is ready'
        $other = New-RegressionTab $Context $window $Context.AuxiliarySecondAddress
        $model.CurrentBrowser = $source
        $icon = Get-TabMuteIcon $window $source
        Assert-SvgIconLoaded $icon 'tab mute SVG loads a nonempty drawing from the application resources'
        Assert-Regression ($null -eq (Find-CommandButton $window $model.ToggleMuteCommand)) 'toolbar has no mute button'
        Assert-Regression (!$source.IsMuted -and $icon.Visibility -eq [System.Windows.Visibility]::Collapsed) 'tab starts unmuted with its mute icon hidden'
        Assert-BrowserAudioMuted $Context $source.Browser $false 'tab starts with native CEF audio unmuted'

        $shortcut = @($window.InputBindings | Where-Object {
            $_.Key -eq [System.Windows.Input.Key]::M -and $_.Modifiers -eq [System.Windows.Input.ModifierKeys]::Control
        })
        Assert-Regression ($shortcut.Count -eq 1 -and $shortcut[0].Command -eq $model.ToggleMuteCommand -and
            $shortcut[0].CommandParameter -eq $source) 'Ctrl+M is uniquely bound to muting the selected tab'
        $homeShortcut = @($window.InputBindings | Where-Object {
            $_.Key -eq [System.Windows.Input.Key]::Home -and $_.Modifiers -eq [System.Windows.Input.ModifierKeys]::Alt
        })
        Assert-Regression ($homeShortcut.Count -eq 1 -and $homeShortcut[0].Command -eq $model.ShowMainWindowCommand) 'Alt+Home is uniquely bound to showing the main window'
        $actionsMenu = Open-BrowserActionsMenu $Context $window
        $actionItems = @($actionsMenu.Items | Where-Object { $_ -is [System.Windows.Controls.MenuItem] })
        $muteItem = $actionItems[3]
        Assert-Regression ($actionItems[2].Command -eq $model.NewBrowserWindowCommand -and
            $muteItem.Command -eq $model.ToggleMuteCommand -and
            $muteItem.InputGestureText -eq 'Ctrl+M' -and
            $muteItem.Header -eq $Context.LanguageService.GetString('browser_muteSite')) 'actions menu offers mute as the next action after new window with its shortcut hint'
        Assert-Regression ($actionItems[0].Command -eq $model.ShowMainWindowCommand -and
            $actionItems[0].InputGestureText -eq 'Alt+Home') 'show main window advertises its Alt+Home shortcut'
        $actionsMenu.IsOpen = $false
        Invoke-BrowserCommand $muteItem.Command $muteItem.CommandParameter
        Assert-Regression $source.IsMuted 'actions menu mutes the selected tab view model'
        Assert-BrowserAudioMuted $Context $source.Browser $true 'actions menu mutes the selected native CEF browser'
        Assert-BrowserAudioMuted $Context $other.Browser $false 'actions mute leaves the other native browser unmuted'
        Assert-Regression ($icon.Visibility -eq [System.Windows.Visibility]::Visible -and !$other.IsMuted) 'actions mute shows the same tab icon without changing the other tab'

        $skin = @($Context.Application.Resources.MergedDictionaries | Where-Object {
            "$($_.Source)" -like '*HandyControl;component/Themes/Skin*.xaml'
        })[0]
        $savedSkin = $skin.Source
        try {
            foreach ($skinName in @('SkinDark', 'SkinDefault', 'SkinDark')) {
                $skin.Source = [Uri]"pack://application:,,,/HandyControl;component/Themes/$skinName.xaml"
                $window.UpdateLayout()
                $expectedColor = $Context.Application.FindResource('PrimaryTextBrush').Color
                Assert-Regression ($icon.Fill -is [System.Windows.Media.SolidColorBrush] -and
                    $icon.Fill.Color -eq $expectedColor) "existing tab mute icon follows the theme text brush ($skinName)"
                $colors = @(Get-DrawingColors $icon.Source.Drawing)
                Assert-Regression ($colors.Count -gt 0 -and @($colors | Where-Object { $_ -ne $expectedColor }).Count -eq 0) "rendered mute drawing uses the theme text color ($skinName)"
                Assert-Regression ($icon.Visibility -eq [System.Windows.Visibility]::Visible -and $source.IsMuted) 'theme switching preserves the muted tab icon and state'
            }
        } finally {
            $skin.Source = $savedSkin
        }

        $tabMenu = Open-TabContextMenu $Context $window $source
        $tabMuteItem = $tabMenu.Items[1]
        Assert-Regression ($tabMuteItem.Header -eq $Context.LanguageService.GetString('browser_unmuteSite') -and
            $tabMuteItem.InputGestureText -eq 'Ctrl+M') 'tab menu follows action mute state and displays the same shortcut'
        $tabMenu.IsOpen = $false
        $actionsMenu = Open-BrowserActionsMenu $Context $window
        foreach ($language in $Context.LanguageService.GetSupportedLanguage()) {
            $Context.LanguageService.SetCurrentLanguage($language)
            $label = $Context.LanguageService.GetString('browser_unmuteSite')
            Assert-Regression (![string]::IsNullOrWhiteSpace($label)) "unmute menu label exists ($language)"
            Wait-Regression $Context { $muteItem.Header -eq $label -and $tabMuteItem.Header -eq $label } "both mute menus update their unmute translation ($language)"
        }
        $Context.LanguageService.SetCurrentLanguage($savedLanguage)
        $actionsMenu.IsOpen = $false
        $model.CurrentBrowser = $other
        $actionsMenu = Open-BrowserActionsMenu $Context $window
        Assert-Regression ($muteItem.CommandParameter -eq $other -and
            $muteItem.Header -eq $Context.LanguageService.GetString('browser_muteSite')) 'actions mute menu follows selection and the new tab mute state'
        $actionsMenu.IsOpen = $false
        $model.CurrentBrowser = $source
        $actionsMenu = Open-BrowserActionsMenu $Context $window
        Assert-Regression ($muteItem.Header -eq $Context.LanguageService.GetString('browser_unmuteSite')) 'returning to the muted tab restores its unmute action'
        $actionsMenu.IsOpen = $false
        Invoke-BrowserCommand $muteItem.Command $muteItem.CommandParameter
        Assert-BrowserAudioMuted $Context $source.Browser $false 'actions unmute restores native CEF audio'
        Assert-Regression (!$source.IsMuted -and $icon.Visibility -eq [System.Windows.Visibility]::Collapsed) 'actions unmute hides the same tab mute icon'

        $model.CurrentBrowser = $other
        $otherIcon = Get-TabMuteIcon $window $other
        foreach ($addressFocus in @($false, $true)) {
            $expected = !$addressFocus
            Set-TabKeyboardFocus $Context $window -Address:$addressFocus
            $handle = (New-Object System.Windows.Interop.WindowInteropHelper($window)).Handle
            Assert-Regression ([RegressionKeyboard]::IsForegroundWindow($handle)) 'Ctrl+M targets the test window'
            try {
                [RegressionKeyboard]::SetKey(0x11, $false)
                $controlHeld = $true
                Wait-Regression $Context {
                    [System.Windows.Input.Keyboard]::Modifiers -eq [System.Windows.Input.ModifierKeys]::Control
                } 'Ctrl is held for the mute shortcut'
                [RegressionKeyboard]::PressKey(0x4D)
                Wait-Regression $Context { $other.IsMuted -eq $expected } "real Ctrl+M toggles the selected tab (address focus=$addressFocus)"
            } finally {
                if ($controlHeld) { [RegressionKeyboard]::SetKey(0x11, $true); $controlHeld = $false }
            }
            Assert-BrowserAudioMuted $Context $other.Browser $expected "Ctrl+M updates native CEF audio (address focus=$addressFocus)"
            Assert-BrowserAudioMuted $Context $source.Browser $false 'Ctrl+M leaves the background native browser unchanged'
            $visibility = if ($expected) { [System.Windows.Visibility]::Visible } else { [System.Windows.Visibility]::Collapsed }
            Assert-Regression ($model.CurrentBrowser -eq $other -and !$source.IsMuted -and
                $otherIcon.Visibility -eq $visibility) 'Ctrl+M preserves selection and updates only the selected tab icon'
            $actionsMenu = Open-BrowserActionsMenu $Context $window
            $labelKey = if ($expected) { 'browser_unmuteSite' } else { 'browser_muteSite' }
            Assert-Regression ($muteItem.CommandParameter -eq $other -and
                $muteItem.Header -eq $Context.LanguageService.GetString($labelKey)) 'actions menu follows the keyboard mute state'
            $actionsMenu.IsOpen = $false
        }

        $other.ToggleMute()
        $other.ToggleMute()
        Assert-Regression (!$other.IsMuted) 'two immediate mute toggles preserve the view model state'
        Assert-BrowserAudioMuted $Context $other.Browser $false 'two immediate mute toggles preserve native CEF audio'
        $window.ForceCloseWindow()
        $other.ToggleMute()
        Assert-Regression (!$other.IsMuted) 'mute operations ignore a closed browser without changing view model state'
    } finally {
        if ($controlHeld) { [RegressionKeyboard]::SetKey(0x11, $true) }
        if ($null -ne $actionsMenu) { $actionsMenu.IsOpen = $false }
        if ($null -ne $tabMenu) { $tabMenu.IsOpen = $false }
        if ($null -ne $window) { $window.ForceCloseWindow() }
        $Context.LanguageService.SetCurrentLanguage($savedLanguage)
        $settings.AlwaysShowTabStrip = $savedTabStrip
        $settings.DisableBrowserShortcuts = $savedShortcuts
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
        Invoke-TabMenuItem $menu 4
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
        $scroll = $tabs.Template.FindName('PART_ScrollViewer', $tabs)
        Assert-Regression ($newButton.IsVisible -and $scroll.ScrollableWidth -lt 1) 'tabs shrink to fit beside the new tab button'

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

function Assert-TabWidths($Window, [double]$ExpectedWidth, [string]$Scenario) {
    $Window.UpdateLayout()
    $tabs = $Window.FindName('tabList')
    foreach ($browser in $Window.ViewModel.Browsers) {
        $header = $tabs.ItemContainerGenerator.ContainerFromItem($browser)
        Assert-Regression ($null -ne $header -and [Math]::Abs($header.ActualWidth - $ExpectedWidth) -lt 0.01) $Scenario
    }
}

function Test-BrowserTabAdaptiveWidth($Context) {
    $window = New-RegressionWindow $Context -Width 1000
    try {
        $model = $window.ViewModel
        $first = $model.CurrentBrowser
        $second = New-RegressionTab $Context $window
        $third = New-RegressionTab $Context $window
        $tabs = $window.FindName('tabList')
        $strip = $window.FindName('tabStrip')
        $newButton = $window.FindName('newTabButton')
        $scroll = $tabs.Template.FindName('PART_ScrollViewer', $tabs)
        Assert-TabWidths $window 250 'tabs use their default width when space is available'

        $window.Width = 600
        $window.UpdateLayout()
        $width = $tabs.ItemContainerGenerator.ContainerFromItem($first).ActualWidth
        Assert-Regression ($width -gt 80 -and $width -lt 250) 'resizing shrinks tabs within the width limits'
        Assert-TabWidths $window $width 'selected and background tabs have equal widths'
        Assert-Regression ($scroll.ScrollableWidth -lt 1) 'shrinking tabs avoids unnecessary horizontal scrolling'

        $extras = @(1..3 | ForEach-Object { New-RegressionTab $Context $window })
        $window.Width = 400
        Assert-TabWidths $window 80 'overflowing tabs stop shrinking at the minimum width'
        Assert-Regression ($scroll.ScrollableWidth -gt 0) 'minimum-width tabs overflow horizontally'
        $buttonPosition = $newButton.TranslatePoint([System.Windows.Point]::new(0, 0), $strip)
        Assert-Regression ($newButton.IsVisible -and $newButton.IsEnabled -and
            $buttonPosition.X -ge 0 -and $buttonPosition.X + $newButton.ActualWidth -le $strip.ActualWidth + 0.01) 'overflow keeps the new tab button fully accessible'

        $model.CurrentBrowser = $first
        $window.UpdateLayout()
        $model.CurrentBrowser = $extras[-1]
        Wait-Regression $Context { $scroll.HorizontalOffset -gt 0 } 'selecting an overflowing tab brings it into view'
        $lastHeader = $tabs.ItemContainerGenerator.ContainerFromItem($extras[-1])
        $closeButton = Find-TabElement $lastHeader 'closeTabButton'
        Assert-Regression ($closeButton.IsVisible -and $closeButton.IsEnabled -and $closeButton.ActualWidth -eq 20) 'minimum-width tab retains its close button'
        $peer = New-Object System.Windows.Automation.Peers.ButtonAutomationPeer($closeButton)
        $peer.GetPattern([System.Windows.Automation.Peers.PatternInterface]::Invoke).Invoke()
        Wait-Regression $Context { $model.Browsers.Count -eq 5 } 'close button works at the minimum tab width'
        $peer = New-Object System.Windows.Automation.Peers.ButtonAutomationPeer($newButton)
        $peer.GetPattern([System.Windows.Automation.Peers.PatternInterface]::Invoke).Invoke()
        Wait-Regression $Context { $model.Browsers.Count -eq 6 -and $model.CurrentBrowser.IsBrowserReady() } 'new tab button works while tabs overflow'
        Assert-TabWidths $window 80 'new tabs share the minimum width while space is exhausted'

        foreach ($browser in @($model.Browsers | Where-Object { $_ -ne $first -and $_ -ne $second -and $_ -ne $third })) {
            $model.CloseBrowser($browser)
        }
        Wait-Regression $Context { $model.Browsers.Count -eq 3 } 'extra overflow tabs close'
        $window.UpdateLayout()
        $width = $tabs.ItemContainerGenerator.ContainerFromItem($first).ActualWidth
        Assert-Regression ($width -gt 80 -and $width -lt 250 -and $scroll.ScrollableWidth -lt 1) 'closing tabs restores space and removes overflow'
        Assert-TabWidths $window $width 'remaining tabs expand equally after closing tabs'

        $window.Width = 1000
        Assert-TabWidths $window 250 'enlarging the window restores the default width'
        $window.Width = 600
        $model.CloseBrowser($third)
        Wait-Regression $Context { $model.Browsers.Count -eq 2 } 'third tab closes after shrinking the window'
        Assert-TabWidths $window 250 'closing a tab restores the default width when enough space remains'
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
        foreach ($language in $Context.LanguageService.GetSupportedLanguage()) {
            $label = $Context.LanguageService.GetString($language, 'settings_alwaysShowTabStrip')
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

function Assert-TabFullscreen($Window, [string]$Scenario) {
    $Window.UpdateLayout()
    Assert-Regression ($Window.ViewModel.Fullscreen -and
        $Window.WindowStyle -eq [System.Windows.WindowStyle]::None -and
        $Window.WindowState -eq [System.Windows.WindowState]::Maximized -and
        !$Window.FindName('tabStrip').IsVisible -and !$Window.FindName('addressTextBox').IsVisible
    ) "$Scenario preserves window fullscreen and hides browser chrome"
}

function Test-BrowserTabFullscreen($Context) {
    $savedSizeInfo = [CefFlashBrowser.Data.GlobalData]::Settings.BrowserWindowSizeInfo
    foreach ($initialState in @([System.Windows.WindowState]::Normal, [System.Windows.WindowState]::Maximized)) {
        $window = New-RegressionWindow $Context
        try {
            $model = $window.ViewModel
            $first = $model.CurrentBrowser
            $firstView = Find-BrowserView $window $first
            $window.WindowState = $initialState
            Invoke-TabShortcut $window F11 ([System.Windows.Input.ModifierKeys]::None)
            Assert-TabFullscreen $window "F11 from $initialState"
            Invoke-TabShortcut $window T Control
            Wait-Regression $Context { $model.Browsers.Count -eq 2 -and $model.CurrentBrowser.IsBrowserReady() } 'fullscreen Ctrl+T initializes a new tab'
            $second = $model.CurrentBrowser
            Assert-TabFullscreen $window 'Ctrl+T'
            Invoke-ViewCallback $firstView 'OnFullScreenChanged' @($false)
            Assert-TabFullscreen $window 'late fullscreen exit from the previous tab'
            Invoke-TabShortcut $window K ([System.Windows.Input.ModifierKeys]::Control -bor [System.Windows.Input.ModifierKeys]::Shift)
            Wait-Regression $Context { $model.Browsers.Count -eq 3 -and $model.CurrentBrowser.IsBrowserReady() } 'fullscreen duplication initializes a new tab'
            $duplicate = $model.CurrentBrowser
            Assert-TabFullscreen $window 'duplicating a tab'
            Invoke-TabShortcut $window Tab Control
            Assert-Regression ($model.CurrentBrowser -eq $first) 'fullscreen Ctrl+Tab selects the next tab'
            Assert-TabFullscreen $window 'Ctrl+Tab'
            Invoke-TabShortcut $window Tab ([System.Windows.Input.ModifierKeys]::Control -bor [System.Windows.Input.ModifierKeys]::Shift)
            Assert-Regression ($model.CurrentBrowser -eq $duplicate) 'fullscreen Ctrl+Shift+Tab selects the previous tab'
            Assert-TabFullscreen $window 'Ctrl+Shift+Tab'
            $model.CloseBrowser($duplicate)
            Wait-Regression $Context { $model.Browsers.Count -eq 2 } 'fullscreen current tab closes'
            Assert-Regression ($model.CurrentBrowser -eq $second) 'fullscreen close selects the neighboring tab'
            Assert-TabFullscreen $window 'closing the selected tab'
            Invoke-TabShortcut $window F11 ([System.Windows.Input.ModifierKeys]::None)
            Assert-Regression (!$model.Fullscreen -and
                $window.WindowStyle -eq [System.Windows.WindowStyle]::SingleBorderWindow -and
                $window.WindowState -eq $initialState -and $window.FindName('tabStrip').IsVisible -and
                $window.FindName('addressTextBox').IsVisible) "F11 restores browser chrome and the original $initialState state after tab changes"
            $model.CurrentBrowser = $first
            Invoke-TabShortcut $window T Control
            Wait-Regression $Context { $model.Browsers.Count -eq 3 -and $model.CurrentBrowser.IsBrowserReady() } 'normal-window new tab initializes'
            Assert-Regression (!$model.Fullscreen -and $window.WindowState -eq $initialState) 'tab changes outside fullscreen preserve the window state'
        } finally {
            $window.ForceCloseWindow()
            [CefFlashBrowser.Data.GlobalData]::Settings.BrowserWindowSizeInfo = $savedSizeInfo
        }
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
        Assert-Regression ($model.Fullscreen -and $first.Address -eq 'about:blank') 'foreground tab preserves fullscreen and the source page'
        Assert-TabFullscreen $window 'foreground new-page routing'
        Assert-Regression ($model.Browsers[0] -eq $first -and $model.Browsers[1] -eq $foreground -and $model.Browsers[2] -eq $background -and $model.Browsers[3] -eq $last) 'repeated page requests insert the newest tab immediately after their source'
        Assert-Regression ($foreground.Address -eq $Context.AuxiliarySecondAddress -and $background.Address -eq $Context.AuxiliaryAddress) 'new page requests retain their target addresses'

        Wait-Regression $Context { $background.IsBrowserReady() } 'background source tab initialization'
        $backgroundView = Find-BrowserView $window $background
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
        Assert-Regression (!$model.ShowFindPopup -and $model.Fullscreen) 'switching tabs resets find and preserves fullscreen'
        Assert-TabFullscreen $window 'selecting a new tab'
        Assert-Regression (!$firstView.IsVisible -and $secondView.IsVisible) 'only the selected tab is visible'
        Assert-Regression (!$firstBrowser.IsDisposed -and $first.IntegratedDevToolsHandle -eq $toolsHandle) 'hidden tab retains browser and DevTools'
        $model.CurrentBrowser = $first
        Wait-Regression $Context { $window.Title -eq 'Auxiliary regression' } 'title follows selected tab'
        Assert-Regression ([object]::ReferenceEquals((Find-BrowserView $window $first), $firstView)) 'switching preserves the BrowserView instance'
        Assert-Regression ((Invoke-JavaScript $Context $firstBrowser 'window.preservedTabValue') -eq 42) 'switching preserves JavaScript state'
        Assert-Regression ($firstView.IsVisible -and !$secondView.IsVisible) 'switching restores the original view'
        Assert-TabFullscreen $window 'selecting an existing tab'
        $third = New-RegressionTab $Context $window
        $model.CloseBrowser($second)
        Wait-Regression $Context { $model.Browsers.Count -eq 2 } 'background tab closes'
        Assert-Regression ([object]::ReferenceEquals($model.CurrentBrowser, $third)) 'closing background tab preserves selection'
        Assert-Regression $second.Browser.IsDisposed 'background tab releases CEF'
        $model.CurrentBrowser = $first
        $model.CloseBrowser($first)
        Wait-Regression $Context { $model.Browsers.Count -eq 1 } 'selected tab closes'
        Assert-Regression ([object]::ReferenceEquals($model.CurrentBrowser, $third)) 'closing selected tab selects its right neighbor'
        Assert-TabFullscreen $window 'closing a tab with a right neighbor'
        Assert-Regression ($firstBrowser.IsDisposed -and $first.IntegratedDevToolsHandle -eq [IntPtr]::Zero) 'tab close releases its browser and DevTools'
        $fourth = New-RegressionTab $Context $window
        $model.CloseBrowser($fourth)
        Wait-Regression $Context { $model.Browsers.Count -eq 1 } 'last-position tab closes'
        Assert-Regression ([object]::ReferenceEquals($model.CurrentBrowser, $third)) 'closing final-position tab selects its left neighbor'
        Assert-TabFullscreen $window 'closing a tab with a left neighbor'
        $closeBinding = $window.InputBindings | Where-Object {
            $_.Key -eq [System.Windows.Input.Key]::W -and $_.Modifiers -eq [System.Windows.Input.ModifierKeys]::Control
        }
        Invoke-BrowserCommand $closeBinding.Command $closeBinding.CommandParameter
        Wait-Regression $Context { !$window.IsVisible } 'Ctrl+W on the final tab closes its window'
        Assert-Regression ($null -eq $model.CurrentBrowser -and !$model.Fullscreen -and $third.Browser.IsDisposed) 'final tab close clears selection and fullscreen and releases CEF'
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
        $draggedField = $tabs.GetType().GetField('_draggedItem', [Reflection.BindingFlags]'Instance,NonPublic')
        $draggedField.SetValue($tabs, $third)
        $firstHeader = $tabs.ItemContainerGenerator.ContainerFromItem($first)
        $point = $firstHeader.TranslatePoint([System.Windows.Point]::new(1, 10), $tabs)
        Invoke-ViewCallback $tabs 'UpdateTabDrag' @($point, $false)
        Assert-Regression ($model.Browsers[0] -eq $third -and $model.CurrentBrowser -eq $third) 'dragging past the left midpoint moves the tab left'
        $window.UpdateLayout()
        $secondHeader = $tabs.ItemContainerGenerator.ContainerFromItem($second)
        $point = $secondHeader.TranslatePoint([System.Windows.Point]::new($secondHeader.ActualWidth - 1, 10), $tabs)
        Invoke-ViewCallback $tabs 'UpdateTabDrag' @($point, $false)
        Assert-Regression ($model.Browsers[2] -eq $third) 'dragging past the right midpoint moves the tab right'
        Invoke-ViewCallback $tabs 'UpdateTabDrag' @([System.Windows.Point]::new(0, -10), $false)
        Assert-Regression ($model.Browsers[2] -eq $third) 'dragging outside the tab strip leaves the order unchanged'

        $extras = @(1..3 | ForEach-Object { New-RegressionTab $Context $window })
        $model.CurrentBrowser = $third
        $window.Width = 400
        $window.UpdateLayout()
        $scroll = $tabs.Template.FindName('PART_ScrollViewer', $tabs)
        $scroll.ScrollToLeftEnd()
        $window.UpdateLayout()
        $offset = $scroll.HorizontalOffset
        $point = $scroll.TranslatePoint([System.Windows.Point]::new($scroll.ActualWidth - 1, 10), $tabs)
        Invoke-ViewCallback $tabs 'UpdateTabDrag' @($point, $true)
        Assert-Regression ($scroll.HorizontalOffset -gt $offset) 'dragging at the right edge scrolls overflowing tabs'
        $point = $scroll.TranslatePoint([System.Windows.Point]::new(1, 10), $tabs)
        Invoke-ViewCallback $tabs 'UpdateTabDrag' @($point, $true)
        Assert-Regression ($scroll.HorizontalOffset -eq 0) 'dragging at the left edge scrolls back'
        $draggedField.SetValue($tabs, $null)

        foreach ($browser in $extras) { $model.CloseBrowser($browser) }
        Wait-Regression $Context { $model.Browsers.Count -eq 3 } 'extra drag overflow tabs close'
        $model.MoveBrowser($third, 2)
        $model.CloseBrowser($third)
        Wait-Regression $Context { $model.Browsers.Count -eq 2 } 'reordered selected tab closes'
        Assert-Regression ($model.CurrentBrowser -eq $second) 'closing a reordered tab selects its current neighbor'
    } finally {
        $window.ForceCloseWindow()
    }
}

function Assert-TabDragPreview($Window, $Browser, [string]$Scenario) {
    $tabs = $Window.FindName('tabList')
    $draggedField = $tabs.GetType().GetField('_draggedItem', [Reflection.BindingFlags]'Instance,NonPublic')
    $draggedField.SetValue($tabs, $Browser)
    try {
        Invoke-ViewCallback $tabs 'ShowTabDragPreview' @()
        $snapshot = $tabs.Template.FindName('PART_DragPreviewImage', $tabs).Source
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
        Invoke-ViewCallback $tabs 'HideTabDragPreview' @()
        $draggedField.SetValue($tabs, $null)
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
        1..3 | ForEach-Object { New-RegressionTab $Context $window | Out-Null }
        $model.MoveBrowser($third, $model.Browsers.Count - 1)
        $model.CurrentBrowser = $third
        $window.Width = 400
        $window.UpdateLayout()
        $tabs = $window.FindName('tabList')
        $tabs.ScrollIntoView($third)
        $window.UpdateLayout()
        $scroll = $tabs.Template.FindName('PART_ScrollViewer', $tabs)
        Assert-Regression ($scroll.HorizontalOffset -gt 0) 'preview check covers a scrolled tab strip'
        Assert-TabDragPreview $window $third 'last tab after horizontal scrolling'
        Assert-Regression (!$tabs.Template.FindName('PART_DragPreview', $tabs).IsOpen -and $null -eq $tabs.DraggedItem) 'preview cleanup restores the tab strip'
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
