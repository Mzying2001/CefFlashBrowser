using CefFlashBrowser.Models;
using CefFlashBrowser.Utils;
using SimpleMvvm;
using SimpleMvvm.Command;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.ViewModels
{
    public class SaveMgrWorkspaceViewModel : ViewModelBase
    {
        private CancellationTokenSource
            _reloadCancellationTokenSource = new CancellationTokenSource();

        public DelegateCommand ReloadSolFilesCommand { get; }
        public DelegateCommand CancelReloadCommand { get; }
        public DelegateCommand ShowInExplorerCommand { get; }
        public DelegateCommand ExportSolCommand { get; }
        public DelegateCommand ImportSolCommand { get; }
        public DelegateCommand DeleteSolCommand { get; }
        public DelegateCommand EditSolCommand { get; }


        private string _workspaceDir;
        public string WorkspaceDir
        {
            get => _workspaceDir;
            set => UpdateValue(ref _workspaceDir, value);
        }

        private RangeObservableCollection<SolFileInfo> _solFiles;
        public RangeObservableCollection<SolFileInfo> SolFiles
        {
            get => _solFiles;
            set => UpdateValue(ref _solFiles, value);
        }


        private async Task ReloadSolFilesAsync()
        {
            var token = _reloadCancellationTokenSource.Token;
            await ReloadSolFilesAsync(token);
        }

        private async Task ReloadSolFilesAsync(CancellationToken token)
        {
            SolFiles = new RangeObservableCollection<SolFileInfo>();

            await Task.Run(async () =>
            {
                int batch = 50;
                var buffer = new List<SolFileInfo>(batch);

                void addToUI()
                {
                    if (buffer.Count > 0)
                    {
                        InvokeOnUIThread(() => SolFiles.AddRange(buffer));
                        buffer.Clear();
                    }
                }

                foreach (var solFile in EnumerateSolFiles())
                {
                    if (token.IsCancellationRequested)
                        break;
                    else
                    {
                        buffer.Add(solFile);

                        if (buffer.Count >= batch)
                            addToUI();

                        await Task.Yield();
                    }
                }

                addToUI();
            }, token);
        }

        private IEnumerable<SolFileInfo> EnumerateSolFiles()
        {
            foreach (var websitePath in Directory.GetDirectories(WorkspaceDir))
            {
                foreach (var filePath in Directory
                    .EnumerateFiles(websitePath, "*.sol", SearchOption.AllDirectories))
                {
                    yield return new SolFileInfo
                    {
                        FilePath = filePath,
                        FileName = Path.GetFileName(filePath),
                        WebsiteFolderName = Path.GetFileName(websitePath),
                        PathInWebsiteFolder = filePath.Substring(websitePath.Length)
                    };
                }
            }
        }

        public void CancelReload()
        {
            _reloadCancellationTokenSource.Cancel();
            _reloadCancellationTokenSource = new CancellationTokenSource();
        }

        private void ShowInExplorer(SolFileInfo solFile)
        {
            try
            {
                Process.Start("explorer.exe", $"/select,{solFile.FilePath}");
            }
            catch (Exception e)
            {
                LogHelper.LogError("Failed to open explorer", e);
                WindowManager.ShowError(e.Message);
            }
        }

        private void ExportSol(SolFileInfo solFile)
        {
            try
            {
                var sfd = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = solFile.FileName,
                    Filter = $"{LanguageManager.GetString("common_solFile")}|*.sol",
                };

                if (sfd.ShowDialog() == true)
                {
                    File.Copy(solFile.FilePath, sfd.FileName, true);
                    WindowManager.Alert(LanguageManager.GetString("message_exported"));
                }
            }
            catch (Exception e)
            {
                LogHelper.LogError("Failed to export sol file", e);
                WindowManager.ShowError(e.Message);
            }
        }

        private void ImportSol(SolFileInfo solFile)
        {
            try
            {
                var ofd = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = $"{LanguageManager.GetString("common_solFile")}|*.sol",
                };

                if (ofd.ShowDialog() == true)
                {
                    File.Copy(ofd.FileName, solFile.FilePath, true);
                    WindowManager.Alert(LanguageManager.GetString("message_imported"));
                }
            }
            catch (Exception e)
            {
                LogHelper.LogError("Failed to import sol file", e);
                WindowManager.ShowError(e.Message);
            }
        }

        private void DeleteSol(SolFileInfo solFile)
        {
            try
            {
                string msg = LanguageManager.GetFormattedString("message_deleteFile", solFile.FileName);

                WindowManager.Confirm(msg, callback: result =>
                {
                    if (result == true)
                    {
                        File.Delete(solFile.FilePath);
                        SolFiles.Remove(solFile);
                        WindowManager.Alert(LanguageManager.GetString("message_deleted"));
                    }
                });
            }
            catch (Exception e)
            {
                LogHelper.LogError("Failed to delete sol file", e);
                WindowManager.ShowError(e.Message);
            }
        }

        private void EditSolFile(SolFileInfo solFile)
        {
            WindowManager.ShowSolEditorWindow(solFile.FilePath);
        }

        public SaveMgrWorkspaceViewModel(string directory)
        {
            WorkspaceDir = directory;

            ReloadSolFilesCommand = new AsyncDelegateCommand(ReloadSolFilesAsync);
            CancelReloadCommand = new DelegateCommand(CancelReload);
            ShowInExplorerCommand = new DelegateCommand<SolFileInfo>(ShowInExplorer);
            ExportSolCommand = new DelegateCommand<SolFileInfo>(ExportSol);
            ImportSolCommand = new DelegateCommand<SolFileInfo>(ImportSol);
            DeleteSolCommand = new DelegateCommand<SolFileInfo>(DeleteSol);
            EditSolCommand = new DelegateCommand<SolFileInfo>(EditSolFile);

            ReloadSolFilesCommand.Execute(null);
        }
    }
}
