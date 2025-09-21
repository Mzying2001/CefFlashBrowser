﻿using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Sol;
using CefFlashBrowser.ViewModels;
using CefFlashBrowser.Views;
using CefFlashBrowser.Views.Dialogs;
using SimpleMvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>
        /// Display a window.
        /// </summary>
        public static TWindow ShowWindow<TWindow>(bool modal = false, bool singleton = false, Action<TWindow> initializer = null) where TWindow : Window, new()
        {
            var window = CreateWindow(singleton, initializer);

            if (modal) { DialogHelper.ShowModal(window); }
            else { window.Show(); }

            return window;
        }

        /// <summary>
        /// Create a new window instance, supporting singleton windows and initializer actions.
        /// </summary>
        public static TWindow CreateWindow<TWindow>(bool singleton = false, Action<TWindow> initializer = null) where TWindow : Window, new()
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
                window.SourceInitialized += windowSourceInitializedHandler;
                void windowSourceInitializedHandler(object sender, EventArgs e)
                {
                    initializer((TWindow)sender);
                    ((TWindow)sender).SourceInitialized -= windowSourceInitializedHandler;
                }
            }

            return window;
        }

        /// <summary>
        /// Create a new window instance with theme support.
        /// </summary>
        private static TWindow NewWindow<TWindow>() where TWindow : Window, new()
        {
            var window = new TWindow()
            { Style = (Style)Application.Current.Resources["CustomWindowStyle"] };

            void themeChangedHandler(object theme)
            {
                ThemeManager.ChangeTitleBarColor(window, (Theme)theme);
            }

            window.SourceInitialized += (sender, e) =>
            {
                var theme = GlobalData.Settings.FollowSystemTheme ? ThemeManager.GetSystemTheme() : GlobalData.Settings.Theme;
                ThemeManager.ChangeTitleBarColor((Window)sender, theme);
                Messenger.Global.Register(MessageTokens.THEME_CHANGED, themeChangedHandler);
            };

            window.Closed += (sender, e) =>
            {
                Messenger.Global.Unregister(MessageTokens.THEME_CHANGED, themeChangedHandler);
            };

            return window;
        }


        public static void ShowMainWindow()
        {
            ShowWindow<MainWindow>(singleton: true);
        }

        public static void ShowBrowser(string address)
        {
            var browserWindow = ShowWindow<BrowserWindow>(initializer: window =>
            {
                _browserWindows.Add(window);
                ((BrowserWindowViewModel)window.DataContext).Address = address;
            });

            browserWindow.Closing += (s, e) =>
            {
                if (e.Cancel) return;

                var window = (BrowserWindow)s;
                _browserWindows.Remove(window);

                if (_browserWindows.Count == 0
                    && !GlobalData.IsStartWithoutMainWindow
                    && GlobalData.Settings.HideMainWindowOnBrowsing)
                {
                    ShowMainWindow();
                }
            };
        }

        public static BrowserWindow GetLatestBrowserWindow()
        {
            return _browserWindows.LastOrDefault();
        }

        public static void ShowSwfPlayer(string fileName)
        {
            ShowWindow<SwfPlayerWindow>(initializer: window => window.FileName = fileName);
        }

        public static void ShowPopupWebPage(string address, CefSharp.IPopupFeatures popupFeatures = null)
        {
            ShowWindow<PopupWebPage>(initializer: window =>
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

        public static void ShowViewSourceWindow(string address)
        {
            ShowWindow<ViewSourceWindow>(initializer: window => window.Address = address);
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
            var dialog = ShowWindow<SelectLanguageDialog>(modal: true);
            return DialogHelper.GetDialogResult(dialog) == true;
        }

        public static bool ShowAddFavoriteDialog(string name = "", string url = "")
        {
            var dialog = ShowWindow<AddFavoriteDialog>(modal: true, initializer: window =>
            {
                if (window.DataContext is AddFavoriteDialogViewModel vm)
                {
                    vm.Name = name;
                    vm.Url = url;
                }
            });
            return DialogHelper.GetDialogResult(dialog) == true;
        }

        public static void Alert(string message = "", string title = "", Action<bool> callback = null)
        {
            var dialog = ShowWindow<JsAlertDialog>(modal: true, initializer: window =>
            {
                window.Title = title;
                window.Message = message;
            });
            callback?.Invoke(DialogHelper.GetDialogResult(dialog) == true);
        }

        public static void Confirm(string message = "", string title = "", Action<bool?> callback = null)
        {
            var dialog = ShowWindow<JsConfirmDialog>(modal: true, initializer: window =>
            {
                window.Title = title;
                window.Message = message;
            });
            callback?.Invoke(DialogHelper.GetDialogResult(dialog));
        }

        public static void Prompt(string message = "", string title = "", string defaultInputText = "", Action<bool?, string> callback = null)
        {
            var dialog = ShowWindow<JsPromptDialog>(modal: true, initializer: window =>
            {
                window.Title = title;
                window.Message = message;
                window.InputText = defaultInputText;
            });
            callback?.Invoke(DialogHelper.GetDialogResult(dialog), dialog.InputText);
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

        public static void ShowSolEditorWindow(string fileName)
        {
            try
            {
                var file = new SolFileWrapper(fileName);
                ShowSolEditorWindow(file);
            }
            catch (Exception e)
            {
                LogHelper.LogError($"Failed to open Sol file: {fileName}", e);
                ShowError(e.Message);
            }
        }

        public static void ShowTextEditor(string title = "", string defaultText = "", Func<string, bool> verifyText = null, Action<bool?, string> callback = null)
        {
            var dialog = ShowWindow<TextEditorDialog>(true, initializer: window =>
            {
                window.Title = title;
                window.Text = defaultText;
                window.VerifyText = verifyText;
            });
            callback?.Invoke(DialogHelper.GetDialogResult(dialog), dialog.Text);
        }

        public static void ShowAddSolItemDialog(Func<string, bool> verifyName = null, Action<bool?, string, SolTypeDesc> callback = null)
        {
            var dialog = ShowWindow<AddSolItemDialog>(true, initializer: window =>
            {
                window.Types = SolHelper.GetSupportedTypes();
                window.VerifyName = verifyName;
            });
            callback?.Invoke(DialogHelper.GetDialogResult(dialog), dialog.ItemName, dialog.SelectedType);
        }

        public static void ShowAddSolArrayItem(bool canChangeArrayType = true, bool isAssocArrayItem = false, Func<string, bool> verifyName = null, Action<bool?, string, SolTypeDesc> callback = null)
        {
            var dialog = ShowWindow<AddSolItemDialog>(true, initializer: window =>
            {
                window.Types = SolHelper.GetSupportedTypes();
                window.VerifyName = verifyName;
                window.IsArrayItem = true;
                window.IsAssocArrayItem = isAssocArrayItem;
                window.CanChangeArrayType = canChangeArrayType;
            });
            callback?.Invoke(DialogHelper.GetDialogResult(dialog), dialog.ItemName, dialog.SelectedType);
        }
    }
}
