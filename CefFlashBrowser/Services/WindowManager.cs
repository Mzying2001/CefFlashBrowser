using CefFlashBrowser.Data;
using CefFlashBrowser.Models;
using CefFlashBrowser.Services.UserScripts;
using CefFlashBrowser.Sol;
using CefFlashBrowser.Utils;
using CefFlashBrowser.ViewModels;
using CefFlashBrowser.Views;
using CefFlashBrowser.Views.Dialogs;
using Mzying2001.MonkeySharp.Core.Domain;
using SimpleMvvm.Ioc;
using SimpleMvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Interop;

namespace CefFlashBrowser.Services
{
    public static class WindowManager
    {
        // store singleton window instances
        private static readonly Dictionary<Type, Window> _singletonWindows;

        // store all browser window instances
        private static readonly List<BrowserWindow> _browserWindows;

        // store Sol editor windows by view model to ensure only one editor per Sol file
        private static readonly Dictionary<SolEditorWindowViewModel, Window> _solEditorWindows;


        /// <summary>
        /// Initialize static members of the WindowManager class.
        /// </summary>
        static WindowManager()
        {
            _singletonWindows = new Dictionary<Type, Window>();
            _browserWindows = new List<BrowserWindow>();
            _solEditorWindows = new Dictionary<SolEditorWindowViewModel, Window>();
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
                    BringWindowToFront(window);
                    return window;
                }
                else
                {
                    window = NewWindow<TWindow>();
                    window.Closed += (s, e) => _singletonWindows.Remove(typeof(TWindow));
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
            var window = new TWindow();

            void themeChangedHandler(object theme)
            {
                ThemeManager.ChangeTitleBarColor(window, (Theme)theme);
            }

            void redrawFrameHandler(object _)
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                var style = HwndHelper.GetWindowStyle(hwnd);

                if ((style & Win32.WS_CAPTION) == Win32.WS_CAPTION)
                {
                    HwndHelper.SetWindowStyle(hwnd, style & ~Win32.WS_CAPTION);
                    HwndHelper.RedrawWindowFrame(hwnd);
                    HwndHelper.SetWindowStyle(hwnd, style);
                    HwndHelper.RedrawWindowFrame(hwnd);
                }
            }

            window.SourceInitialized += (sender, e) =>
            {
                var theme = GlobalData.Settings.FollowSystemTheme ? ThemeManager.GetSystemTheme() : GlobalData.Settings.Theme;
                ThemeManager.ChangeTitleBarColor((Window)sender, theme);
                Messenger.Global.Register(MessageTokens.THEME_CHANGED, themeChangedHandler);
                Messenger.Global.Register(MessageTokens.REDRAW_ALL_FRAMES, redrawFrameHandler);
            };

            window.Closed += (sender, e) =>
            {
                Messenger.Global.Unregister(MessageTokens.THEME_CHANGED, themeChangedHandler);
                Messenger.Global.Unregister(MessageTokens.REDRAW_ALL_FRAMES, redrawFrameHandler);
            };

            return window;
        }

        /// <summary>
        /// Bring an existing window to the front.
        /// </summary>
        private static void BringWindowToFront(Window window)
        {
            window.Dispatcher.InvokeAsync(() =>
            {
                if (window.WindowState == WindowState.Minimized)
                    window.WindowState = WindowState.Normal;
                window.Activate();
            });
        }

        /// <summary>
        /// If active browser window exists, return it; otherwise, return the last opened browser window.
        /// </summary>
        public static BrowserWindow GetCurrentBrowserWindow()
        {
            if (_browserWindows.FirstOrDefault(x => x.IsActive) is BrowserWindow activeWindow)
            {
                return activeWindow;
            }
            else
            {
                return _browserWindows.LastOrDefault();
            }
        }


        public static void ShowMainWindow()
        {
            ShowWindow<MainWindow>(singleton: true);
        }

        public static void ShowBrowser(string address)
        {
            OpenBrowser(address);
        }

        internal static BrowserWindow OpenBrowser(string address, bool activate = true)
        {
            var browserWindow = CreateWindow<BrowserWindow>(initializer: window =>
            {
                _browserWindows.Add(window);
                ((BrowserWindowViewModel)window.DataContext).CurrentBrowser.Address = address;

                DialogHelper.GetBeforeDestroyHandlers(window).Add((s, e) =>
                {
                    _browserWindows.Remove((BrowserWindow)s);

                    if (_browserWindows.Count == 0
                        && !GlobalData.IsStartWithoutMainWindow
                        && GlobalData.Settings.HideMainWindowOnBrowsing)
                    { ShowMainWindow(); }
                });
            });

            browserWindow.ShowActivated = activate;
            browserWindow.Show();
            return browserWindow;
        }

        public static void ShowSwfPlayer(string fileName)
        {
            ShowWindow<SwfPlayerWindow>(initializer: window => window.FileName = fileName);
        }

        public static void ShowPopupWebPage(string address, WindowSizeInfo sizeInfo = null)
        {
            ShowWindow<PopupWebPage>(initializer: window =>
            {
                WindowSizeInfo.Apply(sizeInfo, window);
                window.SetCurrentValue(PopupWebPage.AddressProperty, address);
            });
        }

        public static void ShowFavoritesManager()
        {
            ShowWindow<FavoritesManager>(singleton: true);
        }

        public static void ShowUserScriptManager()
        {
            ShowWindow<UserScriptManager>(singleton: true);
        }

        internal static void ShowUserScriptDetails(UserScriptItemViewModel script, UserScriptManager owner)
        {
            var dialog = CreateWindow<UserScriptDetailsDialog>();
            dialog.SetScript(script);
            DialogHelper.ShowModal(dialog, owner);
        }

        internal static void ShowUserScriptPermissions(BrowserWindow owner, string address)
        {
            var dialog = CreateWindow<UserScriptPermissionsWindow>();
            dialog.SetPage(SimpleIoc.Global.GetInstance<UserScriptService>(), address);
            DialogHelper.ShowModal(dialog, owner);
        }

        internal static bool ConfirmUserScriptInstallation(UserScriptMetadata metadata, string origin,
            Window owner, bool update = false, CancellationToken token = default)
        {
            var dialog = CreateWindow<UserScriptInstallDialog>();
            dialog.SetScript(metadata, origin, update);
            return ShowModal(dialog, owner, token);
        }

        internal static UserScriptPermissionDialog CreateUserScriptPermissionDialog(string description)
        {
            var dialog = CreateWindow<UserScriptPermissionDialog>();
            dialog.SetDescription(description);
            return dialog;
        }

        internal static UserScriptNotificationWindow CreateUserScriptNotificationWindow(string title, string text, Window owner)
        {
            var window = CreateWindow<UserScriptNotificationWindow>();
            window.SetMessage(title, text);
            window.Owner = owner;
            return window;
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

        public static bool ShowAddFavoriteDialog(AddFavoriteDialogViewModel vm)
        {
            var dialog = ShowWindow<AddFavoriteDialog>(modal: true, initializer: window => window.DataContext = vm);
            return DialogHelper.GetDialogResult(dialog) == true;
        }

        public static void Alert(string message = "", string title = "", Action<bool> callback = null)
        {
            bool accepted = ShowAlertDialog(message, title);
            callback?.Invoke(accepted);
        }

        internal static bool ShowAlertDialog(string message, string title, Window owner = null, CancellationToken token = default)
        {
            var dialog = CreateWindow<JsAlertDialog>(initializer: window =>
            {
                window.Title = title;
                window.Message = message;
            });
            return ShowModal(dialog, owner, token);
        }

        private static bool ShowModal(Window dialog, Window owner, CancellationToken token)
        {
            using (token.Register(() => dialog.Dispatcher.InvokeAsync(() =>
            {
                if (dialog.IsVisible)
                    dialog.Close();
            })))
            {
                token.ThrowIfCancellationRequested();
                return DialogHelper.ShowModal(dialog, owner) == true;
            }
        }

        public static void Confirm(string message = "", string title = "", Action<bool?> callback = null)
        {
            var dialog = ShowConfirmDialog(message, title);
            callback?.Invoke(DialogHelper.GetDialogResult(dialog));
        }

        internal static JsConfirmDialog ShowConfirmDialog(string message, string title,
            Action<JsConfirmDialog> onSourceInitialized = null)
        {
            return ShowWindow<JsConfirmDialog>(modal: true, initializer: window =>
            {
                window.Title = title;
                window.Message = message;
                onSourceInitialized?.Invoke(window);
            });
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
            string title = "Error";
            try
            {
                title = SimpleIoc.Global.GetInstance<LanguageService>().GetString("common_error");
            }
            catch { }
            MessageBox.Show(errMsg, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        internal static void OpenInDefaultBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
            }
            catch (Exception e)
            {
                LogHelper.LogError($"Failed to open URL in default browser: {url}", e);
                ShowError(e.Message);
            }
        }

        public static void ShowSolSaveManager()
        {
            ShowWindow<SolSaveManager>(singleton: true);
        }

        public static void ShowSolEditorWindow(string fileName)
        {
            try
            {
                var vm = _solEditorWindows.Keys
                    .Where(x => string.Equals(x.FilePath, fileName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

                if (vm != null)
                {
                    var window = _solEditorWindows[vm];
                    BringWindowToFront(window);
                }
                else
                {
                    var file = new SolFileWrapper(fileName);
                    vm = new SolEditorWindowViewModel(file);

                    ShowWindow<SolEditorWindow>(initializer: window =>
                    {
                        window.DataContext = vm;
                        window.Closed += (s, e) => _solEditorWindows.Remove(vm);
                        _solEditorWindows.Add(vm, window);
                    });
                }
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
