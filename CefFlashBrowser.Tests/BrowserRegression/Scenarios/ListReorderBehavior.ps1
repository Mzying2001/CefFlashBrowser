function Start-ListReorderDrag($Behavior, $Item) {
    # Enter the same drag session as a mouse gesture without taking over the desktop cursor.
    $method = $Behavior.GetType().GetMethod('BeginDrag', [Reflection.BindingFlags]'Instance,NonPublic')
    return $method.Invoke($Behavior, [object[]]@($Item.PSObject.BaseObject))
}

function Invoke-ListReorderDragEvent($List, $Data, [Windows.Point]$Point, [Windows.RoutedEvent]$Event) {
    Invoke-RegressionDragEvent $List $Data $Point $Event ([Windows.DragDropEffects]::Move)
}

function Invoke-RegressionDragEvent($Target, $Data, [Windows.Point]$Point, [Windows.RoutedEvent]$Event, [Windows.DragDropEffects]$AllowedEffects) {
    $constructor = [Windows.DragEventArgs].GetConstructors([Reflection.BindingFlags]'Instance,NonPublic')[0]
    $eventArgs = $constructor.Invoke([object[]]@($Data.PSObject.BaseObject, [Windows.DragDropKeyStates]::LeftMouseButton,
        $AllowedEffects, $Target.PSObject.BaseObject, $Point))
    $eventArgs.RoutedEvent = $Event
    $Target.RaiseEvent($eventArgs)
    return $eventArgs
}

function Invoke-ListReorderDragQuery($List, [bool]$EscapePressed) {
    $constructor = [Windows.QueryContinueDragEventArgs].GetConstructors([Reflection.BindingFlags]'Instance,NonPublic')[0]
    $query = $constructor.Invoke([object[]]@($EscapePressed, [Windows.DragDropKeyStates]::LeftMouseButton))
    $query.RoutedEvent = [Windows.DragDrop]::PreviewQueryContinueDragEvent
    $List.RaiseEvent($query)
    return $query.Action
}

function Show-ListReorderPreview($Context, $Behavior, [Windows.Point]$Point) {
    Invoke-ViewCallback $Behavior 'ShowDragPreview' @($Point)
    $field = $Behavior.GetType().GetField('_dragPreview', [Reflection.BindingFlags]'Instance,NonPublic')
    $popup = $field.GetValue($Behavior)
    Assert-Regression ($null -ne $popup) 'a realized row creates a drag preview'
    Wait-Regression $Context { $popup.IsOpen -and [Windows.PresentationSource]::FromVisual($popup.Child) } 'drag preview opens its popup window'
    return $popup
}

function Assert-ListReorderPreview($Popup, $Row) {
    $image = $Popup.Child.Child
    $snapshot = $image.Source
    $dpi = [Windows.Media.VisualTreeHelper]::GetDpi($Row)
    $pixels = [byte[]]::new($snapshot.PixelWidth * $snapshot.PixelHeight * 4)
    $snapshot.CopyPixels($pixels, $snapshot.PixelWidth * 4, 0)
    $hasContent = $false
    for ($i = 3; $i -lt $pixels.Length; $i += 4) {
        if ($pixels[$i] -ne 0) { $hasContent = $true; break }
    }
    Assert-Regression ($hasContent -and $snapshot.IsFrozen -and $image.Width -eq $Row.ActualWidth -and
        $image.Height -eq $Row.ActualHeight -and $snapshot.PixelWidth -eq [Math]::Ceiling($Row.ActualWidth * $dpi.DpiScaleX) -and
        $snapshot.PixelHeight -eq [Math]::Ceiling($Row.ActualHeight * $dpi.DpiScaleY)) 'the preview displays the realized row at its original size and DPI'

    $source = [Windows.PresentationSource]::FromVisual($Popup.Child)
    $style = [CefFlashBrowser.Utils.HwndHelper]::GetWindowExStyle($source.Handle)
    Assert-Regression (!$Popup.IsHitTestVisible -and !$Popup.Focusable -and
        ($style -band [CefFlashBrowser.Utils.Win32]::WS_EX_TRANSPARENT) -and
        ($style -band [CefFlashBrowser.Utils.Win32]::WS_EX_NOACTIVATE)) 'the preview window lets native drag hit testing reach the list'
}

function Invoke-ListReorderGiveFeedback($List, [Windows.DragDropEffects]$Effects) {
    $constructor = [Windows.GiveFeedbackEventArgs].GetConstructors([Reflection.BindingFlags]'Instance,NonPublic')[0]
    $feedback = $constructor.Invoke([object[]]@($Effects, $true))
    $feedback.RoutedEvent = [Windows.DragDrop]::PreviewGiveFeedbackEvent
    $List.RaiseEvent($feedback)
    return $feedback
}

function Get-ListReorderAdorners($List) {
    foreach ($viewport in @(Find-FaviconElements $List ([Windows.Controls.ScrollContentPresenter]))) {
        $layer = [Windows.Documents.AdornerLayer]::GetAdornerLayer($viewport)
        if ($null -ne $layer) {
            $adorners = $layer.GetAdorners($viewport)
            if ($null -ne $adorners) { $adorners }
        }
    }
}

function Test-ListReorderBehavior($Context) {
    $window = New-Object Windows.Window
    $window.Width = 320
    $window.Height = 320
    $window.Left = -10000
    $window.Top = -10000
    $window.ShowActivated = $false
    $window.ShowInTaskbar = $false
    $panel = New-Object Windows.Controls.StackPanel
    $window.Content = $panel

    $list = New-Object Windows.Controls.ListBox
    $peer = New-Object Windows.Controls.ListView
    $view = New-Object Windows.Controls.GridView
    $column = New-Object Windows.Controls.GridViewColumn
    $column.Width = 200
    $column.DisplayMemberBinding = New-Object Windows.Data.Binding
    $view.Columns.Add($column)
    $peer.View = $view
    $items = @('alpha', 'beta', 'gamma')
    foreach ($control in @($list, $peer)) {
        $control.AllowDrop = $true
        $control.Height = 125
        $control.ItemsSource = $items
        [void]$panel.Children.Add($control)
    }

    $state = @{ Accept = $false; Requests = New-Object 'Collections.Generic.List[object]' }
    $execute = [Action[object]]({ param($request) $state.Requests.Add($request) }.GetNewClosure())
    $command = [SimpleMvvm.Command.DelegateCommand[object]]::new($execute)
    $command.CanExecuteFunc = [Func[object, bool]]({ param($request) return $state.Accept }.GetNewClosure())
    $behavior = New-Object CefFlashBrowser.Views.Behaviors.ListReorderBehavior
    $peerBehavior = New-Object CefFlashBrowser.Views.Behaviors.ListReorderBehavior
    $behavior.ReorderCommand = $command
    $peerBehavior.ReorderCommand = $command
    $peerBehavior.IsDragEnabled = $true
    $behaviors = [Microsoft.Xaml.Behaviors.Interaction]::GetBehaviors($list)
    $behaviors.Add($behavior)
    [Microsoft.Xaml.Behaviors.Interaction]::GetBehaviors($peer).Add($peerBehavior)
    $over = [Windows.DragDrop]::PreviewDragOverEvent
    $drop = [Windows.DragDrop]::PreviewDropEvent

    try {
        $window.Show()
        Wait-Regression $Context { $list.IsLoaded -and $peer.IsLoaded } 'standalone reorder lists load'
        $window.UpdateLayout()
        $row = $list.ItemContainerGenerator.ContainerFromItem('alpha')
        $peerRow = $peer.ItemContainerGenerator.ContainerFromItem('alpha')
        $point = $row.TranslatePoint([Windows.Point]::new(12, $row.ActualHeight / 4), $list)
        $peerPoint = $peerRow.TranslatePoint([Windows.Point]::new(12, $peerRow.ActualHeight / 4), $peer)
        Assert-Regression (!$behavior.IsDragEnabled -and $null -eq (Start-ListReorderDrag $behavior 'gamma')) 'list reordering is disabled by default'

        $behavior.IsDragEnabled = $true
        $data = Start-ListReorderDrag $behavior 'gamma'
        $popup = Show-ListReorderPreview $Context $behavior $point
        Assert-ListReorderPreview $popup ($list.ItemContainerGenerator.ContainerFromItem('gamma'))
        $preview = Invoke-ListReorderDragEvent $list $data $point $over
        $feedback = Invoke-ListReorderGiveFeedback $list $preview.Effects
        Assert-Regression ($popup.IsOpen -and $feedback.UseDefaultCursors -and !$feedback.Handled) 'an invalid target retains the row preview and system cursor feedback'
        $result = Invoke-ListReorderDragEvent $list $data $point $drop
        Assert-Regression ($preview.Handled -and $preview.Effects -eq [Windows.DragDropEffects]::None -and
            $result.Effects -eq [Windows.DragDropEffects]::None -and $state.Requests.Count -eq 0 -and
            @(Get-ListReorderAdorners $list).Count -eq 0) 'CanExecute rejects both the preview and drop'
        Invoke-ViewCallback $behavior 'EndDrag' @()
        Assert-Regression (!$popup.IsOpen -and $null -eq $popup.Child) 'an invalid drop releases the popup and its image when the drag ends'

        $state.Accept = $true
        $data = Start-ListReorderDrag $behavior 'gamma'
        $peerData = Start-ListReorderDrag $peerBehavior 'gamma'
        $peerPopup = Show-ListReorderPreview $Context $peerBehavior $peerPoint
        Assert-ListReorderPreview $peerPopup ($peer.ItemContainerGenerator.ContainerFromItem('gamma'))
        $foreign = Invoke-ListReorderDragEvent $peer $data $peerPoint $drop
        Assert-Regression (!$foreign.Handled -and $state.Requests.Count -eq 0) 'a peer with the same items ignores the source list payload'
        $peerPreview = Invoke-ListReorderDragEvent $peer $peerData $peerPoint $over
        Assert-Regression ($peerPreview.Effects -eq [Windows.DragDropEffects]::Move -and
            @(Get-ListReorderAdorners $peer).Count -eq 1) 'the same behavior previews reordering in the ListView item viewport'
        Invoke-ViewCallback $peerBehavior 'EndDrag' @()

        $popup = Show-ListReorderPreview $Context $behavior $point
        $preview = Invoke-ListReorderDragEvent $list $data $point $over
        $feedback = Invoke-ListReorderGiveFeedback $list $preview.Effects
        Assert-Regression ($preview.Effects -eq [Windows.DragDropEffects]::Move -and
            $feedback.Handled -and !$feedback.UseDefaultCursors -and $popup.IsOpen -and
            @(Get-ListReorderAdorners $list).Count -eq 1 -and $state.Requests.Count -eq 0) 'a valid target shows the preview and insertion line without the default drag cursor'
        [void](Invoke-ListReorderDragEvent $list $data $point ([Windows.DragDrop]::PreviewDragLeaveEvent))
        Assert-Regression ($popup.IsOpen -and @(Get-ListReorderAdorners $list).Count -eq 0) 'leaving the list clears the insertion line while keeping the floating preview'
        [void](Invoke-ListReorderDragEvent $list $data $point $over)
        $behavior.IsDragEnabled = $false
        Assert-Regression (!$popup.IsOpen -and $null -eq $popup.Child -and @(Get-ListReorderAdorners $list).Count -eq 0 -and
            (Invoke-ListReorderDragQuery $list $false) -eq [Windows.DragAction]::Cancel) 'disabling during a drag clears the preview and line and cancels the gesture'
        $behavior.IsDragEnabled = $true
        [void](Invoke-ListReorderDragEvent $list $data $point $drop)
        Assert-Regression ($state.Requests.Count -eq 0) 'reenabling the behavior does not revive the canceled gesture'
        Invoke-ViewCallback $behavior 'EndDrag' @()

        $data = Start-ListReorderDrag $behavior 'gamma'
        $popup = Show-ListReorderPreview $Context $behavior $point
        Assert-Regression ((Invoke-ListReorderDragQuery $list $true) -eq [Windows.DragAction]::Cancel -and
            !$popup.IsOpen -and $null -eq $popup.Child) 'Escape cancels the drag and releases its preview'
        Invoke-ViewCallback $behavior 'EndDrag' @()

        $data = Start-ListReorderDrag $behavior 'gamma'
        $popup = Show-ListReorderPreview $Context $behavior $point
        $result = Invoke-ListReorderDragEvent $list $data $point $drop
        [void](Invoke-ListReorderDragEvent $list $data $point $drop)
        $request = $state.Requests[0]
        Assert-Regression ($result.Effects -eq [Windows.DragDropEffects]::Move -and $state.Requests.Count -eq 1 -and
            $request.Item -ceq 'gamma' -and $request.TargetItem -ceq 'alpha' -and !$request.InsertAfter -and
            ($list.Items -join ',') -ceq 'alpha,beta,gamma' -and !$popup.IsOpen -and $null -eq $popup.Child) 'a drop releases the preview and sends one relative move request'
        Invoke-ViewCallback $behavior 'EndDrag' @()

        $data = Start-ListReorderDrag $behavior 'gamma'
        $popup = Show-ListReorderPreview $Context $behavior $point
        [void](Invoke-ListReorderDragEvent $list $data $point $over)
        $viewport = @(Find-FaviconElements $list ([Windows.Controls.ScrollContentPresenter]))[0]
        $layer = [Windows.Documents.AdornerLayer]::GetAdornerLayer($viewport)
        [void]$panel.Children.Remove($list)
        Wait-Regression $Context { !$list.IsLoaded } 'standalone reorder list unloads'
        Assert-Regression (!$popup.IsOpen -and $null -eq $popup.Child -and $null -eq $layer.GetAdorners($viewport) -and
            (Invoke-ListReorderDragQuery $list $false) -eq [Windows.DragAction]::Cancel) 'unloading the list clears drag feedback and cancels the gesture'
        Invoke-ViewCallback $behavior 'EndDrag' @()

        [void]$panel.Children.Add($list)
        Wait-Regression $Context { $list.IsLoaded } 'standalone reorder list loads again'
        $window.UpdateLayout()
        $data = Start-ListReorderDrag $behavior 'gamma'
        [void](Invoke-ListReorderDragEvent $list $data $point $over)
        [void]$behaviors.Remove($behavior)
        $result = Invoke-ListReorderDragEvent $list $data $point $drop
        Assert-Regression (@(Get-ListReorderAdorners $list).Count -eq 0 -and !$result.Handled -and
            (Invoke-ListReorderDragQuery $list $false) -eq [Windows.DragAction]::Cancel -and $list.AllowDrop) 'detaching cancels the gesture and leaves the host drop setting intact'
    } finally {
        Invoke-ViewCallback $behavior 'EndDrag' @()
        Invoke-ViewCallback $peerBehavior 'EndDrag' @()
        $window.Close()
    }
}
