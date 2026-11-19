function Test-UserScriptSettings($Context) {
    $globalData = [CefFlashBrowser.Data.GlobalData]
    $flags = [Reflection.BindingFlags]'Static,NonPublic'
    $settingsField = $globalData.GetField('<Settings>k__BackingField', $flags)
    $pathField = $globalData.GetField('<SettingsPath>k__BackingField', $flags)
    $originalSettings = $globalData::Settings
    $originalPath = $globalData::SettingsPath
    $language = $Context.LanguageService.CurrentLanguage
    $path = Join-Path $Context.TemporaryPath 'userscript-settings.json'
    $downloadDirectory = Join-Path $Context.TemporaryPath 'Custom downloads'
    $window = $null
    try {
        $pathField.SetValue($null, $path)
        @{
            Language = $language
            EnableUserScripts = $false
            TrustedUserScriptPageWorld = $true
            DefaultUserScriptPermissionBehavior = 1
            UserScriptDownloadDirectory = $downloadDirectory
        } | ConvertTo-Json | Set-Content -LiteralPath $path -Encoding UTF8
        $globalData::InitSettings()

        $window = New-Object CefFlashBrowser.Views.SettingsWindow
        $window.WindowStartupLocation = [System.Windows.WindowStartupLocation]::Manual
        $window.Left = -10000
        $window.ShowInTaskbar = $false
        $window.ShowActivated = $false
        $window.Show()

        $group = $window.FindName('userScriptSettings')
        $enabled = $window.FindName('enableUserScripts')
        $trusted = $window.FindName('trustedUserScriptPageWorld')
        $permission = $window.FindName('defaultUserScriptPermissionBehavior')
        Wait-Regression $Context { $permission.Items.Count -eq 3 -and $null -ne $permission.SelectedValue } 'userscript permission choices are bound'
        Assert-Regression ($group.Items.Count -eq 3 -and !$enabled.IsChecked -and $trusted.IsChecked -and
            $permission.SelectedValue -eq 'Allow') 'userscript settings display the three saved preferences without a download directory control'

        $nextLanguage = if ($language -eq 'en-US') { 'zh-CN' } else { 'en-US' }
        $Context.LanguageService.SetCurrentLanguage($nextLanguage)
        Wait-Regression $Context {
            $group.Header -eq $Context.LanguageService.GetString('settings_headerUserScripts') -and
            $permission.Items[0].Name -eq $Context.LanguageService.GetString('userScriptPermissionBehavior_prompt') -and
            $permission.Items[1].Name -eq $Context.LanguageService.GetString('userScriptPermissionBehavior_allow') -and
            $permission.Items[2].Name -eq $Context.LanguageService.GetString('userScriptPermissionBehavior_deny')
        } 'userscript settings labels follow the selected language'
        Assert-Regression ([System.Windows.Automation.AutomationProperties]::GetName($enabled) -ceq 'EnableUserScripts' -and
            [System.Windows.Automation.AutomationProperties]::GetName($trusted) -ceq 'TrustedUserScriptPageWorld' -and
            [System.Windows.Automation.AutomationProperties]::GetName($permission) -ceq 'DefaultUserScriptPermissionBehavior') 'userscript settings keep stable automation names across languages'

        $enabled.IsChecked = $true
        $trusted.IsChecked = $false
        $permission.SelectedValue = [CefFlashBrowser.Models.UserScriptPermissionBehavior]::Deny
        Wait-Regression $Context {
            $globalData::Settings.EnableUserScripts -and !$globalData::Settings.TrustedUserScriptPageWorld -and
            $globalData::Settings.DefaultUserScriptPermissionBehavior -eq 'Deny'
        } 'editing userscript settings updates the model'
        $window.Close()
        $window = $null

        $saved = Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
        Assert-Regression ($saved.EnableUserScripts -and !$saved.TrustedUserScriptPageWorld -and
            $saved.DefaultUserScriptPermissionBehavior -is [int] -and $saved.DefaultUserScriptPermissionBehavior -eq 2 -and
            $saved.UserScriptDownloadDirectory -eq $downloadDirectory) 'closing settings saves the edits and preserves the configured download directory'
    } finally {
        if ($null -ne $window) { $window.Close() }
        $settingsField.SetValue($null, $originalSettings)
        $pathField.SetValue($null, $originalPath)
        $Context.LanguageService.SetCurrentLanguage($language)
    }
}
