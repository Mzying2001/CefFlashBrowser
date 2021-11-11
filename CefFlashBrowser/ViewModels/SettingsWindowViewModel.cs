using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Models.FlashBrowser;
using CefFlashBrowser.Views;
using CefFlashBrowser.Views.Dialogs.JsDialogs;
using SimpleMvvm;
using SimpleMvvm.Command;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace CefFlashBrowser.ViewModels
{
    public class SettingsWindowViewModel : ViewModelBase
    {
        public DelegateCommand SetNavigationTypeCommand { get; set; }
        public DelegateCommand SetSearchEngineCommand { get; set; }
        public DelegateCommand DeleteCacheCommand { get; set; }
        public DelegateCommand PopupAboutCefCommand { get; set; }

        public ObservableCollection<EnumDescription<NavigationType.Type>> NavigationTypes { get; set; }
        public ObservableCollection<EnumDescription<SearchEngine.Engine>> SearchEngines { get; set; }

        public int CurrentNavigationTypeIndex
        {
            get
            {
                var li = (from i in NavigationType.GetNavigationTypes() select i.Value).ToList();
                return li.IndexOf(GlobalData.Settings.NavigationType);
            }
        }

        public int CurrentSearchEngineIndex
        {
            get
            {
                var li = (from i in SearchEngine.GetSupportedSearchEngines() select i.Value).ToList();
                return li.IndexOf(GlobalData.Settings.SearchEngine);
            }
        }

        private void SetNavigationType(NavigationType.Type type)
        {
            GlobalData.Settings.NavigationType = type;
        }

        private void SetSearchEngine(SearchEngine.Engine engine)
        {
            GlobalData.Settings.SearchEngine = engine;
        }

        private void DeleteDirectory(string path)
        {
            new PathInfo(PathInfo.PathType.Directory, path).Delete();
        }

        private void DeleteCache()
        {
            JsConfirmDialog.Show(LanguageManager.GetString("message_deleteCache"), "", result =>
            {
                if (result == true)
                {
                    while (true)
                    {
                        try
                        {
                            CefSharp.Cef.Shutdown();
                            DeleteDirectory(FlashBrowserBase.CachePath);
                            break;
                        }
                        catch (Exception e)
                        {
                            var tmp = System.Windows.MessageBox.Show(
                                string.Format("{0}\n\n{1}:\n{2}", LanguageManager.GetString("error_deleteCachesRetry"), LanguageManager.GetString("error_message"), e.Message),
                                LanguageManager.GetString("title_error"), System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Error);

                            if (tmp == System.Windows.MessageBoxResult.No)
                            {
                                break;
                            }
                        }
                    }

                    Process.Start(Process.GetCurrentProcess().MainModule.FileName);
                    System.Windows.Application.Current.Shutdown();
                }
            });
        }

        private void PopupAboutCef()
        {
            BrowserWindow.Show("chrome://version/");
        }

        public SettingsWindowViewModel()
        {
            NavigationTypes = new ObservableCollection<EnumDescription<NavigationType.Type>>(NavigationType.GetNavigationTypes());
            SearchEngines = new ObservableCollection<EnumDescription<SearchEngine.Engine>>(SearchEngine.GetSupportedSearchEngines());

            SetNavigationTypeCommand = new DelegateCommand<NavigationType.Type>(SetNavigationType);
            SetSearchEngineCommand = new DelegateCommand<SearchEngine.Engine>(SetSearchEngine);
            DeleteCacheCommand = new DelegateCommand(DeleteCache);
            PopupAboutCefCommand = new DelegateCommand(PopupAboutCef);
        }
    }
}
