using CefFlashBrowser.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace CefFlashBrowser.Views.Dialogs
{
    /// <summary>
    /// SelectLanguageDialog.xaml 的交互逻辑
    /// </summary>
    public partial class SelectLanguageDialog : Window
    {
        private readonly List<string> supportedLanguage;

        public SelectLanguageDialog()
        {
            InitializeComponent();
            supportedLanguage = LanguageManager.GetSupportedLanguage().ToList();
            langList.ItemsSource = from item in supportedLanguage select LanguageManager.GetLanguageName(item);

            for (int i = 0; i < supportedLanguage.Count; i++)
            {
                if (supportedLanguage[i] == LanguageManager.CurrentLanguage)
                {
                    langList.SelectedIndex = i;
                    break;
                }
            }
        }

        private void LangListSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (langList.SelectedIndex != -1)
                LanguageManager.CurrentLanguage = supportedLanguage[langList.SelectedIndex];
        }

        private void OkButtonClick(object sender, RoutedEventArgs e)
        {
            WindowManager.ShowMainWindow();
            Close();
        }
    }
}
