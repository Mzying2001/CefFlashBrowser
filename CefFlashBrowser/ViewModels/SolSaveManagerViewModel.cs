using CefFlashBrowser.Data;
using CefFlashBrowser.Models;
using CefFlashBrowser.Utils;
using SimpleMvvm;
using SimpleMvvm.Command;
using System;
using System.IO;
using System.Linq;
using System.Windows.Data;

namespace CefFlashBrowser.ViewModels
{
    public class SolSaveManagerViewModel : ViewModelBase
    {
        private string[] _filterKeywords = Array.Empty<string>();

        public DelegateCommand ReloadWorkspacesCommand { get; }
        public DelegateCommand FilterCommand { get; }


        private SaveMgrWorkspaceViewModel[] _workspaces;
        public SaveMgrWorkspaceViewModel[] Workspaces
        {
            get => _workspaces;
            set => UpdateValue(ref _workspaces, value);
        }

        private SaveMgrWorkspaceViewModel _currentWorkspace;
        public SaveMgrWorkspaceViewModel CurrentWorkspace
        {
            get => _currentWorkspace;
            set
            {
                UpdateValue(ref _currentWorkspace, value);
                value?.SolFiles.Refresh();
            }
        }

        private string _filterText = string.Empty;
        public string FilterText
        {
            get => _filterText;
            set
            {
                if (UpdateValue(ref _filterText, value))
                {
                    UpdateFilterKeywords();
                    CurrentWorkspace?.SolFiles.Refresh();
                }
            }
        }


        private void ReloadWorkspaces()
        {
            try
            {
                Workspaces = Directory.GetDirectories(GlobalData.SharedObjectsPath)
                    .Select(dir => new SaveMgrWorkspaceViewModel(dir)).ToArray();
                CurrentWorkspace = GetMostRecentWorkspace(Workspaces);
            }
            catch (Exception e)
            {
                Workspaces = Array.Empty<SaveMgrWorkspaceViewModel>();
                CurrentWorkspace = null;
                LogHelper.LogError("Failed to load workspaces", e);
            }
        }

        private void UpdateFilterKeywords()
        {
            if (string.IsNullOrWhiteSpace(FilterText))
            {
                _filterKeywords = Array.Empty<string>();
            }
            else
            {
                _filterKeywords = FilterText
                    .Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        private void Filter(FilterEventArgs e)
        {
            if (e.Item is SolFileInfo solFileInfo)
            {
                if (_filterKeywords.Length == 0)
                {
                    e.Accepted = true;
                    return;
                }

                foreach (var keyword in _filterKeywords)
                {
                    if (solFileInfo.FileName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        solFileInfo.WebsiteFolderName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        e.Accepted = true;
                        return;
                    }
                }
            }

            e.Accepted = false;
        }

        private static SaveMgrWorkspaceViewModel GetMostRecentWorkspace(
            SaveMgrWorkspaceViewModel[] workspaces)
        {
            if (workspaces == null || workspaces.Length == 0)
                return null;

            SaveMgrWorkspaceViewModel best = workspaces[0];
            DateTime bestTime = GetDirectoryLastWriteTimeRecursive(best.WorkspaceDir);

            for (int i = 1; i < workspaces.Length; i++)
            {
                DateTime time = GetDirectoryLastWriteTimeRecursive(workspaces[i].WorkspaceDir);

                if (time > bestTime)
                {
                    bestTime = time;
                    best = workspaces[i];
                }
            }

            return best;
        }

        private static DateTime GetDirectoryLastWriteTimeRecursive(string dirPath)
        {
            var dirInfo = new DirectoryInfo(dirPath);
            DateTime latest = dirInfo.LastWriteTime;

            try
            {
                foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    if (file.LastWriteTime > latest)
                        latest = file.LastWriteTime;
                }
            }
            catch
            {
                // fall back to directory timestamp if files cannot be enumerated
            }

            return latest;
        }

        public SolSaveManagerViewModel()
        {
            ReloadWorkspacesCommand = new DelegateCommand(ReloadWorkspaces);
            FilterCommand = new DelegateCommand<FilterEventArgs>(Filter);
            ReloadWorkspaces();
        }
    }
}
