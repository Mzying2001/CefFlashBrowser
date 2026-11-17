using CefFlashBrowser.ViewModels;
using System;
using System.Windows;

namespace CefFlashBrowser.Views
{
    public partial class UserScriptManager : Window
    {
        public UserScriptManager()
        {
            InitializeComponent();
            Loaded += async (sender, e) => await ((UserScriptManagerViewModel)DataContext).OpenAsync();
        }

        protected override void OnClosed(EventArgs e)
        {
            ((UserScriptManagerViewModel)DataContext).Close();
            base.OnClosed(e);
        }
    }
}
