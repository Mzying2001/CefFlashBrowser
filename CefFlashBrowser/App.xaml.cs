using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Singleton;
using CefFlashBrowser.Utils;
using Newtonsoft.Json;
using System;
using System.Windows;

namespace CefFlashBrowser
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private bool _started = false;
        private readonly MsgReceiver _msgReceiver;

        public App()
        {
            _msgReceiver = new MsgReceiver();
            _msgReceiver.ReceivedData += ReceivedData;
        }

        private void ReceivedData(byte[] data)
        {
            try
            {
                string json = System.Text.Encoding.UTF8.GetString(data);
                LogHelper.LogInfo($"Received data from another instance, data: {json}");

                var args = JsonConvert.DeserializeObject<string[]>(json);
                if (_started) ExecuteArguments(args);
            }
            catch (Exception e)
            {
                LogHelper.LogError("Failed to process received data", e);
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            if (GlobalData.Settings.FirstStart)
            {
                if (WindowManager.ShowSelectLanguageDialog())
                {
                    GlobalData.Settings.FirstStart = false;
                    WindowManager.ShowMainWindow();
                }
                else
                {
                    Shutdown();
                    return;
                }
            }
            else if (e.Args.Length == 0)
            {
                WindowManager.ShowMainWindow();
            }
            else
            {
                ExecuteArguments(e.Args);
                GlobalData.IsStartWithoutMainWindow = true;
            }

            ShutdownMode = ShutdownMode.OnLastWindowClose;
            _started = true;
        }

        private void ExecuteArguments(string[] args)
        {
            if (args.Length == 0)
            {
                WindowManager.ShowMainWindow();
                GlobalData.IsStartWithoutMainWindow = false;
            }
            else
            {
                foreach (var arg in args)
                {
                    if (UrlHelper.IsLocalSwfFile(arg))
                    {
                        WindowManager.ShowSwfPlayer(arg);
                    }
                    else if (UrlHelper.IsLocalSolFile(arg))
                    {
                        WindowManager.ShowSolEditorWindow(arg);
                    }
                    else // open browser for other urls
                    {
                        WindowManager.ShowBrowser(arg);
                    }
                }
            }
        }
    }
}
