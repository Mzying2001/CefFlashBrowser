using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Views;
using CefFlashBrowser.Views.Dialogs.JsDialogs;
using SimpleMvvm;
using SimpleMvvm.Command;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
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

        private void DeleteCacheViaBat()
        {
            string bat = "taskkill /f /im CefFlashBrowser.exe\n" +
                         "timeout 1\n" +
                         "rd /s /q caches\\\n" +
                         "mshta vbscript:msgbox(\"done\",64,\"\")(window.close)\n" +
                         "start CefFlashBrowser.exe\n" +
                         "del _.bat";
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "_.bat"), bat);

            Process.Start(new ProcessStartInfo()
            {
                FileName = "_.bat",
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }

        private void DeleteCacheViaLauncher(string path)
        {
            Process.Start(new ProcessStartInfo()
            {
                FileName = path,
                Arguments = "-delcaches"
            });
        }

        private void DeleteCache()
        {
            JsConfirmDialog.Show(LanguageManager.GetString("message_deleteCache"), "", result =>
            {
                if (result == true)
                {
                    var launcher = @"..\Launcher.exe";
                    if (File.Exists(launcher))
                        DeleteCacheViaLauncher(launcher);
                    else
                        DeleteCacheViaBat();
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
