using CefFlashBrowser.Sol;
using SimpleMvvm;
using SimpleMvvm.Command;
using System;

namespace CefFlashBrowser.ViewModels
{
    public class SolEditorWindowViewModel : ViewModelBase
    {
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
            RemoveItemCommand = new DelegateCommand<SolNodeViewModel>(RemoveItem);
            AddChildCommand = new DelegateCommand<SolNodeViewModel>(AddChild);
            EditTextCommand = new DelegateCommand<SolNodeViewModel>(EditText);
            ImportBinaryCommand = new DelegateCommand<SolNodeViewModel>(ImportBinary);
            ExportBinaryCommand = new DelegateCommand<SolNodeViewModel>(ExportBinary);
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
    }
}
