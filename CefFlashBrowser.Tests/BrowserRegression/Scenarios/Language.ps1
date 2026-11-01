function Test-LanguageService($Context) {
    $service = $Context.LanguageService
    $settings = [CefFlashBrowser.Data.GlobalData]::Settings
    $savedLanguage = $service.GetCurrentLanguage()
    $messages = New-Object 'System.Collections.Generic.List[object]'
    $onLanguageChanged = [Delegate]::CreateDelegate([Action[object]], $messages, 'Add')
    $token = [CefFlashBrowser.Data.MessageTokens]::LANGUAGE_CHANGED
    [void][SimpleMvvm.Messaging.Messenger]::Global.Register($token, $onLanguageChanged)
    try {
        $languages = $service.GetSupportedLanguage()
        Assert-Regression ($languages -contains 'zh-CN' -and $languages -contains 'en-US') 'language construction loads supported translations without an init call'
        foreach ($language in $languages) {
            $service.SetCurrentLanguage($language)
            Assert-Regression ($service.GetCurrentLanguage() -eq $language -and $settings.Language -eq $language -and
                $Context.Application.FindResource('language_name') -eq $service.GetLanguageName($language) -and
                $service.GetString('language_name') -eq $service.GetLanguageName($language) -and
                ![string]::IsNullOrWhiteSpace($service.GetLocale($language))) "language switching updates resources, settings and lookups ($language)"
            Assert-Regression ($messages[$messages.Count - 1] -eq $language) "language switching sends its language in the notification ($language)"
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
        foreach ($language in @('regression-unsupported', $null)) {
            Assert-Regression (!$service.IsSupportedLanguage($language)) 'unsupported and null language identifiers are rejected'
            $service.SetCurrentLanguage($language)
            Assert-Regression ($service.GetCurrentLanguage() -eq 'zh-CN' -and $settings.Language -eq 'zh-CN' -and
                [object]::ReferenceEquals($dictionary, $Context.Application.Resources.MergedDictionaries[0]) -and
                $messages.Count -eq $messageCount) 'invalid language changes leave resources, settings and notifications unchanged'
            Assert-Regression ($service.GetString($language, 'language_name') -eq $service.GetString('language_name')) 'unsupported language lookups fall back to the active resources'
        }
        $logStream = [IO.File]::Open((Join-Path $Context.TemporaryPath 'app.log'),
            [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
        $logReader = New-Object IO.StreamReader($logStream)
        try { $log = $logReader.ReadToEnd() } finally { $logReader.Dispose() }
        Assert-Regression ($log.Contains("Language 'regression-unsupported' is not supported.") -and
            $log.Contains("Language 'regression-unsupported' is not supported. Falling back to current language.")) 'language errors are written through the injected logger'
    } finally {
        [void][SimpleMvvm.Messaging.Messenger]::Global.Unregister($token, $onLanguageChanged)
        $service.SetCurrentLanguage($savedLanguage)
    }
}
