function Find-ProxyPasswordBox($Element) {
    if ($Element -is [HandyControl.Controls.PasswordBox]) {
        $binding = $Element.GetBindingExpression([HandyControl.Controls.PasswordBox]::UnsafePasswordProperty)
        if ($null -ne $binding -and $binding.ParentBinding.Path.Path -eq 'ProxySettings.PlainPassword') {
            return $Element
        }
    }
    for ($index = 0; $index -lt [System.Windows.Media.VisualTreeHelper]::GetChildrenCount($Element); $index++) {
        $passwordBox = Find-ProxyPasswordBox ([System.Windows.Media.VisualTreeHelper]::GetChild($Element, $index))
        if ($null -ne $passwordBox) { return $passwordBox }
    }
}

function Test-ProxySettings($Context) {
    $globalDataType = [CefFlashBrowser.Data.GlobalData]
    $privateStatic = [Reflection.BindingFlags]'Static,NonPublic'
    $settingsField = $globalDataType.GetField('<Settings>k__BackingField', $privateStatic)
    $pathField = $globalDataType.GetField('<SettingsPath>k__BackingField', $privateStatic)
    $savedSettings = [CefFlashBrowser.Data.GlobalData]::Settings
    $savedPath = [CefFlashBrowser.Data.GlobalData]::SettingsPath
    $path = Join-Path $Context.TemporaryPath 'proxy-settings.json'
    $legacyPassword = 'legacy-proxy-secret'
    $password = 'new-proxy-secret'
    $window = $null
    try {
        $pathField.SetValue($null, $path)
        $fixture = @{
            Language = 'en-US'
            UnknownSetting = 'keep'
            ProxySettings = @{
                EnableProxy = $true
                IP = '127.0.0.1'
                Port = '8080'
                UserName = 'proxy-user'
                Password = $legacyPassword
                PlainPassword = $legacyPassword
            }
        } | ConvertTo-Json
        [IO.File]::WriteAllText($path, $fixture, [Text.Encoding]::UTF8)
        [CefFlashBrowser.Data.GlobalData]::InitSettings()
        $settings = [CefFlashBrowser.Data.GlobalData]::Settings
        Assert-Regression ($settings.ProxySettings.Password -eq '' -and $settings.ProxySettings.PlainPassword -eq '' -and
            $settings.Language -eq 'en-US' -and $settings.ProxySettings.EnableProxy -and
            $settings.ProxySettings.IP -eq '127.0.0.1' -and $settings.ProxySettings.Port -eq '8080' -and
            $settings.ProxySettings.UserName -eq 'proxy-user') 'loading legacy proxy credentials discards plaintext and preserves other settings'
        Assert-Regression ([CefFlashBrowser.Data.GlobalData]::SaveSettings()) 'legacy proxy settings save successfully'
        $json = [IO.File]::ReadAllText($path)
        $saved = $json | ConvertFrom-Json
        Assert-Regression (!$json.Contains($legacyPassword) -and $saved.ProxySettings.Password -eq '' -and
            $saved.ProxySettings.PSObject.Properties.Name -notcontains 'PlainPassword' -and
            $saved.UnknownSetting -eq 'keep') 'settings merge removes legacy plaintext and preserves unrelated fields'

        $settings.ProxySettings.PlainPassword = $password
        Assert-Regression ([CefFlashBrowser.Data.GlobalData]::SaveSettings()) 'encrypted proxy settings save successfully'
        [CefFlashBrowser.Data.GlobalData]::InitSettings()
        Assert-Regression ([CefFlashBrowser.Data.GlobalData]::Settings.ProxySettings.PlainPassword -eq $password -and
            ![IO.File]::ReadAllText($path).Contains($password)) 'proxy passwords survive file save and reload without plaintext on disk'

        $window = New-Object CefFlashBrowser.Views.SettingsWindow
        $window.WindowStartupLocation = [System.Windows.WindowStartupLocation]::Manual
        $window.Left = -10000
        $window.ShowInTaskbar = $false
        $window.ShowActivated = $false
        $window.Show()
        Wait-Regression $Context { $null -ne (Find-ProxyPasswordBox $window) } 'settings password binding is available'
        $passwordBox = Find-ProxyPasswordBox $window
        Assert-Regression ($passwordBox.Password -eq $password -and !$passwordBox.ShowPassword -and
            !$passwordBox.ShowEyeButton -and $passwordBox.ActualPasswordBox.IsVisible) 'settings UI loads the proxy password into a masked password box'
        $passwordBox.ActualPasswordBox.Password = 'edited-proxy-secret'
        Wait-Regression $Context {
            [CefFlashBrowser.Data.GlobalData]::Settings.ProxySettings.PlainPassword -eq 'edited-proxy-secret'
        } 'settings password edits update encrypted credentials'
        $window.Close()
        $window = $null
        $json = [IO.File]::ReadAllText($path)
        $saved = $json | ConvertFrom-Json
        Assert-Regression (!$json.Contains('edited-proxy-secret') -and
            $saved.ProxySettings.Password.StartsWith('dpapi:v1:') -and
            $saved.ProxySettings.PSObject.Properties.Name -notcontains 'PlainPassword') 'closing settings saves only the edited password ciphertext'

        $window = New-Object CefFlashBrowser.Views.SettingsWindow
        $window.WindowStartupLocation = [System.Windows.WindowStartupLocation]::Manual
        $window.Left = -10000
        $window.ShowInTaskbar = $false
        $window.ShowActivated = $false
        $window.Show()
        $passwordBox = Find-ProxyPasswordBox $window
        Assert-Regression ($passwordBox.Password -eq 'edited-proxy-secret' -and !$passwordBox.ShowPassword) 'reopening settings restores the edited password in masked form'
        $passwordBox.ActualPasswordBox.Clear()
        Wait-Regression $Context {
            [CefFlashBrowser.Data.GlobalData]::Settings.ProxySettings.Password -eq ''
        } 'clearing the password box removes the stored ciphertext'
        $window.Close()
        $window = $null
        [CefFlashBrowser.Data.GlobalData]::InitSettings()
        Assert-Regression ([CefFlashBrowser.Data.GlobalData]::Settings.ProxySettings.Password -eq '' -and
            [CefFlashBrowser.Data.GlobalData]::Settings.ProxySettings.PlainPassword -eq '') 'cleared proxy passwords remain empty after save and reload'

        $invalidSettings = @{ ProxySettings = @{ EnableProxy = $legacyPassword } } | ConvertTo-Json
        [IO.File]::WriteAllText($path, $invalidSettings)
        [CefFlashBrowser.Data.GlobalData]::InitSettings()
        Assert-Regression (![CefFlashBrowser.Data.GlobalData]::Settings.ProxySettings.EnableProxy) 'invalid settings fall back without logging sensitive JSON exception values'
        [IO.File]::WriteAllText($path, '{"ProxySettings":{"Password":"' + $legacyPassword + '"},')
        Assert-Regression ([CefFlashBrowser.Data.GlobalData]::SaveSettings()) 'invalid settings merge falls back to a safe overwrite'
        $logStream = [IO.File]::Open((Join-Path $Context.TemporaryPath 'app.log'),
            [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
        $logReader = New-Object IO.StreamReader($logStream)
        try { $log = $logReader.ReadToEnd() } finally { $logReader.Dispose() }
        Assert-Regression (!$log.Contains($legacyPassword) -and !$log.Contains($password) -and
            !$log.Contains('edited-proxy-secret')) 'settings loading and saving do not leak password values into logs'
    } finally {
        try {
            if ($null -ne $window) { $window.Close() }
        } finally {
            $settingsField.SetValue($null, $savedSettings)
            $pathField.SetValue($null, $savedPath)
        }
    }
}
