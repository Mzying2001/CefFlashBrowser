using CefFlashBrowser.Services.Downloads;
using CefFlashBrowser.ViewModels;
using SimpleMvvm.Command;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
            ICommand open = _model.OpenCommand;

            if (e.ChangedButton == MouseButton.Left
                && ItemsControl.ContainerFromElement(downloadList, e.OriginalSource as DependencyObject) is ListViewItem
                && open.CanExecute(null))
            {
                open.Execute(null);
            }
        }
    }
}
