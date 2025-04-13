using CefFlashBrowser.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;

namespace CefFlashBrowser.Views.Dialogs
{
    public partial class AddSolItemDialog : Window
    {
        public Func<string, bool> VerifyName { get; set; } = null;


        public IEnumerable<SolTypeDesc> Types
        {
            get { return (IEnumerable<SolTypeDesc>)GetValue(TypesProperty); }
            set { SetValue(TypesProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Types.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TypesProperty =
            DependencyProperty.Register(nameof(Types), typeof(IEnumerable<SolTypeDesc>), typeof(AddSolItemDialog), new PropertyMetadata(Array.Empty<SolTypeDesc>()));


        public SolTypeDesc SelectedType
        {
            get { return (SolTypeDesc)GetValue(SelectedTypeProperty); }
            set { SetValue(SelectedTypeProperty, value); }
        }

        // Using a DependencyProperty as the backing store for SelectedType.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SelectedTypeProperty =
            DependencyProperty.Register(nameof(SelectedType), typeof(SolTypeDesc), typeof(AddSolItemDialog), new PropertyMetadata(null));


        public bool IsArrayItem
        {
            get { return (bool)GetValue(IsArrayItemProperty); }
            set { SetValue(IsArrayItemProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsArrayItem.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsArrayItemProperty =
            DependencyProperty.Register(nameof(IsArrayItem), typeof(bool), typeof(AddSolItemDialog), new PropertyMetadata(false));


        public bool IsAssocArrayItem
        {
            get { return (bool)GetValue(IsAssocArrayItemProperty); }
            set { SetValue(IsAssocArrayItemProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsAssocArrayItem.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsAssocArrayItemProperty =
            DependencyProperty.Register(nameof(IsAssocArrayItem), typeof(bool), typeof(AddSolItemDialog), new PropertyMetadata(false));


        public bool CanChangeArrayType
        {
            get { return (bool)GetValue(CanChangeArrayTypeProperty); }
            set { SetValue(CanChangeArrayTypeProperty, value); }
        }

        // Using a DependencyProperty as the backing store for CanChangeArrayType.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CanChangeArrayTypeProperty =
            DependencyProperty.Register(nameof(CanChangeArrayType), typeof(bool), typeof(AddSolItemDialog), new PropertyMetadata(true));


        public string ItemNameEdit
        {
            get { return (string)GetValue(ItemNameEditProperty); }
            set { SetValue(ItemNameEditProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ItemNameEdit.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ItemNameEditProperty =
            DependencyProperty.Register(nameof(ItemNameEdit), typeof(string), typeof(AddSolItemDialog), new PropertyMetadata(string.Empty));


        /// <summary>
        /// The name of the item to be added, null if it is an dense array item.
        /// </summary>
        public string ItemName
        {
            get
            {
                if (IsArrayItem && !IsAssocArrayItem)
                {
                    return null;
                }
                else
                {
                    return ItemNameEdit;
                }
            }
            set
            {
                SetCurrentValue(ItemNameEditProperty, value);
            }
        }


        public AddSolItemDialog()
        {
            InitializeComponent();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (e.Cancel) return;

            if (DialogResult == true)
            {
                if (VerifyName?.Invoke(ItemName) == false)
                {
                    e.Cancel = true;
                }
            }
        }
    }
}
