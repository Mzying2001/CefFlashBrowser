using CefFlashBrowser.Services.Downloads;
using CefFlashBrowser.ViewModels;
using SimpleMvvm.Command;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CefFlashBrowser.Views
{
    public partial class DownloadManagerWindow : Window
    {
        private readonly DownloadManagerViewModel _model;

        public DownloadManagerWindow()
        {
            InitializeComponent();

            _model = new DownloadManagerViewModel(DownloadManagerService.Current);
            DataContext = _model;

            InputBindings.Add(new KeyBinding(_model.NewCommand, new KeyGesture(Key.N, ModifierKeys.Control)));
            downloadList.InputBindings.Add(new KeyBinding(_model.RemoveCommand, new KeyGesture(Key.Delete)));
            InputBindings.Add(new KeyBinding(new DelegateCommand(Close), new KeyGesture(Key.W, ModifierKeys.Control)));

            Activated += (_, __) => DownloadManagerService.Current.Refresh();
            Closed += (_, __) => _model.Dispose();
        }

        private void CloseWindow(object sender, RoutedEventArgs e) => Close();

        private void SelectDownloadRow(object sender, MouseButtonEventArgs e)
        {
            var row = (ListViewItem)sender;
            row.IsSelected = true;
            row.Focus();
        }

        private void OpenSelected(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            var source = e.OriginalSource as DependencyObject;
            var row = ItemsControl.ContainerFromElement(downloadList, source) as ListViewItem;
            if (row == null)
                return;

            for (var element = source; element != null && element != row;)
            {
                if (element is FrameworkElement cell && cell.Name == "downloadRowActions")
                    return;

                element = element is Visual ? VisualTreeHelper.GetParent(element)
                    : element is FrameworkContentElement content ? content.Parent : LogicalTreeHelper.GetParent(element);
            }

            ICommand open = _model.OpenCommand;
            if (open.CanExecute(null))
                open.Execute(null);
        }
    }
}
