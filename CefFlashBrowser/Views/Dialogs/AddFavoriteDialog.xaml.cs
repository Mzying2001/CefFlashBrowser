using CefFlashBrowser.Models.StaticData;
using CefFlashBrowser.ViewModels.DialogViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace CefFlashBrowser.Views.Dialogs
{
    /// <summary>
    /// AddFavoriteDialog.xaml 的交互逻辑
    /// </summary>
    public partial class AddFavoriteDialog : Window
    {
        AddFavoriteDialogViewModel VModel => (AddFavoriteDialogViewModel)DataContext;

        public AddFavoriteDialog()
        {
            InitializeComponent();
            VModel.CloseWindow = Close;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            NameTextBox.Focus();
            NameTextBox.SelectAll();
        }

        public static bool Show(string name, string url)
        {
            var dialog = new AddFavoriteDialog();
            var vmodel = dialog.VModel;

            vmodel.Name = name;
            vmodel.Url = url;

            dialog.ShowDialog();
            return vmodel.Result;
        }
    }
}
