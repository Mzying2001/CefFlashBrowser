using CefFlashBrowser.FlashBrowser;
using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
using CefFlashBrowser.Views;
using CefFlashBrowser.Views.Dialogs.JsDialogs;
using SimpleMvvm;
using SimpleMvvm.Command;
using System;
using System.Diagnostics;

namespace CefFlashBrowser.ViewModels
{
    public class SettingsWindowViewModel : ViewModelBase
    {
        public DelegateCommand SetNavigationTypeCommand { get; set; }
        public DelegateCommand SetSearchEngineCommand { get; set; }
        public DelegateCommand DeleteCacheCommand { get; set; }
        public DelegateCommand PopupAboutCefCommand { get; set; }
        public DelegateCommand SetNewPageBehaviorCommand { get; set; }

        public bool DisableOnBeforeUnloadDialog
        {
            get => GlobalData.Settings.DisableOnBeforeUnloadDialog;
            set
            {
                GlobalData.Settings.DisableOnBeforeUnloadDialog = value;
                RaisePropertyChanged();
            }
        }

        private void SetNavigationType(NavigationType type)
        {
            GlobalData.Settings.NavigationType = type;
        }

        private void SetSearchEngine(SearchEngine engine)
        {
            GlobalData.Settings.SearchEngine = engine;
        }

        private void DeleteDirectory(string path)
        {
            new PathInfo(PathInfo.PathType.Directory, path).Delete();
        }

        private void DeleteCache()
        {
            JsConfirmDialog.ShowDialog(LanguageManager.GetString("message_deleteCache"), "", result =>
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

        private void SetNewPageBehavior(NewPageBehavior newPageBehavior)
        {
            GlobalData.Settings.NewPageBehavior = newPageBehavior;
        }

        public SettingsWindowViewModel()
        {
            SetNavigationTypeCommand = new DelegateCommand<NavigationType>(SetNavigationType);
            SetSearchEngineCommand = new DelegateCommand<SearchEngine>(SetSearchEngine);
            DeleteCacheCommand = new DelegateCommand(DeleteCache);
            PopupAboutCefCommand = new DelegateCommand(PopupAboutCef);
            SetNewPageBehaviorCommand = new DelegateCommand<NewPageBehavior>(SetNewPageBehavior);
        }
    }
}
