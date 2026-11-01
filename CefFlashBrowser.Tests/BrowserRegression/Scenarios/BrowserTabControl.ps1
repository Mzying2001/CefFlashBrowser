function Set-ControlDragItem($Tabs, $Item) {
    $Tabs.GetType().GetField('_draggedItem', [Reflection.BindingFlags]'Instance,NonPublic').SetValue($Tabs, $Item)
}

function Move-ControlDragTo($Tabs, $Target, [bool]$Right) {
    $header = $Tabs.ItemContainerGenerator.ContainerFromItem($Target)
    $x = if ($Right) { $header.ActualWidth - 1 } else { 1 }
    $point = $header.TranslatePoint([System.Windows.Point]::new($x, 10), $Tabs)
    Invoke-ViewCallback $Tabs 'UpdateTabDrag' @($point, $false)
}

function Test-BrowserTabControl($Context) {
    $window = New-Object System.Windows.Window
    $window.Width = 950
    $window.Height = 150
    $window.Left = -10000
    $window.Top = -10000
    $window.ShowActivated = $false
    $window.ShowInTaskbar = $false
    $hostGrid = New-Object System.Windows.Controls.Grid
    $tabs = New-Object CefFlashBrowser.Views.Custom.BrowserTab
    $tabs.VerticalAlignment = 'Top'
    $tabs.Tag = 'consumer state'
    $items = New-Object 'Collections.ObjectModel.ObservableCollection[object]'
    foreach ($item in @('alpha', 'beta', 'gamma')) { $items.Add($item) }
    $source = New-Object 'Collections.ObjectModel.ReadOnlyObservableCollection[object]' -ArgumentList (,$items)
    $tabs.ItemsSource = $source
    $tabs.ItemTemplate = [System.Windows.Markup.XamlReader]::Parse(@'
<DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
              xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <DockPanel>
        <Button x:Name="itemButton" DockPanel.Dock="Right" Content="x"/>
        <TextBlock Text="{Binding}"/>
    </DockPanel>
</DataTemplate>
'@)
    [void]$hostGrid.Children.Add($tabs)
    $window.Content = $hostGrid
    $state = @{ Requests = New-Object 'Collections.Generic.List[object]'; Move = $false; Sender = $null }
    $handler = [CefFlashBrowser.Views.Custom.MoveItemEventHandler]({
        param($sender, $eventArgs)
        $state.Sender = $sender
        $state.Requests.Add($eventArgs)
        if ($state.Move) {
            $items.Move($eventArgs.OldIndex, $eventArgs.NewIndex)
            $eventArgs.Handled = $true
        }
    }.GetNewClosure())

    try {
        $window.Show()
        Wait-Regression $Context { $tabs.IsLoaded } 'standalone BrowserTab loads without a browser view model'
        $window.UpdateLayout()
        Assert-Regression (!$tabs.IsDragEnabled -and !$tabs.AllowDrop) 'tab dragging is disabled by default'
        Assert-Regression ($tabs.ActualItemWidth -eq 250 -and $null -ne $tabs.ItemContainerGenerator.ContainerFromItem('gamma')) 'standalone default template presents ordinary items'
        $window.Width = 400
        $window.UpdateLayout()
        Assert-Regression ($tabs.ActualItemWidth -gt 80 -and $tabs.ActualItemWidth -lt 250) 'standalone tabs shrink with the available width'
        foreach ($item in 1..4) { $items.Add("extra $item") }
        $window.UpdateLayout()
        $scroll = $tabs.Template.FindName('PART_ScrollViewer', $tabs)
        Assert-Regression ($tabs.ActualItemWidth -eq 80 -and $scroll.ScrollableWidth -gt 0) 'standalone tabs overflow at the minimum width'
        $tabs.SelectedItem = 'extra 4'
        Wait-Regression $Context { $scroll.HorizontalOffset -gt 0 } 'standalone selection scrolls into view'
        $offset = $scroll.HorizontalOffset
        $wheel = [System.Windows.Input.MouseWheelEventArgs]::new([System.Windows.Input.Mouse]::PrimaryDevice, 0, 120)
        $wheel.RoutedEvent = [System.Windows.Input.Mouse]::PreviewMouseWheelEvent
        $tabs.RaiseEvent($wheel)
        $window.UpdateLayout()
        Assert-Regression ($wheel.Handled -and $scroll.HorizontalOffset -lt $offset) 'standalone mouse wheel scrolls horizontally'
        foreach ($item in 1..4) { [void]$items.Remove("extra $item") }
        $window.Width = 950
        $window.UpdateLayout()
        Assert-Regression ($tabs.ActualItemWidth -eq 250 -and $scroll.ScrollableWidth -lt 1) 'standalone tabs expand after resizing and removing items'

        Set-ControlDragItem $tabs 'alpha'
        Move-ControlDragTo $tabs 'gamma' $true
        Assert-Regression ($items[0] -eq 'alpha') 'disabled dragging does not reorder items'
        $tabs.IsDragEnabled = $true
        Assert-Regression $tabs.AllowDrop 'drag dependency property enables drop handling'
        Move-ControlDragTo $tabs 'gamma' $true
        Assert-Regression ($items[0] -eq 'alpha') 'unhandled move requests leave a read-only source unchanged'
        $hostGrid.AddHandler([CefFlashBrowser.Views.Custom.BrowserTab]::MoveItemEvent, $handler)
        Move-ControlDragTo $tabs 'gamma' $true
        $request = $state.Requests[0]
        Assert-Regression ($state.Requests.Count -eq 1 -and $request.Item -eq 'alpha' -and
            $request.OldIndex -eq 0 -and $request.NewIndex -eq 2 -and $request.Source -eq $tabs -and
            $state.Sender -eq $hostGrid -and $items[0] -eq 'alpha') 'move request bubbles with its item and final indices without mutating the source'
        $state.Move = $true
        $tabs.SelectedItem = 'alpha'
        Move-ControlDragTo $tabs 'gamma' $true
        $window.UpdateLayout()
        Assert-Regression ($items[2] -eq 'alpha' -and $tabs.SelectedItem -eq 'alpha' -and $state.Requests[1].Handled) 'consumer synchronously handles a move and retains selection'
        Move-ControlDragTo $tabs 'beta' $false
        $window.UpdateLayout()
        Assert-Regression ($items[0] -eq 'alpha' -and $state.Requests[2].OldIndex -eq 2 -and $state.Requests[2].NewIndex -eq 0) 'continuous drag requests use the updated collection order'
        $count = $state.Requests.Count
        Invoke-ViewCallback $tabs 'UpdateTabDrag' @([System.Windows.Point]::new(0, -10), $false)
        Assert-Regression ($state.Requests.Count -eq $count) 'dragging outside the viewport emits no move request'

        # The drag payload belongs to one control even when the peer contains the same items.
        $peer = New-Object CefFlashBrowser.Views.Custom.BrowserTab
        $peer.ItemsSource = $source
        $peer.IsDragEnabled = $true
        [void]$hostGrid.Children.Add($peer)
        $window.UpdateLayout()
        Set-ControlDragItem $peer 'alpha'
        $flags = [Reflection.BindingFlags]'Instance,NonPublic'
        $tabs.GetType().GetField('_isDragging', $flags).SetValue($tabs, $true)
        $peer.GetType().GetField('_isDragging', $flags).SetValue($peer, $true)
        $payloadType = $tabs.GetType().GetNestedType('TabDragData', [Reflection.BindingFlags]::NonPublic)
        $dragItem = $tabs.GetType().GetField('_draggedItem', $flags).GetValue($tabs)
        $payload = $payloadType.GetConstructors()[0].Invoke([object[]]@($tabs.PSObject.BaseObject, $dragItem))
        $data = [System.Windows.DataObject]::new($payloadType, $payload)
        $constructor = [System.Windows.DragEventArgs].GetConstructors($flags)[0]
        $drag = $constructor.Invoke([object[]]@($data.PSObject.BaseObject, [System.Windows.DragDropKeyStates]::LeftMouseButton,
            [System.Windows.DragDropEffects]::Move, $tabs.PSObject.BaseObject, [System.Windows.Point]::new(10, 10)))
        $isDrag = $tabs.GetType().GetMethod('IsTabDrag', $flags)
        Assert-Regression ($isDrag.Invoke($tabs, @($drag)) -and !$isDrag.Invoke($peer, @($drag))) 'drag payloads cannot move items in another BrowserTab'
        $tabs.GetType().GetField('_isDragging', $flags).SetValue($tabs, $false)
        $peer.GetType().GetField('_isDragging', $flags).SetValue($peer, $false)
        [void]$hostGrid.Children.Remove($peer)

        $header = $tabs.ItemContainerGenerator.ContainerFromItem('alpha')
        $button = Find-TabElement $header 'itemButton'
        $down = [System.Windows.Input.MouseButtonEventArgs]::new([System.Windows.Input.Mouse]::PrimaryDevice, 0, [System.Windows.Input.MouseButton]::Left)
        $down.RoutedEvent = [System.Windows.Input.Mouse]::PreviewMouseDownEvent
        $button.RaiseEvent($down)
        $draggedField = $tabs.GetType().GetField('_draggedItem', $flags)
        Assert-Regression ($null -eq $draggedField.GetValue($tabs)) 'item buttons do not start tab dragging'
        $down = [System.Windows.Input.MouseButtonEventArgs]::new([System.Windows.Input.Mouse]::PrimaryDevice, 0, [System.Windows.Input.MouseButton]::Left)
        $down.RoutedEvent = [System.Windows.Input.Mouse]::PreviewMouseDownEvent
        $header.DataContext = 'unrelated presentation data'
        $header.RaiseEvent($down)
        Assert-Regression ($draggedField.GetValue($tabs) -eq 'alpha') 'dragging resolves items through their containers rather than DataContext'
        $header.ClearValue([System.Windows.FrameworkElement]::DataContextProperty)

        foreach ($reason in @('switch', 'disabled', 'hidden', 'removed', 'template', 'unloaded')) {
            [void]$tabs.ApplyTemplate()
            $window.UpdateLayout()
            Set-ControlDragItem $tabs 'alpha'
            $tabs.GetType().GetField('_isDragging', $flags).SetValue($tabs, $true)
            $timer = New-Object System.Windows.Threading.DispatcherTimer
            $timer.Interval = [TimeSpan]::FromSeconds(10)
            $timer.Start()
            $tabs.GetType().GetField('_dragScrollTimer', $flags).SetValue($tabs, $timer)
            Invoke-ViewCallback $tabs 'ShowTabDragPreview' @()
            $preview = $tabs.Template.FindName('PART_DragPreview', $tabs)
            $image = $tabs.Template.FindName('PART_DragPreviewImage', $tabs)
            Wait-Regression $Context { $preview.IsOpen } "$reason preview opens after template loading"
            Assert-Regression ($preview.IsOpen -and $null -ne $image.Source -and $tabs.DraggedItem -eq 'alpha') "$reason starts with an active preview"
            switch ($reason) {
                'switch' { $tabs.IsDragEnabled = $false }
                'disabled' { $tabs.IsEnabled = $false }
                'hidden' { $tabs.Visibility = 'Collapsed' }
                'removed' { [void]$items.Remove('alpha') }
                'template' {
                    $tabs.Template = [System.Windows.Markup.XamlReader]::Parse('<ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="ListBox"><Border/></ControlTemplate>')
                    $window.UpdateLayout()
                }
                'unloaded' {
                    [void]$hostGrid.Children.Remove($tabs)
                    Wait-Regression $Context { !$tabs.IsLoaded } 'standalone tab control unloads'
                }
            }
            Assert-Regression (!$preview.IsOpen -and $null -eq $image.Source -and !$timer.IsEnabled -and
                $null -eq $tabs.DraggedItem -and $null -eq $draggedField.GetValue($tabs) -and
                $tabs.Tag -eq 'consumer state') "$reason cleans up dragging without changing consumer state"
            $query = [System.Windows.QueryContinueDragEventArgs].GetConstructors($flags)[0].Invoke(
                [object[]]@($false, [System.Windows.DragDropKeyStates]::LeftMouseButton))
            $query.RoutedEvent = [System.Windows.DragDrop]::QueryContinueDragEvent
            $tabs.RaiseEvent($query)
            Assert-Regression ($query.Action -eq [System.Windows.DragAction]::Cancel) "$reason cancels the OLE drag session"
            $tabs.GetType().GetField('_isDragging', $flags).SetValue($tabs, $false)
            $tabs.IsDragEnabled = $true
            $tabs.IsEnabled = $true
            $tabs.Visibility = 'Visible'
            if (!$items.Contains('alpha')) { $items.Insert(0, 'alpha') }
            if ($reason -eq 'template') { $tabs.ClearValue([System.Windows.Controls.Control]::TemplateProperty) }
            if ($reason -eq 'unloaded') { [void]$hostGrid.Children.Add($tabs) }
            $window.UpdateLayout()
            Wait-Regression $Context { $tabs.IsLoaded } 'standalone tab control is ready again'
        }
    } finally {
        $hostGrid.RemoveHandler([CefFlashBrowser.Views.Custom.BrowserTab]::MoveItemEvent, $handler)
        $window.Close()
    }
}
