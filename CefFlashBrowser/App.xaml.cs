using CefFlashBrowser.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace CefFlashBrowser
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        public App() : base()
        {
            InitializeComponent();

            Settings.Init();
            FlashBrowser.InitCefFlash();

            LanguageManager.CurrentLanguage = Settings.Language;
        }
    }
}
