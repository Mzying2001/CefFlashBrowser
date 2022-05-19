using CefFlashBrowser.ViewModels;
using CefFlashBrowser.WinformCefSharp4WPF;
using CefSharp;
using System.Windows;
using static CefFlashBrowser.Utils.Win32.VirtualKeys;

namespace CefFlashBrowser.FlashBrowser.Handlers
{
    public class BrowserKeyboardHandler : IKeyboardHandler
    {
        private readonly BrowserWindowViewModel viewModel;

        public BrowserKeyboardHandler()
        {
            viewModel = ((ViewModelLocator)Application.Current.Resources["Locator"]).BrowserWindowViewModel;
        }

        public bool OnKeyEvent(IWebBrowser chromiumWebBrowser, IBrowser browser, KeyType type, int windowsKeyCode, int nativeKeyCode, CefEventFlags modifiers, bool isSystemKey)
        {
            return false;
        }

        public bool OnPreKeyEvent(IWebBrowser chromiumWebBrowser, IBrowser browser, KeyType type, int windowsKeyCode, int nativeKeyCode, CefEventFlags modifiers, bool isSystemKey, ref bool isKeyboardShortcut)
        {
            if (type != KeyType.KeyUp)
                return false;

            ChromiumWebBrowser webBrowser = (ChromiumWebBrowser)chromiumWebBrowser;

            if (modifiers == CefEventFlags.None)
            {
                switch (windowsKeyCode)
                {
                    case VK_ESCAPE: //Esc
                        {
                            browser.StopLoad();
                            return true;
                        }
                    case VK_F5: //F5
                        {
                            browser.Reload();
                            return true;
                        }
                    case VK_F12: //F12
                        {
                            viewModel.ShowDevTools(chromiumWebBrowser);
                            return true;
                        }
                }
            }
            else if (modifiers == CefEventFlags.ControlDown)
            {
                switch (windowsKeyCode)
                {
                    case '0': //Ctrl+0
                        {
                            webBrowser.Dispatcher.Invoke(() =>
                            {
                                webBrowser.ZoomReset();
                            });
                            return true;
                        }
                    case 'D': //Ctrl+D
                        {
                            webBrowser.Dispatcher.Invoke(() =>
                            {
                                viewModel.AddFavorite(webBrowser);
                            });
                            return true;
                        }
                    case 'M': //Ctrl+M
                        {
                            webBrowser.Dispatcher.Invoke(() =>
                            {
                                viewModel.ShowMainWindow();
                            });
                            return true;
                        }
                    case 'O': //Ctrl+O
                        {
                            viewModel.OpenInDefaultBrowser(chromiumWebBrowser.Address);
                            return true;
                        }
                    case 'P': //Ctrl+P
                        {
                            browser.Print();
                            return true;
                        }
                    case 'U': //Ctrl+U
                        {
                            viewModel.ViewSource(chromiumWebBrowser.Address);
                            return true;
                        }
                    case 'S': //Ctrl+S
                        {
                            webBrowser.Dispatcher.Invoke(() =>
                            {
                                viewModel.CreateShortcut(webBrowser);
                            });
                            return true;
                        }
                    case 'W': //Ctrl+W
                        {
                            viewModel.CloseBrowser(chromiumWebBrowser);
                            return true;
                        }
                }
            }
            else if (modifiers == CefEventFlags.AltDown)
            {
                switch (windowsKeyCode)
                {
                    case VK_LEFT: //Alt+Left
                        {
                            browser.GoBack();
                            return true;
                        }
                    case VK_RIGHT: //Alt+Right
                        {
                            browser.GoForward();
                            return true;
                        }
                }
            }

            return false;
        }
    }
}
