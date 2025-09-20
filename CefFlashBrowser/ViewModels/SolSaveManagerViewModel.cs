using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
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
            set => UpdateValue(ref _currentWorkspace, value);
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
                CurrentWorkspace = Workspaces.FirstOrDefault();
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

        public SolSaveManagerViewModel()
        {
            ReloadWorkspacesCommand = new DelegateCommand(ReloadWorkspaces);
            FilterCommand = new DelegateCommand<FilterEventArgs>(Filter);
            ReloadWorkspaces();
        }
    }
}
