using CefFlashBrowser.FlashBrowser;
using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
using CefFlashBrowser.Views;
using CefFlashBrowser.Views.Dialogs.JsDialogs;
using SimpleMvvm;
using SimpleMvvm.Command;
using System;

namespace CefFlashBrowser.ViewModels
{
    public class SettingsWindowViewModel : ViewModelBase
    {
        public DelegateCommand SetNavigationTypeCommand { get; set; }
        public DelegateCommand SetSearchEngineCommand { get; set; }
        public DelegateCommand DeleteCacheCommand { get; set; }
        public DelegateCommand PopupAboutCefCommand { get; set; }
        public DelegateCommand SetNewPageBehaviorCommand { get; set; }
        public DelegateCommand AskRestartAppCommand { get; set; }

        public bool EnableCustomUserAgent
        {
            get => GlobalData.Settings.UserAgentSetting.EnableCustom;
            set
            {
                GlobalData.Settings.UserAgentSetting.EnableCustom = value;
                RaisePropertyChanged();
            }
        }

        public string UserAgent
        {
            get => GlobalData.Settings.UserAgentSetting.UserAgent;
            set
            {
                GlobalData.Settings.UserAgentSetting.UserAgent = value;
                RaisePropertyChanged();
            }
        }

        public bool EnableProxy
        {
            get => GlobalData.Settings.ProxySettings.EnableProxy;
            set
            {
                GlobalData.Settings.ProxySettings.EnableProxy = value;
                RaisePropertyChanged();
            }
        }

        public string ProxyIP
        {
            get => GlobalData.Settings.ProxySettings.IP;
            set
            {
                GlobalData.Settings.ProxySettings.IP = value;
                RaisePropertyChanged();
            }
        }

        public string ProxyPort
        {
            get => GlobalData.Settings.ProxySettings.Port;
            set
            {
                GlobalData.Settings.ProxySettings.Port = value;
                RaisePropertyChanged();
            }
        }

        public string ProxyUserName
        {
            get => GlobalData.Settings.ProxySettings.UserName;
            set
            {
                GlobalData.Settings.ProxySettings.UserName = value;
                RaisePropertyChanged();
            }
        }

        public string ProxyPassword
        {
            get => GlobalData.Settings.ProxySettings.Password;
            set
            {
                GlobalData.Settings.ProxySettings.Password = value;
                RaisePropertyChanged();
            }
        }

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
            JsConfirmDialog.ShowDialog(LanguageManager.GetString("message_deleteCache"), callback: result =>
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

                    App.Restart();
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

        private void AskRestartApp()
        {
            JsConfirmDialog.ShowDialog(LanguageManager.GetString("message_restart"), callback: result =>
            {
                if (result == true)
                    App.Restart();
            });
        }

        public SettingsWindowViewModel()
        {
            if (GlobalData.Settings.ProxySettings == null)
                GlobalData.Settings.ProxySettings = new ProxySettings();

            if (GlobalData.Settings.UserAgentSetting == null)
                GlobalData.Settings.UserAgentSetting = new UserAgentSetting();

            SetNavigationTypeCommand = new DelegateCommand<NavigationType>(SetNavigationType);
            SetSearchEngineCommand = new DelegateCommand<SearchEngine>(SetSearchEngine);
            DeleteCacheCommand = new DelegateCommand(DeleteCache);
            PopupAboutCefCommand = new DelegateCommand(PopupAboutCef);
            SetNewPageBehaviorCommand = new DelegateCommand<NewPageBehavior>(SetNewPageBehavior);
            AskRestartAppCommand = new DelegateCommand(AskRestartApp);
        }
    }
}
