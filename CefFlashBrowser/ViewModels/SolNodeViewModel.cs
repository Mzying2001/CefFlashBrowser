using CefFlashBrowser.Models;
using CefFlashBrowser.Sol;
using CefFlashBrowser.Utils;
using SimpleMvvm;
using SimpleMvvm.Command;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CefFlashBrowser.ViewModels
{
    public class SolNodeViewModel : ViewModelBase
    {
        public DelegateCommand RemoveCommand { get; set; }
        public DelegateCommand AddChildComman { get; set; }


        public SolNodeViewModel Parent { get; }

        private object _name;
        public object Name
        {
            get => _name;
            set => UpdateValue(ref _name, value);
        }

        private object _value;
        public object Value
        {
            get => _value;
            set
            {
                UpdateValue(ref _value, value);
                RaisePropertyChanged(nameof(ValueType));
                UpdateChildren();
                Parent?.OnChildrenValueChanged(this);
            }
        }

        private ObservableCollection<SolNodeViewModel> _children;
        public ObservableCollection<SolNodeViewModel> Children
        {
            get => _children;
            set => UpdateValue(ref _children, value);
        }


        private void UpdateChildren()
        {
            var children = new ObservableCollection<SolNodeViewModel>();

            if (Value is SolFileWrapper file)
            {
                foreach (var pair in SolHelper.GetAllValues(file))
                {
                    children.Add(new SolNodeViewModel(this, pair.Key, pair.Value));
                }
            }
            else if (Value is SolArray arr)
            {
                foreach (var pair in arr.AssocPortion)
                {
                    children.Add(new SolNodeViewModel(this, pair.Key, pair.Value));
                }
                for (int i = 0; i < arr.DensePortion.Count; i++)
                {
                    children.Add(new SolNodeViewModel(this, i, arr.DensePortion[i]));
                }
            }
            else if (Value is SolObject obj)
            {
                foreach (var pair in obj.Properties)
                {
                    children.Add(new SolNodeViewModel(this, pair.Key, pair.Value));
                }
            }

            Children = children;
        }

        private void OnChildrenValueChanged(SolNodeViewModel node)
        {
            if (Value is SolArray arr)
            {
                if (node.Name is string key)
                {
                    arr.AssocPortion[key] = node.Value;
                }
                else if (node.Name is int index)
                {
                    arr.DensePortion[index] = node.Value;
                }
            }
            else if (Value is SolObject obj)
            {
                obj.Properties[node.Name.ToString()] = node.Value;
            }
        }

        public Dictionary<string, object> GetAllValues()
        {
            return Children.ToDictionary(x => x.Name.ToString(), x => x.Value);
        }

        public void Remove()
        {
            if (Parent == null)
            {
                return;
            }

            if (Parent.Value is SolArray arr)
            {
                if (Name is string key)
                {
                    arr.AssocPortion.Remove(key);
                }
                else if (Name is int index)
                {
                    arr.DensePortion.RemoveAt(index);
                }
            }
            else if (Parent.Value is SolObject obj)
            {
                obj.Properties.Remove(Name.ToString());
            }

            Parent.Children.Remove(this);
        }

        public void AddChild(object name, object value)
        {
            if (Value is SolArray arr)
            {
                if (name is string key)
                {
                    arr.AssocPortion[key] = value;
                }
                else if (name is int index)
                {
                    arr.DensePortion.Insert(index, value);
                }
            }
            else if (Value is SolObject obj)
            {
                obj.Properties[name.ToString()] = value;
            }

            Children.Add(new SolNodeViewModel(this, name, value));
        }

        public void AddChild()
        {
            // TODO
        }

        public SolNodeViewModel(SolNodeViewModel parent, object name, object value) : this()
        {
            Parent = parent;
            Name = name;
            Value = value;
        }

        public SolNodeViewModel(SolFileWrapper file) : this()
        {
            Parent = null;
            Name = file.SolName;
            Value = file;
        }

        private SolNodeViewModel()
        {
            RemoveCommand = new DelegateCommand(Remove);
            AddChildComman = new DelegateCommand(AddChild);
        }
    }
}
