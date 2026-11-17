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

function Get-ManagerIconText($Icon) {
    $content = $Icon.Template.FindName('customIcon', $Icon)
    return @(Find-FaviconElements $content ([Windows.Controls.TextBlock]))[0]
}

function Test-BrowserIconStyleReuse($Context) {
    $hostWindow = New-Object Windows.Window
    $hostWindow.Left = -10000
    $hostWindow.ShowInTaskbar = $false
    $hostWindow.ShowActivated = $false
    $panel = New-Object Windows.Controls.StackPanel
    $hostWindow.Content = $panel
    try {
        $style = $Context.Application.FindResource([CefFlashBrowser.Views.Custom.BrowserIcon]::TextFallbackStyleKey)
        $icons = @('A', '中') | ForEach-Object {
            $value = New-Object CefFlashBrowser.Views.Custom.BrowserIcon
            $value.Style = $style
            $value.DefaultIcon = $_
            [void]$panel.Children.Add($value)
            $value
        }
        $plain = New-Object CefFlashBrowser.Views.Custom.BrowserIcon
        [void]$panel.Children.Add($plain)
        $custom = New-Object CefFlashBrowser.Views.Custom.BrowserIcon
        $customText = New-Object Windows.Controls.TextBlock
        $customText.Text = 'custom'
        $custom.DefaultIcon = $customText
        [void]$panel.Children.Add($custom)
        $hostWindow.Show()
        $hostWindow.UpdateLayout()
        Assert-Regression ((Get-ManagerIconText $icons[0]).Text -eq 'A' -and
            (Get-ManagerIconText $icons[1]).Text -eq '中' -and
            ![object]::ReferenceEquals((Get-ManagerIconText $icons[0]), (Get-ManagerIconText $icons[1]))) 'shared fallback style creates independent content without a script data context'
        Assert-Regression ((Get-ManagerIconText $icons[0]).FontSize -eq 8) 'fallback text inherits its font size from the reusable style'
        $icons[0].DefaultIcon = 'B'
        $hostWindow.UpdateLayout()
        Assert-Regression ((Get-ManagerIconText $icons[0]).Text -eq 'B' -and
            (Get-ManagerIconText $icons[1]).Text -eq '中') 'changing one fallback leaves other styled icons unchanged'
        Assert-Regression ($null -eq $plain.DefaultIconTemplate -and
            $plain.Template.FindName('defaultIcon', $plain).Visibility -eq [Windows.Visibility]::Visible) 'unstyled browser icons retain their default page image'
        Assert-Regression ([object]::ReferenceEquals((Get-ManagerIconText $custom), $customText)) 'existing custom fallback elements remain supported'
    } finally {
        $hostWindow.Close()
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
    $iconFixture = New-Object FaviconHttpFixture
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
        Test-BrowserIconStyleReuse $Context
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
        $icon = @(Find-FaviconElements ($list.ItemContainerGenerator.ContainerFromItem($item)) ([CefFlashBrowser.Views.Custom.BrowserIcon]))[0]
        Assert-Regression ($icon.Width -eq 24 -and $icon.Height -eq 24) 'script list displays a 24px icon'
        Assert-Regression ($icon.Template.FindName('fallbackIcon', $icon).Visibility -eq [Windows.Visibility]::Visible -and
            (Get-ManagerIconText $icon).Text -eq 'M') 'script without an icon displays its name initial'
        foreach ($name in @('Bilibili', '哔哩哔哩')) {
            $item.Source = $source.Replace('Manager workflow', $name)
            Complete-UserScriptTask $Context ($model.SaveAsync()) "save fallback sample $name"
            $list.UpdateLayout()
            $label = Get-ManagerIconText $icon
            Assert-Regression ($label -is [Windows.Controls.TextBlock] -and $label.Text -eq $item.IconInitial -and
                $label.LayoutTransform.Value.IsIdentity -and $label.RenderTransform.Value.IsIdentity) "$name displays its initial as ordinary text without transforms"
        }
        $item.Source = $source
        Complete-UserScriptTask $Context ($model.SaveAsync()) 'restore manager script name'
        $toggle = Find-ManagerToggle ($list.ItemContainerGenerator.ContainerFromItem($item))
        Assert-Regression ($toggle.IsChecked -and $toggle.IsEnabled) 'installed list row shows its enabled switch'
        [void][Windows.Controls.Primitives.ToggleButton].GetMethod('OnClick', [Reflection.BindingFlags]'Instance,NonPublic').Invoke($toggle, @())
        Wait-Regression $Context { !$model.IsBusy -and !$item.IsEnabled } 'list switch disables the script'
        Assert-Regression (!$toggle.IsChecked) 'switch follows committed state'
        $iconSource = $source.Replace('// @version', ('// @icon ' + $iconFixture.BaseUrl + "/a-icon.png`n// @version"))
        $item.Source = $iconSource
        Assert-Regression ([string]::IsNullOrEmpty($icon.FaviconUrl)) 'unsaved edits do not refresh the list icon'
        Complete-UserScriptTask $Context ($model.SaveAsync()) 'save script icon metadata'
        Wait-Regression $Context {
            $image = $icon.Template.FindName('favicon', $icon)
            $icon.FaviconUrl -eq ($iconFixture.BaseUrl + '/a-icon.png') -and
                $null -ne $image.Source -and !$image.Source.IsDownloading -and
                $icon.Template.FindName('fallbackIcon', $icon).Visibility -eq [Windows.Visibility]::Collapsed
        } 'saved script icon loads in the list'
        $item.Source = $iconSource.Replace('/a-icon.png', '/missing.png').Replace('Manager workflow', '脚本图标')
        Complete-UserScriptTask $Context ($model.SaveAsync()) 'save missing script icon'
        Wait-Regression $Context {
            $icon.HasImageFailed -and
                $icon.Template.FindName('fallbackIcon', $icon).Visibility -eq [Windows.Visibility]::Visible -and
                (Get-ManagerIconText $icon).Text -eq '脚'
        } 'failed image displays the updated script name initial'
        $item.Source = $iconSource.Replace('/a-icon.png', '/b-icon.png')
        Complete-UserScriptTask $Context ($model.SaveAsync()) 'replace failed script icon'
        Wait-Regression $Context {
            $image = $icon.Template.FindName('favicon', $icon)
            !$icon.HasImageFailed -and $null -ne $image.Source -and !$image.Source.IsDownloading -and
                $icon.Template.FindName('fallbackIcon', $icon).Visibility -eq [Windows.Visibility]::Collapsed
        } 'valid script icon recovers after an image failure'
        $item.Source = $source
        Complete-UserScriptTask $Context ($model.SaveAsync()) 'remove script icon metadata'
        Wait-Regression $Context {
            $icon.Template.FindName('fallbackIcon', $icon).Visibility -eq [Windows.Visibility]::Visible -and
                (Get-ManagerIconText $icon).Text -eq 'M'
        } 'removing the icon restores the initial fallback'
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
            $list = $window.FindName('scriptList')
            $list.ScrollIntoView($item)
            $list.UpdateLayout()
            $icon = @(Find-FaviconElements ($list.ItemContainerGenerator.ContainerFromItem($item)) ([CefFlashBrowser.Views.Custom.BrowserIcon]))[0]
            $textBrush = $window.FindResource('PrimaryTextBrush')
            $content = $icon.Template.FindName('customIcon', $icon)
            $background = @(Find-FaviconElements $content ([Windows.Controls.Border]))[0]
            Assert-Regression ((Get-ManagerIconText $icon).Foreground.Color -eq $textBrush.Color -and
                $background.Background.Color -eq $textBrush.Color) "script fallback follows the $appearance theme"
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
        $iconFixture.Dispose()
        $Context.LanguageService.SetCurrentLanguage($language)
        Set-ManagerRegressionTheme $Context $theme
    }
}
