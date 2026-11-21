using CefFlashBrowser.Browser.UserScripts;
using CefFlashBrowser.Data;
using CefFlashBrowser.Database;
using CefFlashBrowser.FlashBrowser;
using CefFlashBrowser.Log;
using CefFlashBrowser.Services;
using CefFlashBrowser.Services.Downloads;
using CefFlashBrowser.Services.UserScripts;
using CefFlashBrowser.Services.UserScripts.Persistence;
using CefFlashBrowser.Singleton;
using CefFlashBrowser.Utils;
using CefSharp;
using Newtonsoft.Json;
using SimpleMvvm.Ioc;
using SimpleMvvm.Messaging;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace CefFlashBrowser
{
    internal static class Program
    {
        private static bool _restart;
        private static bool _dataInitialized;
        private static Mutex _mutex;

        private static IDatabase _database;
        private static FileLogger _logger;
        private static FavoritesService _favoritesService;
        private static FaviconService _faviconService;
        private static UserScriptService _userScriptService;
        private static DownloadManagerService _downloadManagerService;

        private const string InsufficientDirectoryPermissionsMessage =
            "CefFlashBrowser 需要对程序运行目录拥有读取、写入和删除文件的权限，无法继续运行。\r\n\r\n" +
            "程序目录：{0}\r\n\r\n" +
            "请修改目录权限、将程序移动到有读写权限的位置，或以管理员身份运行。\r\n\r\n" +
            "------------------------------------------------------------\r\n\r\n" +
            "CefFlashBrowser requires permission to read, write, and delete files in its application " +
            "directory and cannot continue.\r\n\r\n" +
            "Application directory: {0}\r\n\r\n" +
            "Please change the directory permissions, move the application to a location where it has " +
            "read and write access, or run it as an administrator.";

        [STAThread]
        private static void Main(string[] args)
        {
            string appDirectory = AppDomain.CurrentDomain.BaseDirectory;

            if (DirectoryHelper.CanReadAndWriteDirectory(appDirectory))
            {
                Run(args);
            }
            else
            {
                MessageBox.Show(
                    string.Format(InsufficientDirectoryPermissionsMessage, appDirectory),
                    "CefFlashBrowser - Insufficient Permissions",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private static void Run(string[] args)
        {
            try
            {
                InitializeDllDirectories();

                RegisterServices();
                _mutex = new Mutex(true, "CefFlashBrowser", out bool isNewInstance);

                if (isNewInstance)
                {
                    _dataInitialized = GlobalData.InitData();

                    var app = new App();
                    app.InitializeComponent();

                    SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));
                    _ = InitializeFaviconsAsync();

                    var cts = new CancellationTokenSource();
                    var delLogsTask = LogHelper.DeleteExpiredLogsAsync(cts.Token);
                    var delCachesTask = DirectoryHelper.DeletePendingDeleteDirectoriesAsync(GlobalData.CachesPath, cts.Token);
                    var delTempFilesTask = FileHelper.DeleteTempFilesAsync(GlobalData.DataPath, cts.Token);

                    InitCefFlash();
                    InitTheme();

                    LogHelper.LogInfo($"Application started successfully, pid: {Process.GetCurrentProcess().Id}");
                    LogHelper.LogInfo($"CefFlashBrowser Version: {Assembly.GetExecutingAssembly().GetName().Version}");

                    app.Run();
                    cts.Cancel();
                    WaitAllTask(delLogsTask, delCachesTask, delTempFilesTask);
                }
                else
                {
                    string json = JsonConvert.SerializeObject(args);
                    LogHelper.LogInfo($"Another instance is running, send args to it: {json}");
                    MsgReceiver.SendGlobalData(Encoding.UTF8.GetBytes(json));
                }
            }
            catch (Exception e)
            {
                LogHelper.LogWtf("Unhandled exception in Main method", e);
                WindowManager.ShowError(e.Message);
            }
            finally
            {
                try { OnTerminate(); }
                finally { UnregisterServices(); }

                if (_restart)
                {
                    Process.Start(Process.GetCurrentProcess().MainModule.FileName);
                }
            }
        }

        private static void WaitAllTask(params Task[] tasks)
        {
            try { Task.WaitAll(tasks); }
            catch (Exception e) { LogHelper.LogError("Exception occurred while waiting for tasks", e); }
        }

        private static void InitializeDllDirectories()
        {
            if (!Win32.SetDefaultDllDirectories(Win32.LOAD_LIBRARY_SEARCH_DEFAULT_DIRS))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to configure native DLL search directories.");

            foreach (string directory in new[] { GlobalData.CefDllPath, GlobalData.SqliteDllPath })
            {
                if (Win32.AddDllDirectory(directory) == IntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to add native DLL search directory '{directory}'.");
            }
        }

        private static void InitTheme()
        {
            ThemeManager.ChangeTheme(GlobalData.Settings.FollowSystemTheme ? ThemeManager.GetSystemTheme() : GlobalData.Settings.Theme);
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += UserPreferenceChangedHandler;
        }

        private static void UserPreferenceChangedHandler(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
        {
            if (e.Category == Microsoft.Win32.UserPreferenceCategory.General && GlobalData.Settings.FollowSystemTheme)
            {
                var theme = ThemeManager.GetSystemTheme();
                ThemeManager.ChangeTheme(theme);
            }
        }

        private static void InitCefFlash()
        {
            CefSharpSettings.ConcurrentTaskExecution = true;
            CefSharpSettings.WcfEnabled = false;
            Environment.SetEnvironmentVariable("ComSpec", GlobalData.CommandProxyPath); // Filter the Flash diagnostic; forward other commands to cmd.

            var settings = new CefFlashSettings()
            {
                Locale = SimpleIoc.Global.GetInstance<LanguageService>().GetLocale(GlobalData.Settings.Language),
                LogFile = GlobalData.CefLogPath,
                CachePath = GlobalData.CachesPath,
                PpapiFlashPath = GlobalData.FlashPath,
                EnableSystemFlash = true,
                BrowserSubprocessPath = GlobalData.SubprocessPath
            };

            if (GlobalData.Settings.FakeFlashVersionSetting.Enable)
            {
                settings.PpapiFlashVersion = GlobalData.Settings.FakeFlashVersionSetting.FlashVersion;
            }
            else
            {
                settings.PpapiFlashVersion = FileVersionInfo.GetVersionInfo(GlobalData.FlashPath).FileVersion.Replace(',', '.');
            }

            if (GlobalData.Settings.UserAgentSetting.EnableCustom)
            {
                settings.UserAgent = GlobalData.Settings.UserAgentSetting.UserAgent;
            }

            if (GlobalData.Settings.ProxySettings.EnableProxy)
            {
                var proxySettings = GlobalData.Settings.ProxySettings;
                CefSharpSettings.Proxy = new ProxyOptions(proxySettings.IP, proxySettings.Port, proxySettings.UserName, proxySettings.PlainPassword);
            }

            if (GlobalData.Settings.DisableGpuAcceleration)
            {
                settings.CefCommandLineArgs["disable-gpu"] = "1";
                settings.CefCommandLineArgs["disable-gpu-compositing"] = "1";
                settings.CefCommandLineArgs["disable-gpu-vsync"] = "1";
            }

#if !DEBUG
            settings.LogSeverity = LogSeverity.Error;
#endif

            settings.CefCommandLineArgs["autoplay-policy"] = "no-user-gesture-required";

            // Chromium 80+ throttles very small Flash instances through the plugin power saver,
            // so 1x1 "heartbeat" SWFs that some sites use to probe Flash availability never run.
            // Disabling the power saver lets those small instances start as well.
            settings.CefCommandLineArgs["override-plugin-power-saver-for-testing"] = "never";

            Cef.Initialize(settings);
        }

        private static void OnTerminate()
        {
            Microsoft.Win32.SystemEvents.UserPreferenceChanged -= UserPreferenceChangedHandler;
            _mutex?.Dispose();

            if (_dataInitialized)
            {
                _favoritesService?.Save();
                GlobalData.SaveData();
            }

            StopUserScriptsForExit();

            if (Cef.IsInitialized)
            {
                Cef.Shutdown();
            }

            if (_restart)
            {
                LogHelper.LogInfo("Restarting application...");
            }
            else
            {
                LogHelper.LogInfo($"Application terminated, pid: {Process.GetCurrentProcess().Id}");
            }
        }

        internal static void StopUserScriptsForExit()
        {
            // Do not resolve an unused service during failed/forwarding startup.
            var work = StopDownloadsAndScriptsAsync();

            if (!work.IsCompleted && Application.Current?.Dispatcher is Dispatcher dispatcher
                && dispatcher.CheckAccess() && !dispatcher.HasShutdownStarted)
            {
                var frame = new DispatcherFrame();

                work.GetAwaiter().OnCompleted(() => dispatcher.InvokeAsync(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
            }

            work.GetAwaiter().GetResult();
        }

        private static async Task StopDownloadsAndScriptsAsync()
        {
            if (_downloadManagerService != null)
                await _downloadManagerService.StopAsync();

            if (_userScriptService != null)
                await _userScriptService.StopAsync();
        }

        public static void Restart()
        {
            _restart = true;
            Messenger.Global.Send(MessageTokens.CLOSE_ALL_BROWSERS, null);
            StopUserScriptsForExit();
            Application.Current.Shutdown();
        }

        private static void RegisterServices()
        {
            SimpleIoc.Global.Register<ILogger>(
                () => _logger = new FileLogger(GlobalData.GetAppLogPath), Lifetime.Singleton);

            SimpleIoc.Global.Register<LanguageService>(
                () => new LanguageService(SimpleIoc.Global.GetInstance<ILogger>()), Lifetime.Singleton);

            SimpleIoc.Global.Register<IDatabase>(
                () => _database = new SqliteDatabase(GlobalData.DatabasePath), Lifetime.Singleton);

            SimpleIoc.Global.Register<DatabaseService>(
                () => new DatabaseService(SimpleIoc.Global.GetInstance<IDatabase>()), Lifetime.Singleton);

            SimpleIoc.Global.Register<DownloadManagerService>(
                () => _downloadManagerService = new DownloadManagerService(
                    SimpleIoc.Global.GetInstance<DatabaseService>(), SimpleIoc.Global.GetInstance<ILogger>(),
                    SimpleIoc.Global.GetInstance<LanguageService>(), Application.Current), Lifetime.Singleton);

            SimpleIoc.Global.Register<FavoritesService>(
                () => _favoritesService = new FavoritesService(
                    GlobalData.FavoritesPath, SimpleIoc.Global.GetInstance<ILogger>()), Lifetime.Singleton);

            SimpleIoc.Global.Register<UserScriptService>(
                () => _userScriptService = new UserScriptService(
                    new UserScriptStorage(SimpleIoc.Global.GetInstance<DatabaseService>()),
                    GlobalData.MonkeySharpPath, GlobalData.UserScriptCachePath,
                    new UserScriptContentService(GlobalData.Settings.ProxySettings),
                    new UserScriptPermissionPrompt(), LogShutdownError), Lifetime.Singleton);

            SimpleIoc.Global.Register<FaviconService>(
                () => _faviconService = new FaviconService(
                    SimpleIoc.Global.GetInstance<DatabaseService>(), SimpleIoc.Global.GetInstance<ILogger>()), Lifetime.Singleton);
        }

        private static async Task InitializeFaviconsAsync()
        {
            try { await SimpleIoc.Global.GetInstance<FaviconService>().InitializeAsync(); }
            catch (Exception e) { LogHelper.LogWtf("Failed to initialize the favicon service", e); }
        }

        private static async Task StopDatabaseAsync(Task faviconWork)
        {
            try
            {
                await faviconWork.ConfigureAwait(false);
            }
            catch (Exception e)
            {
                LogShutdownError("Failed to stop application storage consumers", e);
            }

            try
            {
                if (_database != null)
                    await _database.StopAsync().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                LogShutdownError("Failed to stop the browser database", e);
            }
            finally
            {
                try
                {
                    _logger?.Dispose();
                }
                catch { }
            }
        }

        private static void LogShutdownError(string message, Exception e)
        {
            try { _logger?.Log(LogLevel.Error, message, e); }
            catch { }
        }

        private static void UnregisterServices()
        {
            var faviconWork = _faviconService?.StopAsync() ?? Task.CompletedTask;
            var scriptWork = _userScriptService?.StopAsync() ?? Task.CompletedTask;
            var stopTask = StopDatabaseAsync(Task.WhenAll(faviconWork, scriptWork));

            if (!stopTask.Wait(TimeSpan.FromSeconds(5)))
                LogHelper.LogWtf("Timed out waiting for favicon writes; the database will close after queued operations finish.");

            SimpleIoc.Global.Unregister<DownloadManagerService>(false);
            _downloadManagerService = null;

            SimpleIoc.Global.Unregister<UserScriptService>(false);
            SimpleIoc.Global.Unregister<FaviconService>(false);
            SimpleIoc.Global.Unregister<FavoritesService>(false);
            SimpleIoc.Global.Unregister<DatabaseService>(false);
            SimpleIoc.Global.Unregister<LanguageService>(false);
            SimpleIoc.Global.Unregister<IDatabase>(false);
            SimpleIoc.Global.Unregister<ILogger>(false);
        }
    }
}
