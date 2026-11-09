function Test-ContextMenuRouting($Context) {
    $savedBehavior = [CefFlashBrowser.Data.GlobalData]::Settings.NewPageBehavior
    $savedEngine = [CefFlashBrowser.Data.GlobalData]::Settings.SearchEngine
    try {
        [CefFlashBrowser.Data.GlobalData]::Settings.SearchEngine = [CefFlashBrowser.Models.SearchEngine]::DuckDuckGo
        $selection = 'context menu & + search'
        $searchUrl = [CefFlashBrowser.Utils.UrlHelper]::GetSearchUrl(
            [CefFlashBrowser.Data.GlobalData]::Settings.SearchEngine, $selection)
        foreach ($behavior in [Enum]::GetValues([CefFlashBrowser.Models.NewPageBehavior])) {
            [CefFlashBrowser.Data.GlobalData]::Settings.NewPageBehavior = $behavior
            foreach ($commandName in @('Search', 'OpenSelectedUrl', 'OpenInNewTab', 'OpenInNewWindow')) {
                $beforeWindows = @($Context.Application.Windows)
                $window = New-RegressionWindow $Context
                try {
                    $model = $window.ViewModel
                    $source = $model.CurrentBrowser
                    $last = New-RegressionTab $Context $window
                    $model.CurrentBrowser = $source
                    $command = [CefFlashBrowser.Browser.Handlers.ContextMenuHandler].GetField($commandName).GetRawConstantValue()
                    $url = if ($commandName -eq 'Search') { $searchUrl } else { $Context.AuxiliaryAddress }
                    $text = if ($commandName -eq 'Search') { $selection } else { $url }
                    $effectiveBehavior = switch ($commandName) {
                        'OpenInNewTab' { [CefFlashBrowser.Models.NewPageBehavior]::NewTab }
                        'OpenInNewWindow' { [CefFlashBrowser.Models.NewPageBehavior]::NewWindow }
                        default { $behavior }
                    }
                    $model.ToggleFullscreen($source)
                    $handled = Invoke-ContextMenuCommand $source.Browser $command $text $url
                    $preserveFullscreen = $effectiveBehavior -eq [CefFlashBrowser.Models.NewPageBehavior]::NewTab
                    Assert-Regression ($handled -and $model.Fullscreen -eq $preserveFullscreen) "$commandName applies fullscreen behavior for $effectiveBehavior ($behavior)"
                    if ($preserveFullscreen) {
                        Assert-TabFullscreen $window "$commandName ($behavior)"
                    }
                    if ($effectiveBehavior -eq [CefFlashBrowser.Models.NewPageBehavior]::NewTab) {
                        Assert-Regression ($model.Browsers.Count -eq 3 -and $model.Browsers[1] -eq $model.CurrentBrowser -and
                            $model.CurrentBrowser.Address -eq $url -and $model.Browsers[0] -eq $source -and
                            $model.Browsers[2] -eq $last -and $source.Address -eq 'about:blank' -and
                            $Context.Application.Windows.Count -eq $beforeWindows.Count + 1) "$commandName inserts and selects a tab immediately after its source ($behavior)"
                    } elseif ($effectiveBehavior -eq [CefFlashBrowser.Models.NewPageBehavior]::OriginalWindow) {
                        Assert-Regression ($model.Browsers.Count -eq 2 -and $model.CurrentBrowser -eq $source -and
                            $source.Address -eq $url -and $last.Address -eq 'about:blank' -and
                            $Context.Application.Windows.Count -eq $beforeWindows.Count + 1) "$commandName navigates only its source tab ($behavior)"
                    } else {
                        $child = @($Context.Application.Windows | Where-Object { $_ -ne $window -and !$beforeWindows.Contains($_) })
                        Assert-Regression ($child.Count -eq 1 -and $child[0] -is [CefFlashBrowser.Views.BrowserWindow] -and
                            $child[0].ViewModel.CurrentBrowser.Address -eq $url -and $model.Browsers.Count -eq 2 -and
                            $model.CurrentBrowser -eq $source -and $source.Address -eq 'about:blank') "$commandName opens a browser window and preserves the source tab ($behavior)"
                        $child[0].ForceCloseWindow()
                    }
                    $window.ForceCloseWindow()
                    $windowCount = $Context.Application.Windows.Count
                    Assert-Regression (!(Invoke-ContextMenuCommand $source.Browser $command $text $url) -and
                        $Context.Application.Windows.Count -eq $windowCount) "$commandName ignores callbacks from a closed browser ($behavior)"
                } finally {
                    foreach ($created in @($Context.Application.Windows | Where-Object { !$beforeWindows.Contains($_) })) {
                        $created.ForceCloseWindow()
                    }
                }
            }
        }
    } finally {
        [CefFlashBrowser.Data.GlobalData]::Settings.NewPageBehavior = $savedBehavior
        [CefFlashBrowser.Data.GlobalData]::Settings.SearchEngine = $savedEngine
    }
}

function Test-ContextMenuItems($Context) {
    $savedDisabled = [CefFlashBrowser.Data.GlobalData]::Settings.DisableBrowserContextMenu
    $savedLanguage = $Context.LanguageService.CurrentLanguage
    $handler = New-Object CefFlashBrowser.Browser.Handlers.ContextMenuHandler
    try {
        [CefFlashBrowser.Data.GlobalData]::Settings.DisableBrowserContextMenu = $false
        $newTab = [CefFlashBrowser.Browser.Handlers.ContextMenuHandler]::OpenInNewTab
        $newWindow = [CefFlashBrowser.Browser.Handlers.ContextMenuHandler]::OpenInNewWindow
        $search = [CefFlashBrowser.Browser.Handlers.ContextMenuHandler]::Search
        $selectedUrl = [CefFlashBrowser.Browser.Handlers.ContextMenuHandler]::OpenSelectedUrl
        $separator = [CefSharp.CefMenuCommand]::NotFound
        $copy = [CefSharp.CefMenuCommand]::Copy
        foreach ($scenario in @(
            @{ Name = 'plain page'; Text = ''; Link = ''; Expected = @($copy) },
            @{ Name = 'link'; Text = ''; Link = 'https://example.com'; Expected = @($newTab, $newWindow, $separator, $copy) },
            @{ Name = 'selected text'; Text = 'search text'; Link = ''; Expected = @($search, $separator, $copy) },
            @{ Name = 'selected URL'; Text = 'https://example.com'; Link = ''; Expected = @($selectedUrl, $search, $separator, $copy) },
            @{ Name = 'link and selected URL'; Text = 'https://example.com'; Link = 'https://example.org'; Expected = @($newTab, $newWindow, $selectedUrl, $search, $separator, $copy) }
        )) {
            $parameters = New-Object RegressionContextMenuParamsProxy
            $parameters.SelectionText = $scenario.Text
            $parameters.LinkUrl = $scenario.Link
            $menu = New-Object RegressionMenuModelProxy
            $handler.OnBeforeContextMenu($null, $null, $null, $parameters.GetTransparentProxy(), $menu.GetTransparentProxy())
            Assert-Regression (($menu.Commands -join ',') -eq ($scenario.Expected -join ',')) "context menu commands and separator match $($scenario.Name)"
        }
        foreach ($language in $Context.LanguageService.GetSupportedLanguage()) {
            $Context.LanguageService.SetCurrentLanguage($language)
            $parameters = New-Object RegressionContextMenuParamsProxy
            $parameters.LinkUrl = 'https://example.com'
            $menu = New-Object RegressionMenuModelProxy
            $handler.OnBeforeContextMenu($null, $null, $null, $parameters.GetTransparentProxy(), $menu.GetTransparentProxy())
            Assert-Regression (![string]::IsNullOrWhiteSpace($menu.Labels[0]) -and
                $menu.Labels[0] -eq $Context.LanguageService.GetString('browser_openInNewTab')) "new tab menu has a localized label ($language)"
        }
        [CefFlashBrowser.Data.GlobalData]::Settings.DisableBrowserContextMenu = $true
        $handler.OnBeforeContextMenu($null, $null, $null, $parameters.GetTransparentProxy(), $menu.GetTransparentProxy())
        Assert-Regression ($menu.Commands.Count -eq 0) 'disabling context menus removes every item'
    } finally {
        [CefFlashBrowser.Data.GlobalData]::Settings.DisableBrowserContextMenu = $savedDisabled
        $Context.LanguageService.SetCurrentLanguage($savedLanguage)
    }
}

function Test-AuxiliaryContextMenu($Context) {
    $savedBehavior = [CefFlashBrowser.Data.GlobalData]::Settings.NewPageBehavior
    try {
        foreach ($auxType in $Context.AuxiliaryTypes) {
            $beforeWindows = @($Context.Application.Windows)
            $window = New-AuxiliaryWindow $Context $auxType -WaitForLoad
            try {
                $source = $window.FindName('browserView').ViewModel
                $originalAddress = $source.Address
                $mutePage = [CefFlashBrowser.Browser.Handlers.ContextMenuHandler]::MutePage
                Assert-Regression (Invoke-ContextMenuCommand $source.Browser $mutePage) "$($auxType.Name) handles the mute menu command"
                Assert-Regression $source.IsMuted "$($auxType.Name) stores mute state in its view model"
                Assert-BrowserAudioMuted $Context $source.Browser $true "$($auxType.Name) mutes its native CEF browser"
                Assert-Regression (Invoke-ContextMenuCommand $source.Browser $mutePage) "$($auxType.Name) handles the unmute menu command"
                Assert-Regression (!$source.IsMuted) "$($auxType.Name) clears its view model mute state"
                Assert-BrowserAudioMuted $Context $source.Browser $false "$($auxType.Name) restores its native CEF audio"
                foreach ($commandName in @('Search', 'OpenSelectedUrl', 'OpenInNewTab')) {
                    [CefFlashBrowser.Data.GlobalData]::Settings.NewPageBehavior = if ($commandName -eq 'OpenInNewTab') {
                        [CefFlashBrowser.Models.NewPageBehavior]::NewTab
                    } else { [CefFlashBrowser.Models.NewPageBehavior]::OriginalWindow }
                    $command = [CefFlashBrowser.Browser.Handlers.ContextMenuHandler].GetField($commandName).GetRawConstantValue()
                    $url = if ($commandName -eq 'Search') {
                        [CefFlashBrowser.Utils.UrlHelper]::GetSearchUrl([CefFlashBrowser.Data.GlobalData]::Settings.SearchEngine, 'auxiliary search')
                    } else { $Context.AuxiliaryAddress }
                    $text = if ($commandName -eq 'Search') { 'auxiliary search' } else { $url }
                    $handled = Invoke-ContextMenuCommand $source.Browser $command $text $url
                    $child = @($Context.Application.Windows | Where-Object { $_ -ne $window -and !$beforeWindows.Contains($_) })
                    Assert-Regression ($handled -and $child.Count -eq 1 -and
                        $child[0] -is [CefFlashBrowser.Views.BrowserWindow] -and
                        $child[0].ViewModel.CurrentBrowser.Address -eq $url -and $source.Address -eq $originalAddress) "$($auxType.Name) opens $commandName in a browser window"
                    $child[0].ForceCloseWindow()
                }
            } finally {
                foreach ($created in @($Context.Application.Windows | Where-Object { !$beforeWindows.Contains($_) })) {
                    $created.ForceCloseWindow()
                }
            }
        }
    } finally {
        [CefFlashBrowser.Data.GlobalData]::Settings.NewPageBehavior = $savedBehavior
    }
}

function Find-ToolbarMuteButton($Element, $Command) {
    if ($Element -is [System.Windows.Controls.Button] -and
        [object]::ReferenceEquals($Element.Command, $Command)) {
        return $Element
    }

    for ($index = 0; $index -lt [System.Windows.Media.VisualTreeHelper]::GetChildrenCount($Element); $index++) {
        $button = Find-ToolbarMuteButton ([System.Windows.Media.VisualTreeHelper]::GetChild($Element, $index)) $Command
        if ($null -ne $button) {
            return $button
        }
    }
}

function Test-ContextMenuMute($Context) {
    $savedDisabled = [CefFlashBrowser.Data.GlobalData]::Settings.DisableBrowserContextMenu
    $savedTabStrip = [CefFlashBrowser.Data.GlobalData]::Settings.AlwaysShowTabStrip
    $window = $null
    try {
        [CefFlashBrowser.Data.GlobalData]::Settings.DisableBrowserContextMenu = $false
        [CefFlashBrowser.Data.GlobalData]::Settings.AlwaysShowTabStrip = $true
        $window = New-RegressionWindow $Context
        $model = $window.ViewModel
        $source = $model.CurrentBrowser
        $browser = $source.Browser
        $mutePage = [CefFlashBrowser.Browser.Handlers.ContextMenuHandler]::MutePage
        $icon = Get-TabMuteIcon $window $source
        Assert-SvgIconLoaded $icon 'tab mute SVG loads a nonempty drawing from the application resources'

        Assert-Regression (!$source.IsMuted -and $icon.Visibility -eq [System.Windows.Visibility]::Collapsed) 'tab starts unmuted with its mute icon hidden'
        Assert-BrowserAudioMuted $Context $browser $false 'tab starts with native CEF audio unmuted'

        $model.ToggleMute($source)
        Wait-Regression $Context {
            $source.IsMuted
        } 'ToggleMute mutes the browser and updates IsMuted'
        Assert-BrowserAudioMuted $Context $browser $true 'view model mute state is applied to the native CEF browser'

        Assert-Regression ($icon.Visibility -eq [System.Windows.Visibility]::Visible) 'muted tab shows the same tab mute icon'

        $muteButton = Find-ToolbarMuteButton $window $model.ToggleMuteCommand
        Assert-Regression ($null -ne $muteButton -and
            "$($muteButton.Content.UriSource)" -like '*mute.svg*' -and
            $muteButton.ToolTip -eq $Context.LanguageService.GetString('browser_unmuteSite')) 'muted toolbar shows the muted icon and unmute tooltip'
        Assert-SvgIconLoaded $muteButton.Content 'toolbar mute SVG loads a nonempty drawing from the application resources'

        $handler = $browser.MenuHandler.PSObject.BaseObject
        $parameters = New-Object RegressionContextMenuParamsProxy
        $menu = New-Object RegressionMenuModelProxy
        $unmutedBrowser = (New-Object RegressionAudioBrowserProxy((New-Object RegressionAudioHostProxy($false)).GetTransparentProxy())).GetTransparentProxy()
        $handler.OnBeforeContextMenu($null, $unmutedBrowser, $null, $parameters.GetTransparentProxy(), $menu.GetTransparentProxy())
        Assert-Regression ($menu.Commands.Count -eq 3 -and $menu.Commands[0] -eq $mutePage -and
            $menu.Commands[1] -eq [CefSharp.CefMenuCommand]::NotFound -and
            $menu.Labels[0] -eq $Context.LanguageService.GetString('browser_muteSite') -and
            !$menu.Checked.Contains($mutePage)) 'context menu shows the localized unmuted mute entry'

        $model.ToggleMute($source)
        Wait-Regression $Context {
            !$source.IsMuted
        } 'ToggleMute restores audio and clears IsMuted'
        Assert-BrowserAudioMuted $Context $browser $false 'view model unmute state is applied to the native CEF browser'

        Assert-Regression ($icon.Visibility -eq [System.Windows.Visibility]::Collapsed) 'unmuted tab hides the same tab mute icon'

        $muteButton = Find-ToolbarMuteButton $window $model.ToggleMuteCommand
        Assert-Regression ($null -ne $muteButton -and
            "$($muteButton.Content.UriSource)" -like '*volume.svg*' -and
            $muteButton.ToolTip -eq $Context.LanguageService.GetString('browser_muteSite')) 'unmuted toolbar shows the volume icon and mute tooltip'
        Assert-SvgIconLoaded $muteButton.Content 'toolbar volume SVG loads a nonempty drawing from the application resources'
        Assert-Regression ($muteButton.CommandParameter -eq $source) 'toolbar mute command targets the selected tab'

        Invoke-BrowserCommand $muteButton.Command $muteButton.CommandParameter
        Wait-Regression $Context {
            $source.IsMuted
        } 'toolbar button mutes through its command'
        Assert-BrowserAudioMuted $Context $browser $true 'toolbar mute command changes native CEF audio'

        $menu = New-Object RegressionMenuModelProxy
        $mutedBrowser = (New-Object RegressionAudioBrowserProxy((New-Object RegressionAudioHostProxy($true)).GetTransparentProxy())).GetTransparentProxy()
        $handler.OnBeforeContextMenu($null, $mutedBrowser, $null, $parameters.GetTransparentProxy(), $menu.GetTransparentProxy())
        Assert-Regression ($menu.Commands[0] -eq $mutePage -and
            $menu.Labels[0] -eq $Context.LanguageService.GetString('browser_unmuteSite') -and
            $menu.Checked.Contains($mutePage)) 'context menu shows the localized muted entry as checked'

        Assert-Regression (Invoke-ContextMenuCommand $browser $mutePage) 'unmute context menu command is handled'
        Wait-Regression $Context {
            !$source.IsMuted
        } 'context menu command restores audio'
        Assert-BrowserAudioMuted $Context $browser $false 'page context menu restores native CEF audio'

        $source.ToggleMute()
        $source.ToggleMute()
        Assert-Regression (!$source.IsMuted) 'two immediate mute toggles preserve the view model state'
        Assert-BrowserAudioMuted $Context $browser $false 'two immediate mute toggles preserve native CEF audio'

        $window.ForceCloseWindow()
        $source.ToggleMute()
        Assert-Regression (!$source.IsMuted -and !(Invoke-ContextMenuCommand $browser $mutePage)) 'mute operations ignore a closed browser without changing view model state'
    } finally {
        if ($null -ne $window) { $window.ForceCloseWindow() }
        [CefFlashBrowser.Data.GlobalData]::Settings.DisableBrowserContextMenu = $savedDisabled
        [CefFlashBrowser.Data.GlobalData]::Settings.AlwaysShowTabStrip = $savedTabStrip
    }
}

function Test-ContextMenu($Context) {
    Test-ContextMenuRouting $Context
    Test-ContextMenuItems $Context
    Test-ContextMenuMute $Context
    Test-AuxiliaryContextMenu $Context
}
