using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace CefFlashBrowser.Views
{
    public partial class BrowserWindow
    {
        private bool _featureButtonsAdded = false;
        private ContextMenu _inputMemoryMenu;

        private void AddFeatureButtons()
        {
            if (_featureButtonsAdded || findPopup == null)
            {
                return;
            }

            var toolbar = FindParent<StackPanel>(findPopup);
            if (toolbar == null)
            {
                return;
            }

            var insertIndex = toolbar.Children.IndexOf(findPopup);
            if (insertIndex < 0)
            {
                insertIndex = toolbar.Children.Count;
            }

            toolbar.Children.Insert(insertIndex, CreateFlashAccelerationButton());
            toolbar.Children.Insert(insertIndex + 1, CreateInputMemoryButton());
            _featureButtonsAdded = true;
        }

        private Button CreateFlashAccelerationButton()
        {
            var button = CreateToolbarButton("⚡", "Flash加速");
            button.Click += delegate
            {
                browser.EnableFlashAcceleration();
                Keyboard.Focus(browser);
            };
            return button;
        }

        private Button CreateInputMemoryButton()
        {
            var button = CreateToolbarButton("⌨", "键鼠记忆");
            button.Click += delegate
            {
                if (_inputMemoryMenu == null)
                {
                    _inputMemoryMenu = CreateInputMemoryMenu();
                }
                OpenBottomContextMenu(button, _inputMemoryMenu);
            };
            return button;
        }

        private Button CreateToolbarButton(string icon, string tooltip)
        {
            return new Button
            {
                Width = 30,
                Height = 30,
                Padding = new Thickness(0),
                Margin = new Thickness(10, 0, 0, 0),
                ToolTip = tooltip,
                Content = new TextBlock
                {
                    Text = icon,
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                }
            };
        }

        private ContextMenu CreateInputMemoryMenu()
        {
            var menu = new ContextMenu
            {
                VerticalOffset = -8,
                HorizontalOffset = 8
            };

            menu.Items.Add(CreateInputMemoryMenuItem("开始记录", delegate
            {
                browser.StartInputMemoryRecording();
                Keyboard.Focus(browser);
            }));

            menu.Items.Add(CreateInputMemoryMenuItem("停止记录", delegate
            {
                browser.StopInputMemoryRecording();
                Keyboard.Focus(browser);
            }));

            menu.Items.Add(new Separator());

            menu.Items.Add(CreateInputMemoryMenuItem("回放记录", delegate
            {
                browser.ReplayInputMemory();
            }));

            menu.Items.Add(CreateInputMemoryMenuItem("清空记录", delegate
            {
                browser.ClearInputMemory();
                Keyboard.Focus(browser);
            }));

            return menu;
        }

        private MenuItem CreateInputMemoryMenuItem(string header, RoutedEventHandler click)
        {
            var item = new MenuItem { Header = header };
            item.Click += click;
            return item;
        }

        private static T FindParent<T>(DependencyObject element) where T : DependencyObject
        {
            while (element != null)
            {
                element = LogicalTreeHelper.GetParent(element);
                if (element is T result)
                {
                    return result;
                }
            }
            return null;
        }

        private void BrowserWindowLoadedFeatureButtons(object sender, RoutedEventArgs e)
        {
            AddFeatureButtons();
        }
    }
}
