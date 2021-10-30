using CefFlashBrowser.Models;
using CefFlashBrowser.ViewModels.DialogViewModels;
using SimpleMvvm.Messaging;
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
    /// SelectLanguageDialog.xaml 的交互逻辑
    /// </summary>
    public partial class SelectLanguageDialog : Window
    {
        public SelectLanguageDialog()
        {
            InitializeComponent();

            string token = MessageTokens.CreateToken(MessageTokens.CLOSE_WINDOW, typeof(SelectLanguageDialogViewModel));
            Messenger.Global.Register(token, CloseWindow);
            Closing += (s, e) => Messenger.Global.Unregister(token, CloseWindow);
        }

        private void CloseWindow(object obj)
        {
            Close();
        }
    }
}
