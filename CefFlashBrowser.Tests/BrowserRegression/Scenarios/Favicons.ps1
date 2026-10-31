function Find-FaviconElements($Element, [Type]$ElementType) {
    if ($Element -is $ElementType) {
        Write-Output $Element
    }
    for ($index = 0; $index -lt [System.Windows.Media.VisualTreeHelper]::GetChildrenCount($Element); $index++) {
        Find-FaviconElements ([System.Windows.Media.VisualTreeHelper]::GetChild($Element, $index)) $ElementType
    }
}

function Test-Favicons($Context) {
    $fixture = New-Object FaviconHttpFixture
    $window = New-RegressionWindow $Context
    $browser = $window.ViewModel.CurrentBrowser.Browser
    $service = $Context.FaviconService
    $manager = $null
    $mainWindow = $null
    $actionsMenu = $null
    $iconWindow = $null
    $originalFavorites = @([CefFlashBrowser.Data.GlobalData]::Favorites)
    $pageA = $fixture.BaseUrl + '/a'
    $pageB = $fixture.BaseUrl + '/b'
    $iconA = $fixture.BaseUrl + '/a-icon.png'
    $iconB = $fixture.BaseUrl + '/b-icon.png'
    try {
        Invoke-BrowserCommand $browser.LoadUrlCommand $pageA
        Wait-Regression $Context { $service.GetIconUrl($pageA) -eq $iconA } 'browsing an unsaved page records its reported favicon'
        Invoke-BrowserCommand $browser.LoadUrlCommand ($fixture.BaseUrl + '/redirect')
        Wait-Regression $Context { $service.GetIconUrl($pageB) -eq $iconB } 'redirect records the final page favicon'
        Assert-Regression ($null -eq $service.GetIconUrl($fixture.BaseUrl + '/redirect')) 'redirect does not create an alias for an unobserved URL'

        $favorites = [CefFlashBrowser.Data.GlobalData]::Favorites
        $favorites.Clear()
        $favorite = New-Object CefFlashBrowser.Models.Website('Page A', $pageA)
        $favorites.Add($favorite)
        $favorites.Add((New-Object CefFlashBrowser.Models.Website('Old favorite', 'https://unvisited.invalid/')))
        $manager = New-Object CefFlashBrowser.Views.FavoritesManager
        $manager.Left = -10000
        $manager.ShowInTaskbar = $false
        $manager.ShowActivated = $false
        $manager.Show()
        $list = @(Find-FaviconElements $manager ([System.Windows.Controls.ListBox]))[0]
        Wait-Regression $Context { @(Find-FaviconElements $list ([CefFlashBrowser.Views.Custom.BrowserIcon])).Count -eq 2 } 'favorites manager creates favicon controls'
        $icons = @(Find-FaviconElements $list ([CefFlashBrowser.Views.Custom.BrowserIcon]))
        Assert-Regression ($icons[0].FaviconUrl -eq $iconA -and $null -eq $icons[1].FaviconUrl) 'saved and unvisited favorites use their own mapping or default icon'

        $mainWindow = New-Object CefFlashBrowser.Views.MainWindow
        $mainWindow.Left = -10000
        $mainWindow.ShowInTaskbar = $false
        $mainWindow.ShowActivated = $false
        $mainWindow.Show()
        $mainFavoritesMenu = @(Find-FaviconElements $mainWindow ([System.Windows.Controls.Menu]))[0].Items[0]
        $mainFavoritesMenu.IsSubmenuOpen = $true
        $actionsMenu = $window.Resources['browserActionsMenu']
        $actionsMenu.PlacementTarget = $window
        $actionsMenu.IsOpen = $true
        $browserFavoritesMenu = @($actionsMenu.Items | Where-Object {
            $_ -is [System.Windows.Controls.MenuItem] -and [object]::ReferenceEquals($_.ItemsSource, $favorites)
        })[0]
        $browserFavoritesMenu.IsSubmenuOpen = $true
        Wait-Regression $Context {
            $mainItem = $mainFavoritesMenu.ItemContainerGenerator.ContainerFromItem($favorite)
            $browserItem = $browserFavoritesMenu.ItemContainerGenerator.ContainerFromItem($favorite)
            $null -ne $mainItem -and $null -ne $browserItem -and
                @(Find-FaviconElements $mainItem ([CefFlashBrowser.Views.Custom.BrowserIcon])).Count -eq 1 -and
                @(Find-FaviconElements $browserItem ([CefFlashBrowser.Views.Custom.BrowserIcon])).Count -eq 1
        } 'both favorites menus create favicon controls'
        $mainItem = $mainFavoritesMenu.ItemContainerGenerator.ContainerFromItem($favorite)
        $browserItem = $browserFavoritesMenu.ItemContainerGenerator.ContainerFromItem($favorite)
        Assert-Regression (@(Find-FaviconElements $mainItem ([CefFlashBrowser.Views.Custom.BrowserIcon]))[0].FaviconUrl -eq $iconA) 'home favorites menu uses the saved page icon'
        Assert-Regression (@(Find-FaviconElements $browserItem ([CefFlashBrowser.Views.Custom.BrowserIcon]))[0].FaviconUrl -eq $iconA) 'browser favorites menu uses its item URL while another page is active'

        $manager.DataContext.SelectedWebsite = $favorite
        $manager.FindName('nameTextBox').Text = 'Edited B'
        $manager.FindName('urlTextBox').Text = $pageB
        Invoke-BrowserCommand $manager.CommitEditCommand
        Wait-Regression $Context {
            $currentIcons = @(Find-FaviconElements $list ([CefFlashBrowser.Views.Custom.BrowserIcon]))
            $currentIcons.Count -eq 2 -and $currentIcons[0].FaviconUrl -eq $iconB
        } 'editing a favorite rebinds its favicon to the new URL'
        Assert-Regression (![object]::ReferenceEquals($favorite, $favorites[0]) -and $favorites[0].Name -eq 'Edited B') 'committed edit replaces the item at its original position'
        Assert-Regression ([object]::ReferenceEquals($manager.DataContext.SelectedWebsite, $favorites[0])) 'committed edit preserves logical selection'
        $mainFavoritesMenu.IsSubmenuOpen = $true
        $browserFavoritesMenu.IsSubmenuOpen = $true
        Wait-Regression $Context {
            $mainItem = $mainFavoritesMenu.ItemContainerGenerator.ContainerFromItem($favorites[0])
            $browserItem = $browserFavoritesMenu.ItemContainerGenerator.ContainerFromItem($favorites[0])
            $null -ne $mainItem -and $null -ne $browserItem -and $browserItem.CommandParameter -eq $pageB
        } 'both menus rebind edited favorite command parameters'
        [void]$service.RecordAsync($pageB, $iconA).GetAwaiter().GetResult()
        Wait-Regression $Context {
            @(Find-FaviconElements $list ([CefFlashBrowser.Views.Custom.BrowserIcon]))[0].FaviconUrl -eq $iconA
        } 'an open favorites manager refreshes when its mapping changes'
        $missingIcon = $fixture.BaseUrl + '/missing.png'
        [void]$service.RecordAsync($pageB, $missingIcon).GetAwaiter().GetResult()
        Wait-Regression $Context {
            $boundIcon = @(Find-FaviconElements $list ([CefFlashBrowser.Views.Custom.BrowserIcon]))[0]
            $boundIcon.HasImageFailed
        } 'a favorite falls back when its saved image cannot be downloaded'
        $boundIcon = @(Find-FaviconElements $list ([CefFlashBrowser.Views.Custom.BrowserIcon]))[0]
        Assert-Regression ([System.Windows.Data.BindingOperations]::IsDataBound($boundIcon, [CefFlashBrowser.Views.Custom.BrowserIcon]::FaviconUrlProperty)) 'image failure preserves the favicon URL binding'
        Assert-Regression ($service.GetIconUrl($pageB) -eq $missingIcon) 'image failure retains the recorded mapping'
        [void]$service.RecordAsync($pageB, $iconB).GetAwaiter().GetResult()

        $icon = New-Object CefFlashBrowser.Views.Custom.BrowserIcon
        $iconWindow = New-Object System.Windows.Window
        $iconWindow.Content = $icon
        $iconWindow.Left = -10000
        $iconWindow.ShowInTaskbar = $false
        $iconWindow.ShowActivated = $false
        $iconWindow.Show()
        [void]$icon.ApplyTemplate()
        $failureState = @{ Count = 0 }
        $failureHandler = [EventHandler[System.Windows.ExceptionRoutedEventArgs]]({
            param($sender, $eventArgs)
            $failureState.Count++
        }.GetNewClosure())
        $icon.Template.FindName('favicon', $icon).add_ImageFailed($failureHandler)
        foreach ($failurePath in @('/missing.png', '/bad.png')) {
            $previousFailures = $failureState.Count
            $icon.FaviconUrl = $fixture.BaseUrl + $failurePath
            Wait-Regression $Context {
                $failureState.Count -gt $previousFailures -and
                    $icon.Template.FindName('fallbackIcon', $icon).Visibility -eq [System.Windows.Visibility]::Visible
            } "image failure $failurePath displays the default icon"
        }
        $icon.FaviconUrl = $fixture.BaseUrl + '/slow-bad.png'
        Wait-Regression $Context { $fixture.SlowRequests -gt 0 } 'delayed image request starts'
        $icon.FaviconUrl = $iconB
        Wait-Regression $Context {
            $image = $icon.Template.FindName('favicon', $icon)
            $null -ne $image.Source -and !$image.Source.IsDownloading -and
                $icon.Template.FindName('fallbackIcon', $icon).Visibility -eq [System.Windows.Visibility]::Collapsed
        } 'a valid image resets failure state'
        $fixture.ReleaseSlowResponse()
        # Drain queued WPF notifications after the abandoned request completes.
        $settle = [Diagnostics.Stopwatch]::StartNew()
        Wait-Regression $Context { $settle.ElapsedMilliseconds -gt 300 } 'delayed old image failure settles'
        Assert-Regression (!$icon.HasImageFailed -and $null -ne $icon.Template.FindName('favicon', $icon).Source) 'old image failure cannot hide the new icon'

        Invoke-BrowserCommand $browser.LoadUrlCommand $pageA
        Wait-Regression $Context { $browser.Address -eq $pageA -and !$browser.IsLoading } 'page A loads before queued callback check'
        $captureMethod = [CefFlashBrowser.FlashBrowser.ChromiumWebBrowserEx].GetMethod('CaptureFavicon', [Reflection.BindingFlags]'Instance,NonPublic')
        $urlList = New-Object 'System.Collections.Generic.List[string]'
        $urlList.Add($fixture.BaseUrl + '/stale.png')
        [void]$captureMethod.Invoke($browser.PSObject.BaseObject, [object[]]@($browser.GetBrowser().PSObject.BaseObject, $urlList.PSObject.BaseObject))
        $navigationMethod = [CefFlashBrowser.FlashBrowser.ChromiumWebBrowserEx].GetMethod('UpdateNavigation', [Reflection.BindingFlags]'Instance,NonPublic')
        [void]$navigationMethod.Invoke($browser, [object[]]@($pageB))
        Invoke-BrowserCommand $browser.LoadUrlCommand $pageB
        Wait-Regression $Context { $service.GetIconUrl($pageB) -eq $iconB -and !$browser.IsLoading } 'queued old favicon is discarded after navigation'
        Assert-Regression ($service.GetIconUrl($pageA) -eq $iconA) 'queued page A callback never overwrites a mapping after navigation'
        $urlList[0] = $fixture.BaseUrl + '/closed.png'
        [void]$captureMethod.Invoke($browser.PSObject.BaseObject, [object[]]@($browser.GetBrowser().PSObject.BaseObject, $urlList.PSObject.BaseObject))
        $window.ForceCloseWindow()
        [void]$captureMethod.Invoke($browser.PSObject.BaseObject, [object[]]@($Context.DisposedBrowser.PSObject.BaseObject, $urlList.PSObject.BaseObject))
        $settle.Restart()
        Wait-Regression $Context { $settle.ElapsedMilliseconds -gt 100 } 'closed browser callbacks settle'
        Assert-Regression ($service.GetIconUrl($pageB) -eq $iconB) 'closing a tab discards queued discovery and ignores callbacks with a disposed browser'
    } finally {
        if ($null -ne $actionsMenu) { $actionsMenu.IsOpen = $false }
        if ($null -ne $mainWindow) { $mainWindow.Close() }
        if ($null -ne $iconWindow) { $iconWindow.Close() }
        if ($null -ne $manager) { $manager.Close() }
        $window.ForceCloseWindow()
        [CefFlashBrowser.Data.GlobalData]::Favorites.Clear()
        foreach ($item in $originalFavorites) { [CefFlashBrowser.Data.GlobalData]::Favorites.Add($item) }
        $fixture.Dispose()
    }
}
