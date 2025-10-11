using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
using SimpleMvvm;
using SimpleMvvm.Command;
using SimpleMvvm.Messaging;
using System;
using System.Collections.Generic;

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
        public DelegateCommand SetThemeCommand { get; set; }

        public List<ItemViewModel<NavigationType>> NavigationTypes { get; } = new List<ItemViewModel<NavigationType>>
        {
            new ItemViewModel<NavigationType>(NavigationType.Automatic, "navigationType_auto"),
            new ItemViewModel<NavigationType>(NavigationType.SearchOnly, "navigationType_searchOnly"),
            new ItemViewModel<NavigationType>(NavigationType.NavigateOnly, "navigationType_navigateOnly")
        };

        public int CurrentNavigationTypeIndex
        {
            get => ItemViewModel.GetIndex(NavigationTypes, GlobalData.Settings.NavigationType);
        }

        public List<ItemViewModel<SearchEngine>> SearchEngines { get; } = new List<ItemViewModel<SearchEngine>>
        {
            new ItemViewModel<SearchEngine>(SearchEngine.Baidu, "searchEngine_baidu"),
            new ItemViewModel<SearchEngine>(SearchEngine.DuckDuckGo, "searchEngine_duckDuckGo"),
            new ItemViewModel<SearchEngine>(SearchEngine.Google, "searchEngine_google"),
            new ItemViewModel<SearchEngine>(SearchEngine.Bing, "searchEngine_bing"),
            //new ItemViewModel<SearchEngine>(SearchEngine.Sogou, "searchEngine_sogou"),
            //new ItemViewModel<SearchEngine>(SearchEngine.So360, "searchEngine_so360"),
            new ItemViewModel<SearchEngine>(SearchEngine.Yandex, "searchEngine_yandex"),
            new ItemViewModel<SearchEngine>(SearchEngine.Bilibili, "searchEngine_bilibili"),
            new ItemViewModel<SearchEngine>(SearchEngine.Game4399, "searchEngine_game4399")
        };

        public int CurrentSearchEngineIndex
        {
            get => ItemViewModel.GetIndex(SearchEngines, GlobalData.Settings.SearchEngine);
        }

        public List<ItemViewModel<NewPageBehavior>> NewPageBehaviors { get; } = new List<ItemViewModel<NewPageBehavior>>
        {
            new ItemViewModel<NewPageBehavior>(NewPageBehavior.OriginalWindow, "newPageBehavior_originalWindow"),
            new ItemViewModel<NewPageBehavior>(NewPageBehavior.NewWindow, "newPageBehavior_newWindow")
        };

        public int CurrentNewPageBehaviorIndex
        {
            get => ItemViewModel.GetIndex(NewPageBehaviors, GlobalData.Settings.NewPageBehavior);
        }

        public List<ItemViewModel<Theme>> Themes { get; } = new List<ItemViewModel<Theme>>
        {
            new ItemViewModel<Theme>(Theme.Light, "theme_light"),
            new ItemViewModel<Theme>(Theme.Dark, "theme_dark")
        };

        public int CurrentThemeIndex
        {
            get => ItemViewModel.GetIndex(Themes, GlobalData.Settings.Theme);
        }

        public bool EnableFakeFlashVersion
        {
            get => GlobalData.Settings.FakeFlashVersionSetting.Enable;
            set
            {
                GlobalData.Settings.FakeFlashVersionSetting.Enable = value;
                RaisePropertyChanged();
            }
        }

        public string FakeFlashVersion
        {
            get => GlobalData.Settings.FakeFlashVersionSetting.FlashVersion;
            set
            {
                GlobalData.Settings.FakeFlashVersionSetting.FlashVersion = value;
                RaisePropertyChanged();
            }
        }

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

        public bool DisableFullscreen
        {
            get => GlobalData.Settings.DisableFullScreen;
            set
            {
                GlobalData.Settings.DisableFullScreen = value;
                RaisePropertyChanged();
            }
        }

        public bool HideMainWindowOnBrowsing
        {
            get => GlobalData.Settings.HideMainWindowOnBrowsing;
            set
            {
                GlobalData.Settings.HideMainWindowOnBrowsing = value;
                RaisePropertyChanged();
            }
        }

        public bool FollowSystemTheme
        {
            get => GlobalData.Settings.FollowSystemTheme;
            set
            {
                GlobalData.Settings.FollowSystemTheme = value;
                ThemeManager.ChangeTheme(value ? ThemeManager.GetSystemTheme() : GlobalData.Settings.Theme);
                RaisePropertyChanged();
            }
        }

        public bool EnableIntegratedDevTools
        {
            get => GlobalData.Settings.EnableIntegratedDevTools;
            set
            {
                GlobalData.Settings.EnableIntegratedDevTools = value;
                RaisePropertyChanged();
            }
        }

        public bool SaveZoomLevel
        {
            get => GlobalData.Settings.SaveZoomLevel;
            set
            {
                GlobalData.Settings.SaveZoomLevel = value;
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
            WindowManager.Confirm(LanguageManager.GetString("message_deleteCache"), callback: result =>
            {
                if (result == true)
                {
                    while (true)
                    {
                        try
                        {
                            Messenger.Global.Send(MessageTokens.CLOSE_ALL_BROWSERS, null);
                            CefSharp.Cef.Shutdown();
                            DeleteDirectory(GlobalData.CachesPath);
                            break;
                        }
                        catch (Exception e)
                        {
                            LogHelper.LogError("Delete cache failed", e);

                            string msg = string.Format("{0}\n\n{1}:\n{2}",
                                LanguageManager.GetString("error_deleteCachesRetry"), LanguageManager.GetString("error_message"), e.Message);

                            bool retry = false;
                            WindowManager.Confirm(msg, LanguageManager.GetString("dialog_error"), tmp => retry = tmp ?? false);

                            if (!retry) { break; }
                        }
                    }

                    Program.Restart();
                }
            });
        }

        private void PopupAboutCef()
        {
            WindowManager.ShowPopupWebPage("chrome://version/");
        }

        private void SetNewPageBehavior(NewPageBehavior newPageBehavior)
        {
            GlobalData.Settings.NewPageBehavior = newPageBehavior;
        }

        private void AskRestartApp()
        {
            WindowManager.Confirm(LanguageManager.GetString("message_restart"), callback: result =>
            {
                if (result == true)
                {
                    Messenger.Global.Send(MessageTokens.CLOSE_ALL_BROWSERS, null);
                    Program.Restart();
                }
            });
        }

        private void SetTheme(Theme theme)
        {
            GlobalData.Settings.Theme = theme;
            ThemeManager.ChangeTheme(theme);
        }

        public SettingsWindowViewModel()
        {
            SetNavigationTypeCommand = new DelegateCommand<NavigationType>(SetNavigationType);
            SetSearchEngineCommand = new DelegateCommand<SearchEngine>(SetSearchEngine);
            DeleteCacheCommand = new DelegateCommand(DeleteCache);
            PopupAboutCefCommand = new DelegateCommand(PopupAboutCef);
            SetNewPageBehaviorCommand = new DelegateCommand<NewPageBehavior>(SetNewPageBehavior);
            AskRestartAppCommand = new DelegateCommand(AskRestartApp);
            SetThemeCommand = new DelegateCommand<Theme>(SetTheme);
        }
    }
}
