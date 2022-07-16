using CefFlashBrowser.Views;
using CefFlashBrowser.Views.Dialogs;
using System;
using System.Collections.Generic;
using System.Windows;

namespace CefFlashBrowser.Utils
{
    public static class WindowManager
    {
        private static readonly Dictionary<Type, Window> _windows;

        static WindowManager()
        {
            _windows = new Dictionary<Type, Window>();
        }

        public static TWindow ShowWindow<TWindow>(bool modal = false, bool save = false) where TWindow : Window, new()
        {
            TWindow window = null;

            if (save)
            {
                if (_windows.ContainsKey(typeof(TWindow)))
                {

                    window = (TWindow)_windows[typeof(TWindow)];
                    window.WindowState = window.WindowState == WindowState.Minimized ? WindowState.Normal : window.WindowState;
                    window.Activate();
                    return window;
                }
                else
                {
                    window = new TWindow();
                    window.Closing += (s, e) => _windows.Remove(s.GetType());
                    _windows.Add(typeof(TWindow), window);
                }
            }
            else
            {
                window = new TWindow();
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

        public static MainWindow ShowMainWindow()
        {
            return ShowWindow<MainWindow>(save: true);
        }

        public static BrowserWindow ShowBrowser(string address)
        {
            BrowserWindow window = ShowWindow<BrowserWindow>();
            window.browser.Load(address);
            return window;
        }

        public static SwfPlayerWindow ShowSwfPlayer(string fileName)
        {
            SwfPlayerWindow window = ShowWindow<SwfPlayerWindow>();
            window.FileName = fileName;
            return window;
        }

        public static PopupWebPage ShowPopupWebPage(string address, CefSharp.IPopupFeatures popupFeatures = null)
        {
            PopupWebPage window = ShowWindow<PopupWebPage>();
            window.Address = address;
            if (popupFeatures != null)
            {
                window.Left = popupFeatures.X;
                window.Top = popupFeatures.Y;
                window.Width = popupFeatures.Width;
                window.Height = popupFeatures.Height;
            }
            return window;
        }

        public static ViewSourceWindow ShowViewSourceWindow(string address)
        {
            ViewSourceWindow window = ShowWindow<ViewSourceWindow>();
            window.Address = address;
            return window;
        }

        public static void ShowFavoritesManager()
        {
            ShowWindow<FavoritesManager>(true);
        }

        public static void ShowSettingsWindow()
        {
            ShowWindow<SettingsWindow>(true);
        }

        public static bool ShowSelectLanguageDialog()
        {
            SelectLanguageDialog window = ShowWindow<SelectLanguageDialog>(true);
            return window.DialogResult == true;
        }

        public static bool ShowAddFavoriteDialog(string name = "", string url = "")
        {
            return new AddFavoriteDialog() { ItemName = name, ItemUrl = url }.ShowDialog() == true;
        }
    }
}
