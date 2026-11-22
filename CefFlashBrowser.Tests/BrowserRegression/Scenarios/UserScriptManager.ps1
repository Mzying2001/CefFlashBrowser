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
    Wait-Regression $Context {
        if (!$editor.TextArea.IsKeyboardFocusWithin) { [void]$editor.TextArea.Focus() }
        $editor.TextArea.IsKeyboardFocusWithin
    } 'script editor receives keyboard input'
    [RegressionKeyboard]::SetKey(0x11, $false)
    try {
        [RegressionKeyboard]::PressKey($Key)
    } finally {
        [RegressionKeyboard]::SetKey(0x11, $true)
    }
}

function Open-ManagerEditorContextMenu($Context, $Window) {
    Set-TabWindowForeground $Context $Window
    $editor = $Window.FindName('sourceEditor')
    [void]$editor.Focus()
    Wait-Regression $Context { $editor.TextArea.IsKeyboardFocusWithin } 'editor focus before opening its context menu'
    [RegressionKeyboard]::PressKey(0x5D)
    Wait-Regression $Context { $editor.ContextMenu.IsOpen } 'editor context menu opens through native context-menu input'
    return $editor.ContextMenu
}

function Test-ManagerStatusLine($Context, $Window) {
    $model = $Window.DataContext
    $editor = $Window.FindName('sourceEditor')
    $statusBar = $Window.Content.Children | Where-Object { $_ -is [Windows.Controls.Primitives.StatusBar] }
    $statusText = $statusBar.Items[0]

    $editor.SelectAll()
    $editor.TextArea.PerformTextInput('invalid metadata')
    Complete-UserScriptTask $Context ($model.SaveAsync()) 'save a draft without metadata'
    Assert-Regression ($statusText.Text -ceq $Context.LanguageService.GetString('userScriptManager_invalidMetadata')) 'status bar displays the invalid metadata message'
    $Window.UpdateLayout()
    $singleLineHeight = $statusBar.ActualHeight

    $editor.Document.Remove(0, $editor.Document.TextLength)
    Complete-UserScriptTask $Context ($model.SaveAsync()) 'save an empty draft'
    $Window.UpdateLayout()
    Assert-Regression ($statusText.Text.Contains('source')) 'empty draft status keeps the error parameter'
    Assert-Regression ($statusBar.ActualHeight -eq $singleLineHeight) 'empty draft error fits on one status bar line'
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
        Assert-Regression ($editor.Options.IndentationSize -eq 2 -and $editor.Options.ConvertTabsToSpaces) 'editor defaults to two-space indentation'
        $model.NewScript()
        $first = $model.SelectedScript
        Assert-Regression ($first.Source.Contains("`r`n  'use strict';`r`n")) 'new script template uses two-space indentation'
        $sample = "const answer = 42;`r`n// 中文 comment`r`n`tlet value = `"text`";"
        $first.Source = $sample
        Wait-Regression $Context { $editor.IsEnabled -and $editor.Text -ceq $sample } 'editor displays the source with its original tabs and newlines'
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
        $indentOffset = $editor.Document.GetLineByNumber(4).Offset
        $editor.CaretOffset = $indentOffset
        [RegressionKeyboard]::PressKey(0x09)
        Wait-Regression $Context { $first.Source -ceq ($sample + $suffix).Insert($indentOffset, '  ') } 'Tab inserts two spaces at the start of the line'
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
        $editor.Select(0, 5)
        $contextMenu = Open-ManagerEditorContextMenu $Context $Window
        $contextItems = @($contextMenu.Items | Where-Object { $_ -is [Windows.Controls.MenuItem] })
        Assert-Regression ($contextItems.Count -eq 6 -and @($contextItems | Where-Object {
            ![object]::ReferenceEquals($_.CommandTarget, $editor.TextArea)
        }).Count -eq 0) 'editor context menu routes all six editing commands to its text area'
        Wait-Regression $Context { $contextItems[2].IsEnabled -and $contextItems[3].IsEnabled -and $contextItems[4].IsEnabled } 'selection and clipboard enable context-menu cut, copy and paste'
        $contextItems[3].Command.Execute($null, $contextItems[3].CommandTarget)
        Assert-Regression ([Windows.Clipboard]::GetText() -ceq 'const' -and $editor.SelectedText -ceq 'const') 'context-menu copy preserves the selected text'
        $contextItems[2].Command.Execute($null, $contextItems[2].CommandTarget)
        Assert-Regression (!$first.Source.StartsWith('const')) 'context-menu cut updates the draft'
        $contextItems[4].Command.Execute($null, $contextItems[4].CommandTarget)
        Assert-Regression ($first.Source.StartsWith('const')) 'context-menu paste updates the draft'
        $contextItems[0].Command.Execute($null, $contextItems[0].CommandTarget)
        Assert-Regression (!$first.Source.StartsWith('const')) 'context-menu undo reverses the paste'
        $contextItems[1].Command.Execute($null, $contextItems[1].CommandTarget)
        Assert-Regression ($first.Source.StartsWith('const')) 'context-menu redo restores the paste'
        $contextItems[5].Command.Execute($null, $contextItems[5].CommandTarget)
        Assert-Regression ($editor.SelectionLength -eq $editor.Text.Length) 'context-menu select-all selects the full document'
        $editor.Select(0, 0)
        $copyWholeLine = $editor.Options.CutCopyWholeLine
        $editor.Options.CutCopyWholeLine = $false
        [Windows.Input.CommandManager]::InvalidateRequerySuggested()
        Wait-Regression $Context { !$contextItems[2].IsEnabled -and !$contextItems[3].IsEnabled } 'context-menu cut and copy disable without a selection'
        $editor.Options.CutCopyWholeLine = $copyWholeLine
        $contextMenu.IsOpen = $false

        $longSource = (('const value = "' + ('x' * 220) + '";' + "`r`n") * 100)
        $first.Source = $longSource
        $editor.CaretOffset = $editor.Document.GetLineByNumber(25).Offset + 45
        $editor.TextArea.PerformTextInput('first edit ')
        $sameSource = $first.Source
        $firstDocument = $editor.Document
        $editor.Select($editor.CaretOffset - 5, 5)
        $firstCaret = $editor.CaretOffset
        $firstSelection = $editor.SelectionStart
        $editor.ScrollToHorizontalOffset(70)
        $editor.ScrollToVerticalOffset(320)
        $Window.UpdateLayout()
        $firstScroll = $editor.TextArea.TextView.ScrollOffset
        Assert-Regression ($firstScroll.X -gt 1 -and $firstScroll.Y -gt 1) 'the first script is scrolled in both directions'

        Send-ManagerShortcut $Context $Window 0x4E
        Wait-Regression $Context { ![object]::ReferenceEquals($first, $model.SelectedScript) } 'Ctrl+N creates a new script from editor focus'
        $second = $model.SelectedScript
        $second.Source = $sameSource
        $Window.UpdateLayout()
        Assert-Regression ($editor.Text -ceq $sameSource -and $editor.HorizontalOffset -lt 1 -and $editor.VerticalOffset -lt 1 -and
            $editor.CaretOffset -eq 0 -and $editor.SelectionLength -eq 0 -and !$editor.CanUndo -and !$editor.CanRedo) 'a new script with identical source starts at the top-left with its own history'
        $secondDocument = $editor.Document
        $model.SelectedScript = $first
        $editor.ScrollToHorizontalOffset($firstScroll.X)
        $editor.ScrollToVerticalOffset($firstScroll.Y)
        $Window.UpdateLayout()
        $model.SelectedScript = $second
        $Window.UpdateLayout()
        Assert-Regression ($editor.HorizontalOffset -lt 1 -and $editor.VerticalOffset -lt 1 -and !$editor.CanUndo) 'switching between identical sources does not carry over scroll position or undo history'
        $editor.CaretOffset = $editor.Document.GetLineByNumber(40).Offset + 55
        $editor.TextArea.PerformTextInput('second edit ')
        $secondDraft = $second.Source
        $editor.ScrollToHorizontalOffset(140)
        $editor.ScrollToVerticalOffset(580)
        $Window.UpdateLayout()
        $secondScroll = $editor.TextArea.TextView.ScrollOffset

        $model.SelectedScript = $first
        $model.SelectedScript = $second
        $model.SelectedScript = $first
        $Window.UpdateLayout()
        Assert-Regression ([object]::ReferenceEquals($editor.Document, $firstDocument) -and $editor.Text -ceq $sameSource -and
            $editor.CaretOffset -eq $firstCaret -and $editor.SelectionStart -eq $firstSelection -and $editor.SelectionLength -eq 5 -and
            [Math]::Abs($editor.HorizontalOffset - $firstScroll.X) -lt 1 -and
            [Math]::Abs($editor.VerticalOffset - $firstScroll.Y) -lt 1) 'rapid switching restores the first draft, caret, selection and scroll position'
        Assert-Regression $editor.CanUndo 'returning to the first script preserves undo'
        $editMenu.Items[0].Command.Execute($null, $editMenu.Items[0].CommandTarget)
        Assert-Regression ($first.Source -ceq $longSource -and $second.Source -ceq $secondDraft -and $editor.CanRedo) 'undo after switching only edits the first draft'

        $model.SelectedScript = $second
        $Window.UpdateLayout()
        Assert-Regression ([object]::ReferenceEquals($editor.Document, $secondDocument) -and $editor.Text -ceq $secondDraft -and
            [Math]::Abs($editor.HorizontalOffset - $secondScroll.X) -lt 1 -and
            [Math]::Abs($editor.VerticalOffset - $secondScroll.Y) -lt 1 -and $editor.CanUndo -and !$editor.CanRedo) 'returning to the second script restores its own scroll position and history'
        [void]$editor.Undo()
        Assert-Regression ($second.Source -ceq $sameSource -and $first.Source -ceq $longSource) 'undo after switching also works for the second draft'

        $model.SelectedScript = $first
        $model.FilterText = 'no matching editor state fixture'
        Assert-Regression (!$editor.IsEnabled -and $editor.Text -ceq '') 'filtering out the selection clears and disables the editor'
        $model.FilterText = ''
        $model.SelectedScript = $first
        Assert-Regression $editor.CanRedo 'redo survives switching scripts and filtering out the selection'
        $editMenu.Items[1].Command.Execute($null, $editMenu.Items[1].CommandTarget)
        Assert-Regression ($first.Source -ceq $sameSource -and $second.Source -ceq $sameSource -and $editor.CanUndo) 'redo after reselecting restores the first edit'

        $second.Source = ''
        $model.SelectedScript = $second
        $Window.UpdateLayout()
        Assert-Regression ($editor.Text -ceq '' -and !$editor.CanUndo -and !$editor.CanRedo -and $editor.CaretOffset -eq 0 -and
            $editor.HorizontalOffset -lt 1 -and $editor.VerticalOffset -lt 1 -and $first.Source -ceq $sameSource) 'an inactive source replaced with empty text opens with fresh editor state'
    } finally {
        $editor.ContextMenu.IsOpen = $false
        if ($clipboardCaptured) { [UserScriptClipboardProbe]::Restore($clipboard) }
        $model.FilterText = ''
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

function Invoke-ManagerEditorWheel($TextView, [int]$Delta) {
    $wheel = [Windows.Input.MouseWheelEventArgs]::new([Windows.Input.Mouse]::PrimaryDevice, 0, $Delta)
    $wheel.RoutedEvent = [Windows.Input.Mouse]::PreviewMouseWheelEvent
    $TextView.RaiseEvent($wheel)
    if (!$wheel.Handled) {
        $wheel.RoutedEvent = [Windows.Input.Mouse]::MouseWheelEvent
        $TextView.RaiseEvent($wheel)
    }
}

function Test-ManagerEditorWheel($Context, $Window, $Editor) {
    Set-TabWindowForeground $Context $Window
    $view = $Editor.TextArea.TextView
    $delta = [Windows.Input.Mouse]::MouseWheelDeltaForOneLine
    $Editor.ScrollToHorizontalOffset(0)
    $Editor.ScrollToVerticalOffset(0)
    $Window.UpdateLayout()

    [RegressionKeyboard]::SetKey(0x10, $false)
    try {
        Wait-Regression $Context { [Windows.Input.Keyboard]::Modifiers -eq [Windows.Input.ModifierKeys]::Shift } 'Shift is held for horizontal scrolling'
        Invoke-ManagerEditorWheel $view (-$delta)
        $Window.UpdateLayout()
        Assert-Regression ($Editor.HorizontalOffset -gt 0 -and $Editor.VerticalOffset -eq 0) 'Shift+wheel down scrolls the editor right without moving vertically'
        Invoke-ManagerEditorWheel $view (2 * $delta)
        $Window.UpdateLayout()
        Assert-Regression ($Editor.HorizontalOffset -eq 0 -and $Editor.VerticalOffset -eq 0) 'Shift+wheel up scrolls the editor left and stops at the start of the line'
    } finally {
        [RegressionKeyboard]::SetKey(0x10, $true)
    }

    Wait-Regression $Context { [Windows.Input.Keyboard]::Modifiers -eq [Windows.Input.ModifierKeys]::None } 'Shift is released for vertical scrolling'
    Invoke-ManagerEditorWheel $view (-$delta)
    $Window.UpdateLayout()
    Assert-Regression ($Editor.HorizontalOffset -eq 0 -and $Editor.VerticalOffset -gt 0) 'unmodified wheel scrolls the editor vertically'
}

function Test-ManagerFindReplace($Context, $Window) {
    $model = $Window.DataContext
    $previousSelection = $model.SelectedScript
    $first = $null
    $second = $null
    $editor = $Window.FindName('sourceEditor')
    $bar = $Window.FindName('searchBar')
    $query = $Window.FindName('searchText')
    $replacement = $Window.FindName('replacementText')
    $replaceRow = $Window.FindName('replaceRow')
    $matchCase = $Window.FindName('matchCase')
    $count = $Window.FindName('matchCountValue')
    $noMatches = $Window.FindName('noMatches')
    $findMenu = $Window.FindName('findMenuItem')
    $replaceMenu = $Window.FindName('replaceMenuItem')
    $next = $Window.FindName('findNext')
    $replaceCurrent = $Window.FindName('replaceCurrent')
    $replaceAll = $Window.FindName('replaceAll')
    try {
        $model.NewScript()
        $first = $model.SelectedScript
        $model.NewScript()
        $second = $model.SelectedScript
        $sample = 'token TOKEN token'
        $first.Source = $sample
        $second.Source = $sample
        $model.SelectedScript = $first
        Wait-Regression $Context { $editor.Text -ceq $sample } 'find fixture is displayed'

        $editor.Select(0, 5)
        Send-ManagerShortcut $Context $Window 0x46
        Wait-Regression $Context { $bar.IsVisible -and $query.IsKeyboardFocused } 'Ctrl+F opens the inline search and focuses its input'
        Assert-Regression ($query.Text -ceq 'token' -and $query.SelectionLength -eq 5 -and !$replaceRow.IsVisible -and
            $count.Text -eq '3') 'find seeds selected text and counts matches regardless of case'
        [RegressionKeyboard]::PressKey(0x0D)
        Wait-Regression $Context { $editor.SelectionStart -eq 6 } 'Enter selects the next match'
        [RegressionKeyboard]::PressKey(0x72)
        Wait-Regression $Context { $editor.SelectionStart -eq 12 } 'F3 advances to the last match'
        [RegressionKeyboard]::PressKey(0x72)
        Wait-Regression $Context { $editor.SelectionStart -eq 0 } 'F3 wraps to the first match'
        [RegressionKeyboard]::SetKey(0x10, $false)
        try { [RegressionKeyboard]::PressKey(0x0D) } finally { [RegressionKeyboard]::SetKey(0x10, $true) }
        Wait-Regression $Context { $editor.SelectionStart -eq 12 } 'Shift+Enter wraps to the last match'
        [RegressionKeyboard]::SetKey(0x10, $false)
        try { [RegressionKeyboard]::PressKey(0x72) } finally { [RegressionKeyboard]::SetKey(0x10, $true) }
        Wait-Regression $Context { $editor.SelectionStart -eq 6 } 'Shift+F3 selects the previous match'

        [RegressionKeyboard]::PressKey(0x1B)
        Wait-Regression $Context { !$bar.IsVisible -and $editor.TextArea.IsKeyboardFocusWithin } 'Escape closes search and returns focus to the editor'
        $findMenu.Command.Execute($null, $findMenu.CommandTarget)
        Assert-Regression ($bar.IsVisible -and !$replaceRow.IsVisible) 'Edit menu opens find'
        [RegressionKeyboard]::SetKey(0x11, $false)
        try { [RegressionKeyboard]::PressKey(0x48) } finally { [RegressionKeyboard]::SetKey(0x11, $true) }
        Wait-Regression $Context { $replaceRow.IsVisible -and $query.IsKeyboardFocused } 'Ctrl+H opens replace from the search box'
        [RegressionKeyboard]::PressKey(0x1B)
        Wait-Regression $Context { !$bar.IsVisible } 'Escape also closes replace mode'
        $replaceMenu.Command.Execute($null, $replaceMenu.CommandTarget)
        Assert-Regression ($replaceRow.IsVisible -and $query.IsKeyboardFocused) 'Edit menu opens replace'

        $query.Text = 'token'
        $matchCase.IsChecked = $true
        Assert-Regression ($count.Text -eq '2' -and $editor.SelectedText -ceq 'token') 'matching case refreshes matches and skips other casing'
        $query.Text = 'missing'
        Assert-Regression ($noMatches.IsVisible -and !$next.IsEnabled -and !$replaceCurrent.IsEnabled -and !$replaceAll.IsEnabled) 'no matches disables navigation and replacement'
        $query.Clear()
        Assert-Regression (!$noMatches.IsVisible -and !$next.IsEnabled -and !$replaceAll.IsEnabled) 'empty query clears the result state'

        $matchCase.IsChecked = $false
        $editor.CaretOffset = 0
        $editor.TextArea.ClearSelection()
        $query.Text = 'token'
        $replacement.Text = 'token$1'
        $replaceCurrent.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Primitives.ButtonBase]::ClickEvent)))
        Assert-Regression ($first.Source -ceq 'token$1 TOKEN token' -and $editor.SelectionStart -eq 8 -and
            $editor.SelectedText -ceq 'TOKEN') 'replace current inserts literal text and advances beyond it'
        $replacement.Clear()
        $replaceCurrent.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Primitives.ButtonBase]::ClickEvent)))
        Assert-Regression ($first.Source -ceq 'token$1  token' -and $second.Source -ceq $sample) 'empty replacement deletes only the selected match in the current draft'

        $sourceBefore = "[a]`r`n[A]`t[a]"
        $sourceAfter = "[a]`$1`r`n[a]`$1`t[a]`$1"
        $first.Source = $sourceBefore
        $query.Text = '[a]'
        $replacement.Text = '[a]$1'
        $replaceAll.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Primitives.ButtonBase]::ClickEvent)))
        Assert-Regression ($first.Source -ceq $sourceAfter -and $second.Source -ceq $sample) 'replace all edits each original literal match once and preserves other text and drafts'
        [void]$editor.Undo()
        Assert-Regression ($first.Source -ceq $sourceBefore -and !$editor.CanUndo) 'one undo restores the whole replace-all operation'
        [void]$editor.Redo()
        Assert-Regression ($first.Source -ceq $sourceAfter) 'one redo reapplies the whole replace-all operation'

        $matchCase.IsChecked = $true
        $editor.Select(0, 3)
        Assert-Regression ($replaceCurrent.IsEnabled) 'a matching editor selection enables replacement'
        $second.Source = $first.Source
        $model.SelectedScript = $second
        Assert-Regression ($replaceRow.IsVisible -and $query.Text -ceq '[a]' -and $replacement.Text -ceq '[a]$1' -and
            $matchCase.IsChecked -and $count.Text -eq '3' -and !$replaceCurrent.IsEnabled -and !$editor.CanUndo) 'identical-source script switch keeps the search inputs and uses the second script selection and history'
        $second.Source = '[A]'
        Assert-Regression ($replaceRow.IsVisible -and $noMatches.IsVisible -and !$replaceAll.IsEnabled) 'source changes refresh the match list without closing the toolbar'
        $model.SelectedScript = $first
        Assert-Regression ($replaceRow.IsVisible -and $count.Text -eq '3' -and $replaceCurrent.IsEnabled -and
            $editor.SelectedText -ceq '[a]' -and $editor.CanUndo) 'switching back refreshes matches and restores the first script selection and history'
        $model.SelectedScript = $null
        Wait-Regression $Context { !$bar.IsVisible -and !$editor.IsEnabled } 'no selected script closes the toolbar'
        Assert-Regression (!$findMenu.Command.CanExecute($null, $findMenu.CommandTarget) -and
            !$replaceMenu.Command.CanExecute($null, $replaceMenu.CommandTarget)) 'no selected script disables find and replace commands'
    } finally {
        $model.SelectedScript = $null
        $query.Clear()
        $replacement.Clear()
        $matchCase.IsChecked = $false
        $model.SelectedScript = $previousSelection
        if ($null -ne $first) { [void]$model.Scripts.Remove($first) }
        if ($null -ne $second) { [void]$model.Scripts.Remove($second) }
    }
}

function Get-ManagerCompletion($Window) {
    $Window.OwnedWindows | Where-Object { $_ -is [ICSharpCode.AvalonEdit.CodeCompletion.CompletionWindow] -and $_.IsVisible }
}

function Get-RenderedOccurrenceCount($Renderer, $View) {
    $View.EnsureVisualLines()
    $drawing = New-Object Windows.Media.DrawingGroup
    $drawingContext = $drawing.Open()
    try {
        $Renderer.Draw($View, $drawingContext)
    } finally {
        $drawingContext.Close()
    }
    if ($drawing.Children.Count -eq 0) { return 0 }
    return $drawing.Children[0].Geometry.GetFlattenedPathGeometry().Figures.Count
}

function Test-ManagerEditorAssistance($Context, $Window) {
    $model = $Window.DataContext
    $editor = $Window.FindName('sourceEditor')
    $bar = $Window.FindName('searchBar')
    $query = $Window.FindName('searchText')
    $menu = $Window.FindName('replaceMenuItem')
    $completionMenu = $Window.FindName('completeCodeMenuItem')
    $view = $editor.TextArea.TextView
    $renderer = $view.BackgroundRenderers | Where-Object { $_.GetType().Name -eq 'ScriptEditorOccurrences' }
    $previousSelection = $model.SelectedScript
    $theme = [CefFlashBrowser.Data.GlobalData]::Settings.Theme
    $draft = $null
    $otherDraft = $null
    try {
        $model.NewScript()
        $draft = $model.SelectedScript
        $draft.Source = 'c'
        $editor.CaretOffset = 1
        Set-TabWindowForeground $Context $Window
        [void]$editor.Focus()
        Wait-Regression $Context { $editor.TextArea.IsKeyboardFocusWithin } 'completion editor receives input'
        $editor.TextArea.PerformTextInput('o')
        Assert-Regression ($null -ne (Get-ManagerCompletion $Window)) 'typing a two-character prefix opens completion'
        $editor.TextArea.PerformTextInput('n')
        $editor.TextArea.PerformTextInput('s')
        Assert-Regression ((Get-ManagerCompletion $Window).CompletionList.SelectedItem.Text -ceq 'const') 'keyword completion follows the typed prefix'
        [RegressionKeyboard]::PressKey(0x09)
        Wait-Regression $Context { $draft.Source -ceq 'const' -and $null -eq (Get-ManagerCompletion $Window) } 'Tab commits the selected completion to the draft'
        Send-ManagerShortcut $Context $Window 0x5A
        Wait-Regression $Context { $draft.Source -ceq 'cons' } 'one undo restores the prefix before completion'

        $draft.Source = 'GM.getVxyz();'
        $editor.CaretOffset = 'GM.getV'.Length
        # The system IME can consume native Ctrl+Space. Verify its binding and invoke the same command through the menu.
        $binding = $editor.InputBindings | Where-Object { $_.Command -eq $completionMenu.Command }
        Assert-Regression ($binding.Key -eq [Windows.Input.Key]::Space -and
            $binding.Modifiers -eq [Windows.Input.ModifierKeys]::Control) 'Ctrl+Space is bound to the completion command'
        $completionMenu.Command.Execute($null, $completionMenu.CommandTarget)
        Assert-Regression ((Get-ManagerCompletion $Window).CompletionList.SelectedItem.Text -ceq 'getValue') 'manual completion offers modern GM members inside a word'
        [RegressionKeyboard]::PressKey(0x0D)
        Wait-Regression $Context { $draft.Source -ceq 'GM.getValue();' } 'Enter replaces the token without duplicating its suffix or receiver'

        $draft.Source = 'GM_getV'
        $editor.CaretOffset = $draft.Source.Length
        $completionMenu.Command.Execute($null, $completionMenu.CommandTarget)
        Wait-Regression $Context {
            (Get-ManagerCompletion $Window).CompletionList.SelectedItem.Text -ceq 'GM_getValue'
        } 'completion includes the official legacy GM spelling'
        [RegressionKeyboard]::PressKey(0x28)
        Wait-Regression $Context { (Get-ManagerCompletion $Window).CompletionList.SelectedItem.Text -ceq 'GM_getValues' } 'Down navigates candidates'
        [RegressionKeyboard]::PressKey(0x1B)
        Wait-Regression $Context { $null -eq (Get-ManagerCompletion $Window) } 'Escape dismisses completion'
        Assert-Regression ($draft.Source -ceq 'GM_getV') 'dismissing completion keeps the original source'

        $draft.Source = '// c'
        $editor.CaretOffset = $draft.Source.Length
        $editor.TextArea.PerformTextInput('o')
        Assert-Regression ($null -eq (Get-ManagerCompletion $Window)) 'typing in a comment skips completion'
        $draft.Source = 'const value = "c";'
        $editor.CaretOffset = 'const value = "c'.Length
        $editor.TextArea.PerformTextInput('o')
        Assert-Regression ($null -eq (Get-ManagerCompletion $Window)) 'typing in a quoted string skips completion'
        $draft.Source = 'c'
        $editor.CaretOffset = 1
        $editor.TextArea.PerformTextInput('o')
        Assert-Regression ($null -ne (Get-ManagerCompletion $Window)) 'completion opens before testing a missing candidate'
        $editor.TextArea.PerformTextInput('z')
        Assert-Regression ($null -eq (Get-ManagerCompletion $Window)) 'typing an unmatched prefix dismisses completion'
        [RegressionKeyboard]::PressKey(0x09)
        Wait-Regression $Context { $draft.Source -ceq 'coz ' } 'Tab indents normally after typing a prefix with no candidates'

        $draft.Source = 'GM.'
        $editor.CaretOffset = 3
        $completionMenu.Command.Execute($null, $completionMenu.CommandTarget)
        Wait-Regression $Context { $null -ne (Get-ManagerCompletion $Window) } 'manual completion opens directly after GM dot'
        $model.NewScript()
        $otherDraft = $model.SelectedScript
        Assert-Regression ($null -eq (Get-ManagerCompletion $Window) -and $draft.Source -ceq 'GM.') 'switching scripts closes completion and preserves the old draft'
        $model.SelectedScript = $draft

        $fixture = 'count count Count counter $count _count count2 2count' + "`r`n" + '$count 中文 中文 中文名'
        $draft.Source = ($fixture + "`r`n" + (('other' + "`r`n") * 50) + 'count')
        $editor.CaretOffset = 0
        $editor.Select(0, 5)
        $editor.ScrollToVerticalOffset(0)
        $Window.UpdateLayout()
        Assert-Regression ((Get-RenderedOccurrenceCount $renderer $view) -eq 1) 'only other complete, case-sensitive tokens are highlighted in the viewport'
        $editor.Select($fixture.IndexOf('counter'), 5)
        Assert-Regression ((Get-RenderedOccurrenceCount $renderer $view) -eq 0) 'a partial token selection suppresses occurrence markers'
        $editor.Select($fixture.IndexOf('$count'), 6)
        Assert-Regression ((Get-RenderedOccurrenceCount $renderer $view) -eq 1) 'dollar-prefixed identifiers match as whole tokens'
        $editor.Select($fixture.IndexOf('中文'), 2)
        Assert-Regression ((Get-RenderedOccurrenceCount $renderer $view) -eq 1) 'Unicode identifiers use the same word boundaries'
        $editor.Select(0, 5)
        $editor.ScrollToEnd()
        $Window.UpdateLayout()
        Assert-Regression ((Get-RenderedOccurrenceCount $renderer $view) -eq 1) 'scrolling highlights a newly visible occurrence with the selection retained'
        $editor.TextArea.ClearSelection()
        Assert-Regression ((Get-RenderedOccurrenceCount $renderer $view) -eq 0) 'clearing the selection removes occurrence markers'

        $longLine = 'count ' + ('x' * 300) + ' count'
        $draft.Source = $longLine
        $editor.Select(0, 5)
        $editor.ScrollToHorizontalOffset(0)
        $Window.UpdateLayout()
        Assert-Regression ((Get-RenderedOccurrenceCount $renderer $view) -eq 0) 'off-screen matches on long lines do not generate markers'
        $editor.ScrollToHorizontalOffset(10000)
        $Window.UpdateLayout()
        Assert-Regression ((Get-RenderedOccurrenceCount $renderer $view) -eq 1) 'horizontal scrolling reveals the matching token'

        $draft.Source = $fixture
        $editor.ScrollToHorizontalOffset(0)
        $editor.ScrollToVerticalOffset(0)
        $editor.Select(0, 5)
        Set-ManagerRegressionTheme $Context ([CefFlashBrowser.Models.Theme]::Light)
        $Window.UpdateLayout()
        $lightBrush = $renderer.MarkerBrush.Color
        Set-ManagerRegressionTheme $Context ([CefFlashBrowser.Models.Theme]::Dark)
        $Window.UpdateLayout()
        Assert-Regression ($lightBrush -ne $renderer.MarkerBrush.Color -and
            (Get-RenderedOccurrenceCount $renderer $view) -eq 1) 'occurrence markers follow the current editor theme'

        $menu.Command.Execute($null, $menu.CommandTarget)
        $Window.UpdateLayout()
        Assert-Regression ($bar.IsVisible -and $query.Text -ceq 'count' -and
            (Get-RenderedOccurrenceCount $renderer $view) -eq 0) 'opening find and replace gives its search highlights priority'
        $draft.Source = 'cons'
        $editor.TextArea.ClearSelection()
        $editor.CaretOffset = 4
        $completionMenu.Command.Execute($null, $completionMenu.CommandTarget)
        Wait-Regression $Context { $null -ne (Get-ManagerCompletion $Window) } 'completion is available with the search bar open'
        [RegressionKeyboard]::PressKey(0x1B)
        Wait-Regression $Context { $null -eq (Get-ManagerCompletion $Window) } 'first Escape dismisses completion with the search bar open'
        Assert-Regression $bar.IsVisible 'first Escape leaves the search bar visible'
        [RegressionKeyboard]::PressKey(0x1B)
        Wait-Regression $Context { !$bar.IsVisible } 'second Escape closes the search bar'
    } finally {
        Set-ManagerRegressionTheme $Context $theme
        $model.SelectedScript = $null
        $query.Clear()
        $model.SelectedScript = $previousSelection
        if ($null -ne $draft) { [void]$model.Scripts.Remove($draft) }
        if ($null -ne $otherDraft) { [void]$model.Scripts.Remove($otherDraft) }
    }
}

function Test-ManagerSearchAppearance($Context, $Window) {
    $editor = $Window.FindName('sourceEditor')
    $bar = $Window.FindName('searchBar')
    $query = $Window.FindName('searchText')
    $menu = $Window.FindName('replaceMenuItem')
    $renderer = $editor.TextArea.TextView.BackgroundRenderers | Where-Object { $_.GetType().Name -eq 'ScriptEditorSearch' }
    $theme = [CefFlashBrowser.Data.GlobalData]::Settings.Theme
    $language = $Context.LanguageService.CurrentLanguage
    $width = $Window.Width
    $scriptColumn = $Window.FindName('scriptListColumn')
    $columnWidth = $scriptColumn.Width
    $filter = $Window.FindName('scriptFilter')
    $list = $Window.FindName('scriptList')
    try {
        $Window.Width = 800
        $Window.UpdateLayout()
        Assert-Regression ($columnWidth.IsAbsolute -and [Math]::Abs($scriptColumn.ActualWidth - 240) -lt 1) 'script list starts at a fixed width'
        $Window.Width = $Window.MinWidth
        $Window.UpdateLayout()
        Assert-Regression ([Math]::Abs($scriptColumn.ActualWidth - 240) -lt 1) 'resizing the window keeps the default script list width'
        $filterOrigin = $filter.TranslatePoint([Windows.Point]::new(0, 0), $list)
        Assert-Regression ([Math]::Abs($filter.ActualWidth - $list.ActualWidth) -lt 1 -and
            $filterOrigin.Y + $filter.ActualHeight -le 0) 'the filter spans the script column above the list at minimum window width'
        $Window.Width = 800
        $Window.UpdateLayout()
        $scriptColumn.Width = New-Object Windows.GridLength($scriptColumn.MaxWidth)
        $Window.UpdateLayout()
        $Window.Width = $Window.MinWidth
        $Window.UpdateLayout()
        Assert-Regression ($scriptColumn.ActualWidth -lt $scriptColumn.Width.Value) 'shrinking the window limits a widened script list'
        $menu.Command.Execute($null, $menu.CommandTarget)
        $editor.CaretOffset = 0
        $editor.TextArea.ClearSelection()
        $query.Text = 'const'
        $matchLine = $editor.Document.GetLineByOffset($editor.SelectionStart).LineNumber
        $view = $editor.TextArea.TextView
        Wait-Regression $Context {
            $view.VisualLinesValid -and $view.VisualLines.Count -gt 0 -and
                $view.VisualLines[0].FirstDocumentLine.LineNumber -le $matchLine -and
                $view.VisualLines[$view.VisualLines.Count - 1].LastDocumentLine.LineNumber -ge $matchLine
        } 'search scrolls a matching line into view with the search box focused'
        Assert-Regression ($editor.SelectedText -ceq 'const' -and $query.IsKeyboardFocused) 'search selects a match and keeps input focus'
        $editor.TextArea.ClearSelection()
        Assert-Regression ($renderer.Count -gt 0) 'theme fixture contains search matches'
        $palette = @{}
        foreach ($appearance in @([CefFlashBrowser.Models.Theme]::Light, [CefFlashBrowser.Models.Theme]::Dark)) {
            Set-ManagerRegressionTheme $Context $appearance
            $editor.ScrollToHorizontalOffset(0)
            $Window.UpdateLayout()
            $matchColor = $renderer.MarkerBrush.Color
            $palette[$appearance.ToString()] = $matchColor
            $bitmap = New-Object Windows.Media.Imaging.RenderTargetBitmap([int]$Window.ActualWidth, [int]$Window.ActualHeight, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
            $bitmap.Render($Window)
            $stride = $bitmap.PixelWidth * 4
            $pixels = New-Object byte[] ($stride * $bitmap.PixelHeight)
            $bitmap.CopyPixels($pixels, $stride, 0)
            $found = $false
            for ($offset = 0; $offset -lt $pixels.Length; $offset += 4) {
                if ($pixels[$offset] -eq $matchColor.B -and $pixels[$offset + 1] -eq $matchColor.G -and
                    $pixels[$offset + 2] -eq $matchColor.R -and $pixels[$offset + 3] -eq 255) {
                    $found = $true
                    break
                }
            }
            Assert-Regression ($found) "editor renders match highlights in the $appearance theme"
            Assert-Regression ($bar.Background.Color -eq $bar.FindResource('SecondaryRegionBrush').Color) "search toolbar follows the $appearance theme"
        }
        $previous = $Window.FindName('findPrevious')
        $next = $Window.FindName('findNext')
        $matchCase = $Window.FindName('matchCase')
        foreach ($locale in $Context.LanguageService.GetSupportedLanguage()) {
            $Context.LanguageService.SetCurrentLanguage($locale)
            Assert-Regression ([HandyControl.Controls.InfoElement]::GetPlaceholder($filter) -ceq
                $Context.LanguageService.GetString('common_filterPlaceholder')) "list filter placeholder follows the language in $locale"
            foreach ($name in @('matchCount', 'noMatches')) {
                $query.Text = if ($name -eq 'matchCount') { 'const' } else { 'missing search result' }
                $Window.UpdateLayout()
                $status = $Window.FindName($name)
                $statusBounds = $status.TransformToAncestor($bar).TransformBounds((New-Object Windows.Rect($status.RenderSize)))
                $nextBounds = $next.TransformToAncestor($bar).TransformBounds((New-Object Windows.Rect($next.RenderSize)))
                $caseBounds = $matchCase.TransformToAncestor($bar).TransformBounds((New-Object Windows.Rect($matchCase.RenderSize)))
                $center = $nextBounds.Top + $nextBounds.Height / 2
                $previousCenter = $previous.TranslatePoint((New-Object Windows.Point(0, ($previous.ActualHeight / 2))), $bar).Y
                $rightEdge = $bar.ActualWidth - $bar.Padding.Right - $bar.BorderThickness.Right
                Assert-Regression ($status.IsVisible -and $status.ActualWidth -gt 0 -and $matchCase.ActualWidth -gt 0 -and
                    $status.TextAlignment -eq [Windows.TextAlignment]::Right -and [Math]::Abs($statusBounds.Right - $rightEdge) -lt 1 -and
                    $status.Parent.Margin.Left -eq 10 -and $statusBounds.Left -ge $caseBounds.Right + 9 -and
                    [Math]::Abs($statusBounds.Top + $statusBounds.Height / 2 - $center) -lt 1 -and
                    [Math]::Abs($caseBounds.Top + $caseBounds.Height / 2 - $center) -lt 1 -and
                    [Math]::Abs($previousCenter - $center) -lt 1) "search status $name aligns with navigation and match case in $locale"
            }
            $barBounds = $bar.TransformToAncestor($Window).TransformBounds((New-Object Windows.Rect($bar.RenderSize)))
            Assert-Regression ($barBounds.Right -le $Window.ActualWidth) "search toolbar stays inside the window in $locale"
            foreach ($name in @('replaceCurrent', 'replaceAll')) {
                $control = $Window.FindName($name)
                $bounds = $control.TransformToAncestor($bar).TransformBounds((New-Object Windows.Rect($control.RenderSize)))
                Assert-Regression ($bounds.Right -le $bar.ActualWidth) "search control $name fits at minimum editor width in $locale"
            }
            Assert-Regression ($editor.ActualHeight -gt 0) "editor remains visible below the search toolbar in $locale"
        }
        Assert-Regression ($palette.Light -ne $palette.Dark) 'search highlight colors change with the editor theme'
    } finally {
        $Window.Width = $width
        $scriptColumn.Width = $columnWidth
        $Window.FindName('closeSearchButton').RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Primitives.ButtonBase]::ClickEvent)))
        $editor.TextArea.ClearSelection()
        $Context.LanguageService.SetCurrentLanguage($language)
        Set-ManagerRegressionTheme $Context $theme
    }
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

function Invoke-ManagerWithConfirmation($Context, $Window, [string]$Action, [hashtable]$Expected,
    [switch]$Update, [scriptblock]$Start = { $Model.SaveAsync() }, $CommandItem) {
    $model = $Window.DataContext
    $state = @{ Count = 0; Failure = $null }
    $timer = New-Object Windows.Threading.DispatcherTimer
    $timer.Interval = [TimeSpan]::FromMilliseconds(50)
    $handler = [EventHandler]({
        $dialog = @($Context.Application.Windows | Where-Object {
            $_ -is [CefFlashBrowser.Views.Dialogs.UserScriptInstallDialog] -or $_ -is [CefFlashBrowser.Views.Dialogs.JsConfirmDialog]
        }) | Select-Object -First 1
        if ($null -eq $dialog) { return }

        $state.Count++
        $timer.Stop()
        try {
            Assert-Regression ($dialog -is [CefFlashBrowser.Views.Dialogs.UserScriptInstallDialog] -and
                [object]::ReferenceEquals($dialog.Owner, $Window) -and $model.IsBusy) 'external installation opens a dedicated modal owned by the manager'
            foreach ($name in $Expected.Text.Keys) {
                $control = $dialog.FindName($name)
                $text = $Expected.Text[$name]
                Assert-Regression ($control.Text -ceq $text -and ($control.IsVisible -eq ![string]::IsNullOrWhiteSpace($text))) "installation dialog presents $name from the reviewed script"
                if ($control -is [Windows.Controls.TextBlock] -and $control.Parent -is [Windows.Controls.Grid]) {
                    $grid = $control.Parent
                    $row = [Windows.Controls.Grid]::GetRow($control)
                    $label = $grid.Children | Where-Object {
                        [Windows.Controls.Grid]::GetRow($_) -eq $row -and [Windows.Controls.Grid]::GetColumn($_) -eq 0
                    }
                    Assert-Regression ($label.Visibility -eq $control.Visibility -and
                        ($control.IsVisible -or $grid.RowDefinitions[$row].ActualHeight -eq 0)) "installation dialog hides the entire $name row when absent"
                }
            }

            $icon = $dialog.FindName('scriptIcon')
            $fallback = $icon.Template.FindName('fallbackIcon', $icon)
            Assert-Regression ($icon.FaviconUrl -eq $Expected.IconUrl) 'installation dialog resolves the reviewed icon URL'
            if ($Expected.IconReady) {
                Wait-Regression $Context {
                    $image = $icon.Template.FindName('favicon', $icon)
                    $null -ne $image.Source -and !$image.Source.IsDownloading -and !$fallback.IsVisible
                } 'declared icon loads in the installation dialog'
            } else {
                if ($Expected.IconUrl) {
                    Wait-Regression $Context { $icon.HasImageFailed } 'failed installation icon finishes loading'
                }
                Assert-Regression ($fallback.IsVisible -and (Get-ManagerIconText $icon).Text -eq $Expected.Initial) 'installation dialog shows the script name initial as the icon fallback'
            }

            $install = $dialog.FindName('installButton')
            $cancel = $dialog.FindName('cancelButton')
            $titleKey = if ($Update) { 'userScriptInstall_confirmUpdate' } else { 'userScriptManager_confirmInstall' }
            $buttonKey = if ($Update) { 'userScriptInstall_update' } else { 'userScriptInstall_install' }
            Assert-Regression ($dialog.Title -ceq $Context.LanguageService.GetString($titleKey) -and
                $install.Content -ceq $Context.LanguageService.GetString($buttonKey) -and
                $cancel.Content -ceq $Context.LanguageService.GetString('common_cancel')) 'installation dialog localizes the title and actions'
            if ($Expected.Scrolls) {
                $details = $dialog.FindName('scriptDetails')
                Assert-Regression ($details.ComputedVerticalScrollBarVisibility -eq [Windows.Visibility]::Visible -and
                    $details.ExtentWidth -le $details.ViewportWidth -and $install.IsVisible -and $cancel.IsVisible) 'long script details wrap and scroll above the action buttons'
                Assert-Regression ($dialog.Background.Color -eq $dialog.FindResource('RegionBrush').Color -and
                    $dialog.FindName('scriptDescription').Foreground.Color -eq $dialog.FindResource('PrimaryTextBrush').Color) 'installation dialog uses the active theme colors'
            }

            switch ($Action) {
                'Close' { $dialog.Close() }
                'Escape' {
                    Set-TabWindowForeground $Context $dialog
                    [RegressionKeyboard]::PressKey(0x1B)
                }
                'Enter' {
                    Set-TabWindowForeground $Context $dialog
                    Assert-Regression ($install.IsKeyboardFocused) 'installation action has initial keyboard focus'
                    [RegressionKeyboard]::PressKey(0x0D)
                }
                default {
                    $button = if ($Action -eq 'Install') { $install } else { $cancel }
                    $peer = New-Object Windows.Automation.Peers.ButtonAutomationPeer($button)
                    $peer.GetPattern([Windows.Automation.Peers.PatternInterface]::Invoke).Invoke()
                }
            }
            Wait-Regression $Context { !$dialog.IsVisible } "$Action dismisses the installation dialog"
        } catch {
            $state.Failure = $_
        } finally {
            if ($dialog.IsVisible) { $dialog.Close() }
        }
    }.GetNewClosure())
    $timer.add_Tick($handler)
    $timer.Start()
    try {
        if ($null -eq $CommandItem) {
            Complete-UserScriptTask $Context (& $Start) 'manager installation confirmation completes'
        } else {
            Invoke-BrowserCommand $CommandItem.Command $CommandItem.CommandParameter
            Wait-Regression $Context { !$model.IsBusy } 'manager update command completes'
        }
        if ($null -ne $state.Failure) { throw $state.Failure }
        Assert-Regression ($state.Count -eq 1) 'external installation opens exactly one confirmation'
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
        $menu.Items.Count -eq 9 -and $menu.Items[4] -is [Windows.Controls.Separator] -and
            $menu.Items[5].Command -eq $Window.DataContext.DeleteCommand -and
            $menu.Items[5].Header -eq $Context.LanguageService.GetString('common_delete') -and
            $menu.Items[6] -is [Windows.Controls.Separator] -and
            $menu.Items[7].Command -eq $Window.DataContext.UpdateCommand -and
            [object]::ReferenceEquals($menu.Items[7].CommandParameter, $Script) -and
            $menu.Items[7].Header -eq $Context.LanguageService.GetString('userScriptInstall_update') -and
            $menu.Items[8].Command -eq $Window.DataContext.ShowDetailsCommand -and
            [object]::ReferenceEquals($menu.Items[8].CommandParameter, $Script) -and
            $menu.Items[8].Header -eq $Context.LanguageService.GetString('userScriptManager_showDetails')
    } 'script context menu places update directly before view details and binds both to the row'
    return $menu
}

function Send-ManagerScriptClick($List, $Script, [switch]$Right) {
    $List.ScrollIntoView($Script)
    $List.UpdateLayout()
    $row = $List.ItemContainerGenerator.ContainerFromItem($Script)
    $point = $row.PointToScreen([Windows.Point]::new($row.ActualWidth / 4, $row.ActualHeight / 2))
    [RegressionKeyboard]::ClickMouse([int]$point.X, [int]$point.Y, $Right.IsPresent)
    return $row
}

function Invoke-ManagerDetailsDialog($Context, $Window, $MenuItem, [scriptblock]$Check, [string]$Dismiss) {
    Assert-Regression ($MenuItem.IsEnabled) 'view details is enabled for a script'
    $state = @{ Count = 0; Failure = $null }
    $timer = New-Object Windows.Threading.DispatcherTimer
    $timer.Interval = [TimeSpan]::FromMilliseconds(50)
    $handler = [EventHandler]({
        $dialog = $Context.Application.Windows | Where-Object {
            $_.IsVisible -and $_ -is [CefFlashBrowser.Views.Dialogs.UserScriptDetailsDialog]
        } | Select-Object -First 1
        if ($null -eq $dialog) { return }

        $timer.Stop()
        $state.Count++
        try {
            $button = $dialog.FindName('closeButton')
            Assert-Regression ([object]::ReferenceEquals($dialog.Owner, $Window) -and
                $dialog.Title -ceq $Context.LanguageService.GetString('userScriptDetails_title') -and
                $button.Content -ceq $Context.LanguageService.GetString('common_close')) 'details dialog is owned by the manager and localized'
            & $Check $dialog
            if ($Dismiss -eq 'Button') {
                $peer = New-Object Windows.Automation.Peers.ButtonAutomationPeer($button)
                $peer.GetPattern([Windows.Automation.Peers.PatternInterface]::Invoke).Invoke()
            } else {
                Set-TabWindowForeground $Context $dialog
                if ($Dismiss -eq 'Escape') {
                    [RegressionKeyboard]::PressKey(0x1B)
                } else {
                    [RegressionKeyboard]::SetKey(0x11, $false)
                    try { [RegressionKeyboard]::PressKey(0x57) }
                    finally { [RegressionKeyboard]::SetKey(0x11, $true) }
                }
            }
            Wait-Regression $Context { !$dialog.IsVisible } "$Dismiss closes the details dialog"
        } catch {
            $state.Failure = $_
        } finally {
            if ($dialog.IsVisible) { $dialog.Close() }
        }
    }.GetNewClosure())
    $timer.add_Tick($handler)
    $timer.Start()
    try {
        [void][Windows.Controls.MenuItem].GetMethod('OnClick', [Reflection.BindingFlags]'Instance,NonPublic').Invoke($MenuItem, @())
        Wait-Regression $Context { $state.Count -gt 0 } 'view details displays its dialog'
        if ($null -ne $state.Failure) { throw $state.Failure }
        Assert-Regression ($state.Count -eq 1) 'menu item opens one details dialog'
    } finally {
        $timer.Stop()
        $timer.remove_Tick($handler)
    }
}

function Test-ManagerDetails($Context, $Window, $Item) {
    $model = $Window.DataContext
    $detailsItem = $Window.FindName('showDetailsMenuItem')
    $scriptMenu = [Windows.Controls.ItemsControl]::ItemsControlFromItemContainer($detailsItem)
    $installedSource = $Item.Source
    $draft = $null
    $menu = $null
    try {
        $model.SelectedScript = $null
        $scriptMenu.IsSubmenuOpen = $true
        Wait-Regression $Context { !$detailsItem.IsEnabled } 'view details is disabled without a selection'
        Assert-Regression ($detailsItem.Header -ceq $Context.LanguageService.GetString('userScriptManager_showDetails')) 'script menu localizes view details'
        $model.SelectedScript = $Item
        $previewSource = @'
// ==UserScript==
// @name Details preview
// @name:zh-CN Localized name
// @namespace CefFlashBrowser.Regression
// @version 2.0
// @description A script with long metadata
// @description:zh-CN Localized description
// @exclude-match https://example.com/private/*
// @match https://example.com/*
// @noframes
// @grant none
// @require https://example.com/library.js#md5=d41d8cd98f00b204e9800998ecf8427e
// @resource logo https://example.com/logo.png
// @antifeature:en-US ads Displays ads
// @webRequest {"selector":"https://example.com/ads/*","action":"cancel"}
// @custom first
// @custom second
// @custom-flag
// @custom-long LENGTH
// ==/UserScript==
'@
        $previewSource = $previewSource.Replace('LENGTH', ('x' * 900))
        $editor = $Window.FindName('sourceEditor')
        $editor.SelectAll()
        $editor.TextArea.PerformTextInput($previewSource)
        Invoke-ManagerDetailsDialog $Context $Window $detailsItem {
            param($dialog)
            Assert-Regression ($dialog.FindName('scriptName').Text -ceq 'Details preview' -and
                $dialog.FindName('scriptVersion').Text -ceq '2.0' -and
                $dialog.FindName('scriptDescription').Text -ceq 'A script with long metadata' -and
                (Get-ManagerIconText ($dialog.FindName('scriptIcon'))).Text -ceq 'D') 'details header shows the current editor text and icon fallback'
            $known = $dialog.FindName('metadataRows')
            $unknown = $dialog.FindName('unknownMetadataRows')
            $rows = @{}
            foreach ($row in $known.Items) { $rows[$row.Key] = $row.Value }
            Assert-Regression ($rows['@name:zh-CN'] -ceq 'Localized name' -and $rows['@description:zh-CN'] -ceq 'Localized description' -and
                $rows['@exclude-match'] -ceq 'https://example.com/private/*' -and $rows['@run-at'] -ceq 'document-idle' -and
                $rows.ContainsKey('@noframes') -and $rows['@grant'] -ceq 'none' -and
                $rows['@require'] -ceq 'https://example.com/library.js#md5=d41d8cd98f00b204e9800998ecf8427e' -and
                $rows['@resource'] -ceq 'logo https://example.com/logo.png' -and
                $rows['@antifeature:en-US'] -ceq 'ads Displays ads' -and
                $rows['@webRequest'] -ceq '{"selector":"https://example.com/ads/*","action":"cancel"}') 'details show normalized scalar, flag, localized and structured metadata'
            $unknownSection = $dialog.FindName('unknownMetadataSection')
            $knownSection = $dialog.FindName('metadataSection')
            $position = $unknownSection.TranslatePoint([Windows.Point]::new(0, 0), $knownSection)
            Assert-Regression ($unknownSection.IsVisible -and $position.Y -ge $knownSection.ActualHeight -and
                ($unknown.Items.Key -join ',') -ceq '@custom,@custom-flag,@custom-long' -and
                $unknown.Items[0].Value -ceq ('first' + [Environment]::NewLine + 'second') -and
                $unknown.Items[1].Value -ceq '') 'unknown fields appear last with repeated and empty values preserved'
            $values = @(Find-FaviconElements $known ([Windows.Controls.TextBox]))
            $scroll = $dialog.FindName('scriptDetails')
            Assert-Regression ($values.Count -eq $known.Items.Count -and $values[0].Text -ceq 'Details preview' -and
                @($values | Where-Object { !$_.IsReadOnly }).Count -eq 0 -and $scroll.ScrollableHeight -gt 0 -and
                $scroll.ExtentWidth -le $scroll.ViewportWidth -and $dialog.FindName('closeButton').IsVisible) 'rendered metadata is read-only and wraps in a scrolling area above Close'
        } 'Button'
        Assert-Regression ($Item.IsDirty -and $Item.Source -ceq $previewSource -and
            $editor.Text -ceq $previewSource -and $Item.Installation.Definition.Source -ceq $installedSource) 'preview leaves the editor and installed source intact'

        $model.NewScript()
        $draft = $model.SelectedScript
        $draft.Source = $draft.Source -replace '(?m)^// @name[^\r\n]*', '// @name Draft preview'
        $Item.Source = $installedSource
        $model.SelectedScript = $Item
        Set-TabWindowForeground $Context $Window
        $list = $Window.FindName('scriptList')
        [void](Send-ManagerScriptClick $list $draft)
        Wait-Regression $Context { [object]::ReferenceEquals($model.SelectedScript, $draft) } 'left click selects the draft'
        $row = Send-ManagerScriptClick $list $Item -Right
        $menu = $row.ContextMenu
        Wait-Regression $Context { $menu.IsOpen -and $menu.IsVisible } 'right click opens the installed script menu for the first time'
        Assert-Regression ($Item.IsInstalled -and !$Item.IsDirty -and
            [object]::ReferenceEquals($model.SelectedScript, $Item) -and
            [object]::ReferenceEquals($menu.Items[8].CommandParameter, $Item) -and
            $menu.Items[8].IsEnabled) 'details is enabled on first open after right-clicking a different installed script'
        $menu.IsOpen = $false
        Wait-Regression $Context { !$menu.IsVisible } 'installed script menu closes'

        $menu = Open-ManagerContextMenu $Context $Window $draft
        $model.SelectedScript = $Item
        Invoke-ManagerDetailsDialog $Context $Window $menu.Items[8] {
            param($dialog)
            Assert-Regression ($dialog.FindName('scriptName').Text -ceq 'Draft preview' -and
                !$dialog.FindName('unknownMetadataSection').IsVisible) 'context details use the row draft with an empty unknown section hidden'
        } 'Escape'

        $model.SelectedScript = $draft
        $draft.Source = '// ==UserScript=='
        $draftName = $draft.Name
        $draftCheck = {
            param($dialog)
            Assert-Regression ($dialog.FindName('scriptName').Text -ceq $draftName -and
                $dialog.FindName('invalidMetadata').IsVisible -and
                !$dialog.FindName('metadataSection').IsVisible) 'an incomplete metadata block shows a draft name and a message'
        }.GetNewClosure()
        $scriptMenu.IsSubmenuOpen = $true
        Invoke-ManagerDetailsDialog $Context $Window $detailsItem $draftCheck 'Ctrl+W'
    } finally {
        $scriptMenu.IsSubmenuOpen = $false
        if ($null -ne $menu) { $menu.IsOpen = $false }
        if ($null -ne $draft) { [void]$model.Scripts.Remove($draft) }
        $Item.Source = $installedSource
        $model.SelectedScript = $Item
    }
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

function Invoke-WebScriptNavigation($Context, $Window, [scriptblock]$Navigate, [string]$ExpectedOrigin,
    [string]$Action, [string]$ResultKey, [switch]$Update) {
    $state = @{ Reviews = 0; Results = 0; Failure = $null }
    $timer = New-Object Windows.Threading.DispatcherTimer
    $timer.Interval = [TimeSpan]::FromMilliseconds(25)
    $handler = [EventHandler]({
        $dialog = $Context.Application.Windows | Where-Object {
            $_.IsVisible -and ($_ -is [CefFlashBrowser.Views.Dialogs.UserScriptInstallDialog] -or
                $_ -is [CefFlashBrowser.Views.Dialogs.JsAlertDialog])
        } | Select-Object -First 1
        if ($null -eq $dialog) { return }

        try {
            Assert-Regression ([object]::ReferenceEquals($dialog.Owner, $Window)) 'web installation dialog belongs to the originating browser window'
            if ($dialog -is [CefFlashBrowser.Views.Dialogs.UserScriptInstallDialog]) {
                $state.Reviews++
                Assert-Regression ($Action -and $state.Reviews -eq 1) 'web navigation presents exactly one confirmation'
                $titleKey = if ($Update) { 'userScriptInstall_confirmUpdate' } else { 'userScriptManager_confirmInstall' }
                $buttonKey = if ($Update) { 'userScriptInstall_update' } else { 'userScriptInstall_install' }
                $install = $dialog.FindName('installButton')
                Assert-Regression ($dialog.Title -ceq $Context.LanguageService.GetString($titleKey) -and
                    $install.Content -ceq $Context.LanguageService.GetString($buttonKey) -and
                    $dialog.FindName('scriptName').Text -ceq 'Web installation fixture' -and
                    $dialog.FindName('scriptOrigin').Text -ceq $ExpectedOrigin) 'browser confirmation shows the reviewed source and the localized action'
                $button = if ($Action -eq 'Confirm') { $install } else { $dialog.FindName('cancelButton') }
                Invoke-BrowserCommand $button.Command $button.CommandParameter
            } else {
                $state.Results++
                Assert-Regression ($ResultKey -and $state.Results -eq 1 -and
                    $dialog.Message -ceq $Context.LanguageService.GetString($ResultKey)) "web navigation shows the expected result $ResultKey"
                $dialog.Close()
            }
        } catch {
            $state.Failure = $_
            if ($dialog.IsVisible) { $dialog.Close() }
        }
    }.GetNewClosure())
    $timer.add_Tick($handler)
    $timer.Start()
    try {
        & $Navigate
        Wait-Regression $Context {
            $null -ne $state.Failure -or (($state.Reviews -eq [int][bool]$Action) -and ($state.Results -eq [int][bool]$ResultKey))
        } 'web script navigation completes'
        if ($null -ne $state.Failure) { throw $state.Failure }
    } finally {
        $timer.Stop()
        $timer.remove_Tick($handler)
    }
}

function Test-WebUserScriptInstallation($Context, $Window, $ManagerWindow, $Fixture) {
    $Manager = $ManagerWindow.DataContext
    $model = $Window.ViewModel.CurrentBrowser
    $browser = $model.Browser
    $service = $Context.UserScriptService
    $repository = $service.Repository
    $token = [Threading.CancellationToken]::None
    $settings = [CefFlashBrowser.Data.GlobalData]::Settings
    $page = $Fixture.BaseUrl + '/web-install'
    $origin = $Fixture.BaseUrl + '/redirect-install.USER.JS?channel=stable'
    $source = @"
// ==UserScript==
// @name Web installation fixture
// @namespace browser-regression
// @version 1.0.0
// @downloadURL $origin
// @match $page
// @sandbox DOM
// @grant none
// @run-at document-end
// ==/UserScript==
document.documentElement.setAttribute('data-web-install', 'ready');
"@
    $Fixture.ScriptSource = $source
    $model.Address = $page
    Wait-Regression $Context { $browser.Title -eq 'Web install fixture' -and !$browser.IsLoading } 'web install fixture is ready'
    $history = Invoke-JavaScript $Context $browser 'history.length'
    $tabCount = $Window.ViewModel.Browsers.Count
    $priorFetches = $Fixture.RequestsFor('/manager.user.js')

    Invoke-WebScriptNavigation $Context $Window {
        [void](Invoke-JavaScript $Context $browser "document.getElementById('install').click()")
    } -ExpectedOrigin $origin -Action Cancel
    Assert-Regression (@($Manager.Scripts | Where-Object { $_.IsInstalled -and $_.Origin -ceq $origin }).Count -eq 0 -and
        $browser.Address -ceq $page -and $Fixture.RequestsFor('/manager.user.js') -eq ($priorFetches + 1)) 'cancelling a same-page link preserves the original page and repository'

    Invoke-WebScriptNavigation $Context $Window {
        [void](Invoke-JavaScript $Context $browser "document.getElementById('new-tab-install').click()")
    } -ExpectedOrigin $origin -Action Confirm -ResultKey userScriptManager_refreshPage
    Wait-Regression $Context {
        @($Manager.Scripts | Where-Object { $_.IsInstalled -and $_.Origin -ceq $origin }).Count -eq 1
    } 'open script manager observes the web installation'
    $item = $Manager.Scripts | Where-Object { $_.IsInstalled -and $_.Origin -ceq $origin }
    $key = $item.Installation.ScriptKey
    Assert-Regression ($item.IsEnabled -and $item.Installation.Definition.Source -ceq $source -and
        $Window.ViewModel.Browsers.Count -eq $tabCount -and (Invoke-JavaScript $Context $browser 'history.length') -eq $history -and
        $browser.Address -ceq $page -and $Fixture.RequestsFor('/manager.user.js') -eq ($priorFetches + 2)) 'new-tab install commits the fetched source once and creates no tab or navigation entry'
    Invoke-BrowserCommand $browser.ReloadCommand
    Wait-Regression $Context {
        (Invoke-JavaScript $Context $browser "document.documentElement && document.documentElement.getAttribute('data-web-install')") -eq 'ready'
    } 'browser-installed script executes on page refresh'

    Complete-UserScriptTask $Context ($repository.SetEnabledAsync($key, $false, $token)) 'disable the web script before update' | Out-Null
    $value = [Mzying2001.MonkeySharp.Core.Storage.UserScriptValue]::FromJson('42')
    Complete-UserScriptTask $Context ($service.ValueStore.SetAsync($key.ToString(), 'web-install', $value, $token)) 'save userscript data before update' | Out-Null
    $updated = $source.Replace('1.0.0', '2.0.0')
    $Fixture.ScriptSource = $updated
    Invoke-WebScriptNavigation $Context $Window {
        Invoke-BrowserCommand $browser.LoadUrlCommand ($origin + '#address-bar')
    } -ExpectedOrigin $origin -Action Confirm -ResultKey userScriptManager_refreshPage -Update
    Wait-Regression $Context { $item.Installation.Definition.Source -ceq $updated } 'open script manager observes the web update'
    $saved = Complete-UserScriptTask $Context ($service.ValueStore.GetAsync($key.ToString(), 'web-install', $token)) 'read preserved userscript data'
    Assert-Regression (@($Manager.Scripts | Where-Object { $_.IsInstalled -and $_.Origin -ceq $origin }).Count -eq 1 -and
        $item.Installation.ScriptKey.Equals($key) -and !$item.IsEnabled -and $saved.Value.EncodedValue -ceq $value.EncodedValue -and
        $model.Address -ceq $page -and $Window.FindName('addressTextBox').Text -ceq $page -and
        $Fixture.RequestsFor('/manager.user.js') -eq ($priorFetches + 3)) 'address-bar update preserves the script key, enabled state, data, and current page address'

    $duplicate = Complete-UserScriptTask $Context ($repository.InstallAsync($source, ($origin + '#old-import'), $true, $token)) 'create a historical duplicate'
    Invoke-WebScriptNavigation $Context $Window {
        Invoke-BrowserCommand $browser.LoadUrlCommand $origin
    } -ResultKey userScriptInstall_conflict
    $unmodified = Complete-UserScriptTask $Context ($repository.GetAsync($duplicate.ScriptKey, $token)) 'read untouched historical duplicate'
    Assert-Regression ($unmodified.Definition.Source -ceq $source -and $item.Installation.Definition.Source -ceq $updated) 'conflicting historical installs remain unchanged'
    Complete-UserScriptTask $Context ($repository.RemoveAsync($duplicate.ScriptKey, $token)) 'remove the historical duplicate' | Out-Null

    Invoke-WebScriptNavigation $Context $Window {
        Invoke-BrowserCommand $browser.LoadUrlCommand ($Fixture.BaseUrl + '/invalid-install.user.js')
    } -ResultKey userScriptManager_invalidMetadata
    Invoke-WebScriptNavigation $Context $Window {
        Invoke-BrowserCommand $browser.LoadUrlCommand ($Fixture.BaseUrl + '/missing-install.user.js')
    } -ResultKey userScriptInstall_downloadFailed
    Assert-Regression ($model.Address -ceq $page -and $item.Installation.Definition.Source -ceq $updated) 'invalid source and HTTP failures leave the document and script unchanged'

    $background = $Fixture.BaseUrl + '/web-install-background'
    $model.Address = $background
    Wait-Regression $Context {
        $browser.Title -eq 'Background script fixture' -and !$browser.IsLoading -and
        $Fixture.RequestsFor('/child-install.user.js') -eq 1 -and
        (Invoke-JavaScript $Context $browser 'window.ordinaryResourceRan') -eq $true
    } 'subframes and script resources with the suffix load normally'
    $normal = $Fixture.BaseUrl + '/normal.js'
    Invoke-BrowserCommand $browser.LoadUrlCommand $normal
    Wait-Regression $Context {
        $browser.Address -ceq $normal -and !$browser.IsLoading -and $Fixture.RequestsFor('/normal.js') -eq 1
    } 'ordinary JavaScript URL loads without installation'

    $enabled = $settings.EnableUserScripts
    try {
        $settings.EnableUserScripts = $false
        Invoke-BrowserCommand $browser.LoadUrlCommand ($Fixture.BaseUrl + '/invalid-install.user.js')
        Wait-Regression $Context { $browser.Title -eq 'Plain web page' } 'script links navigate normally when userscripts are off'
    } finally {
        $settings.EnableUserScripts = $enabled
    }

    $closing = New-RegressionWindow $Context
    $closingBrowser = $closing.ViewModel.CurrentBrowser.Browser
    $slow = $Fixture.BaseUrl + '/slow-install.user.js'
    $beforeDuplicate = $browser.Address
    try {
        Invoke-BrowserCommand $closingBrowser.LoadUrlCommand $slow
        Wait-Regression $Context { $Fixture.RequestsFor('/slow-install.user.js') -eq 1 } 'start a pending installation download'
        Invoke-BrowserCommand $browser.LoadUrlCommand ($slow + '#repeat')
        Wait-Regression $Context { $browser.Address -ceq $beforeDuplicate -and !$browser.IsLoading } 'intercept a simultaneous request for the same script'
        $closing.ForceCloseWindow()
        Wait-Regression $Context { $closingBrowser.IsDisposed } 'close the originating browser during download'
        Invoke-WebScriptNavigation $Context $Window {
            Invoke-BrowserCommand $browser.LoadUrlCommand ($Fixture.BaseUrl + '/invalid-install.user.js?after=close')
        } -ResultKey userScriptManager_invalidMetadata
        Assert-Regression ($Fixture.RequestsFor('/slow-install.user.js') -eq 1 -and
            @($Manager.Scripts | Where-Object { $_.Origin -ceq $slow }).Count -eq 0) 'duplicate clicks share one request and closing the source releases the next install'
    } finally {
        $closing.ForceCloseWindow()
        $Fixture.Release('/slow-install.user.js')
    }

    $Fixture.ScriptSource = $updated.Replace('2.0.0', '3.0.0')
    $item.Source += "`n// unsaved edit before update"
    $scriptCount = $Manager.Scripts.Count
    $expected = @{
        Text = @{ scriptName = 'Web installation fixture'; scriptVersion = '3.0.0'; scriptOrigin = $origin }
        Initial = 'W'
    }
    Invoke-ManagerWithConfirmation $Context $ManagerWindow 'Install' $expected -Update -Start { $Manager.ImportUrlAsync($origin) }
    $editor = $ManagerWindow.FindName('sourceEditor')
    Assert-Regression ($Manager.Scripts.Count -eq $scriptCount -and [object]::ReferenceEquals($item, $Manager.SelectedScript) -and
        !$item.IsDirty -and $editor.Text -ceq $Fixture.ScriptSource) 'loading a matching URL confirms immediately and displays the updated source'
    $saved = Complete-UserScriptTask $Context ($service.ValueStore.GetAsync($key.ToString(), 'web-install', $token)) 'read data preserved by the manager update'
    Assert-Regression (@($Manager.Scripts | Where-Object { $_.IsInstalled -and $_.Origin -ceq $origin }).Count -eq 1 -and
        $item.IsInstalled -and $item.Installation.ScriptKey.Equals($key) -and !$item.IsEnabled -and
        $item.Installation.Definition.Source -ceq $Fixture.ScriptSource -and $saved.Value.EncodedValue -ceq $value.EncodedValue) 'manager updates the web installation in place and preserves enabled state and data'

    $Fixture.ScriptSource = $updated.Replace('2.0.0', '4.0.0')
    $expected.Text.scriptVersion = '4.0.0'
    $menu = Open-ManagerContextMenu $Context $ManagerWindow $item
    $updateItem = $menu.Items[7]
    Assert-Regression ($updateItem.IsEnabled) 'update is enabled for the installed row'
    $menu.IsOpen = $false
    $priorFetches = $Fixture.RequestsFor('/manager.user.js')
    Invoke-ManagerWithConfirmation $Context $ManagerWindow 'Install' $expected -Update -CommandItem $updateItem
    Assert-Regression ($Manager.Scripts.Count -eq $scriptCount -and $item.Installation.ScriptKey.Equals($key) -and
        !$item.IsEnabled -and !$item.IsDirty -and $editor.Text -ceq $Fixture.ScriptSource -and
        $Fixture.RequestsFor('/manager.user.js') -eq ($priorFetches + 1)) 'row update applies the confirmed source in place with one fetch'

    $menu = Open-ManagerContextMenu $Context $ManagerWindow $item
    $menu.IsOpen = $false
    $state = @{ Count = 0; Failure = $null }
    $timer = New-Object Windows.Threading.DispatcherTimer
    $timer.Interval = [TimeSpan]::FromMilliseconds(25)
    $handler = [EventHandler]({
        $dialog = $Context.Application.Windows | Where-Object {
            $_.IsVisible -and ($_.Owner -eq $ManagerWindow) -and
                ($_ -is [CefFlashBrowser.Views.Dialogs.JsAlertDialog] -or $_ -is [CefFlashBrowser.Views.Dialogs.UserScriptInstallDialog])
        } | Select-Object -First 1
        if ($null -eq $dialog) { return }

        try {
            $state.Count++
            $message = $Context.LanguageService.GetString('userScriptManager_upToDate') -f $item.Name
            Assert-Regression ($dialog -is [CefFlashBrowser.Views.Dialogs.JsAlertDialog] -and
                $dialog.Message -ceq $message -and $dialog.Message.Contains($item.Name)) 'the up-to-date alert includes the script name'
        } catch {
            $state.Failure = $_
        } finally {
            $dialog.Close()
        }
    }.GetNewClosure())
    $timer.add_Tick($handler)
    $timer.Start()
    try {
        $revision = $item.Installation.RevisionId
        Invoke-BrowserCommand $menu.Items[7].Command $menu.Items[7].CommandParameter
        Wait-Regression $Context { !$Manager.IsBusy } 'up-to-date check completes'
        if ($null -ne $state.Failure) { throw $state.Failure }
        Assert-Regression ($state.Count -eq 1 -and $item.Installation.RevisionId -eq $revision) 'an up-to-date check only shows the result alert'
    } finally {
        $timer.Stop()
        $timer.remove_Tick($handler)
    }
}

function Invoke-BatchUpdateConfirmation($Context, $Window, $Command, $Parameter, [Type]$DialogType,
    [string]$ButtonName, [string]$ExpectedWarning, [scriptblock]$Check) {
    $state = @{ Count = 0; Failure = $null }
    $timer = New-Object Windows.Threading.DispatcherTimer
    $timer.Interval = [TimeSpan]::FromMilliseconds(25)
    $handler = [EventHandler]({
        $dialog = $Context.Application.Windows | Where-Object { $_.IsVisible -and $_.Owner -eq $Window } | Select-Object -First 1
        if ($null -eq $dialog) { return }
        try {
            $state.Count++
            Assert-Regression ($dialog.GetType() -eq $DialogType) 'update confirmation is owned by the batch window'
            & $Check $dialog $ExpectedWarning
            $peer = New-Object Windows.Automation.Peers.ButtonAutomationPeer($dialog.FindName($ButtonName))
            $peer.GetPattern([Windows.Automation.Peers.PatternInterface]::Invoke).Invoke()
            Wait-Regression $Context { !$dialog.IsVisible } 'update confirmation dismisses'
        } catch {
            $state.Failure = $_
            $dialog.Close()
        }
    }.GetNewClosure())
    $timer.add_Tick($handler)
    $timer.Start()
    try {
        Invoke-BrowserCommand $Command $Parameter
        Wait-Regression $Context { !$Window.DataContext.IsBusy } 'batch update action completes'
        if ($null -ne $state.Failure) { throw $state.Failure }
        Assert-Regression ($state.Count -eq 1) 'the update action shows exactly one confirmation'
    } finally {
        $timer.Stop()
        $timer.remove_Tick($handler)
    }
}

function Test-ManagerBatchUpdate($Context, $Window) {
    $manager = $Window.DataContext
    $source = @'
// ==UserScript==
// @name Batch regression NAME
// @namespace CefFlashBrowser.Regression
// @version 1.0
// @match https://example.invalid/*
// @downloadURL UPDATE_URL
// ==/UserScript==
'@
    $fixtures = @((New-Object UserScriptHttpFixture), (New-Object UserScriptHttpFixture))
    $slowPath = '/slow-install.user.js'
    $names = @('A', 'B')
    $menu = $Window.FindName('batchUpdateMenuItem')
    $scriptMenu = [Windows.Controls.ItemsControl]::ItemsControlFromItemContainer($menu)
    Assert-Regression ($scriptMenu.Items[0] -eq $menu -and $scriptMenu.Items[1] -is [Windows.Controls.Separator] -and
        $menu.Header -eq $Context.LanguageService.GetString('userScriptBatchUpdate_menu')) 'batch update is the first Script menu item and is followed by a separator'

    try {
        for ($i = 0; $i -lt $fixtures.Count; $i++) {
            $fixture = $fixtures[$i]
            $path = if ($i -eq 0) { $slowPath } else { '/manager.user.js' }
            $original = $source.Replace('NAME', $names[$i]).Replace('UPDATE_URL', ($fixture.BaseUrl + $path))
            if ($i -eq 1) {
                $original = $original.Replace('// ==/UserScript==', "// @icon $($fixture.BaseUrl)/blocked.png`n// ==/UserScript==")
            }
            $fixture.ScriptSource = $original.Replace('1.0', '2.0')
            Complete-UserScriptTask $Context ($Context.UserScriptService.Repository.InstallAsync($original, $null, ($i -eq 0),
                [Threading.CancellationToken]::None)) 'install a batch update fixture' | Out-Null
        }
        Wait-Regression $Context { @($manager.Scripts | Where-Object Name -like 'Batch regression *').Count -eq 2 } 'manager observes both batch fixtures'
        $edited = $manager.Scripts | Where-Object Name -eq 'Batch regression A'
        $manager.SelectedScript = $edited
        $edited.Source += "`n// pending editor change"
        $warn = $Context.LanguageService.GetString('userScriptBatchUpdate_overwriteEdits')
        $state = @{ Failure = $null; Opened = $false }
        $timer = New-Object Windows.Threading.DispatcherTimer
        $timer.Interval = [TimeSpan]::FromMilliseconds(25)
        $handler = [EventHandler]({
            $batch = $Context.Application.Windows | Where-Object {
                $_.IsVisible -and $_ -is [CefFlashBrowser.Views.Dialogs.UserScriptBatchUpdateDialog]
            } | Select-Object -First 1
            if ($null -eq $batch) { return }
            $timer.Stop()
            $state.Opened = $true
            try {
                Assert-Regression ($batch.Owner -eq $Window -and
                    $batch.Title -eq $Context.LanguageService.GetString('userScriptBatchUpdate_title')) 'batch update opens a localized window owned by the manager'
                $list = $batch.FindName('scriptList')
                $filter = $batch.FindName('scriptFilter')
                $searchIcon = $batch.FindName('filterSearchIcon')
                $emptyMessage = $batch.FindName('scriptListEmptyMessage')
                $check = $batch.FindName('checkAllButton')
                $update = $batch.FindName('updateAllButton')
                Assert-Regression ($list.Items.Count -eq @($manager.Scripts | Where-Object IsInstalled).Count -and
                    $check.IsEnabled -and !$update.IsEnabled) 'batch lists installed scripts and waits for a check before enabling Update All'
                Assert-Regression ($searchIcon.IsVisible -and $searchIcon.UriSource -eq $Window.FindResource('SearchIconUri') -and
                    !$searchIcon.IsHitTestVisible -and $filter.Padding.Left -eq 28) 'batch filter uses the manager search icon and inset'
                Assert-Regression ([Windows.Automation.AutomationProperties]::GetName($filter) -ceq 'ScriptFilter') 'batch filter uses the fixed ScriptFilter automation name'
                $filter.Text = 'Batch regression'
                Set-TabWindowForeground $Context $batch
                [void]$batch.FindName('closeButton').Focus()
                $filter.Select(0, 0)
                [void]$filter.Focus()
                Assert-Regression ($filter.IsKeyboardFocused -and $filter.SelectedText -ceq $filter.Text) 'focusing the batch filter selects the entire query'
                $filter.Text = 'no matching scripts'
                $batch.UpdateLayout()
                $messageOrigin = $emptyMessage.TranslatePoint([Windows.Point]::new(0, 0), $list)
                Assert-Regression ($list.Items.Count -eq 0 -and $emptyMessage.IsVisible -and
                    $emptyMessage.Text -ceq $Context.LanguageService.GetString('userScriptManager_noMatchingScripts') -and
                    [Math]::Abs($messageOrigin.X + ($emptyMessage.ActualWidth - $list.ActualWidth) / 2) -lt 1 -and
                    [Math]::Abs($messageOrigin.Y + ($emptyMessage.ActualHeight - $list.ActualHeight) / 2) -lt 1) 'batch centers the localized message when the filter returns no scripts'
                $filter.Text = 'Batch regression B'
                $batch.UpdateLayout()
                $iconRow = $list.ItemContainerGenerator.ContainerFromItem($list.Items[0])
                $declaredIcon = @(Find-FaviconElements $iconRow ([CefFlashBrowser.Views.Custom.BrowserIcon]))[0]
                Assert-Regression (!$emptyMessage.IsVisible -and $declaredIcon.IsVisible -and $declaredIcon.ActualWidth -eq 16 -and
                    $declaredIcon.ActualHeight -eq 16 -and $declaredIcon.FaviconUrl -eq ($fixtures[1].BaseUrl + '/blocked.png')) 'batch shows a declared script icon at 16 by 16'
                $filter.Text = 'Batch regression A'
                $batch.UpdateLayout()
                Assert-Regression ($list.Items.Count -eq 1) 'batch filter matches the script name'
                $row = $list.Items[0]
                $container = $list.ItemContainerGenerator.ContainerFromItem($row)
                $fallbackIcon = @(Find-FaviconElements $container ([CefFlashBrowser.Views.Custom.BrowserIcon]))[0]
                Assert-Regression ($fallbackIcon.Width -eq 16 -and $fallbackIcon.Height -eq 16 -and
                    (Get-ManagerIconText $fallbackIcon).Text -eq 'B') 'batch uses the script initial when no icon is declared'
                $link = @(Find-FaviconElements $container ([Windows.Controls.TextBlock]) | ForEach-Object { $_.Inlines } |
                    Where-Object { $_ -is [Windows.Documents.Hyperlink] })[0]
                $rowButton = @(Find-FaviconElements $container ([Windows.Controls.Button]))[0]
                Assert-Regression ($link.IsEnabled -and !$rowButton.IsEnabled -and $row.CurrentVersion -eq '1.0' -and
                    $link.Inlines.FirstInline.Text -eq $Context.LanguageService.GetString('userScriptBatchUpdate_check')) 'a new row shows a Get Updates link and a disabled Update button'

                Invoke-BrowserCommand $link.Command $link.CommandParameter
                try {
                    Wait-Regression $Context { $fixtures[0].RequestsFor($slowPath) -eq 1 } 'version check is waiting for the script source'
                    $batch.UpdateLayout()
                    $checking = $Context.LanguageService.GetString('userScriptBatchUpdate_checking')
                    $labels = @(Find-FaviconElements $container ([Windows.Controls.TextBlock]) | Where-Object { $_.IsVisible })
                    Assert-Regression (!$link.Parent.IsVisible -and @($labels | Where-Object Text -ceq $checking).Count -eq 1) 'a pending version check shows only the checking label in place of the link'
                } finally {
                    $fixtures[0].Release($slowPath)
                }
                Wait-Regression $Context { !$batch.DataContext.IsBusy -and $link.Parent.IsVisible -and $link.Inlines.FirstInline.Text -eq '2.0' } 'the link checks its own row and displays the remote version'
                Assert-Regression ($rowButton.IsEnabled -and $update.IsEnabled -and $fixtures[1].RequestsFor('/manager.user.js') -eq 0) 'a newer version enables row and batch actions without fetching other scripts'
                $checkWarning = {
                    param($confirmation, $expectedWarning)
                    Assert-Regression ($confirmation.FindName('scriptVersion').Text -eq '2.0' -and
                        $confirmation.FindName('updateWarning').Text -eq $expectedWarning) 'the row confirmation shows the new version and warns about unsaved edits'
                }
                Invoke-BatchUpdateConfirmation $Context $batch $rowButton.Command $rowButton.CommandParameter `
                    ([CefFlashBrowser.Views.Dialogs.UserScriptInstallDialog]) 'cancelButton' $warn $checkWarning
                Assert-Regression ($edited.IsDirty -and $row.CurrentVersion -eq '1.0') 'cancelling the row update preserves the editor'

                $filter.Text = 'Batch regression'
                Invoke-BrowserCommand $check.Command $check.CommandParameter
                Wait-Regression $Context { !$batch.DataContext.IsBusy -and @($list.Items | Where-Object LatestVersion -eq '2.0').Count -eq 2 } 'Check for Updates checks both visible rows'
                $checkBatchMessage = {
                    param($confirmation, $expectedWarning)
                    Assert-Regression ($confirmation.Message.Contains('Batch regression A') -and
                        $confirmation.Message.Contains('Batch regression B') -and $confirmation.Message.Contains($expectedWarning)) 'one confirmation lists both scripts and flags the edited script'
                }
                Invoke-BatchUpdateConfirmation $Context $batch $update.Command $null `
                    ([CefFlashBrowser.Views.Dialogs.JsConfirmDialog]) 'yesButton' $warn $checkBatchMessage
                Assert-Regression (@($list.Items | Where-Object { $_.CurrentVersion -eq '2.0' -and $_.LatestVersion -eq '2.0' -and !$_.HasStatus }).Count -eq 2 -and
                    !$update.IsEnabled -and
                    $batch.FindName('summaryText').Text -eq ($Context.LanguageService.GetString('userScriptBatchUpdate_summary') -f 2, 0)) 'both rows show the installed versions and the dialog summarizes the update'
                Assert-Regression (!$edited.IsDirty -and $edited.Source -eq $fixtures[0].ScriptSource -and
                    $Window.FindName('sourceEditor').Text -eq $fixtures[0].ScriptSource) 'the manager editor shows the confirmed replacement source'
            } catch {
                $state.Failure = $_
            } finally {
                $batch.Close()
            }
        }.GetNewClosure())
        $timer.add_Tick($handler)
        $timer.Start()
        try {
            Invoke-BrowserCommand $menu.Command $menu.CommandParameter
            if ($null -ne $state.Failure) { throw $state.Failure }
            Assert-Regression ($state.Opened) 'the batch dialog opens from the Script menu'
        } finally {
            $timer.Stop()
            $timer.remove_Tick($handler)
        }
    } finally {
        foreach ($fixture in $fixtures) { $fixture.Dispose() }
    }
}

function Test-ManagerListFilter($Context, $Window, $Item) {
    $model = $Window.DataContext
    $list = $Window.FindName('scriptList')
    $emptyMessage = $Window.FindName('scriptListEmptyMessage')
    $filter = $Window.FindName('scriptFilter')
    $source = $Item.Source
    $drafts = @()
    try {
        $model.SelectedScript = $Item
        $filter.Text = '  wORKFLOW  '
        Assert-Regression ($list.Items.Count -eq 1 -and [object]::ReferenceEquals($list.SelectedItem, $Item)) 'filter trims its input, matches names without case sensitivity and preserves selection'
        $Item.Source += "`n// unsaved filter fixture"
        $editedSource = $Item.Source
        $filter.Text = '1.0.0'
        Assert-Regression ($list.Items.Count -eq 0 -and $null -eq $model.SelectedScript -and
            $Item.Source -ceq $editedSource) 'a version-only query hides the script and preserves its unsaved source'
        Assert-Regression ($emptyMessage.IsVisible -and $emptyMessage.Text -ceq
            $Context.LanguageService.GetString('userScriptManager_noMatchingScripts')) 'a filter with no results shows the matching-script message'
        $filter.Text = '   '
        Assert-Regression ($list.Items.Count -eq $model.Scripts.Count -and $model.IsReorderingEnabled) 'whitespace shows the complete list and permits reordering'
        Assert-Regression (!$emptyMessage.IsVisible) 'clearing the filter hides the empty-list message'
        $filter.Text = 'workflow'
        $model.SelectedScript = $Item
        $Item.Source = $source.Replace('Manager workflow', 'Renamed filter fixture')
        Assert-Regression ($list.Items.Contains($Item)) 'unsaved name changes follow the displayed name'
        Complete-UserScriptTask $Context ($model.SaveAsync()) 'save renamed filter fixture'
        Wait-Regression $Context { !$list.Items.Contains($Item) -and $null -eq $model.SelectedScript } 'saving a non-matching name hides and deselects that script'
        $filter.Text = 'rENAMED'
        Assert-Regression ($list.Items.Contains($Item)) 'the new name is searchable'
        $filter.Text = 'workflow'
        $repository = $Context.UserScriptService.Repository
        Complete-UserScriptTask $Context ($repository.UpdateAsync($Item.Installation.ScriptKey, $source, $null,
            [Threading.CancellationToken]::None)) 'restore the workflow script name through the repository' | Out-Null
        Wait-Regression $Context { $list.Items.Contains($Item) -and $Item.Source -ceq $source } 'an external name change reveals a newly matching script'

        $draftName = $Context.LanguageService.GetString('userScriptManager_newScript')
        $filter.Text = $draftName
        Invoke-BrowserCommand $model.NewCommand
        $drafts += $model.SelectedScript
        Assert-Regression ($filter.Text -ceq $draftName -and !$model.SelectedScript.IsInstalled -and
            $list.Items.Contains($model.SelectedScript)) 'a matching new draft is selected without clearing the filter'
        $filter.Text = 'workflow'
        Invoke-BrowserCommand $model.NewCommand
        $drafts += $model.SelectedScript
        Assert-Regression ($filter.Text.Length -eq 0 -and $list.Items.Contains($model.SelectedScript) -and
            $model.SelectedScript.Name -ceq $draftName) 'a non-matching new draft is selected in the restored list'

        $path = Join-Path $Context.TemporaryPath 'manager-filter.user.js'
        [IO.File]::WriteAllText($path, $source.Replace('Manager workflow', 'Imported filter fixture'))
        foreach ($query in @('filter fixture', 'workflow')) {
            $filter.Text = $query
            Complete-UserScriptTask $Context ($model.ImportFileAsync($path)) "import draft while filtering by $query"
            $drafts += $model.SelectedScript
            $expected = if ($query -eq 'workflow') { '' } else { $query }
            Assert-Regression ($filter.Text -ceq $expected -and $model.SelectedScript.Origin -ceq $path -and
                $list.Items.Contains($model.SelectedScript)) 'import selects its draft and clears the filter only when needed'
        }
    } finally {
        $filter.Text = ''
        foreach ($draft in $drafts) {
            [void]$model.Scripts.Remove($draft)
        }
        $model.SelectedScript = $Item
    }
}

function Test-ManagerExecutionOrder($Context, $Window, $BrowserModel) {
    $repository = $Context.UserScriptService.Repository
    $model = $Window.DataContext
    $token = [Threading.CancellationToken]::None
    $scriptSource = @'
// ==UserScript==
// @name Manager order __MARK__
// @match file:///*
// @noframes
// @grant none
// @run-at document-end
// ==/UserScript==
var root = document.documentElement;
root.setAttribute('data-manager-order', (root.getAttribute('data-manager-order') || '') + '__MARK__');
'@
    $installed = @()
    $behavior = $null
    $filter = $Window.FindName('scriptFilter')
    try {
        foreach ($mark in @('A', 'B')) {
            $installed += Complete-UserScriptTask $Context ($repository.InstallAsync($scriptSource.Replace('__MARK__', $mark), $null, $true, $token)) "install order fixture $mark"
        }
        Invoke-BrowserCommand $BrowserModel.Browser.ReloadCommand
        $readOrder = "document.documentElement && document.documentElement.getAttribute('data-manager-order')"
        Wait-Regression $Context {
            (Invoke-JavaScript $Context $BrowserModel.Browser $readOrder) -ceq 'AB'
        } 'same-stage scripts execute in installation order'

        $key = $installed[1].ScriptKey
        Wait-Regression $Context { @($model.Scripts | Where-Object { $_.IsInstalled -and $_.Installation.ScriptKey.Equals($key) }).Count -eq 1 } 'manager lists the order fixture'
        $item = @($model.Scripts | Where-Object { $_.IsInstalled -and $_.Installation.ScriptKey.Equals($key) })[0]
        $menu = Open-ManagerContextMenu $Context $Window $item
        $resources = @('common_moveUp', 'common_moveDown', 'common_moveToTop', 'common_moveToBottom')
        $commands = @($model.MoveUpCommand, $model.MoveDownCommand, $model.MoveToFirstCommand, $model.MoveToLastCommand)
        for ($i = 0; $i -lt $resources.Count; $i++) {
            Assert-Regression ($menu.Items[$i].Command -eq $commands[$i] -and
                $menu.Items[$i].Header -eq $Context.LanguageService.GetString($resources[$i])) "order menu item $i binds to the manager and current language"
        }
        Wait-Regression $Context {
            $menu.Items[0].IsEnabled -and !$menu.Items[1].IsEnabled -and
                $menu.Items[2].IsEnabled -and !$menu.Items[3].IsEnabled
        } 'last installed script only offers moves toward the beginning'
        $menu.IsOpen = $false
        $list = $Window.FindName('scriptList')
        $behavior = @([Microsoft.Xaml.Behaviors.Interaction]::GetBehaviors($list) | Where-Object {
            $_ -is [CefFlashBrowser.Views.Behaviors.ListReorderBehavior]
        })[0]
        Assert-Regression ($behavior.IsDragEnabled -and [object]::ReferenceEquals($behavior.ReorderCommand, $model.ReorderCommand)) 'the manager enables list reordering and binds its persistence command'
        $first = @($model.Scripts | Where-Object { $_.IsInstalled -and $_.Installation.ScriptKey.Equals($installed[0].ScriptKey) })[0]
        $editor = $Window.FindName('sourceEditor')
        $editor.TextArea.PerformTextInput('// unsaved drag edit')
        $editedSource = $item.Source
        $filter.Text = 'Manager'
        Assert-Regression ($list.Items.Count -eq $model.Scripts.Count -and !$behavior.IsDragEnabled -and
            $null -eq (Start-ListReorderDrag $behavior $item)) 'a filter matching every script also prevents starting a drag'
        $filter.Text = 'Manager order'
        $menu = Open-ManagerContextMenu $Context $Window $item
        Assert-Regression (@($menu.Items | Where-Object { $_.Visibility -eq [Windows.Visibility]::Collapsed }).Count -eq 5 -and
            $menu.Items[5].IsEnabled -and $menu.Items[6].Visibility -eq [Windows.Visibility]::Visible -and
            $menu.Items[7].IsEnabled -and $menu.Items[8].IsEnabled) 'filtering leaves delete, update and details available'
        $menu.IsOpen = $false
        Assert-Regression ($list.Items.Count -eq 2 -and [object]::ReferenceEquals($list.Items[0], $first) -and
            [object]::ReferenceEquals($list.Items[1], $item) -and $editor.Text -ceq $editedSource -and $editor.CanUndo) 'filtered rows retain their order, selection and source edits'
        $filter.Text = ''
        $menu = Open-ManagerContextMenu $Context $Window $item
        Assert-Regression (@($menu.Items | Where-Object { $_.Visibility -eq [Windows.Visibility]::Visible }).Count -eq 9 -and
            $menu.Items[0].IsEnabled -and $behavior.IsDragEnabled) 'clearing the filter restores the move items, their separator and drag reordering'
        $menu.IsOpen = $false
        Invoke-BrowserCommand $menu.Items[0].Command
        Wait-Regression $Context { !$model.IsBusy -and $model.Scripts.IndexOf($item) -lt $model.Scripts.IndexOf($first) } 'the menu moves the script after clearing the filter'
        $snapshot = Complete-UserScriptTask $Context ($repository.GetSnapshotAsync($token)) 'read the saved menu order'
        Assert-Regression ($snapshot[$model.Scripts.IndexOf($item)].ScriptKey.Equals($item.Installation.ScriptKey) -and
            $snapshot[$model.Scripts.IndexOf($first)].ScriptKey.Equals($first.Installation.ScriptKey)) 'the menu persists the restored ordering operation'
        Invoke-BrowserCommand $model.MoveDownCommand
        Wait-Regression $Context { !$model.IsBusy -and $model.Scripts.IndexOf($item) -gt $model.Scripts.IndexOf($first) } 'restore the pre-drag order'

        $flags = [Reflection.BindingFlags]'Instance,NonPublic'
        $Window.UpdateLayout()
        $sourceRow = $list.ItemContainerGenerator.ContainerFromItem($item)
        $targetRow = $list.ItemContainerGenerator.ContainerFromItem($first)
        $toggle = Find-ManagerToggle $sourceRow
        $down = [Windows.Input.MouseButtonEventArgs]::new([Windows.Input.Mouse]::PrimaryDevice, 0, [Windows.Input.MouseButton]::Left)
        $down.RoutedEvent = [Windows.Input.Mouse]::PreviewMouseDownEvent
        $toggle.RaiseEvent($down)
        $candidate = $behavior.GetType().GetField('_dragCandidate', $flags)
        Assert-Regression ($null -eq $candidate.GetValue($behavior)) 'pressing the enable switch does not start a drag'

        $before = $targetRow.TranslatePoint([Windows.Point]::new(65, $targetRow.ActualHeight / 4), $list)
        $over = [Windows.DragDrop]::PreviewDragOverEvent
        $drop = [Windows.DragDrop]::PreviewDropEvent
        $data = Start-ListReorderDrag $behavior $item
        $result = Invoke-ListReorderDragEvent $list $data $before $over
        Assert-Regression ($result.Effects -eq [Windows.DragDropEffects]::Move -and @(Get-ListReorderAdorners $list).Count -eq 1 -and
            $model.Scripts.IndexOf($first) -lt $model.Scripts.IndexOf($item)) 'the upper half shows an insertion line without changing script order'
        [void](Invoke-ListReorderDragEvent $list $data $before ([Windows.DragDrop]::PreviewDragLeaveEvent))
        Assert-Regression (@(Get-ListReorderAdorners $list).Count -eq 0) 'leaving the list clears the insertion line'
        [void](Invoke-ListReorderDragEvent $list $data $before $over)
        Assert-Regression ((Invoke-ListReorderDragQuery $list $true) -eq [Windows.DragAction]::Cancel -and
            @(Get-ListReorderAdorners $list).Count -eq 0 -and $model.Scripts.IndexOf($first) -lt $model.Scripts.IndexOf($item)) 'Esc clears the drop preview and preserves script order'
        Invoke-ViewCallback $behavior 'EndDrag' @()

        $data = Start-ListReorderDrag $behavior $item
        $result = Invoke-ListReorderDragEvent $list $data $before $drop
        Invoke-ViewCallback $behavior 'EndDrag' @()
        Assert-Regression ($result.Effects -eq [Windows.DragDropEffects]::Move) 'the list accepts a drop from its own drag'
        Wait-Regression $Context { !$model.IsBusy -and $model.Scripts.IndexOf($item) -eq ($model.Scripts.IndexOf($first) - 1) } 'dropping in the upper half inserts before the target'
        Assert-Regression ([object]::ReferenceEquals($model.SelectedScript, $item) -and
            $item.IsDirty -and $editor.Text -ceq $editedSource -and @(Get-ListReorderAdorners $list).Count -eq 0) 'dragging preserves selection and unsaved source and clears the insertion line'
        Assert-Regression ((Invoke-JavaScript $Context $BrowserModel.Browser $readOrder) -ceq 'AB') 'reordering does not rerun scripts in the existing document'
        Invoke-BrowserCommand $BrowserModel.Browser.ReloadCommand
        Wait-Regression $Context {
            (Invoke-JavaScript $Context $BrowserModel.Browser $readOrder) -ceq 'BA'
        } 'same-stage scripts follow the custom order after refresh'

        $Window.UpdateLayout()
        $targetRow = $list.ItemContainerGenerator.ContainerFromItem($first)
        $after = $targetRow.TranslatePoint([Windows.Point]::new(65, $targetRow.ActualHeight * 3 / 4), $list)
        $data = Start-ListReorderDrag $behavior $item
        [void](Invoke-ListReorderDragEvent $list $data $after $drop)
        Invoke-ViewCallback $behavior 'EndDrag' @()
        Wait-Regression $Context { !$model.IsBusy -and $model.Scripts.IndexOf($item) -eq ($model.Scripts.IndexOf($first) + 1) } 'dropping in the lower half inserts after the target'

        $Window.UpdateLayout()
        $topItem = $model.Scripts[0]
        $lastRow = $list.ItemContainerGenerator.ContainerFromItem($item)
        $blank = $lastRow.TranslatePoint([Windows.Point]::new(65, $lastRow.ActualHeight + 5), $list)
        $data = Start-ListReorderDrag $behavior $topItem
        $result = Invoke-ListReorderDragEvent $list $data $blank $drop
        Invoke-ViewCallback $behavior 'EndDrag' @()
        Assert-Regression ($result.Effects -eq [Windows.DragDropEffects]::Move) 'the blank space after the last installed row accepts a drop'
        Wait-Regression $Context { !$model.IsBusy -and [object]::ReferenceEquals($list.Items[$list.Items.Count - 1], $topItem) } 'dropping in the trailing space moves the script to the end'

        $selection = $model.SelectedScript
        Invoke-BrowserCommand $model.NewCommand
        $draft = $model.SelectedScript
        try {
            $Window.UpdateLayout()
            $draftRow = $list.ItemContainerGenerator.ContainerFromItem($draft)
            $draftPoint = $draftRow.TranslatePoint([Windows.Point]::new(65, $draftRow.ActualHeight / 2), $list)
            Assert-Regression ($null -eq (Start-ListReorderDrag $behavior $draft)) 'an uninstalled draft cannot start a drag'
            $data = Start-ListReorderDrag $behavior $item
            $result = Invoke-ListReorderDragEvent $list $data $draftPoint $drop
            Assert-Regression ($result.Effects -eq [Windows.DragDropEffects]::None -and !$model.IsBusy) 'an uninstalled draft is excluded as a drop target'
        } finally {
            Invoke-ViewCallback $behavior 'EndDrag' @()
            $model.SelectedScript = $selection
            [void]$model.Scripts.Remove($draft)
        }

        foreach ($index in 1..15) {
            $installed += Complete-UserScriptTask $Context ($repository.InstallAsync($scriptSource.Replace('__MARK__', "Scroll $index"), $null, $false, $token)) "install drag overflow fixture $index"
        }
        $list.ScrollIntoView($item)
        $Window.UpdateLayout()
        $viewport = @(Find-FaviconElements $list ([Windows.Controls.ScrollContentPresenter]))[0]
        $scroll = $viewport.TemplatedParent
        $offset = $scroll.VerticalOffset
        $edge = $viewport.TranslatePoint([Windows.Point]::new(65, $viewport.ActualHeight - 5), $list)
        $data = Start-ListReorderDrag $behavior $item
        [void](Invoke-ListReorderDragEvent $list $data $edge $over)
        Wait-Regression $Context { $scroll.VerticalOffset -gt $offset } 'dragging near the bottom edge scrolls the list'
        $filter.Text = 'Manager order'
        try {
            $Window.UpdateLayout()
            $stoppedOffset = $scroll.VerticalOffset
            $elapsed = [Diagnostics.Stopwatch]::StartNew()
            Wait-Regression $Context { $elapsed.ElapsedMilliseconds -ge 350 } 'two drag-scroll intervals elapse'
            Assert-Regression (!$behavior.IsDragEnabled -and $scroll.VerticalOffset -eq $stoppedOffset -and
                @(Get-ListReorderAdorners $list).Count -eq 0 -and
                $model.Scripts.IndexOf($item) -eq ($model.Scripts.IndexOf($first) + 1)) 'starting a filter cancels the drag, stops scrolling and preserves script order'
        } finally {
            $filter.Text = ''
        }
    } finally {
        $filter.Text = ''
        if ($null -ne $behavior) { Invoke-ViewCallback $behavior 'EndDrag' @() }
        foreach ($installation in $installed) {
            Complete-UserScriptTask $Context ($repository.RemoveAsync($installation.ScriptKey, $token)) 'remove order fixture' | Out-Null
        }
    }
}

function Invoke-ManagerFileDragEvent($Target, [string[]]$Paths, [Windows.RoutedEvent]$Event) {
    $data = [Windows.DataObject]::new([Windows.DataFormats]::FileDrop, $Paths)
    Invoke-RegressionDragEvent $Target $data ([Windows.Point]::new(0, 0)) $Event ([Windows.DragDropEffects]::Copy)
}

function Import-ManagerFileByDrop($Context, $Window, [string]$Path, [string]$Source) {
    $model = $Window.DataContext
    $list = $Window.FindName('scriptList')
    $editor = $Window.FindName('sourceEditor')
    $enter = [Windows.DragDrop]::PreviewDragEnterEvent
    $over = [Windows.DragDrop]::PreviewDragOverEvent
    $drop = [Windows.DragDrop]::PreviewDropEvent
    $copy = [Windows.DragDropEffects]::Copy
    $none = [Windows.DragDropEffects]::None
    $userScript = Join-Path $Context.TemporaryPath 'manager-drag.user.js'
    $textFile = Join-Path $Context.TemporaryPath 'manager-drag.txt'
    $directory = Join-Path $Context.TemporaryPath 'manager-directory.js'
    [IO.File]::WriteAllText($userScript, $Source)
    [IO.File]::WriteAllText($textFile, $Source)
    [void][IO.Directory]::CreateDirectory($directory)

    $result = Invoke-ManagerFileDragEvent $list $userScript $enter
    Assert-Regression ($result.Handled -and $result.Effects -eq $copy) 'the script list accepts a .user.js file'
    $result = Invoke-ManagerFileDragEvent $editor.TextArea $Path $over
    Assert-Regression ($result.Handled -and $result.Effects -eq $copy) 'the editor accepts an uppercase .JS file'

    $invalidDrops = @(
        @{ Paths = @($textFile); Description = 'a non-JavaScript file' }
        @{ Paths = @($directory); Description = 'a directory with a .js suffix' }
        @{ Paths = @($Path, $userScript); Description = 'multiple script files' }
    )
    foreach ($invalid in $invalidDrops) {
        $preview = Invoke-ManagerFileDragEvent $editor.TextArea $invalid.Paths $over
        $result = Invoke-ManagerFileDragEvent $editor.TextArea $invalid.Paths $drop
        Assert-Regression ($preview.Handled -and $preview.Effects -eq $none -and
            $result.Handled -and $result.Effects -eq $none) "the manager rejects $($invalid.Description)"
    }

    $setBusy = $model.GetType().GetProperty('IsBusy').GetSetMethod($true)
    try {
        $setBusy.Invoke($model, @($true))
        $preview = Invoke-ManagerFileDragEvent $list $Path $over
        $result = Invoke-ManagerFileDragEvent $list $Path $drop
        Assert-Regression ($preview.Handled -and $preview.Effects -eq $none -and
            $result.Handled -and $result.Effects -eq $none) 'the manager rejects file drops while busy'
    } finally {
        $setBusy.Invoke($model, @($false))
    }

    $count = $model.Scripts.Count
    $result = Invoke-ManagerFileDragEvent $editor.TextArea $Path $drop
    Assert-Regression ($result.Handled -and $result.Effects -eq $copy) 'the manager accepts the file dropped on the editor'
    Wait-Regression $Context { !$model.IsBusy -and $model.SelectedScript.Origin -ceq $Path } 'load dropped script into the editor'
    Assert-Regression ($model.Scripts.Count -eq ($count + 1) -and !$model.SelectedScript.IsInstalled -and
        $model.SelectedScript.RequiresConfirmation -and $editor.Text -ceq $Source) 'a dropped file opens as an editable draft awaiting installation'
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
        $list = $window.FindName('scriptList')
        $filter = $window.FindName('scriptFilter')
        $emptyMessage = $window.FindName('scriptListEmptyMessage')
        $batchMenu = $window.FindName('batchUpdateMenuItem')
        Assert-Regression (!$batchMenu.IsEnabled) 'batch updates are disabled when no script is installed'
        $window.UpdateLayout()
        $messageOrigin = $emptyMessage.TranslatePoint([Windows.Point]::new(0, 0), $list)
        Assert-Regression ($emptyMessage.IsVisible -and
            [Math]::Abs($messageOrigin.X + ($emptyMessage.ActualWidth - $list.ActualWidth) / 2) -lt 1 -and
            [Math]::Abs($messageOrigin.Y + ($emptyMessage.ActualHeight - $list.ActualHeight) / 2) -lt 1) 'the empty-list message is centered in the script list'
        $noInstalledScripts = $Context.LanguageService.GetString('userScriptManager_noInstalledScripts')
        Assert-Regression ($emptyMessage.Text -ceq $noInstalledScripts) 'an empty manager shows the no-installed-scripts message'
        $filter.Text = 'missing script'
        Assert-Regression ($emptyMessage.IsVisible -and $emptyMessage.Text -ceq $noInstalledScripts) 'filtering an empty manager keeps the no-installed-scripts message'
        $nextLanguage = if ($language -eq 'en-US') { 'zh-CN' } else { 'en-US' }
        $Context.LanguageService.SetCurrentLanguage($nextLanguage)
        Wait-Regression $Context {
            $emptyMessage.Text -cne $noInstalledScripts -and $emptyMessage.Text -ceq
            $Context.LanguageService.GetString('userScriptManager_noInstalledScripts')
        } 'the visible empty-list message follows language changes'
        $Context.LanguageService.SetCurrentLanguage($language)
        $filter.Text = ''
        Test-BrowserIconStyleReuse $Context
        Test-ManagerSourceEditor $Context $window
        Test-ManagerEditorAssistance $Context $window
        Test-ManagerFindReplace $Context $window
        [CefFlashBrowser.Services.WindowManager]::ShowUserScriptManager()
        Assert-Regression (@($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.UserScriptManager] }).Count -eq 1) 'manager entry reuses a single window'
        $gestures = @($window.InputBindings | ForEach-Object { $_.Gesture.GetDisplayStringForCulture([Globalization.CultureInfo]::InvariantCulture) })
        Assert-Regression (($gestures -join ',') -eq 'Ctrl+N,Ctrl+S,Ctrl+W,Ctrl+F,Ctrl+H') 'manager exposes new, save, close, find and replace keyboard bindings'
        Invoke-BrowserCommand $model.NewCommand $null
        $item = $model.SelectedScript
        Assert-Regression ($list.Items.Contains($item) -and !$item.IsInstalled -and
            !$emptyMessage.IsVisible) 'a visible draft hides the empty-list message'
        Assert-Regression (!$batchMenu.IsEnabled) 'an uninstalled draft does not enable batch updates'
        $editor = $window.FindName('sourceEditor')
        Test-ManagerStatusLine $Context $window
        $editor.SelectAll()
        $editor.TextArea.PerformTextInput($source)
        Send-ManagerShortcut $Context $window 0x53
        Wait-Regression $Context { !$model.IsBusy -and $item.IsInstalled -and !$item.IsDirty } 'Ctrl+S saves the latest editor text without confirmation'
        Assert-Regression ($item.IsInstalled -and !$item.IsDirty -and $item.IsEnabled) 'new script saves and enables directly'
        Assert-Regression ($batchMenu.IsEnabled) 'saving the first script enables batch updates immediately'
        $key = $item.Installation.ScriptKey
        $sourcePath = Join-Path (Join-Path $Context.TemporaryPath ('MonkeySharp\' + $key)) 'userscript.js'
        Assert-Regression ([IO.File]::ReadAllText($sourcePath) -eq $source) 'manager persists source through the shared repository'
        Test-ManagerDetails $Context $window $item
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
        Test-ManagerExecutionOrder $Context $window $browserWindow.ViewModel.CurrentBrowser
        Test-ManagerListFilter $Context $window $item
        $model.SelectedScript = $item
        $list = $window.FindName('scriptList')
        $list.ScrollIntoView($item)
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
        $window.FindName('scriptFilter').Text = $draft.Name
        Send-ManagerShortcut $Context $window 0x57
        Wait-Regression $Context { !$window.IsVisible } 'Ctrl+W closes the manager from editor focus'
        [CefFlashBrowser.Services.WindowManager]::ShowUserScriptManager()
        $window = @($Context.Application.Windows | Where-Object { $_ -is [CefFlashBrowser.Views.UserScriptManager] })[0]
        Wait-Regression $Context { $window.IsLoaded -and !$model.IsBusy } 'manager reopened'
        $editor = $window.FindName('sourceEditor')
        Assert-Regression ($window.FindName('scriptFilter').Text.Length -eq 0) 'reopening the manager clears the list filter'
        Assert-Regression ([object]::ReferenceEquals($draft, $model.SelectedScript) -and $item.IsDirty -and $item.Source.Contains('unsaved session draft')) 'selection and unsaved drafts survive manager closure'
        Assert-Regression ($editor.Text -ceq $draft.Source) 'reopened editor displays the selected unsaved draft'
        $path = Join-Path $Context.TemporaryPath 'manager-import.JS'
        $minimalSource = $source -replace '(?m)^// @(match|sandbox|grant).*\r?\n', ''
        [IO.File]::WriteAllText($path, $minimalSource, (New-Object Text.UTF8Encoding($true)))
        Import-ManagerFileByDrop $Context $window $path $minimalSource
        $expected = @{
            Text = @{
                scriptName = 'Manager workflow'; scriptVersion = '1.0.0'; scriptDescription = ''; scriptAuthor = ''; scriptOrigin = $path
                scriptMatches = ''; scriptWorld = ''; scriptGrants = ''; scriptConnects = ''; scriptRequires = ''; scriptResources = ''
            }
            Initial = 'M'
        }
        Invoke-ManagerWithConfirmation $Context $window 'Close' $expected
        Assert-Regression (!$model.SelectedScript.IsInstalled -and $model.SelectedScript.Source -ceq $minimalSource) 'closing installation preserves the local draft'
        $description = ('A long description for the script installation review. ' * 90).TrimEnd()
        $missingIcon = $iconFixture.BaseUrl + '/missing.png'
        $reviewedSource = $source.Replace('Manager workflow', '脚本安装确认').Replace('// @version 1.0.0',
            "// @version 2.0.0`n// @author Local author`n// @description $description`n// @icon $missingIcon")
        $model.SelectedScript.Source = $reviewedSource
        $expected = @{
            Text = @{
                scriptName = '脚本安装确认'; scriptVersion = '2.0.0'; scriptDescription = $description
                scriptAuthor = 'Local author'; scriptOrigin = $path; scriptMatches = 'file:///*'
                scriptWorld = 'DOM'; scriptGrants = 'none'
            }
            IconUrl = $missingIcon; Initial = '脚'; Scrolls = $true
        }
        Set-ManagerRegressionTheme $Context ([CefFlashBrowser.Models.Theme]::Light)
        Invoke-ManagerWithConfirmation $Context $window 'Cancel' $expected
        Assert-Regression (!$model.SelectedScript.IsInstalled -and $model.SelectedScript.Source -ceq $reviewedSource) 'cancel button preserves edited script without installing'
        Set-ManagerRegressionTheme $Context ([CefFlashBrowser.Models.Theme]::Dark)
        Invoke-ManagerWithConfirmation $Context $window 'Escape' $expected
        Assert-Regression (!$model.SelectedScript.IsInstalled -and $model.SelectedScript.Source -ceq $reviewedSource) 'Escape preserves edited script without installing'
        Invoke-ManagerWithConfirmation $Context $window 'Install' $expected
        Assert-Regression ($model.SelectedScript.IsInstalled -and $model.SelectedScript.Installation.Definition.Source -ceq $reviewedSource) 'install button commits the reviewed local source'
        Set-ManagerRegressionTheme $Context $theme

        $url = $fixture.BaseUrl + '/manager.user.js'
        $dependency = $fixture.BaseUrl + '/dependency.js'
        $secondDependency = $fixture.BaseUrl + '/blocked.js'
        $resource = $fixture.BaseUrl + '/resource.txt'
        $secondResource = $fixture.BaseUrl + '/blocked.png'
        $metadata = @"
// @grant GM_getValue
// @grant GM_setValue
// @include https://fixture.invalid/*
// @inject-into content
// @icon64 /blocked.png
// @connect 127.0.0.1
// @connect localhost
// @require $dependency
// @require $secondDependency
// @resource fixture $resource
// @resource second $secondResource
"@
        $fixture.ScriptSource = $source.Replace('// @grant none', $metadata)
        Complete-UserScriptTask $Context ($model.ImportUrlAsync($url)) 'download URL draft'
        Assert-Regression (!$model.SelectedScript.IsInstalled -and $fixture.RequestsFor('/manager.user.js') -eq 1) 'URL download produces a pending draft'
        $expected = @{
            Text = @{
                scriptName = 'Manager workflow'
                scriptOrigin = $url
                scriptMatches = "file:///*`r`nhttps://fixture.invalid/*"
                scriptWorld = "DOM`r`ncontent"
                scriptGrants = "GM_getValue`r`nGM_setValue"
                scriptConnects = "127.0.0.1`r`nlocalhost"
                scriptRequires = "$dependency`r`n$secondDependency"
                scriptResources = "fixture $resource`r`nsecond $secondResource"
            }
            IconUrl = $fixture.BaseUrl + '/blocked.png'; IconReady = $true
        }
        Invoke-ManagerWithConfirmation $Context $window 'Enter' $expected
        Assert-Regression ($model.SelectedScript.IsInstalled -and $model.SelectedScript.Installation.Definition.Source -ceq $fixture.ScriptSource -and
            $fixture.RequestsFor('/manager.user.js') -eq 1) 'Enter installs the reviewed URL source without another source request'
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
        Invoke-ManagerDeleteWithConfirmation $Context $window $menu.Items[5] $false
        Assert-Regression ($model.Scripts.Contains($deleteTarget) -and $deleteTarget.Source.EndsWith('keep until approved') -and
            [IO.File]::Exists($deletePath)) 'declined context deletion preserves installed source and unsaved edits'
        $topDelete = @($window.Content.Children[0].Items | ForEach-Object { $_.Items } |
            Where-Object { $_ -is [Windows.Controls.MenuItem] -and $_.Command -eq $model.DeleteCommand })[0]
        Invoke-ManagerDeleteWithConfirmation $Context $window $topDelete $null
        Assert-Regression ($model.Scripts.Contains($deleteTarget) -and [IO.File]::Exists($deletePath)) 'closing the top-menu confirmation cancels deletion'
        $menu = Open-ManagerContextMenu $Context $window $deleteTarget
        Invoke-ManagerDeleteWithConfirmation $Context $window $menu.Items[5] $true
        Assert-Regression (!$model.Scripts.Contains($deleteTarget) -and ![IO.File]::Exists($deletePath) -and
            $model.Scripts.Contains($previousSelection) -and $model.Scripts.Count -eq $countBeforeDelete + 1) 'approved context deletion removes only the right-clicked script without retaining its dirty draft'
        $model.SelectedScript = $previousSelection
        $menu = Open-ManagerContextMenu $Context $window $deleteDraft
        Invoke-ManagerDeleteWithConfirmation $Context $window $menu.Items[5] $false
        Assert-Regression ($model.Scripts.Contains($deleteDraft) -and $deleteDraft.Source -eq $draftSource) 'declined draft deletion preserves its source'
        Invoke-ManagerDeleteWithConfirmation $Context $window $topDelete $true
        Assert-Regression (!$model.Scripts.Contains($deleteDraft) -and $model.Scripts.Count -eq $countBeforeDelete) 'approved top-menu deletion removes the draft'
        Assert-Regression ($null -eq $window.FindName('scriptList').ContextMenu) 'blank script-list space has no deletion menu'
        $model.SelectedScript = $previousSelection
        $searchAutomationNames = @{
            closeSearchButton = 'Close'
            searchText = 'Find'
            findPrevious = 'PreviousMatch'
            findNext = 'NextMatch'
            matchCase = 'MatchCase'
            replacementText = 'Replace'
            replaceCurrent = 'ReplaceCurrent'
        }
        foreach ($locale in @('en-US', 'zh-CN', 'zh-TW', 'de', 'fr-FR', 'it', 'ja', 'ko', 'ru')) {
            $Context.LanguageService.SetCurrentLanguage($locale)
            Wait-Regression $Context { $window.Title -eq $Context.LanguageService.GetString('userScriptManager_title') } "manager language ($locale)"
            Assert-Regression (![string]::IsNullOrEmpty($window.Title)) 'manager title is localized in every supported language'
            $replaceMenu = $window.FindName('replaceMenuItem')
            $replaceMenu.Command.Execute($null, $replaceMenu.CommandTarget)
            $searchInput = $window.FindName('searchText')
            $replaceAllLabel = $window.FindName('replaceAll').Content
            Assert-Regression ([HandyControl.Controls.InfoElement]::GetPlaceholder($searchInput) -ceq $Context.LanguageService.GetString('common_find') -and
                $replaceMenu.Header -ceq $Context.LanguageService.GetString('userScriptManager_replace') -and
                $window.FindName('matchCase').Content -ceq $Context.LanguageService.GetString('userScriptManager_matchCase') -and
                $window.FindName('replaceCurrent').ToolTip -ceq $Context.LanguageService.GetString('userScriptManager_replaceCurrent') -and
                $replaceAllLabel -ceq $Context.LanguageService.GetString('userScriptManager_replaceAll')) "search toolbar and menu localize in $locale"
            foreach ($controlName in $searchAutomationNames.Keys) {
                $control = $window.FindName($controlName)
                Assert-Regression ([Windows.Automation.AutomationProperties]::GetName($control) -ceq $searchAutomationNames[$controlName]) "automation name for $controlName stays fixed in $locale"
            }
            $editorMenu = Open-ManagerEditorContextMenu $Context $window
            $editorMenuItems = @($editorMenu.Items | Where-Object { $_ -is [Windows.Controls.MenuItem] })
            $editorMenuKeys = @('common_undo', 'common_redo', 'common_cut', 'common_copy', 'common_paste', 'common_selectAll')
            for ($index = 0; $index -lt $editorMenuKeys.Count; $index++) {
                Assert-Regression ($editorMenuItems[$index].Header -eq $Context.LanguageService.GetString($editorMenuKeys[$index])) "editor context menu localizes $($editorMenuKeys[$index]) in $locale"
            }
            $editorMenu.IsOpen = $false
            $menu = Open-ManagerContextMenu $Context $window $previousSelection
            Invoke-ManagerDeleteWithConfirmation $Context $window $menu.Items[5] $false
        }
        $Context.LanguageService.SetCurrentLanguage('zh-CN')
        $model.SelectedScript = $item
        $item.Source = $source + "`r`n// https://example.invalid/icon.png`r`n" + ((1..60 | ForEach-Object { "const value$_ = 42; // readable code with a long comment " + ('x' * 100) }) -join "`r`n")
        Wait-Regression $Context { $editor.Text -ceq $item.Source } 'theme fixture source is displayed'
        Test-ManagerSearchAppearance $Context $window
        Test-ManagerEditorWheel $Context $window $editor
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
            $editor.TextArea.TextView.EnsureVisualLines()
            $linkElements = @($editor.TextArea.TextView.VisualLines | ForEach-Object { $_.Elements } |
                Where-Object { $_ -is [ICSharpCode.AvalonEdit.Rendering.VisualLineLinkText] })
            Assert-Regression ($linkElements.Count -ge 1 -and $editor.Options.EnableHyperlinks) "$appearance editor recognizes web links"
            Assert-Regression ((Get-ManagerColorContrast $editor.TextArea.TextView.LinkTextForegroundBrush.Color $editor.Background.Color) -ge 4.5) "$appearance link text has readable contrast"
            $themeKeywords[$appearance.ToString()] = $editor.SyntaxHighlighting.GetNamedColor('JavaScriptKeyWords').Foreground.GetColor($null)
            $newEditor = New-Object CefFlashBrowser.Views.Custom.ScriptEditor
            Assert-Regression ($newEditor.Background.Color -eq $editor.Background.Color -and
                $newEditor.TextArea.TextView.LinkTextForegroundBrush.Color -eq $editor.TextArea.TextView.LinkTextForegroundBrush.Color -and
                $newEditor.SyntaxHighlighting.GetNamedColor('JavaScriptKeyWords').Foreground.GetColor($null) -eq $themeKeywords[$appearance.ToString()]) "new editors initialize with the current $appearance theme"
            $editorMenu = Open-ManagerEditorContextMenu $Context $window
            Assert-Regression ((Get-ManagerColorContrast $editorMenu.Items[0].Foreground.Color $editorMenu.Background.Color) -ge 4.5 -and
                (Get-ManagerColorContrast $editorMenu.Background.Color $editor.Background.Color) -lt 2) "editor context menu follows the $appearance theme with readable text"
            $editorMenu.IsOpen = $false
            $menu = Open-ManagerContextMenu $Context $window $item
            Invoke-ManagerDeleteWithConfirmation $Context $window $menu.Items[5] $false
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
        Test-WebUserScriptInstallation $Context $browserWindow $window $fixture
        Test-ManagerBatchUpdate $Context $window
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
