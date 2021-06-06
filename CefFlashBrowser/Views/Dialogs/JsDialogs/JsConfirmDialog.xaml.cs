using CefFlashBrowser.ViewModels.DialogViewModels.JsDialogViewModels;
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

namespace CefFlashBrowser.Views.Dialogs.JsDialogs
{
    /// <summary>
    /// JsConfirmDialog.xaml 的交互逻辑
    /// </summary>
    public partial class JsConfirmDialog : Window
    {
        JsConfirmDialogViewModel VModel => (JsConfirmDialogViewModel)DataContext;

        public JsConfirmDialog()
        {
            InitializeComponent();
            VModel.CloseWindow = Close;
        }

        public static bool Show(string message, string title = "")
        {
            var dialog = new JsConfirmDialog();
            var vmodel = dialog.VModel;

            vmodel.Message = message;
            vmodel.Title = title;
            dialog.ShowDialog();

            return vmodel.DialogResult;
        }
    }
}
