using CefFlashBrowser.Data;
using CefFlashBrowser.Database;
using CefFlashBrowser.FlashBrowser;
using CefFlashBrowser.Log;
using CefFlashBrowser.Services;
using CefFlashBrowser.Singleton;
using CefFlashBrowser.Utils;
using CefSharp;
using Newtonsoft.Json;
using SimpleMvvm.Ioc;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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

        private static IntPtr _sqliteLibraryHandle;
        private static IDatabase _database;
        private static FileLogger _logger;
        private static FavoritesService _favoritesService;
        private static FaviconService _faviconService;

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

            if (DirectoryAccessChecker.CanReadAndWrite(appDirectory))
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
                RegisterServices();
                _mutex = new Mutex(true, "CefFlashBrowser", out bool isNewInstance);

                Win32.SetDllDirectory(GlobalData.CefDllPath);
                AppDomain.CurrentDomain.AssemblyResolve += ResolveCefSharpAssembly;

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

        private static Assembly ResolveCefSharpAssembly(object sender, ResolveEventArgs e)
        {
            // Load CefSharp dlls
            string assemblyName = new AssemblyName(e.Name).Name;
            string assemblyPath = Path.Combine(GlobalData.CefDllPath, assemblyName + ".dll");
            return File.Exists(assemblyPath) ? Assembly.LoadFrom(assemblyPath) : null;
        }

        private static void InitCefFlash()
        {
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

        public static void Restart()
        {
            _restart = true;
            Application.Current.Shutdown();
        }

        private static void RegisterServices()
        {
            SimpleIoc.Global.Register<ILogger>(
                () => _logger = new FileLogger(GlobalData.GetAppLogPath), Lifetime.Singleton);

            SimpleIoc.Global.Register<LanguageService>(
                () => new LanguageService(SimpleIoc.Global.GetInstance<ILogger>()), Lifetime.Singleton);

            SimpleIoc.Global.Register<IDatabase>(
                DatabaseFactory, Lifetime.Singleton);

            SimpleIoc.Global.Register<DatabaseService>(
                () => new DatabaseService(SimpleIoc.Global.GetInstance<IDatabase>()), Lifetime.Singleton);

            SimpleIoc.Global.Register<FavoritesService>(
                () => _favoritesService = new FavoritesService(
                    GlobalData.FavoritesPath, SimpleIoc.Global.GetInstance<ILogger>()), Lifetime.Singleton);

            SimpleIoc.Global.Register<FaviconService>(
                () => _faviconService = new FaviconService(
                    SimpleIoc.Global.GetInstance<DatabaseService>(), SimpleIoc.Global.GetInstance<ILogger>()), Lifetime.Singleton);

            IDatabase DatabaseFactory()
            {
                InitializeSQLite();
                return _database = new SqliteDatabase(GlobalData.DatabasePath);
            }
        }

        private static void InitializeSQLite()
        {
            if (_sqliteLibraryHandle != IntPtr.Zero)
                return;

            string libraryPath = Path.Combine(GlobalData.SqliteDllPath, "SQLite.Interop.dll");

            // Keep the native module loaded until process exit, including queued database shutdown work.
            _sqliteLibraryHandle = Win32.LoadLibrary(libraryPath);

            if (_sqliteLibraryHandle == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error, $"Failed to load SQLite native library '{libraryPath}' (Win32 error {error}).");
            }
        }

        private static async Task InitializeFaviconsAsync()
        {
            try { await SimpleIoc.Global.GetInstance<FaviconService>().InitializeAsync(); }
            catch (Exception e) { LogHelper.LogWtf("Failed to initialize the favicon service", e); }
        }

        private static async Task StopDatabaseAsync(Task faviconWork)
        {
            try { await faviconWork.ConfigureAwait(false); }
            catch (Exception e) { LogShutdownError("Failed to stop the favicon service", e); }

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
                try { _logger?.Dispose(); }
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
            var stopTask = StopDatabaseAsync(faviconWork);

            if (!stopTask.Wait(TimeSpan.FromSeconds(5)))
                LogHelper.LogWtf("Timed out waiting for favicon writes; the database will close after queued operations finish.");

            SimpleIoc.Global.Unregister<FaviconService>(false);
            SimpleIoc.Global.Unregister<FavoritesService>(false);
            SimpleIoc.Global.Unregister<DatabaseService>(false);
            SimpleIoc.Global.Unregister<LanguageService>(false);
            SimpleIoc.Global.Unregister<IDatabase>(false);
            SimpleIoc.Global.Unregister<ILogger>(false);
        }
    }
}
