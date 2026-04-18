using CefFlashBrowser.Data;
using CefFlashBrowser.Singleton;
using CefFlashBrowser.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace CefFlashBrowser
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private bool _started = false;
        private readonly MsgReceiver _msgReceiver;
        private readonly object _pendingArgsLock = new object();
        private readonly Queue<string[]> _pendingArgs = new Queue<string[]>();

        public App()
        {
            _msgReceiver = new MsgReceiver();
            _msgReceiver.ReceivedData += ReceivedDataHandler;
        }

        private void ReceivedDataHandler(object sender, ReceivedDataEventArgs e)
        {
            try
            {
                string json = System.Text.Encoding.UTF8.GetString(e.Data);
                LogHelper.LogInfo($"Received data from another instance, data: {json}");

                if (!(JsonConvert.DeserializeObject<string[]>(json) is string[] args))
                {
                    return;
                }

                // If OnStartup has not finished yet, queue the args and let the
                // startup path drain them once the main UI is up. Previously
                // these args were silently dropped, so a second instance that
                // sent "open this SWF" while the first instance was still
                // initialising did nothing.
                if (!_started)
                {
                    lock (_pendingArgsLock)
                    {
                        if (!_started)
                        {
                            _pendingArgs.Enqueue(args);
                            return;
                        }
                    }
                }

                ExecuteArguments(args);
            }
            catch (Exception ex)
            {
                LogHelper.LogError("Failed to process received data", ex);
            }
        }

        private void DrainPendingArgs()
        {
            string[][] toExecute;
            lock (_pendingArgsLock)
            {
                _started = true;
                toExecute = _pendingArgs.ToArray();
                _pendingArgs.Clear();
            }

            foreach (var args in toExecute)
            {
                try
                {
                    ExecuteArguments(args);
                }
                catch (Exception ex)
                {
                    LogHelper.LogError("Failed to process queued startup args", ex);
                }
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            if (GlobalData.Settings.DisableGpuAccelerationWPF)
            {
                RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
            }

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
            DrainPendingArgs();
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
