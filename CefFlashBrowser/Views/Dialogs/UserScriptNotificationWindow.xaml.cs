using System;
using System.Windows;
using System.Windows.Media;

namespace CefFlashBrowser.Views.Dialogs
{
    public partial class UserScriptNotificationWindow : Window
    {
        public event EventHandler Clicked;

        public UserScriptNotificationWindow()
        {
            InitializeComponent();
        }

        internal void SetMessage(string title, string text)
        {
            if (!string.IsNullOrWhiteSpace(title)) Title = title;
            notificationText.Text = text;
        }

        internal void SetImage(ImageSource image)
        {
            notificationImage.Source = image;
            notificationImage.Visibility = Visibility.Visible;
        }

        private void NotificationClick(object sender, RoutedEventArgs e) => Clicked?.Invoke(this, EventArgs.Empty);
        private void CloseClick(object sender, RoutedEventArgs e) => Close();
    }
}
