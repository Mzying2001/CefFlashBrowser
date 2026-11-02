function Test-LanguageService($Context) {
    $service = $Context.LanguageService
    $settings = [CefFlashBrowser.Data.GlobalData]::Settings
    $savedLanguage = $service.CurrentLanguage
    $messages = New-Object 'System.Collections.Generic.List[object]'
    $propertyChanges = New-Object 'System.Collections.Generic.List[object]'
    $onPropertyChanged = [ComponentModel.PropertyChangedEventHandler]({
        param($sender, $eventArgs)
        $propertyChanges.Add($eventArgs.PropertyName)
    }.GetNewClosure())
    $service.add_PropertyChanged($onPropertyChanged)
    $onLanguageChanged = [Delegate]::CreateDelegate([Action[object]], $messages, 'Add')
    $token = [CefFlashBrowser.Data.MessageTokens]::LANGUAGE_CHANGED
    [void][SimpleMvvm.Messaging.Messenger]::Global.Register($token, $onLanguageChanged)
    try {
        $languages = $service.GetSupportedLanguage()
        Assert-Regression ($languages -contains 'zh-CN' -and $languages -contains 'en-US') 'language construction loads supported translations without an init call'
        foreach ($language in $languages) {
            $propertyChangeCount = $propertyChanges.Count
            $service.SetCurrentLanguage($language)
            Assert-Regression ($service.CurrentLanguage -eq $language -and $settings.Language -eq $language -and
                $Context.Application.FindResource('language_name') -eq $service.GetLanguageName($language) -and
                $service.GetString('language_name') -eq $service.GetLanguageName($language) -and
                ![string]::IsNullOrWhiteSpace($service.GetLocale($language))) "language switching updates resources, settings and lookups ($language)"
            Assert-Regression ($messages[$messages.Count - 1] -eq $language) "language switching sends its language in the notification ($language)"
            Assert-Regression ($propertyChanges.Count -eq $propertyChangeCount + 1 -and
                $propertyChanges[$propertyChanges.Count - 1] -eq 'CurrentLanguage') "language switching notifies CurrentLanguage bindings ($language)"
        }

        $service.SetCurrentLanguage('en-US')
        Assert-Regression ($service.GetFormattedString('message_removeItem', @('sample')) -eq
            'Are you sure to remove "sample"?') 'formatted language lookups use the selected translation'
        Assert-Regression ($service.GetString('regression_missing_language_key') -eq '' -and
            $service.GetFormattedString('regression_missing_language_key', @('sample')) -eq '') 'missing language keys return empty strings'

        $dictionary = @($Context.Application.Resources.MergedDictionaries | Where-Object {
            $_ -is [CefFlashBrowser.Infrastructure.Wpf.NamedResourceDictionary] -and $_.get_Name() -eq 'Language'
        })[0]
        $dictionary['regression_language_fallback'] = 'retained translation'
        $service.SetCurrentLanguage('zh-CN')
        Assert-Regression ($service.GetString('regression_language_fallback') -eq 'retained translation') 'switching languages preserves translations missing from the target dictionary'

        $dictionary = $Context.Application.Resources.MergedDictionaries[0]
        $messageCount = $messages.Count
        $propertyChangeCount = $propertyChanges.Count
        foreach ($language in @('regression-unsupported', $null)) {
            Assert-Regression (!$service.IsSupportedLanguage($language)) 'unsupported and null language identifiers are rejected'
            $service.SetCurrentLanguage($language)
            Assert-Regression ($service.CurrentLanguage -eq 'zh-CN' -and $settings.Language -eq 'zh-CN' -and
                [object]::ReferenceEquals($dictionary, $Context.Application.Resources.MergedDictionaries[0]) -and
                $messages.Count -eq $messageCount -and $propertyChanges.Count -eq $propertyChangeCount) 'invalid language changes leave resources, settings and notifications unchanged'
            Assert-Regression ($service.GetString($language, 'language_name') -eq $service.GetString('language_name')) 'unsupported language lookups fall back to the active resources'
        }
        $logStream = [IO.File]::Open((Join-Path $Context.TemporaryPath 'app.log'),
            [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
        $logReader = New-Object IO.StreamReader($logStream)
        try { $log = $logReader.ReadToEnd() } finally { $logReader.Dispose() }
        Assert-Regression ($log.Contains("Language 'regression-unsupported' is not supported.") -and
            $log.Contains("Language 'regression-unsupported' is not supported. Falling back to current language.")) 'language errors are written through the injected logger'
    } finally {
        $service.remove_PropertyChanged($onPropertyChanged)
        [void][SimpleMvvm.Messaging.Messenger]::Global.Unregister($token, $onLanguageChanged)
        $service.SetCurrentLanguage($savedLanguage)
    }
}

function Get-LanguageItemText($Element) {
    if ($Element -is [System.Windows.Controls.TextBlock]) { return $Element.Text }
    for ($index = 0; $index -lt [System.Windows.Media.VisualTreeHelper]::GetChildrenCount($Element); $index++) {
        $text = Get-LanguageItemText ([System.Windows.Media.VisualTreeHelper]::GetChild($Element, $index))
        if ($null -ne $text) { return $text }
    }
}

function Test-LanguageSelectors($Context) {
    $service = $Context.LanguageService
    $savedLanguage = $service.CurrentLanguage
    $settings = [CefFlashBrowser.Data.GlobalData]::Settings
    $savedWindowSize = $settings.MainWindowSizeInfo
    $mainWindow = $null
    $dialog = $null
    try {
        $mainWindow = New-Object CefFlashBrowser.Views.MainWindow
        $dialog = New-Object CefFlashBrowser.Views.Dialogs.SelectLanguageDialog
        foreach ($window in @($mainWindow, $dialog)) {
            $window.WindowStartupLocation = [System.Windows.WindowStartupLocation]::Manual
            $window.Left = -10000
            $window.ShowInTaskbar = $false
            $window.ShowActivated = $false
            $window.Show()
        }
        $menu = $mainWindow.FindName('languageMenu')
        $menu.Parent.IsSubmenuOpen = $true
        $menu.IsSubmenuOpen = $true
        $list = $dialog.FindName('langList')
        $languages = $service.GetSupportedLanguage()
        Wait-Regression $Context {
            $null -ne $menu.ItemContainerGenerator.ContainerFromItem($languages[0]) -and
                $list.SelectedItem -eq $savedLanguage
        } 'language selectors initially reflect the saved language'
        Assert-Regression (($menu.Items -join ',') -eq ($languages -join ',') -and
            ($list.Items -join ',') -eq ($languages -join ',')) 'both language selectors contain language codes in the service order'
        foreach ($language in $languages) {
            $item = $menu.ItemContainerGenerator.ContainerFromItem($language)
            Assert-Regression ($item.Header -eq $service.GetLanguageName($language) -and
                $item.CommandParameter -eq $language -and $null -ne $item.Command -and
                $item.IsChecked -eq ($language -eq $savedLanguage)) "language menu displays its name, command parameter and initial check ($language)"
            $list.ScrollIntoView($language)
            $list.UpdateLayout()
            Wait-Regression $Context {
                $container = $list.ItemContainerGenerator.ContainerFromItem($language)
                $null -ne $container -and (Get-LanguageItemText $container) -eq $service.GetLanguageName($language)
            } "language dialog displays the language name ($language)"
        }

        foreach ($language in @('en-US', 'zh-TW', 'de', 'en-US')) {
            $service.SetCurrentLanguage($language)
            Wait-Regression $Context {
                $list.SelectedItem -eq $language -and $menu.ItemContainerGenerator.ContainerFromItem($language).IsChecked
            } "language selectors follow service changes ($language)"
            foreach ($candidate in $languages) {
                Assert-Regression ($menu.ItemContainerGenerator.ContainerFromItem($candidate).IsChecked -eq
                    ($candidate -eq $language)) "language menu has exactly the current language checked ($language, $candidate)"
            }
        }

        $item = $menu.ItemContainerGenerator.ContainerFromItem('zh-CN')
        Invoke-BrowserCommand $item.Command $item.CommandParameter
        Wait-Regression $Context {
            $service.CurrentLanguage -eq 'zh-CN' -and $settings.Language -eq 'zh-CN' -and $list.SelectedItem -eq 'zh-CN'
        } 'language menu commands switch the language and update the open dialog'
        $list.ScrollIntoView('en-US')
        $list.UpdateLayout()
        $list.ItemContainerGenerator.ContainerFromItem('en-US').IsSelected = $true
        Wait-Regression $Context {
            $service.CurrentLanguage -eq 'en-US' -and $settings.Language -eq 'en-US' -and
                $menu.ItemContainerGenerator.ContainerFromItem('en-US').IsChecked -and
                !$menu.ItemContainerGenerator.ContainerFromItem('zh-CN').IsChecked
        } 'language dialog selection switches the language and updates the menu'
        $service.SetCurrentLanguage('zh-TW')
        Wait-Regression $Context { $list.SelectedItem -eq 'zh-TW' } 'language dialog keeps following service changes after user selection'
    } finally {
        if ($null -ne $dialog) { $dialog.Close() }
        if ($null -ne $mainWindow) { $mainWindow.Close() }
        $settings.MainWindowSizeInfo = $savedWindowSize
        $service.SetCurrentLanguage($savedLanguage)
    }
}
