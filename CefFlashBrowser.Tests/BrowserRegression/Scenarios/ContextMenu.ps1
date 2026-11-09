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
                    Assert-Regression ($handled -and !$model.Fullscreen) "$commandName handles the command and exits fullscreen ($behavior)"
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
    $savedLanguage = [CefFlashBrowser.Data.GlobalData]::Settings.Language
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
        foreach ($language in [CefFlashBrowser.Services.LanguageManager]::GetSupportedLanguage()) {
            [CefFlashBrowser.Data.GlobalData]::Settings.Language = $language
            $parameters = New-Object RegressionContextMenuParamsProxy
            $parameters.LinkUrl = 'https://example.com'
            $menu = New-Object RegressionMenuModelProxy
            $handler.OnBeforeContextMenu($null, $null, $null, $parameters.GetTransparentProxy(), $menu.GetTransparentProxy())
            Assert-Regression (![string]::IsNullOrWhiteSpace($menu.Labels[0]) -and
                $menu.Labels[0] -eq [CefFlashBrowser.Services.LanguageManager]::GetString('browser_openInNewTab')) "new tab menu has a localized label ($language)"
        }
        [CefFlashBrowser.Data.GlobalData]::Settings.DisableBrowserContextMenu = $true
        $handler.OnBeforeContextMenu($null, $null, $null, $parameters.GetTransparentProxy(), $menu.GetTransparentProxy())
        Assert-Regression ($menu.Commands.Count -eq 0) 'disabling context menus removes every item'
    } finally {
        [CefFlashBrowser.Data.GlobalData]::Settings.DisableBrowserContextMenu = $savedDisabled
        [CefFlashBrowser.Data.GlobalData]::Settings.Language = $savedLanguage
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

function Find-MuteTabIcon($Element) {
    if ($Element -is [SharpVectors.Converters.SvgIcon] -and "$($Element.UriSource)" -like '*mute.svg*') {
        return $Element
    }

    for ($index = 0; $index -lt [System.Windows.Media.VisualTreeHelper]::GetChildrenCount($Element); $index++) {
        $icon = Find-MuteTabIcon ([System.Windows.Media.VisualTreeHelper]::GetChild($Element, $index))
        if ($null -ne $icon) {
            return $icon
        }
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
    [CefFlashBrowser.Data.GlobalData]::Settings.DisableBrowserContextMenu = $false
    [CefFlashBrowser.Data.GlobalData]::Settings.AlwaysShowTabStrip = $true
    $window = New-RegressionWindow $Context
    try {
        $model = $window.ViewModel
        $source = $model.CurrentBrowser
        $browser = $source.Browser
        $mutePage = [CefFlashBrowser.Browser.Handlers.ContextMenuHandler]::MutePage

        Assert-Regression (!$browser.IsMuted) 'tab starts unmuted'

        $model.ToggleMute($source)
        Wait-Regression $Context {
            $source.Browser.IsMuted
        } 'ToggleMute mutes the browser and updates IsMuted'

        $icon = Find-MuteTabIcon $window
        Assert-Regression ($null -ne $icon -and $icon.Visibility -eq [System.Windows.Visibility]::Visible) 'muted tab shows the mute icon'

        $muteButton = Find-ToolbarMuteButton $window $model.ToggleMuteCommand
        Assert-Regression ($null -ne $muteButton -and
            "$($muteButton.Content.UriSource)" -like '*mute.svg*' -and
            $muteButton.ToolTip -eq [CefFlashBrowser.Services.LanguageManager]::GetString('browser_unmuteSite')) 'muted toolbar shows the muted icon and unmute tooltip'

        $handler = $browser.MenuHandler.PSObject.BaseObject
        $parameters = New-Object RegressionContextMenuParamsProxy
        $menu = New-Object RegressionMenuModelProxy
        $unmutedBrowser = (New-Object RegressionAudioBrowserProxy((New-Object RegressionAudioHostProxy($false)).GetTransparentProxy())).GetTransparentProxy()
        $handler.OnBeforeContextMenu($null, $unmutedBrowser, $null, $parameters.GetTransparentProxy(), $menu.GetTransparentProxy())
        Assert-Regression ($menu.Commands.Count -eq 3 -and $menu.Commands[0] -eq $mutePage -and
            $menu.Commands[1] -eq [CefSharp.CefMenuCommand]::NotFound -and
            $menu.Labels[0] -eq [CefFlashBrowser.Services.LanguageManager]::GetString('browser_muteSite') -and
            !$menu.Checked.Contains($mutePage)) 'context menu shows the localized unmuted mute entry'

        $model.ToggleMute($source)
        Wait-Regression $Context {
            !$source.Browser.IsMuted
        } 'ToggleMute restores audio and clears IsMuted'

        $icon = Find-MuteTabIcon $window
        Assert-Regression ($null -ne $icon -and $icon.Visibility -eq [System.Windows.Visibility]::Collapsed) 'unmuted tab hides the mute icon'

        $muteButton = Find-ToolbarMuteButton $window $model.ToggleMuteCommand
        Assert-Regression ($null -ne $muteButton -and
            "$($muteButton.Content.UriSource)" -like '*volume.svg*' -and
            $muteButton.ToolTip -eq [CefFlashBrowser.Services.LanguageManager]::GetString('browser_muteSite')) 'unmuted toolbar shows the volume icon and mute tooltip'

        Invoke-BrowserCommand $muteButton.Command $muteButton.CommandParameter
        Wait-Regression $Context {
            $source.Browser.IsMuted
        } 'toolbar button mutes through its command'

        $menu = New-Object RegressionMenuModelProxy
        $mutedBrowser = (New-Object RegressionAudioBrowserProxy((New-Object RegressionAudioHostProxy($true)).GetTransparentProxy())).GetTransparentProxy()
        $handler.OnBeforeContextMenu($null, $mutedBrowser, $null, $parameters.GetTransparentProxy(), $menu.GetTransparentProxy())
        Assert-Regression ($menu.Commands[0] -eq $mutePage -and
            $menu.Labels[0] -eq [CefFlashBrowser.Services.LanguageManager]::GetString('browser_unmuteSite') -and
            $menu.Checked.Contains($mutePage)) 'context menu shows the localized muted entry as checked'

        Assert-Regression (Invoke-ContextMenuCommand $browser $mutePage) 'unmute context menu command is handled'
        Wait-Regression $Context {
            !$source.Browser.IsMuted
        } 'context menu command restores audio'
    } finally {
        $window.ForceCloseWindow()
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
