using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using System.Windows;

namespace CefFlashBrowser.Views
{
    /// <summary>
    /// SwfPlayerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SwfPlayerWindow : Window
    {


        public string FileName
        {
            get { return (string)GetValue(FileNameProperty); }
            set { SetValue(FileNameProperty, value); }
        }

        // Using a DependencyProperty as the backing store for FileName.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty FileNameProperty =
            DependencyProperty.Register("FileName", typeof(string), typeof(SwfPlayerWindow), new PropertyMetadata(string.Empty));


        public SwfPlayerWindow()
        {
            InitializeComponent();

            if (GlobalData.Settings.SwfWindowSizeInfo != null)
                GlobalData.Settings.SwfWindowSizeInfo.Apply(this);
        }

        public SwfPlayerWindow(string fileName) : this()
        {
            FileName = fileName;
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            browser.GetBrowser().CloseBrowser(true);
            window.WindowState = WindowState.Normal;
            GlobalData.Settings.SwfWindowSizeInfo = WindowSizeInfo.GetSizeInfo(this);
        }

        private void Browser_OnCreateNewWindow(object sender, FlashBrowser.Handlers.LifeSpanHandler.NewWindowEventArgs e)
        {
            e.CancelPopup = true;
            BrowserWindow.Show(e.TargetUrl);
        }

        public static void Show(string fileName)
        {
            new SwfPlayerWindow(fileName).Show();
        }
    }
}
