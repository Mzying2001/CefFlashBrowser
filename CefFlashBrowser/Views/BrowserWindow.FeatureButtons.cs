using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace CefFlashBrowser.Views
{
    public partial class BrowserWindow
    {
        private bool _featureButtonsAdded = false;
        private ContextMenu _speedGearMenu;
        private ContextMenu _inputMemoryMenu;

        static BrowserWindow()
        {
            EventManager.RegisterClassHandler(
                typeof(BrowserWindow),
                LoadedEvent,
                new RoutedEventHandler(OnBrowserWindowLoadedFeatureButtons));
        }

        private static void OnBrowserWindowLoadedFeatureButtons(object sender, RoutedEventArgs e)
        {
            if (sender is BrowserWindow window)
            {
                window.AddFeatureButtons();
            }
        }

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

            toolbar.Children.Insert(insertIndex, CreateSpeedGearButton());
            toolbar.Children.Insert(insertIndex + 1, CreateInputMemoryButton());
            _featureButtonsAdded = true;
        }

        private Button CreateSpeedGearButton()
        {
            var button = CreateToolbarButton("⏩", "变速齿轮");
            button.Click += delegate
            {
                if (_speedGearMenu == null)
                {
                    _speedGearMenu = CreateSpeedGearMenu();
                }
                OpenBottomContextMenu(button, _speedGearMenu);
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

        private ContextMenu CreateSpeedGearMenu()
        {
            var menu = new ContextMenu
            {
                VerticalOffset = -8,
                HorizontalOffset = 8
            };

            menu.Items.Add(CreateSpeedGearMenuItem("0.5x", 0.5));
            menu.Items.Add(CreateSpeedGearMenuItem("1x", 1.0));
            menu.Items.Add(CreateSpeedGearMenuItem("2x", 2.0));
            menu.Items.Add(CreateSpeedGearMenuItem("5x", 5.0));
            menu.Items.Add(CreateSpeedGearMenuItem("10x", 10.0));
            menu.Items.Add(CreateSpeedGearMenuItem("20x", 20.0));
            menu.Items.Add(CreateSpeedGearMenuItem("50x", 50.0));
            menu.Items.Add(CreateSpeedGearMenuItem("100x", 100.0));

            return menu;
        }

        private MenuItem CreateSpeedGearMenuItem(string header, double factor)
        {
            var item = new MenuItem { Header = header };
            item.Click += delegate
            {
                browser.SetSpeedGearFactor(factor);
                Keyboard.Focus(browser);
            };
            return item;
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
    }
}
