using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Sol;
using CefFlashBrowser.Utils;
using SimpleMvvm;
using SimpleMvvm.Command;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace CefFlashBrowser.ViewModels
{
    public class SolSaveManagerViewModel : ViewModelBase
    {
        public DelegateCommand ReloadWorkSpacesCommand { get; }
        public DelegateCommand ShowInExplorerCommand { get; }
        public DelegateCommand ExportSolCommand { get; }
        public DelegateCommand ImportSolCommand { get; }
        public DelegateCommand DeleteSolCommand { get; }
        public DelegateCommand EditSolCommand { get; }


        private string[] _workSpaces;
        public string[] WorkSpaces
        {
            get => _workSpaces;
            set => UpdateValue(ref _workSpaces, value);
        }

        private string _currentWorkSpace;
        public string CurrentWorkSpace
        {
            get => _currentWorkSpace;
            set
            {
                UpdateValue(ref _currentWorkSpace, value);
                SolFiles = GetSolFileInfos(value);
            }
        }

        private ObservableCollection<SolFileInfo> _solFiles;
        public ObservableCollection<SolFileInfo> SolFiles
        {
            get => _solFiles;
            set => UpdateValue(ref _solFiles, value);
        }


        private void ReloadWorkSpaces()
        {
            try
            {
                WorkSpaces = Directory.GetDirectories(GlobalData.SharedObjectsPath);
                CurrentWorkSpace = WorkSpaces.FirstOrDefault();
            }
            catch
            {
                WorkSpaces = new string[0];
                CurrentWorkSpace = null;
            }
        }

        private ObservableCollection<SolFileInfo> GetSolFileInfos(string workSpace)
        {
            var solFiles = new ObservableCollection<SolFileInfo>();
            if (workSpace == null) return solFiles;

            foreach (var website in Directory.GetDirectories(workSpace))
                AddSolFiles(website, solFiles);
            return solFiles;
        }

        private void AddSolFiles(string path, IList<SolFileInfo> list)
        {
            foreach (var file in Directory.GetFiles(path, "*.sol", SearchOption.AllDirectories))
            {
                list.Add(new SolFileInfo
                {
                    FilePath = file,
                    FileName = Path.GetFileName(file),
                    WebsiteFolder = Path.GetFileName(path),
                    PathInWebsiteFolder = file.Substring(path.Length)
                });
            }
        }

        private void ShowInExplorer(SolFileInfo solFile)
        {
            try
            {
                Process.Start("explorer.exe", $"/select,{solFile.FilePath}");
            }
            catch (Exception e)
            {
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
                    Filter = $"{LanguageManager.GetString("solSaveManager_filterSol")}|*.sol",
                };

                if (sfd.ShowDialog() == true)
                {
                    File.Copy(solFile.FilePath, sfd.FileName, true);
                    WindowManager.Alert(LanguageManager.GetString("message_exported"));
                }
            }
            catch (Exception e)
            {
                WindowManager.ShowError(e.Message);
            }
        }

        private void ImportSol(SolFileInfo solFile)
        {
            try
            {
                var ofd = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = $"{LanguageManager.GetString("solSaveManager_filterSol")}|*.sol",
                };

                if (ofd.ShowDialog() == true)
                {
                    File.Copy(ofd.FileName, solFile.FilePath, true);
                    WindowManager.Alert(LanguageManager.GetString("message_imported"));
                }
            }
            catch (Exception e)
            {
                WindowManager.ShowError(e.Message);
            }
        }

        private void DeleteSol(SolFileInfo solFile)
        {
            try
            {
                string msg = string.Format(LanguageManager.GetString("message_deleteFile"), solFile.FileName);

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
                WindowManager.ShowError(e.Message);
            }
        }

        private void EditSolFile(SolFileInfo solFile)
        {
            try
            {
                var file = SolFileWrapper.ReadFile(solFile.FilePath);
                WindowManager.ShowSolEditorWindow(file);
            }
            catch (Exception e)
            {
                WindowManager.ShowError(e.Message);
            }
        }

        public SolSaveManagerViewModel()
        {
            ReloadWorkSpacesCommand = new DelegateCommand(ReloadWorkSpaces);
            ShowInExplorerCommand = new DelegateCommand<SolFileInfo>(ShowInExplorer);
            ExportSolCommand = new DelegateCommand<SolFileInfo>(ExportSol);
            ImportSolCommand = new DelegateCommand<SolFileInfo>(ImportSol);
            DeleteSolCommand = new DelegateCommand<SolFileInfo>(DeleteSol);
            EditSolCommand = new DelegateCommand<SolFileInfo>(EditSolFile);

            ReloadWorkSpaces();
        }
    }
}
