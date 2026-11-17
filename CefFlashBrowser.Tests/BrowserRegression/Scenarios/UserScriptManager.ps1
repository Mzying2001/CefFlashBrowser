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

function Send-ManagerShortcut($Context, $Window, [int]$Key) {
    Set-TabWindowForeground $Context $Window
    $editor = $Window.FindName('sourceEditor')
    [void]$editor.Focus()
    Wait-Regression $Context { $editor.TextArea.IsKeyboardFocusWithin } 'script editor receives keyboard input'
    [RegressionKeyboard]::SetKey(0x11, $false)
    try {
        [RegressionKeyboard]::PressKey($Key)
    } finally {
        [RegressionKeyboard]::SetKey(0x11, $true)
    }
}

function Test-ManagerSourceEditor($Context, $Window) {
    $model = $Window.DataContext
    $editor = $Window.FindName('sourceEditor')
    $previousSelection = $model.SelectedScript
    $first = $null
    $second = $null
    $clipboardCaptured = $false
    try {
        $model.SelectedScript = $null
        Wait-Regression $Context { !$editor.IsEnabled -and $editor.Text -eq '' } 'empty script selection clears and disables the editor'
        Assert-Regression ($editor -is [ICSharpCode.AvalonEdit.TextEditor] -and $editor.ShowLineNumbers -and !$editor.WordWrap) 'manager uses AvalonEdit with line numbers and unwrapped source'
        Assert-Regression ($editor.Options.IndentationSize -eq 4 -and !$editor.Options.ConvertTabsToSpaces) 'editor preserves tabs with a four-column width'
        $model.NewScript()
        $first = $model.SelectedScript
        $sample = "const answer = 42;`r`n// 中文 comment`r`nlet value = `"text`";"
        $first.Source = $sample
        Wait-Regression $Context { $editor.IsEnabled -and $editor.Text -ceq $sample } 'model source changes replace the displayed document without newline conversion'
        $highlighter = New-Object ICSharpCode.AvalonEdit.Highlighting.DocumentHighlighter($editor.Document, $editor.SyntaxHighlighting)
        try {
            $colors = @(1..3 | ForEach-Object { $highlighter.HighlightLine($_).Sections | ForEach-Object { $_.Color.Name } })
            foreach ($name in @('JavaScriptKeyWords', 'Digits', 'Comment', 'String')) {
                Assert-Regression ($colors -contains $name) "JavaScript highlights $name tokens"
            }
        } finally {
            $highlighter.Dispose()
        }
        $shared = [ICSharpCode.AvalonEdit.Highlighting.HighlightingManager]::Instance.GetDefinition('JavaScript')
        $other = New-Object CefFlashBrowser.Views.Custom.ScriptEditor
        Assert-Regression (![object]::ReferenceEquals($shared, $editor.SyntaxHighlighting) -and
            ![object]::ReferenceEquals($other.SyntaxHighlighting, $editor.SyntaxHighlighting)) 'editors own independent JavaScript color definitions'
        $editor.CaretOffset = $editor.Text.Length
        $suffix = "`r`n// typed draft"
        $editor.TextArea.PerformTextInput($suffix)
        Assert-Regression ($first.Source -ceq ($sample + $suffix) -and $editor.CaretOffset -eq $editor.Text.Length -and
            $editor.Document.UndoStack.CanUndo) 'typing updates the draft immediately without resetting caret or undo'
        $editMenu = $Window.Content.Children[0].Items[1]
        $wrongTargets = @($editMenu.Items | Where-Object {
            $_ -is [Windows.Controls.MenuItem] -and ![object]::ReferenceEquals($_.CommandTarget, $editor.TextArea)
        })
        Assert-Regression ($wrongTargets.Count -eq 0) 'edit menu routes commands to the AvalonEdit text area'
        $editMenu.Items[0].Command.Execute($null, $editMenu.Items[0].CommandTarget)
        Assert-Regression ($first.Source -ceq $sample) 'edit menu undo restores the previous draft'
        $editMenu.Items[1].Command.Execute($null, $editMenu.Items[1].CommandTarget)
        Assert-Regression ($first.Source -ceq ($sample + $suffix)) 'edit menu redo restores the typed draft'
        Send-ManagerShortcut $Context $Window 0x5A
        Wait-Regression $Context { $first.Source -ceq $sample } 'Ctrl+Z reaches AvalonEdit undo'
        Send-ManagerShortcut $Context $Window 0x59
        Wait-Regression $Context { $first.Source -ceq ($sample + $suffix) } 'Ctrl+Y reaches AvalonEdit redo'
        [RegressionKeyboard]::PressKey(0x09)
        Wait-Regression $Context { $first.Source.EndsWith("`t") } 'Tab inserts a tab instead of moving keyboard focus'
        Assert-Regression ($editor.TextArea.IsKeyboardFocusWithin) 'tab indentation retains editor focus'
        $clipboard = [UserScriptClipboardProbe]::Capture()
        $clipboardCaptured = $true
        $editor.Select(0, 5)
        $editMenu.Items[4].Command.Execute($null, $editMenu.Items[4].CommandTarget)
        Assert-Regression ([Windows.Clipboard]::GetText() -ceq 'const') 'copy menu copies the editor selection'
        $editMenu.Items[3].Command.Execute($null, $editMenu.Items[3].CommandTarget)
        Assert-Regression (!$first.Source.StartsWith('const')) 'cut menu updates the bound draft'
        $editMenu.Items[5].Command.Execute($null, $editMenu.Items[5].CommandTarget)
        Assert-Regression ($first.Source.StartsWith('const')) 'paste menu updates the bound draft'
        $editMenu.Items[6].Command.Execute($null, $editMenu.Items[6].CommandTarget)
        Assert-Regression ($editor.SelectionLength -eq $editor.Text.Length) 'select-all menu selects the full document'
        $sameSource = $first.Source
        Send-ManagerShortcut $Context $Window 0x4E
        Wait-Regression $Context { ![object]::ReferenceEquals($first, $model.SelectedScript) } 'Ctrl+N creates a new script from editor focus'
        $second = $model.SelectedScript
        $second.Source = $sameSource
        Wait-Regression $Context { $editor.Text -ceq $sameSource } 'another script can display identical source'
        $editor.CaretOffset = $editor.Text.Length
        $editor.TextArea.PerformTextInput('// second script')
        $model.SelectedScript = $first
        Wait-Regression $Context { $editor.Text -ceq $first.Source } 'selection restores the first draft'
        Assert-Regression (!$editor.Document.UndoStack.CanUndo -and !$editor.Document.UndoStack.CanRedo -and
            $second.Source.EndsWith('// second script')) 'switching scripts isolates undo and preserves the other draft'
        $editor.CaretOffset = $editor.Text.Length
        $editor.TextArea.PerformTextInput('// same-source switch')
        $second.Source = $first.Source
        $model.SelectedScript = $second
        Wait-Regression $Context { $editor.Text -ceq $second.Source } 'identical-source selection is displayed'
        Assert-Regression (!$editor.Document.UndoStack.CanUndo -and !$editor.Document.UndoStack.CanRedo) 'identical-source script switches also clear undo and redo'
    } finally {
        if ($clipboardCaptured) { [UserScriptClipboardProbe]::Restore($clipboard) }
        $model.SelectedScript = $previousSelection
        if ($null -ne $first) { [void]$model.Scripts.Remove($first) }
        if ($null -ne $second) { [void]$model.Scripts.Remove($second) }
    }
}

function Get-ManagerColorContrast($First, $Second) {
    $luminances = @($First, $Second) | ForEach-Object {
        $channels = @($_.R, $_.G, $_.B) | ForEach-Object {
            $value = $_ / 255.0
            if ($value -le 0.04045) { $value / 12.92 } else { [Math]::Pow(($value + 0.055) / 1.055, 2.4) }
        }
        $channels[0] * 0.2126 + $channels[1] * 0.7152 + $channels[2] * 0.0722
    }
    return ([Math]::Max($luminances[0], $luminances[1]) + 0.05) / ([Math]::Min($luminances[0], $luminances[1]) + 0.05)
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

function Open-ManagerContextMenu($Context, $Window, $Script) {
    $list = $Window.FindName('scriptList')
    $list.ScrollIntoView($Script)
    $list.UpdateLayout()
    $container = $list.ItemContainerGenerator.ContainerFromItem($Script)
    $right = New-Object Windows.Input.MouseButtonEventArgs([Windows.Input.Mouse]::PrimaryDevice, 0, [Windows.Input.MouseButton]::Right)
    $right.RoutedEvent = [Windows.UIElement]::PreviewMouseRightButtonDownEvent
    $container.RaiseEvent($right)
    Assert-Regression ([object]::ReferenceEquals($Window.DataContext.SelectedScript, $Script)) 'right click selects the target script'
    $menu = $container.ContextMenu
    $menu.PlacementTarget = $container
    $menu.IsOpen = $true
    Wait-Regression $Context {
        $menu.Items[0].Command -eq $Window.DataContext.DeleteCommand -and
            $menu.Items[0].Header -eq $Context.LanguageService.GetString('userScriptManager_delete')
    } 'script context menu binds to the manager and current language'
    return $menu
}

function Invoke-ManagerDeleteWithConfirmation($Context, $Window, $MenuItem, [Nullable[bool]]$Accept) {
    $model = $Window.DataContext
    $expected = [string]::Format($Context.LanguageService.GetString('userScriptManager_confirmDelete'), $model.SelectedScript.Name)
    $state = @{ Count = 0; Message = ''; Busy = $false }
    $timer = New-Object Windows.Threading.DispatcherTimer
    $timer.Interval = [TimeSpan]::FromMilliseconds(50)
    $handler = [EventHandler]({
        $dialog = @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.Dialogs.JsConfirmDialog] }) | Select-Object -First 1
        if ($null -ne $dialog) {
            $state.Count++
            $state.Message = $dialog.Message
            $canDelete = [Windows.Input.ICommand].GetMethod('CanExecute').Invoke($model.DeleteCommand, [object[]]@($null))
            $state.Busy = $model.IsBusy -and !$canDelete
            if ($null -eq $Accept) { $dialog.Close() }
            else { [CefFlashBrowser.Utils.DialogHelper]::SetDialogResult($dialog, $Accept) }
        }
    }.GetNewClosure())
    $timer.add_Tick($handler)
    $timer.Start()
    try {
        $owner = [Windows.Controls.ItemsControl]::ItemsControlFromItemContainer($MenuItem)
        if ($owner -is [Windows.Controls.ContextMenu]) { $owner.IsOpen = $false }
        Invoke-BrowserCommand $MenuItem.Command $MenuItem.CommandParameter
        Wait-Regression $Context { !$model.IsBusy } 'reviewed script deletion'
        Assert-Regression ($state.Count -eq 1 -and $state.Message -eq $expected) 'deletion opens exactly one localized confirmation naming the target'
        Assert-Regression ($state.Busy) 'deletion stays disabled while confirmation is open'
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
        Test-ManagerSourceEditor $Context $window
        [CefFlashBrowser.Services.WindowManager]::ShowUserScriptManager()
        Assert-Regression (@($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.UserScriptManager] }).Count -eq 1) 'manager entry reuses a single window'
        Assert-Regression ($window.InputBindings.Count -eq 3) 'manager exposes new, save and close keyboard bindings'
        Invoke-BrowserCommand $model.NewCommand $null
        $item = $model.SelectedScript
        $editor = $window.FindName('sourceEditor')
        $editor.SelectAll()
        $editor.TextArea.PerformTextInput($source)
        Send-ManagerShortcut $Context $window 0x53
        Wait-Regression $Context { !$model.IsBusy -and $item.IsInstalled -and !$item.IsDirty } 'Ctrl+S saves the latest editor text without confirmation'
        Assert-Regression ($item.IsInstalled -and !$item.IsDirty -and $item.IsEnabled) 'new script saves and enables directly'
        $key = $item.Installation.ScriptKey
        $sourcePath = Join-Path (Join-Path $Context.TemporaryPath ('MonkeySharp\' + $key)) 'userscript.js'
        Assert-Regression ([IO.File]::ReadAllText($sourcePath) -eq $source) 'manager persists source through the shared repository'
        $reloadedSource = $source + "`r`n// externally reloaded source"
        [IO.File]::WriteAllText($sourcePath, $reloadedSource, (New-Object Text.UTF8Encoding($false)))
        Complete-UserScriptTask $Context ($model.ReloadAsync()) 'reload external source into the editor'
        Assert-Regression ($editor.Text -ceq $reloadedSource -and !$item.IsDirty -and !$editor.CanUndo) 'source reload updates the editor and resets undo without marking it dirty'
        $item.Source = $source
        Complete-UserScriptTask $Context ($model.SaveAsync()) 'restore the manager workflow source'
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
        Send-ManagerShortcut $Context $window 0x57
        Wait-Regression $Context { !$window.IsVisible } 'Ctrl+W closes the manager from editor focus'
        [CefFlashBrowser.Services.WindowManager]::ShowUserScriptManager()
        $window = @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.UserScriptManager] })[0]
        Wait-Regression $Context { $window.IsLoaded -and !$model.IsBusy } 'manager reopened'
        $editor = $window.FindName('sourceEditor')
        Assert-Regression ([object]::ReferenceEquals($draft, $model.SelectedScript) -and $item.IsDirty -and $item.Source.Contains('unsaved session draft')) 'selection and unsaved drafts survive manager closure'
        Assert-Regression ($editor.Text -ceq $draft.Source) 'reopened editor displays the selected unsaved draft'
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
        $previousSelection = $model.SelectedScript
        $countBeforeDelete = $model.Scripts.Count
        $model.NewScript()
        $deleteTarget = $model.SelectedScript
        $deleteTarget.Source = $source.Replace('Manager workflow', 'Delete target')
        Complete-UserScriptTask $Context ($model.SaveAsync()) 'install deletion fixture'
        $deletePath = Join-Path (Join-Path $Context.TemporaryPath ('MonkeySharp\' + $deleteTarget.Installation.ScriptKey)) 'userscript.js'
        $deleteTarget.Source += "`n// keep until approved"
        $model.NewScript()
        $deleteDraft = $model.SelectedScript
        $draftSource = $deleteDraft.Source
        $model.SelectedScript = $previousSelection
        $menu = Open-ManagerContextMenu $Context $window $deleteTarget
        Invoke-ManagerDeleteWithConfirmation $Context $window $menu.Items[0] $false
        Assert-Regression ($model.Scripts.Contains($deleteTarget) -and $deleteTarget.Source.EndsWith('keep until approved') -and
            [IO.File]::Exists($deletePath)) 'declined context deletion preserves installed source and unsaved edits'
        $topDelete = @($window.Content.Children[0].Items | ForEach-Object { $_.Items } |
            Where-Object { $_ -is [Windows.Controls.MenuItem] -and $_.Command -eq $model.DeleteCommand })[0]
        Invoke-ManagerDeleteWithConfirmation $Context $window $topDelete $null
        Assert-Regression ($model.Scripts.Contains($deleteTarget) -and [IO.File]::Exists($deletePath)) 'closing the top-menu confirmation cancels deletion'
        $menu = Open-ManagerContextMenu $Context $window $deleteTarget
        Invoke-ManagerDeleteWithConfirmation $Context $window $menu.Items[0] $true
        Assert-Regression (!$model.Scripts.Contains($deleteTarget) -and ![IO.File]::Exists($deletePath) -and
            $model.Scripts.Contains($previousSelection) -and $model.Scripts.Count -eq $countBeforeDelete + 1) 'approved context deletion removes only the right-clicked script without retaining its dirty draft'
        $model.SelectedScript = $previousSelection
        $menu = Open-ManagerContextMenu $Context $window $deleteDraft
        Invoke-ManagerDeleteWithConfirmation $Context $window $menu.Items[0] $false
        Assert-Regression ($model.Scripts.Contains($deleteDraft) -and $deleteDraft.Source -eq $draftSource) 'declined draft deletion preserves its source'
        Invoke-ManagerDeleteWithConfirmation $Context $window $topDelete $true
        Assert-Regression (!$model.Scripts.Contains($deleteDraft) -and $model.Scripts.Count -eq $countBeforeDelete) 'approved top-menu deletion removes the draft'
        Assert-Regression ($null -eq $window.FindName('scriptList').ContextMenu) 'blank script-list space has no deletion menu'
        $model.SelectedScript = $previousSelection
        foreach ($locale in @('en-US', 'zh-CN', 'zh-TW', 'de', 'fr-FR', 'it', 'ja', 'ko', 'ru')) {
            $Context.LanguageService.SetCurrentLanguage($locale)
            Wait-Regression $Context { $window.Title -eq $Context.LanguageService.GetString('userScriptManager_title') } "manager language ($locale)"
            Assert-Regression (![string]::IsNullOrEmpty($window.Title)) 'manager title is localized in every supported language'
            $menu = Open-ManagerContextMenu $Context $window $previousSelection
            Invoke-ManagerDeleteWithConfirmation $Context $window $menu.Items[0] $false
        }
        $Context.LanguageService.SetCurrentLanguage('zh-CN')
        $model.SelectedScript = $item
        $item.Source = $source + "`r`n" + ((1..60 | ForEach-Object { "const value$_ = 42; // readable code with a long comment " + ('x' * 100) }) -join "`r`n")
        Wait-Regression $Context { $editor.Text -ceq $item.Source } 'theme fixture source is displayed'
        $editor.CaretOffset = $editor.Text.Length
        $editor.TextArea.PerformTextInput("`r`n// theme draft")
        $editor.Select(10, 12)
        $editor.ScrollToVerticalOffset(25)
        $editor.ScrollToHorizontalOffset(16)
        $window.UpdateLayout()
        $scrollViewer = $editor.Template.FindName('PART_ScrollViewer', $editor)
        Assert-Regression ($scrollViewer.ComputedVerticalScrollBarVisibility -eq [Windows.Visibility]::Visible -and
            $scrollViewer.ComputedHorizontalScrollBarVisibility -eq [Windows.Visibility]::Visible) 'long source exposes both editor scrollbars'
        $editorState = @{
            Document = $editor.Document; Source = $item.Source; Dirty = $item.IsDirty
            Caret = $editor.CaretOffset; SelectionStart = $editor.SelectionStart; SelectionLength = $editor.SelectionLength
            VerticalOffset = $editor.VerticalOffset; HorizontalOffset = $editor.HorizontalOffset
        }
        $sharedHighlighting = [ICSharpCode.AvalonEdit.Highlighting.HighlightingManager]::Instance.GetDefinition('JavaScript')
        $sharedKeyword = $sharedHighlighting.GetNamedColor('JavaScriptKeyWords').Foreground.GetColor($null)
        $themeKeywords = @{}
        foreach ($appearance in @([CefFlashBrowser.Models.Theme]::Light, [CefFlashBrowser.Models.Theme]::Dark, [CefFlashBrowser.Models.Theme]::Light)) {
            Set-ManagerRegressionTheme $Context $appearance
            $frame = New-Object Windows.Threading.DispatcherFrame
            [void]$Context.Application.Dispatcher.BeginInvoke([Windows.Threading.DispatcherPriority]::ApplicationIdle,
                [Action]({ $frame.Continue = $false }.GetNewClosure()))
            [Windows.Threading.Dispatcher]::PushFrame($frame)
            $window.UpdateLayout()
            Assert-Regression ([object]::ReferenceEquals($editorState.Document, $editor.Document) -and
                $editorState.Source -ceq $item.Source -and $editorState.Dirty -eq $item.IsDirty -and
                $editorState.Caret -eq $editor.CaretOffset -and $editorState.SelectionStart -eq $editor.SelectionStart -and
                $editorState.SelectionLength -eq $editor.SelectionLength -and $editor.Document.UndoStack.CanUndo -and
                [Math]::Abs($editorState.VerticalOffset - $editor.VerticalOffset) -lt 1 -and
                [Math]::Abs($editorState.HorizontalOffset - $editor.HorizontalOffset) -lt 1) "editor preserves draft, caret, selection, scrolling and undo across $appearance theme changes"
            Assert-Regression ($editor.Background.Color -eq $window.FindResource('RegionBrush').Color -and
                $editor.Foreground.Color -eq $window.FindResource('PrimaryTextBrush').Color -and
                $editor.BorderBrush.Color -eq $window.FindResource('BorderBrush').Color -and
                $editor.TextArea.Caret.CaretBrush.Color -eq $editor.Foreground.Color) "editor surfaces and caret follow the $appearance theme"
            foreach ($color in @($editor.SyntaxHighlighting.NamedHighlightingColors)) {
                Assert-Regression ((Get-ManagerColorContrast $color.Foreground.GetColor($null) $editor.Background.Color) -ge 4.5) "$appearance syntax color $($color.Name) has readable contrast"
            }
            Assert-Regression ((Get-ManagerColorContrast $editor.LineNumbersForeground.Color $editor.Background.Color) -ge 4.5 -and
                (Get-ManagerColorContrast $editor.TextArea.SelectionForeground.Color $editor.TextArea.SelectionBrush.Color) -ge 4.5) "$appearance line numbers and selected text have readable contrast"
            $themeKeywords[$appearance.ToString()] = $editor.SyntaxHighlighting.GetNamedColor('JavaScriptKeyWords').Foreground.GetColor($null)
            $newEditor = New-Object CefFlashBrowser.Views.Custom.ScriptEditor
            Assert-Regression ($newEditor.Background.Color -eq $editor.Background.Color -and
                $newEditor.SyntaxHighlighting.GetNamedColor('JavaScriptKeyWords').Foreground.GetColor($null) -eq $themeKeywords[$appearance.ToString()]) "new editors initialize with the current $appearance theme"
            $menu = Open-ManagerContextMenu $Context $window $item
            Invoke-ManagerDeleteWithConfirmation $Context $window $menu.Items[0] $false
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
        Assert-Regression ($themeKeywords.Light -ne $themeKeywords.Dark -and
            $sharedHighlighting.GetNamedColor('JavaScriptKeyWords').Foreground.GetColor($null) -eq $sharedKeyword) 'theme changes recolor the editor without changing shared highlighting'
        [void]$editor.Undo()
        Assert-Regression (!$item.Source.EndsWith('// theme draft')) 'undo history still works after repeated theme changes'
        [void]$editor.Redo()
        Assert-Regression ($item.Source.EndsWith('// theme draft')) 'redo history still works after repeated theme changes'
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
