function Test-FavoritesService($Context) {
    $service = $Context.FavoritesService
    $ioc = [SimpleMvvm.Ioc.SimpleIoc]::Global
    $locator = $Context.Application.Resources['Locator']
    Assert-Regression ([object]::ReferenceEquals($service, $ioc.GetInstance([CefFlashBrowser.Services.FavoritesService], @())) -and
        [object]::ReferenceEquals($service, $locator.FavoritesService) -and
        [object]::ReferenceEquals($service.Favorites, $locator.FavoritesManagerViewModel.Favorites)) 'favorites consumers share the registered service and collection'

    $path = Join-Path $Context.TemporaryPath 'favorites-service.json'
    $service = New-Object CefFlashBrowser.Services.FavoritesService($path, $Context.Logger)
    $collection = $service.Favorites
    Assert-Regression ($collection.Count -eq 0 -and !(Test-Path -LiteralPath $path)) 'missing favorites load as an empty collection without creating a file'
    $first = New-Object CefFlashBrowser.Models.Website(([string][char]0x6536 + [char]0x85cf + ' "quoted"'), 'https://example.invalid/first?x=1&y=2#fragment')
    $second = New-Object CefFlashBrowser.Models.Website('Second', 'about:blank')
    $collection.Add($first)
    $collection.Add($second)
    $collection.Move(1, 0)
    Assert-Regression (!(Test-Path -LiteralPath $path)) 'collection changes do not save favorites automatically'
    Assert-Regression ($service.Save()) 'favorites save to the injected path'
    $saved = [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8) | ConvertFrom-Json
    Assert-Regression ($saved.PSObject.Properties.Name -contains 'Favorites' -and $saved.Favorites.Count -eq 2) 'favorites retain their existing JSON wrapper and array format'
    $reloaded = New-Object CefFlashBrowser.Services.FavoritesService($path, $Context.Logger)
    Assert-Regression ($reloaded.Favorites.Count -eq 2 -and $reloaded.Favorites[0].Name -eq $second.Name -and
        $reloaded.Favorites[0].Url -eq $second.Url -and $reloaded.Favorites[1].Name -eq $first.Name -and
        $reloaded.Favorites[1].Url -eq $first.Url) 'favorites construction restores names, URLs, Unicode and saved order'

    $collection[1] = New-Object CefFlashBrowser.Models.Website('Edited', 'https://example.invalid/edited')
    $collection.RemoveAt(0)
    Assert-Regression ($service.Save()) 'edited and removed favorites save successfully'
    $reloaded = New-Object CefFlashBrowser.Services.FavoritesService($path, $Context.Logger)
    Assert-Regression ($reloaded.Favorites.Count -eq 1 -and $reloaded.Favorites[0].Name -eq 'Edited' -and
        $reloaded.Favorites[0].Url -eq 'https://example.invalid/edited') 'saved edits and deletions survive reloading'
    $collection.Clear()
    Assert-Regression ($service.Save()) 'an empty favorites collection saves successfully'
    $reloaded = New-Object CefFlashBrowser.Services.FavoritesService($path, $Context.Logger)
    Assert-Regression ($reloaded.Favorites.Count -eq 0 -and [object]::ReferenceEquals($collection, $service.Favorites)) 'saving preserves the collection instance and supports empty favorites'

    $originalJson = [IO.File]::ReadAllText($path)
    $collection.Add($first)
    $lockedFile = [IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None)
    try {
        $unreadable = New-Object CefFlashBrowser.Services.FavoritesService($path, $Context.Logger)
        Assert-Regression ($unreadable.Favorites.Count -eq 0) 'an unreadable favorites file falls back to an empty collection'
        Assert-Regression (!$service.Save()) 'a favorites write failure returns false'
    } finally {
        $lockedFile.Dispose()
    }
    Assert-Regression ([IO.File]::ReadAllText($path) -eq $originalJson -and
        @(Get-ChildItem -LiteralPath $Context.TemporaryPath -Filter 'favorites-service.json.*.tmp').Count -eq 0) 'a failed save preserves the existing favorites file and removes temporary files'

    foreach ($fixture in @(
        @{ Name = 'invalid JSON'; Json = '{broken' },
        @{ Name = 'null root'; Json = 'null' },
        @{ Name = 'missing array'; Json = '{}' },
        @{ Name = 'null array'; Json = '{"Favorites":null}' }
    )) {
        [IO.File]::WriteAllText($path, $fixture.Json, [Text.Encoding]::UTF8)
        $reloaded = New-Object CefFlashBrowser.Services.FavoritesService($path, $Context.Logger)
        Assert-Regression ($reloaded.Favorites.Count -eq 0 -and [IO.File]::ReadAllText($path) -eq $fixture.Json) "malformed favorites load as empty without overwriting the file ($($fixture.Name))"
    }
}

function Test-FavoritesManagerReorder($Context) {
    $favorites = $Context.FavoritesService.Favorites
    $originalFavorites = @($favorites)
    $first = New-Object CefFlashBrowser.Models.Website('First', 'about:blank')
    $second = New-Object CefFlashBrowser.Models.Website('Second', 'about:blank')
    $third = New-Object CefFlashBrowser.Models.Website('Third', 'about:blank')
    $manager = $null
    try {
        $favorites.Clear()
        $favorites.Add($first)
        $favorites.Add($second)
        $favorites.Add($third)
        $manager = New-Object CefFlashBrowser.Views.FavoritesManager
        $manager.WindowStartupLocation = 'Manual'
        $manager.Left = -10000
        $manager.ShowActivated = $false
        $manager.ShowInTaskbar = $false
        $manager.Show()
        $list = $manager.FindName('favoritesList')
        $model = $manager.DataContext
        $behavior = @([Microsoft.Xaml.Behaviors.Interaction]::GetBehaviors($list) | Where-Object {
            $_ -is [CefFlashBrowser.Views.Behaviors.ListReorderBehavior]
        })[0]
        Assert-Regression ($list.AllowDrop -and $behavior.IsDragEnabled -and
            [object]::ReferenceEquals($behavior.ReorderCommand, $model.ReorderCommand) -and
            [object]::ReferenceEquals($list.ItemsSource, $favorites)) 'the favorites list binds shared drag reordering to the shared favorites collection'

        $model.SelectedWebsite = $third
        $manager.UpdateLayout()
        $row = $list.ItemContainerGenerator.ContainerFromItem($first)
        $before = $row.TranslatePoint([Windows.Point]::new(25, $row.ActualHeight / 4), $list)
        $data = Start-ListReorderDrag $behavior $third
        try {
            $preview = Invoke-ListReorderDragEvent $list $data $before ([Windows.DragDrop]::PreviewDragOverEvent)
            Assert-Regression ($preview.Effects -eq [Windows.DragDropEffects]::Move -and
                @(Get-ListReorderAdorners $list).Count -eq 1) 'dragging a favorite shows an insertion line'
            Assert-Regression ((Invoke-ListReorderDragQuery $list $true) -eq [Windows.DragAction]::Cancel -and
                @(Get-ListReorderAdorners $list).Count -eq 0 -and ($favorites.Name -join ',') -ceq 'First,Second,Third') 'Esc removes the insertion line and preserves favorite order'
        } finally {
            Invoke-ViewCallback $behavior 'EndDrag' @()
        }

        foreach ($step in @(
            @{ Item = $third; Target = $first; RowFraction = 0.25; Offset = 0; Order = 'Third,First,Second'; Label = 'before the first row' },
            @{ Item = $third; Target = $first; RowFraction = 0.75; Offset = 0; Order = 'First,Third,Second'; Label = 'after the next row' },
            @{ Item = $first; Target = $second; RowFraction = 1; Offset = 5; Order = 'Third,Second,First'; Label = 'in the space below the final row' }
        )) {
            $model.SelectedWebsite = $step.Item
            $manager.UpdateLayout()
            $row = $list.ItemContainerGenerator.ContainerFromItem($step.Target)
            $point = $row.TranslatePoint([Windows.Point]::new(25, $row.ActualHeight * $step.RowFraction + $step.Offset), $list)
            $data = Start-ListReorderDrag $behavior $step.Item
            try {
                $result = Invoke-ListReorderDragEvent $list $data $point ([Windows.DragDrop]::PreviewDropEvent)
                Assert-Regression ($result.Effects -eq [Windows.DragDropEffects]::Move -and
                    ($list.Items.Name -join ',') -ceq $step.Order -and ($favorites.Name -join ',') -ceq $step.Order -and
                    [object]::ReferenceEquals($list.SelectedItem, $step.Item) -and
                    [object]::ReferenceEquals($model.SelectedWebsite, $step.Item)) "dropping $($step.Label) updates the list and shared order and preserves selection"
            } finally {
                Invoke-ViewCallback $behavior 'EndDrag' @()
            }
        }

        $manager.Close()
        $manager = $null
        $path = [CefFlashBrowser.Data.GlobalData]::FavoritesPath
        $reloaded = New-Object CefFlashBrowser.Services.FavoritesService($path, $Context.Logger)
        Assert-Regression (($reloaded.Favorites.Name -join ',') -ceq 'Third,Second,First') 'closing the favorites manager saves the drag order'
    } finally {
        if ($null -ne $manager) { $manager.Close() }
        $favorites.Clear()
        foreach ($item in $originalFavorites) { $favorites.Add($item) }
    }
}
