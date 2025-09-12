using CefFlashBrowser.Utils;
using System;
using System.ComponentModel;
using System.Windows;

namespace CefFlashBrowser.Views.Dialogs
{
    /// <summary>
    /// TextEditorDialog.xaml 的交互逻辑
    /// </summary>
    public partial class TextEditorDialog : Window
    {
        public Func<string, bool> VerifyText { get; set; } = null;


        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Text.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(TextEditorDialog), new PropertyMetadata(string.Empty));


        public TextEditorDialog()
        {
            InitializeComponent();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (e.Cancel) return;

            if (DialogHelper.GetDialogResult(this) == true)
            {
                if (VerifyText?.Invoke(Text) == false)
                {
                    e.Cancel = true;
                }
            }
        }
    }
}
