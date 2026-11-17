function Set-ManagerRegressionTheme($Context, $Theme) {
    # The regression harness owns a plain WPF Application, not the production App.
    $skin = $Context.Application.Resources.MergedDictionaries[1]
    $resources = $Context.Application.Resources.MergedDictionaries[2]
    $name = if ($Theme -eq [CefFlashBrowser.Models.Theme]::Dark) { 'SkinDark' } else { 'SkinDefault' }
    $skin.set_Source([Uri]("pack://application:,,,/HandyControl;component/Themes/$name.xaml"))
    $resources.set_Source($resources.get_Source())
}

function Find-ManagerToggle($Element) {
    if ($Element -is [Windows.Controls.CheckBox]) { return $Element }
    for ($index = 0; $index -lt [Windows.Media.VisualTreeHelper]::GetChildrenCount($Element); $index++) {
        $found = Find-ManagerToggle ([Windows.Media.VisualTreeHelper]::GetChild($Element, $index))
        if ($null -ne $found) { return $found }
    }
}

function Save-ManagerWithConfirmation($Context, $Model, [bool]$Accept) {
    $state = @{ Count = 0; Message = '' }
    $timer = New-Object Windows.Threading.DispatcherTimer
    $timer.Interval = [TimeSpan]::FromMilliseconds(50)
    $handler = [EventHandler]({
        $dialog = @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.Dialogs.JsConfirmDialog] }) | Select-Object -First 1
        if ($null -ne $dialog) {
            $state.Count++
            $state.Message = $dialog.Message
            $timer.Stop()
            [CefFlashBrowser.Utils.DialogHelper]::SetDialogResult($dialog, $Accept)
        }
    }.GetNewClosure())
    $timer.add_Tick($handler)
    $timer.Start()
    try {
        Complete-UserScriptTask $Context ($Model.SaveAsync()) 'reviewed script installation'
        Assert-Regression ($state.Count -eq 1 -and $state.Message.Contains('@require')) 'external installation opens exactly one confirmation with permissions and dependencies'
    } finally {
        $timer.Stop()
        $timer.remove_Tick($handler)
    }
}

function Test-UserScriptManager($Context) {
    $window = $null
    $browserWindow = $null
    $fixture = New-Object UserScriptHttpFixture
    $language = $Context.LanguageService.CurrentLanguage
    $theme = [CefFlashBrowser.Data.GlobalData]::Settings.Theme
    $model = $Context.Application.Resources['Locator'].UserScriptManagerViewModel
    $source = @'
// ==UserScript==
// @name Manager workflow
// @namespace CefFlashBrowser.Regression
// @version 1.0.0
// @match file:///*
// @sandbox DOM
// @grant none
// @run-at document-end
// ==/UserScript==
document.documentElement.setAttribute('data-manager', 'saved');
'@
    try {
        [CefFlashBrowser.Services.WindowManager]::ShowUserScriptManager()
        $window = @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.UserScriptManager] })[0]
        Wait-Regression $Context { $window.IsLoaded -and !$model.IsBusy } 'manager initialized'
        [CefFlashBrowser.Services.WindowManager]::ShowUserScriptManager()
        Assert-Regression (@($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.UserScriptManager] }).Count -eq 1) 'manager entry reuses a single window'
        Assert-Regression ($window.InputBindings.Count -eq 3) 'manager exposes new, save and close keyboard bindings'
        Invoke-BrowserCommand $model.NewCommand $null
        $item = $model.SelectedScript
        $editor = $window.FindName('sourceEditor')
        $editor.SetCurrentValue([Windows.Controls.TextBox]::TextProperty, $source)
        $editor.GetBindingExpression([Windows.Controls.TextBox]::TextProperty).UpdateSource()
        Complete-UserScriptTask $Context ($model.SaveAsync()) 'save new script without confirmation'
        Assert-Regression ($item.IsInstalled -and !$item.IsDirty -and $item.IsEnabled) 'new script saves and enables directly'
        $key = $item.Installation.ScriptKey
        $sourcePath = Join-Path (Join-Path $Context.TemporaryPath ('MonkeySharp\' + $key)) 'userscript.js'
        Assert-Regression ([IO.File]::ReadAllText($sourcePath) -eq $source) 'manager persists source through the shared repository'
        $browserWindow = New-RegressionWindow $Context
        $browser = $browserWindow.ViewModel.CurrentBrowser.Browser
        $browserWindow.ViewModel.CurrentBrowser.Address = $Context.AuxiliaryAddress
        Wait-Regression $Context { (Invoke-JavaScript $Context $browser "document.documentElement && document.documentElement.getAttribute('data-manager')") -eq 'saved' } 'manager-installed script executes in real CEF'
        $list = $window.FindName('scriptList')
        $list.UpdateLayout()
        $toggle = Find-ManagerToggle ($list.ItemContainerGenerator.ContainerFromItem($item))
        Assert-Regression ($toggle.IsChecked -and $toggle.IsEnabled) 'installed list row shows its enabled switch'
        [void][Windows.Controls.Primitives.ToggleButton].GetMethod('OnClick', [Reflection.BindingFlags]'Instance,NonPublic').Invoke($toggle, @())
        Wait-Regression $Context { !$model.IsBusy -and !$item.IsEnabled } 'list switch disables the script'
        Assert-Regression (!$toggle.IsChecked) 'switch follows committed state'
        $browserWindow.ViewModel.CurrentBrowser.Address = $Context.AuxiliarySecondAddress
        Wait-Regression $Context { $browser.Title -eq 'Second auxiliary page' } 'navigate after disabling'
        Assert-Regression ($null -eq (Invoke-JavaScript $Context $browser "document.documentElement.getAttribute('data-manager')")) 'disabled manager script does not execute in next document'
        $item.Source += "`n// unsaved session draft"
        $model.NewScript()
        $draft = $model.SelectedScript
        $window.Close()
        [CefFlashBrowser.Services.WindowManager]::ShowUserScriptManager()
        $window = @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.UserScriptManager] })[0]
        Wait-Regression $Context { $window.IsLoaded -and !$model.IsBusy } 'manager reopened'
        Assert-Regression ([object]::ReferenceEquals($draft, $model.SelectedScript) -and $item.IsDirty -and $item.Source.Contains('unsaved session draft')) 'selection and unsaved drafts survive manager closure'
        $path = Join-Path $Context.TemporaryPath 'manager-import.user.js'
        [IO.File]::WriteAllText($path, $source, (New-Object Text.UTF8Encoding($true)))
        Complete-UserScriptTask $Context ($model.ImportFileAsync($path)) 'load local file draft'
        Save-ManagerWithConfirmation $Context $model $false
        Assert-Regression (!$model.SelectedScript.IsInstalled) 'declined installation leaves an editable draft'
        Save-ManagerWithConfirmation $Context $model $true
        Assert-Regression ($model.SelectedScript.IsInstalled) 'accepted local script installs'
        $fixture.ScriptSource = $source
        Complete-UserScriptTask $Context ($model.ImportUrlAsync($fixture.BaseUrl + '/manager.user.js')) 'download URL draft'
        Assert-Regression (!$model.SelectedScript.IsInstalled -and $fixture.RequestsFor('/manager.user.js') -eq 1) 'URL download produces a pending draft'
        Save-ManagerWithConfirmation $Context $model $true
        Assert-Regression ($fixture.RequestsFor('/manager.user.js') -eq 1) 'confirmed installation uses reviewed bytes without another source request'
        $model.SelectedScript.Source += "`n// exported draft"
        $export = Join-Path $Context.TemporaryPath 'manager-export.user.js'
        Complete-UserScriptTask $Context ($model.ExportAsync($export)) 'export current draft'
        Assert-Regression ([IO.File]::ReadAllText($export) -eq $model.SelectedScript.Source) 'export includes current unsaved source'
        foreach ($locale in @('en-US', 'zh-CN', 'zh-TW', 'de', 'fr-FR', 'it', 'ja', 'ko', 'ru')) {
            $Context.LanguageService.SetCurrentLanguage($locale)
            Wait-Regression $Context { $window.Title -eq $Context.LanguageService.GetString('userScriptManager_title') } "manager language ($locale)"
            Assert-Regression (![string]::IsNullOrEmpty($window.Title)) 'manager title is localized in every supported language'
        }
        $Context.LanguageService.SetCurrentLanguage('zh-CN')
        foreach ($appearance in @([CefFlashBrowser.Models.Theme]::Light, [CefFlashBrowser.Models.Theme]::Dark)) {
            Set-ManagerRegressionTheme $Context $appearance
            $frame = New-Object Windows.Threading.DispatcherFrame
            [void]$Context.Application.Dispatcher.BeginInvoke([Windows.Threading.DispatcherPriority]::ApplicationIdle,
                [Action]({ $frame.Continue = $false }.GetNewClosure()))
            [Windows.Threading.Dispatcher]::PushFrame($frame)
            $window.UpdateLayout()
            $bitmap = New-Object Windows.Media.Imaging.RenderTargetBitmap(1000, 650, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
            $bitmap.Render($window)
            $encoder = New-Object Windows.Media.Imaging.PngBitmapEncoder
            $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
            $capturePath = Join-Path ([IO.Path]::GetTempPath()) ("cfb-manager-$appearance-$($Context.Platform).png")
            $stream = [IO.File]::Create($capturePath)
            try { $encoder.Save($stream) } finally { $stream.Dispose() }
            Assert-Regression ($null -ne $window.Background) "manager renders with $appearance theme"
        }
    } finally {
        if ($null -ne $browserWindow) { $browserWindow.ForceCloseWindow() }
        if ($null -ne $window) { $window.Close() }
        $model.Close()
        foreach ($item in @($model.Scripts)) {
            if ($item.IsInstalled) {
                Complete-UserScriptTask $Context ($Context.UserScriptService.Repository.RemoveAsync($item.Installation.ScriptKey, [Threading.CancellationToken]::None)) 'remove manager fixture' | Out-Null
            }
        }
        $model.Scripts.Clear()
        $model.SelectedScript = $null
        $fixture.Dispose()
        $Context.LanguageService.SetCurrentLanguage($language)
        Set-ManagerRegressionTheme $Context $theme
    }
}
