using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using System.Linq;
using System.Windows;

namespace CefFlashBrowser.Views
{
    /// <summary>
    /// SettingsWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();


            var navTypes = EnumDescriptions.GetNavigationTypes();
            cb_navTypes.ItemsSource = navTypes;
            cb_navTypes.SelectedIndex = EnumDescriptions.GetIndex(navTypes, GlobalData.Settings.NavigationType);

            var searchEngines = EnumDescriptions.GetSearchEngines().ToList();
            cb_searchEngines.ItemsSource = searchEngines;
            cb_searchEngines.SelectedIndex = EnumDescriptions.GetIndex(searchEngines, GlobalData.Settings.SearchEngine);

            var newPageBehaviors = EnumDescriptions.GetNewPageBehaviors().ToList();
            cb_newPageBehaviors.ItemsSource = newPageBehaviors;
            cb_newPageBehaviors.SelectedIndex = EnumDescriptions.GetIndex(newPageBehaviors, GlobalData.Settings.NewPageBehavior);
        }
    }
}
