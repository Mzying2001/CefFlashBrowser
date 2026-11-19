function Start-ListReorderDrag($Behavior, $Item) {
    # Enter the same drag session as a mouse gesture without taking over the desktop cursor.
    $method = $Behavior.GetType().GetMethod('BeginDrag', [Reflection.BindingFlags]'Instance,NonPublic')
    return $method.Invoke($Behavior, [object[]]@($Item.PSObject.BaseObject))
}

function Invoke-ListReorderDragEvent($List, $Data, [Windows.Point]$Point, [Windows.RoutedEvent]$Event) {
    $constructor = [Windows.DragEventArgs].GetConstructors([Reflection.BindingFlags]'Instance,NonPublic')[0]
    $eventArgs = $constructor.Invoke([object[]]@($Data.PSObject.BaseObject, [Windows.DragDropKeyStates]::LeftMouseButton,
        [Windows.DragDropEffects]::Move, $List.PSObject.BaseObject, $Point))
    $eventArgs.RoutedEvent = $Event
    $List.RaiseEvent($eventArgs)
    return $eventArgs
}

function Invoke-ListReorderDragQuery($List, [bool]$EscapePressed) {
    $constructor = [Windows.QueryContinueDragEventArgs].GetConstructors([Reflection.BindingFlags]'Instance,NonPublic')[0]
    $query = $constructor.Invoke([object[]]@($EscapePressed, [Windows.DragDropKeyStates]::LeftMouseButton))
    $query.RoutedEvent = [Windows.DragDrop]::PreviewQueryContinueDragEvent
    $List.RaiseEvent($query)
    return $query.Action
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
        $preview = Invoke-ListReorderDragEvent $list $data $point $over
        $result = Invoke-ListReorderDragEvent $list $data $point $drop
        Assert-Regression ($preview.Handled -and $preview.Effects -eq [Windows.DragDropEffects]::None -and
            $result.Effects -eq [Windows.DragDropEffects]::None -and $state.Requests.Count -eq 0 -and
            @(Get-ListReorderAdorners $list).Count -eq 0) 'CanExecute rejects both the preview and drop'
        Invoke-ViewCallback $behavior 'EndDrag' @()

        $state.Accept = $true
        $data = Start-ListReorderDrag $behavior 'gamma'
        $peerData = Start-ListReorderDrag $peerBehavior 'gamma'
        $foreign = Invoke-ListReorderDragEvent $peer $data $peerPoint $drop
        Assert-Regression (!$foreign.Handled -and $state.Requests.Count -eq 0) 'a peer with the same items ignores the source list payload'
        $peerPreview = Invoke-ListReorderDragEvent $peer $peerData $peerPoint $over
        Assert-Regression ($peerPreview.Effects -eq [Windows.DragDropEffects]::Move -and
            @(Get-ListReorderAdorners $peer).Count -eq 1) 'the same behavior previews reordering in the ListView item viewport'
        Invoke-ViewCallback $peerBehavior 'EndDrag' @()

        $preview = Invoke-ListReorderDragEvent $list $data $point $over
        Assert-Regression ($preview.Effects -eq [Windows.DragDropEffects]::Move -and
            @(Get-ListReorderAdorners $list).Count -eq 1 -and $state.Requests.Count -eq 0) 'enabling the behavior shows the insertion line without executing the command'
        $behavior.IsDragEnabled = $false
        Assert-Regression (@(Get-ListReorderAdorners $list).Count -eq 0 -and
            (Invoke-ListReorderDragQuery $list $false) -eq [Windows.DragAction]::Cancel) 'disabling during a drag clears the line and cancels the gesture'
        $behavior.IsDragEnabled = $true
        [void](Invoke-ListReorderDragEvent $list $data $point $drop)
        Assert-Regression ($state.Requests.Count -eq 0) 'reenabling the behavior does not revive the canceled gesture'
        Invoke-ViewCallback $behavior 'EndDrag' @()

        $data = Start-ListReorderDrag $behavior 'gamma'
        $result = Invoke-ListReorderDragEvent $list $data $point $drop
        [void](Invoke-ListReorderDragEvent $list $data $point $drop)
        $request = $state.Requests[0]
        Assert-Regression ($result.Effects -eq [Windows.DragDropEffects]::Move -and $state.Requests.Count -eq 1 -and
            $request.Item -ceq 'gamma' -and $request.TargetItem -ceq 'alpha' -and !$request.InsertAfter -and
            ($list.Items -join ',') -ceq 'alpha,beta,gamma') 'a drop sends one relative move request and leaves collection changes to the command'
        Invoke-ViewCallback $behavior 'EndDrag' @()

        $data = Start-ListReorderDrag $behavior 'gamma'
        [void](Invoke-ListReorderDragEvent $list $data $point $over)
        $viewport = @(Find-FaviconElements $list ([Windows.Controls.ScrollContentPresenter]))[0]
        $layer = [Windows.Documents.AdornerLayer]::GetAdornerLayer($viewport)
        [void]$panel.Children.Remove($list)
        Wait-Regression $Context { !$list.IsLoaded } 'standalone reorder list unloads'
        Assert-Regression ($null -eq $layer.GetAdorners($viewport) -and
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
