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
    /// JsPromptDialog.xaml 的交互逻辑
    /// </summary>
    public partial class JsPromptDialog : Window
    {
        JsPromptDialogViewModel VModel => (JsPromptDialogViewModel)DataContext;

        public JsPromptDialog()
        {
            InitializeComponent();
            VModel.CloseWindow = Close;
        }

        public static (bool, string) Show(string message, string title = "", string defaulePromptText = "")
        {
            var dialog = new JsPromptDialog();
            var vmodel = dialog.VModel;

            vmodel.Message = message;
            vmodel.Title = title;
            vmodel.PromptText = defaulePromptText;
            dialog.ShowDialog();

            return vmodel.DialogResult;
        }
    }
}
