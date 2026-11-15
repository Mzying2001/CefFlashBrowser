using SimpleMvvm;
using SimpleMvvm.Command;
using System;

namespace CefFlashBrowser.ViewModels
{
    public enum UserScriptDownloadState
    {
        Waiting,
        Downloading,
        Completed,
        Cancelled,
        TimedOut,
        Failed
    }

    public sealed class UserScriptDownloadItem : ViewModelBase
    {
        private string _fileName;
        public string FileName
        {
            get => _fileName;
            internal set => UpdateValue(ref _fileName, value);
        }
        private long _received;
        public long Received
        {
            get => _received;
            internal set => UpdateValue(ref _received, value);
        }
        private UserScriptDownloadState _state;
        public UserScriptDownloadState State
        {
            get => _state;
            internal set
            {
                if (UpdateValue(ref _state, value))
                    CancelCommand.CanExecute = value == UserScriptDownloadState.Waiting || value == UserScriptDownloadState.Downloading;
            }
        }
        public DelegateCommand CancelCommand { get; }

        public UserScriptDownloadItem(string name, Action cancel)
        {
            _fileName = name;
            CancelCommand = new DelegateCommand(cancel);
        }
    }
}
