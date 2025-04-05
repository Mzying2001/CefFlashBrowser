using CefFlashBrowser.Models;
using CefFlashBrowser.Sol;
using CefFlashBrowser.Utils;
using SimpleMvvm;
using SimpleMvvm.Command;
using System;
using System.IO;
using System.Xml;

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

        public SolNodeViewModel[] RootNodes
        {
            get => new SolNodeViewModel[] { Root };
        }

        public string SolName
        {
            get => _file.SolName;
        }

        public string FilePath
        {
            get => _file.Path;
        }

        public SolVersion FileFormat
        {
            get => _file.Version;
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
        }

        [Obsolete("Designer only", true)]
        public SolEditorWindowViewModel()
        {
        }

        protected override void Init()
        {
            base.Init();
            SaveFileCommand = new DelegateCommand(SaveFileCmdImpl);
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

        public void SaveFile()
        {
            UpdateSolData();
            _file.Save();
            Status = SolEditorStatus.Saved;
        }

        private void SaveFileCmdImpl()
        {
            try
            {
                if (Status != SolEditorStatus.Ready &&
                    Status != SolEditorStatus.Saved)
                {
                    SaveFile();
                }
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
                    Filter = $"{LanguageManager.GetString("common_solFile")}|*.sol",
                    FileName = SolName
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
            var msg = string.Format(LanguageManager.GetString("message_removeItem"), target.DisplayName);

            WindowManager.Confirm(msg, callback: result =>
            {
                if (result == true)
                {
                    target.Remove();
                    Status = SolEditorStatus.Modified;
                }
            });
        }

        private void AddChild(SolNodeViewModel target)
        {
            // TODO: Implement
        }

        private void EditText(SolNodeViewModel target)
        {
            if (target.Value is string str)
            {
                WindowManager.ShowTextEditor(target.DisplayName, str,
                    verifyText: text =>
                    {
                        return true;
                    },
                    callback: (result, text) =>
                    {
                        if (result == true && str != text)
                        {
                            target.Value = text;
                            Status = SolEditorStatus.Modified;
                        }
                    });
                return;
            }

            var xmlVerfier = new Func<string, bool>(text =>
            {
                bool result = true;
                try
                {
                    new XmlDocument().LoadXml(text);
                }
                catch (XmlException e)
                {
                    WindowManager.ShowError(e.Message);
                    result = false;
                }
                return result;
            });

            if (target.Value is SolXmlDoc xmlDoc)
            {
                WindowManager.ShowTextEditor(target.DisplayName, xmlDoc.Data, xmlVerfier,
                    callback: (result, text) =>
                    {
                        if (result == true && xmlDoc.Data != text)
                        {
                            target.Value = new SolXmlDoc(text);
                            Status = SolEditorStatus.Modified;
                        }
                    });
            }
            else if (target.Value is SolXml xml)
            {
                WindowManager.ShowTextEditor(target.DisplayName, xml.Data, xmlVerfier,
                    callback: (result, text) =>
                    {
                        if (result == true && xml.Data != text)
                        {
                            target.Value = new SolXml(text);
                            Status = SolEditorStatus.Modified;
                        }
                    });
            }
        }

        private void ImportBinary(SolNodeViewModel target)
        {
            if (!(target.Value is byte[]))
            {
                return;
            }

            try
            {
                var ofd = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = $"{LanguageManager.GetString("common_allFiles")}|*.*"
                };

                if (ofd.ShowDialog() == true)
                {
                    target.Value = File.ReadAllBytes(ofd.FileName);
                    Status = SolEditorStatus.Modified;
                }
            }
            catch (Exception e)
            {
                WindowManager.ShowError(e.Message);
            }
        }

        private void ExportBinary(SolNodeViewModel target)
        {
            if (!(target.Value is byte[]))
            {
                return;
            }

            try
            {
                var sfd = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = $"{LanguageManager.GetString("common_allFiles")}|*.*",
                    FileName = target.DisplayName
                };

                if (sfd.ShowDialog() == true)
                {
                    var data = target.Value as byte[];
                    File.WriteAllBytes(sfd.FileName, data);
                }
            }
            catch (Exception e)
            {
                WindowManager.ShowError(e.Message);
            }
        }

        internal void OnNodeChanged(SolNodeViewModel node)
        {
            Status = SolEditorStatus.Modified;
        }
    }
}
