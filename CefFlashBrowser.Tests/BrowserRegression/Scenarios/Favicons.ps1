function Find-FaviconElements($Element, [Type]$ElementType) {
    if ($Element -is $ElementType) {
        Write-Output $Element
    }
    for ($index = 0; $index -lt [System.Windows.Media.VisualTreeHelper]::GetChildrenCount($Element); $index++) {
        Find-FaviconElements ([System.Windows.Media.VisualTreeHelper]::GetChild($Element, $index)) $ElementType
    }
}

function Assert-FaviconMenuIcon($Item, $Website, $ExpectedUrl, [string]$Surface) {
    Assert-Regression ([object]::ReferenceEquals($Item.Icon, $Website)) "$Surface uses the MenuItem icon slot"
    $icons = @(Find-FaviconElements $Item ([CefFlashBrowser.Views.Custom.BrowserIcon]))
    Assert-Regression ($icons.Count -eq 1 -and $icons[0].FaviconUrl -eq $ExpectedUrl) "$Surface displays its own favicon"
    $headers = @(Find-FaviconElements $Item ([System.Windows.Controls.ContentPresenter]) | Where-Object {
        $null -ne $Item.HeaderTemplate -and [object]::ReferenceEquals($_.ContentTemplate, $Item.HeaderTemplate)
    })
    Assert-Regression ($headers.Count -eq 1 -and
        @(Find-FaviconElements $headers[0] ([CefFlashBrowser.Views.Custom.BrowserIcon])).Count -eq 0) "$Surface keeps its header free of favicon controls"
    Assert-Regression (@(Find-FaviconElements $headers[0] ([System.Windows.Controls.TextBlock]) | Where-Object {
        $_.Text -eq $Website.Name
    }).Count -eq 1) "$Surface retains its name in the header"
    if ($null -eq $ExpectedUrl) {
        Assert-Regression ($icons[0].Template.FindName('fallbackIcon', $icons[0]).Visibility -eq [System.Windows.Visibility]::Visible) "$Surface displays the default icon in its icon slot"
    }
}

function Test-SvgFavicon($Icon) {
    $source = $Icon.Template.FindName('favicon', $Icon).Source
    return ($source -is [System.Windows.Media.DrawingImage] -and $source.Width -gt 0 -and $source.Height -gt 0 -and
        $Icon.Template.FindName('fallbackIcon', $Icon).Visibility -eq [System.Windows.Visibility]::Collapsed)
}

function Test-BrowserIconDataUrls($Context) {
    $png = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a/pkAAAAASUVORK5CYII='
    $pngUrl = 'data:image/png;base64,' + $png
    $svg = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 8 8"><rect width="8" height="8" fill="#123456"/></svg>'
    $svgUrl = 'data:image/svg+xml,' + [Uri]::EscapeDataString($svg)
    $svgBase64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($svg))
    $longUrl = $svgUrl + '%3C!--' + ('x' * 70000) + '--%3E'

    $icon = New-Object CefFlashBrowser.Views.Custom.BrowserIcon
    $icon.DefaultIcon = 'D'
    $window = New-Object System.Windows.Window
    $window.Content = $icon
    $window.Left = -10000
    $window.ShowInTaskbar = $false
    $window.ShowActivated = $false
    try {
        $window.Show()
        [void]$icon.ApplyTemplate()
        $image = $icon.Template.FindName('favicon', $icon)
        $fallback = $icon.Template.FindName('fallbackIcon', $icon)
        $percentPng = -join ([Convert]::FromBase64String($png) | ForEach-Object { '%{0:X2}' -f $_ })
        foreach ($url in @(
            $pngUrl,
            ('DATA:Image/PNG; BASE64,' + $png.TrimEnd('=').Replace('/', '%2F')),
            ('data:image/png,' + $percentPng)
        )) {
            $icon.FaviconUrl = $url
            Wait-Regression $Context {
                !$icon.HasImageFailed -and $image.Source -is [System.Windows.Media.Imaging.BitmapSource] -and
                    $image.Source.PixelWidth -eq 1 -and $fallback.Visibility -eq 'Collapsed'
            } 'inline PNG renders as a bitmap'
        }

        foreach ($url in @(
            ('DATA:image/svg+xml;charset=UTF-8;BASE64,' + $svgBase64),
            ($svgUrl + '#fragment'),
            $longUrl
        )) {
            $icon.FaviconUrl = $url
            Wait-Regression $Context { Test-SvgFavicon $icon } 'inline SVG renders as a vector'
            Assert-Regression ($image.Source.Width -eq 8 -and $image.Source.Height -eq 8 -and
                @(Get-DrawingColors $image.Source.Drawing) -contains [System.Windows.Media.ColorConverter]::ConvertFromString('#123456')) 'inline SVG retains its viewBox and fill color'
        }

        foreach ($url in @(
            'data:image/png;base64',
            'data:image/png;base64,***',
            ('data:image/png;base64,' + $png + '='),
            ('data:text/plain;base64,' + $png),
            'data:image/svg+xml,%3Csvg%2F%3E'
        )) {
            $icon.FaviconUrl = $url
            Wait-Regression $Context {
                $icon.HasImageFailed -and $null -eq $image.Source -and $fallback.Visibility -eq 'Visible'
            } "invalid data URL displays the default icon ($url)"
        }

        $icon.FaviconUrl = $longUrl
        $icon.FaviconUrl = 'data:image/png;base64,***'
        $icon.FaviconUrl = $pngUrl
        Wait-Regression $Context {
            !$icon.HasImageFailed -and $image.Source -is [System.Windows.Media.Imaging.BitmapSource] -and
                $fallback.Visibility -eq 'Collapsed'
        } 'the latest data URL replaces pending SVG and invalid data URLs'
        $latestImage = $image.Source
        $settle = [Diagnostics.Stopwatch]::StartNew()
        Wait-Regression $Context { $settle.ElapsedMilliseconds -gt 300 } 'abandoned data URLs settle'
        Assert-Regression (!$icon.HasImageFailed -and [object]::ReferenceEquals($latestImage, $image.Source)) 'abandoned data URLs cannot replace or hide the latest icon'
    } finally {
        $window.Close()
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
    $originalFavorites = @($Context.FavoritesService.Favorites)
    $pageA = $fixture.BaseUrl + '/a'
    $pageB = $fixture.BaseUrl + '/b'
    $iconA = $fixture.BaseUrl + '/a-icon.png'
    $iconB = $fixture.BaseUrl + '/b-icon.png'
    try {
        Invoke-BrowserCommand $browser.LoadUrlCommand ($pageA + '?visit=1#top')
        Wait-Regression $Context { $service.GetIconUrl($pageA) -eq $iconA } 'browsing an unsaved page records its reported favicon'
        Invoke-BrowserCommand $browser.LoadUrlCommand ($fixture.BaseUrl + '/redirect')
        Wait-Regression $Context { $service.GetIconUrl($pageB) -eq $iconB } 'redirect records the final page favicon'
        Assert-Regression ($null -eq $service.GetIconUrl($fixture.BaseUrl + '/redirect')) 'redirect does not create an alias for an unobserved URL'
        [void]$service.RecordAsync($fixture.BaseUrl + '/', $iconA).GetAwaiter().GetResult()
        Assert-Regression ($service.GetIconUrl($fixture.BaseUrl + '/unvisited') -eq $iconA) 'absolute addresses fall back to the same-origin homepage'

        $favorites = $Context.FavoritesService.Favorites
        $favorites.Clear()
        $favorite = New-Object CefFlashBrowser.Models.Website('Page A', ($pageA + '/?saved=1#favorite'))
        $domainFavorite = New-Object CefFlashBrowser.Models.Website('Domain fallback', ($fixture.BaseUrl.Substring('http://'.Length) + '/unvisited'))
        $favorites.Add($favorite)
        $favorites.Add((New-Object CefFlashBrowser.Models.Website('Old favorite', 'https://unvisited.invalid/')))
        $favorites.Add($domainFavorite)
        $manager = New-Object CefFlashBrowser.Views.FavoritesManager
        $manager.Left = -10000
        $manager.ShowInTaskbar = $false
        $manager.ShowActivated = $false
        $manager.Show()
        $list = @(Find-FaviconElements $manager ([System.Windows.Controls.ListBox]))[0]
        Wait-Regression $Context { @(Find-FaviconElements $list ([CefFlashBrowser.Views.Custom.BrowserIcon])).Count -eq 3 } 'favorites manager creates favicon controls'
        $icons = @(Find-FaviconElements $list ([CefFlashBrowser.Views.Custom.BrowserIcon]))
        Assert-Regression ($icons[0].FaviconUrl -eq $iconA -and $null -eq $icons[1].FaviconUrl -and $icons[2].FaviconUrl -eq $iconA) 'favorites manager normalizes page URLs and uses the homepage for a schemeless favorite'

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
        Assert-FaviconMenuIcon $mainItem $favorite $iconA 'home favorite'
        foreach ($item in @($mainItem, $mainFavoritesMenu.Items[0])) {
            $header = @(Find-FaviconElements $item ([System.Windows.Controls.ContentPresenter]) |
                Where-Object { $_.ContentSource -eq 'Header' })[0]
            $headerRight = $header.TranslatePoint([Windows.Point]::new($header.ActualWidth, 0), $item).X
            $rightInset = $item.ActualWidth - $headerRight
            Assert-Regression ([Math]::Abs($rightInset - $item.Padding.Right) -lt 0.5) "home favorites menu item uses only standard right padding (actual $rightInset)"
        }
        Assert-FaviconMenuIcon $browserItem $favorite $iconA 'browser favorite while another page is active'
        Assert-FaviconMenuIcon ($mainFavoritesMenu.ItemContainerGenerator.ContainerFromItem($favorites[1])) $favorites[1] $null 'unvisited home favorite'
        Assert-FaviconMenuIcon ($browserFavoritesMenu.ItemContainerGenerator.ContainerFromItem($favorites[1])) $favorites[1] $null 'unvisited browser favorite'
        Assert-FaviconMenuIcon ($mainFavoritesMenu.ItemContainerGenerator.ContainerFromItem($domainFavorite)) $domainFavorite $iconA 'schemeless home favorite'
        Assert-FaviconMenuIcon ($browserFavoritesMenu.ItemContainerGenerator.ContainerFromItem($domainFavorite)) $domainFavorite $iconA 'schemeless browser favorite'
        Assert-Regression ($null -eq $mainFavoritesMenu.Items[0].Icon -and $null -eq $mainFavoritesMenu.Items[0].HeaderTemplate -and
            $mainFavoritesMenu.Items[1] -is [System.Windows.Controls.Separator]) 'home menu retains its manager entry and separator without a website icon'

        $manager.DataContext.SelectedWebsite = $favorite
        $manager.FindName('nameTextBox').Text = 'Edited B'
        $editedUrl = $pageB.Substring('http://'.Length) + '/?saved=2#favorite'
        $manager.FindName('urlTextBox').Text = $editedUrl
        Invoke-BrowserCommand $manager.CommitEditCommand
        Wait-Regression $Context {
            $currentIcons = @(Find-FaviconElements $list ([CefFlashBrowser.Views.Custom.BrowserIcon]))
            $currentIcons.Count -eq 3 -and $currentIcons[0].FaviconUrl -eq $iconB
        } 'editing a favorite rebinds its favicon to the new URL'
        Assert-Regression (![object]::ReferenceEquals($favorite, $favorites[0]) -and $favorites[0].Name -eq 'Edited B') 'committed edit replaces the item at its original position'
        Assert-Regression ([object]::ReferenceEquals($manager.DataContext.SelectedWebsite, $favorites[0])) 'committed edit preserves logical selection'
        $mainFavoritesMenu.IsSubmenuOpen = $true
        $browserFavoritesMenu.IsSubmenuOpen = $true
        Wait-Regression $Context {
            $mainItem = $mainFavoritesMenu.ItemContainerGenerator.ContainerFromItem($favorites[0])
            $browserItem = $browserFavoritesMenu.ItemContainerGenerator.ContainerFromItem($favorites[0])
            $null -ne $mainItem -and $null -ne $browserItem -and $browserItem.CommandParameter -eq $editedUrl -and
                [object]::ReferenceEquals($mainItem.CommandParameter, $favorites[0]) -and
                @(Find-FaviconElements $mainItem ([CefFlashBrowser.Views.Custom.BrowserIcon]))[0].FaviconUrl -eq $iconB -and
                @(Find-FaviconElements $browserItem ([CefFlashBrowser.Views.Custom.BrowserIcon]))[0].FaviconUrl -eq $iconB
        } 'both menus rebind edited favorite command parameters'
        Assert-FaviconMenuIcon ($mainFavoritesMenu.ItemContainerGenerator.ContainerFromItem($favorites[0])) $favorites[0] $iconB 'edited home favorite'
        Assert-FaviconMenuIcon ($browserFavoritesMenu.ItemContainerGenerator.ContainerFromItem($favorites[0])) $favorites[0] $iconB 'edited browser favorite'
        [void]$service.RecordAsync($fixture.BaseUrl + '?refresh=1', $iconB).GetAwaiter().GetResult()
        Wait-Regression $Context {
            $mainDomainItem = $mainFavoritesMenu.ItemContainerGenerator.ContainerFromItem($domainFavorite)
            $browserDomainItem = $browserFavoritesMenu.ItemContainerGenerator.ContainerFromItem($domainFavorite)
            @(Find-FaviconElements $list ([CefFlashBrowser.Views.Custom.BrowserIcon]))[2].FaviconUrl -eq $iconB -and
                @(Find-FaviconElements $mainDomainItem ([CefFlashBrowser.Views.Custom.BrowserIcon]))[0].FaviconUrl -eq $iconB -and
                @(Find-FaviconElements $browserDomainItem ([CefFlashBrowser.Views.Custom.BrowserIcon]))[0].FaviconUrl -eq $iconB
        } 'homepage changes refresh schemeless favorites on all three surfaces'
        [void]$service.RecordAsync($pageB, $iconA).GetAwaiter().GetResult()
        Wait-Regression $Context {
            $mainItem = $mainFavoritesMenu.ItemContainerGenerator.ContainerFromItem($favorites[0])
            $browserItem = $browserFavoritesMenu.ItemContainerGenerator.ContainerFromItem($favorites[0])
            @(Find-FaviconElements $list ([CefFlashBrowser.Views.Custom.BrowserIcon]))[0].FaviconUrl -eq $iconA -and
                @(Find-FaviconElements $mainItem ([CefFlashBrowser.Views.Custom.BrowserIcon]))[0].FaviconUrl -eq $iconA -and
                @(Find-FaviconElements $browserItem ([CefFlashBrowser.Views.Custom.BrowserIcon]))[0].FaviconUrl -eq $iconA
        } 'open favorites menus and manager refresh when their mapping changes'
        $svgUrl = $fixture.BaseUrl + '/icon'
        [void]$service.RecordAsync($pageB, $svgUrl).GetAwaiter().GetResult()
        Wait-Regression $Context {
            $boundIcon = @(Find-FaviconElements $list ([CefFlashBrowser.Views.Custom.BrowserIcon]))[0]
            $boundIcon.FaviconUrl -eq $svgUrl -and (Test-SvgFavicon $boundIcon)
        } 'a bound favorite displays an extensionless SVG identified by its content type'
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
        $image = $icon.Template.FindName('favicon', $icon)
        $icon.FaviconUrl = $fixture.BaseUrl + '/icon.SVG?version=1#icon'
        Wait-Regression $Context { Test-SvgFavicon $icon } 'an uppercase SVG URL with a query and fragment renders as a vector'
        Assert-Regression ($image.Source.Width -eq 16 -and $image.Source.Height -eq 16) 'the SVG retains its original viewBox'
        Assert-Regression ($fixture.SvgPathRequests -eq 1) 'an SVG favicon uses one HTTP request'

        $localIconPath = Join-Path $Context.TemporaryPath 'local icon.svg'
        [IO.File]::WriteAllText($localIconPath, '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 8 8"><circle cx="4" cy="4" r="4"/></svg>')
        $icon.FaviconUrl = [Uri]::new($localIconPath).AbsoluteUri
        Wait-Regression $Context { Test-SvgFavicon $icon } 'a local SVG file URL renders as a vector'

        foreach ($failurePath in @('/missing.png', '/bad.png', '/bad.svg')) {
            $icon.FaviconUrl = $fixture.BaseUrl + $failurePath
            Wait-Regression $Context {
                $icon.HasImageFailed -and
                    $icon.Template.FindName('fallbackIcon', $icon).Visibility -eq [System.Windows.Visibility]::Visible
            } "image failure $failurePath displays the default icon"
        }
        foreach ($slowPath in @('/slow-bad.png', '/slow.svg')) {
            $previousRequests = $fixture.SlowRequests
            $icon.FaviconUrl = $fixture.BaseUrl + $slowPath
            Wait-Regression $Context { $fixture.SlowRequests -gt $previousRequests } "delayed image request $slowPath starts"
            $icon.FaviconUrl = $iconB
            Wait-Regression $Context {
                !$icon.HasImageFailed -and $image.Source -is [System.Windows.Media.Imaging.BitmapSource] -and
                    $icon.Template.FindName('fallbackIcon', $icon).Visibility -eq [System.Windows.Visibility]::Collapsed
            } 'a valid PNG replaces the pending image and resets failure state'
        }
        $pngSource = $image.Source
        $fixture.ReleaseSlowResponse()
        # Drain queued notifications after the abandoned requests complete.
        $settle = [Diagnostics.Stopwatch]::StartNew()
        Wait-Regression $Context { $settle.ElapsedMilliseconds -gt 300 } 'delayed old image responses settle'
        Assert-Regression (!$icon.HasImageFailed -and [object]::ReferenceEquals($pngSource, $image.Source)) 'stale image responses cannot replace or hide the current icon'

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

        Assert-Regression ([object]::ReferenceEquals($favorites, $manager.DataContext.Favorites) -and
            [object]::ReferenceEquals($favorites, $browserFavoritesMenu.ItemsSource)) 'favorites manager and browser menu bind to the shared service collection'
        $editedFavorite = $favorites[0]
        Invoke-BrowserCommand $manager.DataContext.MoveDownCommand $editedFavorite
        Wait-Regression $Context {
            [object]::ReferenceEquals($list.Items[1], $editedFavorite) -and
                [object]::ReferenceEquals($mainFavoritesMenu.Items[3], $editedFavorite) -and
                [object]::ReferenceEquals($browserFavoritesMenu.Items[1], $editedFavorite)
        } 'reordering a favorite updates the manager and both menus'
        $favorites.RemoveAt(0)
        Wait-Regression $Context {
            $list.Items.Count -eq 2 -and $mainFavoritesMenu.Items.Count -eq 4 -and $browserFavoritesMenu.Items.Count -eq 2
        } 'removing a favorite updates all three surfaces'
        $favorites.Clear()
        Wait-Regression $Context {
            $list.Items.Count -eq 0 -and $mainFavoritesMenu.Items.Count -eq 2 -and
                $mainFavoritesMenu.Items[1].Visibility -eq [System.Windows.Visibility]::Collapsed -and
                $browserFavoritesMenu.Visibility -eq [System.Windows.Visibility]::Collapsed
        } 'empty favorites collapse the home separator and browser favorites menu'
        $favorites.Add($domainFavorite)
        $favorites.Add($editedFavorite)
        Wait-Regression $Context {
            $list.Items.Count -eq 2 -and [object]::ReferenceEquals($mainFavoritesMenu.Items[2], $domainFavorite) -and
                [object]::ReferenceEquals($browserFavoritesMenu.Items[0], $domainFavorite) -and
                $mainFavoritesMenu.Items[1].Visibility -eq [System.Windows.Visibility]::Visible -and
                $browserFavoritesMenu.Visibility -eq [System.Windows.Visibility]::Visible
        } 'adding favorites restores both menus and their shared order'
        $manager.Close()
        $manager = $null
        $savedFavorites = [IO.File]::ReadAllText((Join-Path $Context.TemporaryPath 'favorites.json')) | ConvertFrom-Json
        Assert-Regression ($savedFavorites.Favorites.Count -eq 2 -and
            $savedFavorites.Favorites[0].Name -eq $domainFavorite.Name -and $savedFavorites.Favorites[0].Url -eq $domainFavorite.Url -and
            $savedFavorites.Favorites[1].Name -eq $editedFavorite.Name -and $savedFavorites.Favorites[1].Url -eq $editedFavorite.Url) 'closing the manager directly saves the shared favorites in their current order'

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
        $Context.FavoritesService.Favorites.Clear()
        foreach ($item in $originalFavorites) { $Context.FavoritesService.Favorites.Add($item) }
        $fixture.Dispose()
    }
}
