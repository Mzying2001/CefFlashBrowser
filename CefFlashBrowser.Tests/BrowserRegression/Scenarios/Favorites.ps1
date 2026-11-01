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
