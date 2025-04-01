using CefFlashBrowser.Models;
using CefFlashBrowser.Sol;
using CefFlashBrowser.Utils;
using SimpleMvvm;
using SimpleMvvm.Command;
using System;

namespace CefFlashBrowser.ViewModels
{
    public class SolEditorWindowViewModel : ViewModelBase
    {
        public DelegateCommand SaveFileCommand { get; set; }
        public DelegateCommand SaveAsFileCommand { get; set; }
        public DelegateCommand RemoveItemCommand { get; set; }
        public DelegateCommand AddChildCommand { get; set; }
        public DelegateCommand EditTextCommand { get; set; }
        public DelegateCommand ImportBinaryCommand { get; set; }
        public DelegateCommand ExportBinaryCommand { get; set; }


        private readonly SolFileWrapper _file;
        public SolNodeViewModel Root { get; }
        public SolNodeViewModel[] RootNodes { get; }

        public string SolName
        {
            get => _file.SolName;
        }

        public string FilePath
        {
            get => _file.Path;
        }

        private SolEditorStatus _status = SolEditorStatus.Ready;
        public SolEditorStatus Status
        {
            get => _status;
            set => UpdateValue(ref _status, value);
        }


        public SolEditorWindowViewModel(SolFileWrapper file)
        {
            _file = file;
            Root = new SolNodeViewModel(this, file);
            RootNodes = new SolNodeViewModel[] { Root };
        }

        [Obsolete("Designer only", true)]
        public SolEditorWindowViewModel()
        {
        }

        protected override void Init()
        {
            base.Init();
            SaveFileCommand = new DelegateCommand(SaveFile);
            SaveAsFileCommand = new DelegateCommand(SaveAsFile);
            RemoveItemCommand = new DelegateCommand<SolNodeViewModel>(RemoveItem);
            AddChildCommand = new DelegateCommand<SolNodeViewModel>(AddChild);
            EditTextCommand = new DelegateCommand<SolNodeViewModel>(EditText);
            ImportBinaryCommand = new DelegateCommand<SolNodeViewModel>(ImportBinary);
            ExportBinaryCommand = new DelegateCommand<SolNodeViewModel>(ExportBinary);
        }

        private void UpdateSolData()
        {
            SolHelper.SetAllValues(_file, Root.GetAllValues());
        }

        private void SaveFile()
        {
            try
            {
                UpdateSolData();
                _file.Save();
                Status = SolEditorStatus.Saved;
            }
            catch (Exception e)
            {
                WindowManager.ShowError(e.Message);
            }
        }

        private void SaveAsFile()
        {
            string oldPath = _file.Path;

            try
            {
                var sfd = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Sol files|*.sol",
                    FileName = _file.SolName
                };

                if (sfd.ShowDialog() == true)
                {
                    UpdateSolData();
                    _file.Path = sfd.FileName;
                    _file.Save();
                    RaisePropertyChanged(nameof(FilePath));
                    Status = SolEditorStatus.Saved;
                }
            }
            catch (Exception e)
            {
                _file.Path = oldPath;
                WindowManager.ShowError(e.Message);
            }
        }

        private void RemoveItem(SolNodeViewModel target)
        {
            target.Remove();
        }

        private void AddChild(SolNodeViewModel target)
        {
            // TODO: Implement
        }

        private void EditText(SolNodeViewModel target)
        {
            // TODO: Implement
        }

        private void ImportBinary(SolNodeViewModel target)
        {
            // TODO: Implement
        }

        private void ExportBinary(SolNodeViewModel target)
        {
            // TODO: Implement
        }

        internal void OnNodeChanged(SolNodeViewModel node)
        {
            Status = SolEditorStatus.Modified;
        }
    }
}
