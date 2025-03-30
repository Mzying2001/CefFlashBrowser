using CefFlashBrowser.Sol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CefFlashBrowser.ViewModels
{
    public class SolEditorWindowViewModel
    {
        private readonly SolFileWrapper _file;

        public SolNodeViewModel[] RootNodes { get; set; }

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
            RootNodes = new SolNodeViewModel[] { new SolNodeViewModel(file) };
        }

        [Obsolete("Designer only", true)]
        public SolEditorWindowViewModel()
        {
        }
    }
}
