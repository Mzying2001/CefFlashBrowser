﻿using CefFlashBrowser.Models;
using CefFlashBrowser.Sol;
using CefFlashBrowser.Utils;
using SimpleMvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CefFlashBrowser.ViewModels
{
    public class SolNodeViewModel : ViewModelBase
    {
        public SolEditorWindowViewModel Editor { get; }
        public SolNodeViewModel Parent { get; }

        private object _name;
        public object Name
        {
            get => _name;
            set
            {
                if (!EqualityComparer<object>.Default.Equals(_name, value))
                {
                    if (Parent?.Value is SolArray arr)
                    {
                        if (value is string key)
                        {
                            if (arr.AssocPortion.ContainsKey(key))
                                throw new ArgumentException(LanguageManager.GetFormattedString("error_arrKeyAreadyExists", key));
                        }
                        else if (value is int index)
                        {
                            if (index < 0 || index >= arr.DensePortion.Count)
                                throw new IndexOutOfRangeException();
                        }
                    }
                    else if (Parent?.Value is SolFileWrapper || Parent?.Value is SolObject)
                    {
                        if (value is string key)
                        {
                            if (Parent.Children.Any(node => key.Equals(node.Name)))
                                throw new ArgumentException(LanguageManager.GetFormattedString("error_objPropAreadyExists", key));
                        }
                    }
                    var oldName = _name;
                    _name = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(DisplayName));
                    Parent?.OnChildrenNameChanged(this, oldName);
                    Editor?.OnNodeChanged(SolNodeChangeType.NameChanged, this);
                }
            }
        }

        private object _value;
        public object Value
        {
            get => _value;
            set
            {
                if (!EqualityComparer<object>.Default.Equals(_value, value))
                {
                    UpdateValue(ref _value, value);
                    RaisePropertyChanged(nameof(TypeString));
                    UpdateChildren();
                    Parent?.OnChildrenValueChanged(this);
                    Editor?.OnNodeChanged(SolNodeChangeType.ValueChanged, this);
                }
            }
        }

        private bool _isRemoved = false;
        public bool IsRemoved
        {
            get => _isRemoved;
            private set => UpdateValue(ref _isRemoved, value);
        }

        private ObservableCollection<SolNodeViewModel> _children;
        public ObservableCollection<SolNodeViewModel> Children
        {
            get => _children;
            set => UpdateValue(ref _children, value);
        }

        public string TypeString
        {
            get => SolHelper.GetTypeString(Value);
        }

        public bool IsArrayItem
        {
            get => Parent != null && Parent.Value is SolArray;
        }

        public string DisplayName
        {
            get
            {
                string name = Name == null ? "null" : Name.ToString();
                return IsArrayItem ? $"[{name}]" : name;
            }
        }

        public bool CanAddChild
        {
            get => Value is SolFileWrapper || Value is SolArray || Value is SolObject;
        }

        public bool CanRemove
        {
            get => Parent != null && !IsRemoved;
        }

        public bool CanRename
        {
            get => Name is string;
        }

        public bool CanMove
        {
            get => IsArrayItem && Name is int;
        }


        public void UpdateChildren()
        {
            var children = new ObservableCollection<SolNodeViewModel>();

            if (Value is SolFileWrapper file)
            {
                foreach (var pair in SolHelper.GetAllValues(file))
                {
                    children.Add(new SolNodeViewModel(Editor, this, pair.Key, pair.Value));
                }
            }
            else if (Value is SolArray arr)
            {
                foreach (var pair in arr.AssocPortion)
                {
                    children.Add(new SolNodeViewModel(Editor, this, pair.Key, pair.Value));
                }
                for (int i = 0; i < arr.DensePortion.Count; i++)
                {
                    children.Add(new SolNodeViewModel(Editor, this, i, arr.DensePortion[i]));
                }
            }
            else if (Value is SolObject obj)
            {
                foreach (var pair in obj.Properties)
                {
                    children.Add(new SolNodeViewModel(Editor, this, pair.Key, pair.Value));
                }
            }

            Children = children;
        }

        private void UpdateDensePortionNodeName()
        {
            if (Value is SolArray)
            {
                int index = 0;

                foreach (var node in Children)
                {
                    if (node.Name is int)
                    {
                        node._name = index++;
                        node.RaisePropertyChanged(nameof(Name));
                        node.RaisePropertyChanged(nameof(DisplayName));
                    }
                }
            }
        }

        protected virtual void OnChildrenValueChanged(SolNodeViewModel node)
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

        protected virtual void OnChildrenNameChanged(SolNodeViewModel node, object oldName)
        {
            if (Value is SolArray arr)
            {
                if (oldName is string key)
                {
                    arr.AssocPortion.Remove(key);
                    arr.AssocPortion[node.Name.ToString()] = node.Value;
                }
                else
                {
                    int index = (int)oldName;
                    int offset = (int)node.Name - index;

                    arr.DensePortion.RemoveAt(index);
                    arr.DensePortion.Insert(index + offset, node.Value);

                    // make sure the order is correct
                    int nodeIndex = Children.IndexOf(node);
                    Children.RemoveAt(nodeIndex);
                    Children.Insert(nodeIndex + offset, node);

                    UpdateDensePortionNodeName();
                }
            }
            else if (Value is SolObject obj)
            {
                string key = (string)oldName;
                obj.Properties.Remove(key);
                obj.Properties[node.Name.ToString()] = node.Value;
            }
        }

        public Dictionary<string, object> GetAllValues()
        {
            return Children.ToDictionary(x => x.Name.ToString(), x => x.Value);
        }

        /// <summary>
        /// Adds a new item to the current node.<br/>
        /// When the current node is Array, value with string key is added to the associative portion,
        /// otherwise it is added to the end of the dense portion.
        /// </summary>
        public void AddChild(object name, object value)
        {
            if (!CanAddChild) // Value is not SolFileWrapper, SolArray or SolObject
                return;

            SolNodeViewModel node = null;
            int insertIndex = 0;

            if (Value is SolFileWrapper)
            {
                node = new SolNodeViewModel(Editor, this, name, value);
            }
            else if (Value is SolArray arr)
            {
                if (name is string key)
                {
                    arr.AssocPortion.Add(key, value);
                    node = new SolNodeViewModel(Editor, this, key, value);
                }
                else
                {
                    // always insert at the end
                    int index = arr.DensePortion.Count;
                    arr.DensePortion.Insert(index, value);

                    insertIndex = Children.Count;
                    node = new SolNodeViewModel(Editor, this, index, value);
                }
            }
            else if (Value is SolObject obj)
            {
                obj.Properties.Add(name.ToString(), value);
                node = new SolNodeViewModel(Editor, this, name, value);
            }

            if (node != null)
            {
                Children.Insert(insertIndex, node);
                Editor?.OnNodeChanged(SolNodeChangeType.Added, node);
            }
        }

        public void Remove()
        {
            if (!CanRemove) // Parent is null or already removed
                return;

            bool updateSiblingNames = false;

            if (Parent.Value is SolFileWrapper)
            {
                // do nothing
            }
            else if (Parent.Value is SolArray arr)
            {
                if (Name is string key)
                {
                    arr.AssocPortion.Remove(key);
                }
                else if (Name is int index)
                {
                    updateSiblingNames = true;
                    arr.DensePortion.RemoveAt(index);
                }
            }
            else if (Parent.Value is SolObject obj)
            {
                obj.Properties.Remove(Name.ToString());
            }

            Parent.Children.Remove(this);
            IsRemoved = true;

            if (updateSiblingNames)
                Parent.UpdateDensePortionNodeName();

            Editor?.OnNodeChanged(SolNodeChangeType.Removed, this);
        }

        public SolNodeViewModel(SolEditorWindowViewModel editor, SolNodeViewModel parent, object name, object value)
        {
            Editor = editor;
            Parent = parent;
            _name = name;
            _value = value;
            UpdateChildren();
        }

        public SolNodeViewModel(SolEditorWindowViewModel editor, SolFileWrapper file)
        {
            Editor = editor;
            Parent = null;
            _name = file.SolName;
            _value = file;
            UpdateChildren();
        }
    }
}
