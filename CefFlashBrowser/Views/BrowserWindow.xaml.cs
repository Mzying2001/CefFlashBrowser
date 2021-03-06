﻿using CefFlashBrowser.Models;
using CefFlashBrowser.Models.StaticData;
using CefFlashBrowser.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace CefFlashBrowser.Views
{
    /// <summary>
    /// BrowserWindow.xaml 的交互逻辑
    /// </summary>
    public partial class BrowserWindow : Window
    {
        BrowserWindowViewModel VModel => (BrowserWindowViewModel)DataContext;

        public BrowserWindow()
        {
            InitializeComponent();
            VModel.SetBrowser(browser);
            VModel.CloseWindow = Close;

            var x = Settings.BrowserWindowX;
            var y = Settings.BrowserWindowY;
            var w = Settings.BrowserWindowWidth;
            var h = Settings.BrowserWindowHeight;
            if (x != default &&
                y != default &&
                w != default &&
                h != default)
            {
                Left = x;
                Top = y;
                Width = w;
                Height = h;
            }
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Settings.BrowserWindowX = Left;
            Settings.BrowserWindowY = Top;
            Settings.BrowserWindowWidth = Width;
            Settings.BrowserWindowHeight = Height;
        }

        private void MenuButton_Clicked(object sender, RoutedEventArgs e)
        {
            menuButtonContextMenu.PlacementTarget = sender as UIElement;
            menuButtonContextMenu.IsOpen = true;
        }

        public static void Show(string address)
        {
            var window = new BrowserWindow();
            window.browser.Address = address;
            window.Show();
        }
    }
}
