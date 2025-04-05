using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Sol;
using CefFlashBrowser.ViewModels;
using CefFlashBrowser.Views;
using CefFlashBrowser.Views.Dialogs;
using SimpleMvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Windows;

namespace CefFlashBrowser.Utils
{
    public static class WindowManager
    {
        private static readonly Dictionary<Type, Window> _singletonWindows;
        private static readonly List<BrowserWindow> _browserWindows;

        static WindowManager()
        {
            _singletonWindows = new Dictionary<Type, Window>();
            _browserWindows = new List<BrowserWindow>();
        }

        public static TWindow ShowWindow<TWindow>(bool modal = false, bool singleton = false, Action<TWindow> initializer = null) where TWindow : Window, new()
        {
            TWindow window = null;

            if (singleton)
            {
                if (_singletonWindows.ContainsKey(typeof(TWindow)))
                {
                    window = (TWindow)_singletonWindows[typeof(TWindow)];
                    window.WindowState = window.WindowState == WindowState.Minimized ? WindowState.Normal : window.WindowState;
                    window.Activate();
                    return window;
                }
                else
                {
                    window = NewWindow<TWindow>();
                    window.Closing += (s, e) => _singletonWindows.Remove(s.GetType());
                    _singletonWindows.Add(typeof(TWindow), window);
                }
            }
            else
            {
                window = NewWindow<TWindow>();
            }

            if (initializer != null)
            {
                window.SourceInitialized += WindowSourceInitializedHandler;
                void WindowSourceInitializedHandler(object sender, EventArgs e)
                {
                    initializer((TWindow)sender);
                    ((TWindow)sender).SourceInitialized -= WindowSourceInitializedHandler;
                }
            }

            if (modal)
            {
                window.ShowDialog();
            }
            else
            {
                window.Show();
            }

            return window;
        }

        public static TWindow NewWindow<TWindow>() where TWindow : Window, new()
        {
            var window = new TWindow()
            { Style = (Style)Application.Current.Resources["CustomWindowStyle"] };

            void ThemeChangedHandler(object theme)
            {
                ThemeManager.ChangeTitleBarColor(window, (Theme)theme);
            }

            window.SourceInitialized += (sender, e) =>
            {
                var theme = GlobalData.Settings.FollowSystemTheme ? ThemeManager.GetSystemTheme() : GlobalData.Settings.Theme;
                ThemeManager.ChangeTitleBarColor((Window)sender, theme);
                Messenger.Global.Register(MessageTokens.THEME_CHANGED, ThemeChangedHandler);
            };
            window.Closed += (sender, e) =>
            {
                Messenger.Global.Unregister(MessageTokens.THEME_CHANGED, ThemeChangedHandler);
            };
            return window;
        }

        public static void ShowMainWindow()
        {
            ShowWindow<MainWindow>(singleton: true);
        }

        public static BrowserWindow ShowBrowser(string address)
        {
            var browserWindow = ShowWindow<BrowserWindow>(initializer: window =>
            {
                _browserWindows.Add(window);
                window.browser.Address = address;
            });
            browserWindow.Closing += (s, e) =>
            {
                if (e.Cancel)
                    return;
                var window = (BrowserWindow)s;
                _browserWindows.Remove(window);
                if (_browserWindows.Count == 0 && !GlobalData.IsStartWithoutMainWindow && GlobalData.Settings.HideMainWindowOnBrowsing)
                    ShowMainWindow();
            };
            return browserWindow;
        }

        public static SwfPlayerWindow ShowSwfPlayer(string fileName)
        {
            return ShowWindow<SwfPlayerWindow>(initializer: window => window.FileName = fileName);
        }

        public static PopupWebPage ShowPopupWebPage(string address, CefSharp.IPopupFeatures popupFeatures = null)
        {
            return ShowWindow<PopupWebPage>(initializer: window =>
            {
                window.Address = address;
                if (popupFeatures != null)
                {
                    window.Left = popupFeatures.X;
                    window.Top = popupFeatures.Y;
                    window.Width = popupFeatures.Width;
                    window.Height = popupFeatures.Height;
                }
            });
        }

        public static ViewSourceWindow ShowViewSourceWindow(string address)
        {
            return ShowWindow<ViewSourceWindow>(initializer: window => window.Address = address);
        }

        public static void ShowFavoritesManager()
        {
            ShowWindow<FavoritesManager>(singleton: true);
        }

        public static void ShowSettingsWindow()
        {
            ShowWindow<SettingsWindow>(singleton: true);
        }

        public static bool ShowSelectLanguageDialog()
        {
            return ShowWindow<SelectLanguageDialog>(true).DialogResult == true;
        }

        public static bool ShowAddFavoriteDialog(string name = "", string url = "")
        {
            return ShowWindow<AddFavoriteDialog>(true, initializer: window =>
            {
                window.ItemName = name;
                window.ItemUrl = url;
                window.NameTextBox.SelectAll();
            }).DialogResult == true;
        }

        public static void Alert(string message = "", string title = "", Action<bool> callback = null)
        {
            bool result = ShowWindow<JsAlertDialog>(true, initializer: window =>
            {
                window.Title = title;
                window.Message = message;
            }).DialogResult == true;
            callback?.Invoke(result);
        }

        public static void Confirm(string message = "", string title = "", Action<bool?> callback = null)
        {
            bool? result = ShowWindow<JsConfirmDialog>(true, initializer: window =>
            {
                window.Title = title;
                window.Message = message;
            }).DialogResult;
            callback?.Invoke(result);
        }

        public static void Prompt(string message = "", string title = "", string defaultInputText = "", Action<bool?, string> callback = null)
        {
            JsPromptDialog dialog = ShowWindow<JsPromptDialog>(true, initializer: window =>
            {
                window.Title = title;
                window.Message = message;
                window.InputText = defaultInputText;
            });
            callback?.Invoke(dialog.DialogResult, dialog.InputText);
        }

        public static void ShowError(string errMsg)
        {
            MessageBox.Show(errMsg, LanguageManager.GetString("dialog_error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public static void ShowSolSaveManager()
        {
            ShowWindow<SolSaveManager>(singleton: true);
        }

        public static void ShowSolEditorWindow(SolFileWrapper file)
        {
            ShowWindow<SolEditorWindow>(initializer: window =>
            {
                window.DataContext = new SolEditorWindowViewModel(file);
            });
        }

        public static void ShowTextEditor(string title = "", string defaultText = "", Func<string, bool> verifyText = null, Action<bool?, string> callback = null)
        {
            var dialog = ShowWindow<TextEditorDialog>(true, initializer: window =>
            {
                window.Title = title;
                window.Text = defaultText;
                window.VerifyText = verifyText;
            });
            callback?.Invoke(dialog.DialogResult, dialog.Text);
        }
    }
}
