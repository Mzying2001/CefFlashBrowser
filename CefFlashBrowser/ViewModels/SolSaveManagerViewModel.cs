using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
using SimpleMvvm;
using SimpleMvvm.Command;
using System;
using System.IO;
using System.Linq;

namespace CefFlashBrowser.ViewModels
{
    public class SolSaveManagerViewModel : ViewModelBase
    {
        public DelegateCommand ReloadWorkspacesCommand { get; }


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

        public SolSaveManagerViewModel()
        {
            ReloadWorkspacesCommand = new DelegateCommand(ReloadWorkspaces);
            ReloadWorkspaces();
        }
    }
}
