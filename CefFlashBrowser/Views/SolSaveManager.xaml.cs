using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using CefFlashBrowser.ViewModels;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CefFlashBrowser.Views
{
    /// <summary>
    /// SolSaveManager.xaml 的交互逻辑
    /// </summary>
    public partial class SolSaveManager : Window
    {
        private Point _dragStartPoint;

        public SolSaveManagerViewModel ViewModel
        {
            get => DataContext as SolSaveManagerViewModel;
        }

        public SolSaveManager()
        {
            InitializeComponent();
            WindowSizeInfo.Apply(GlobalData.Settings.SolSaveManagerSizeInfo, this);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            if (!e.Cancel)
            {
                GlobalData.Settings.SolSaveManagerSizeInfo = WindowSizeInfo.GetSizeInfo(this);
            }
        }

        private void ListViewItemMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListViewItem item)
            {
                Dispatcher.InvokeAsync(() =>
                    ViewModel?.CurrentWorkspace?.EditSolCommand.Execute(item.DataContext));
            }
        }

        private void ListViewItemPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void ListViewItemMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed &&
                sender is ListViewItem item && item.DataContext is SolFileInfo solFileInfo)
            {
                Point pos = e.GetPosition(null);
                Vector diff = _dragStartPoint - pos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    // Support dragging the file to outside of the application
                    string[] files = new string[] { solFileInfo.FilePath };
                    DataObject dataObject = new DataObject(DataFormats.FileDrop, files);
                    DragDrop.DoDragDrop(item, dataObject, DragDropEffects.Copy);
                }
            }
        }

        private void ListViewItemPreviewDragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void ListViewItemDrop(object sender, DragEventArgs e)
        {
            var droppedFiles = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (droppedFiles == null ||
                droppedFiles.Length != 1)
            {
                return;
            }

            if (sender is ListViewItem item &&
                item.DataContext is SolFileInfo solFile)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    Activate();
                    ViewModel?.CurrentWorkspace?.ImportSol(solFile, droppedFiles[0]);
                });
            }
        }
    }
}
