using CefFlashBrowser.ViewModels;
using CefSharp;
using Mzying2001.MonkeySharp.CefSharp;
using Mzying2001.MonkeySharp.Core.Apis;
using Mzying2001.MonkeySharp.Core.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;

namespace CefFlashBrowser.Browser.UserScripts
{
    public sealed class UserScriptMenuService : IMenuService, IDisposable
    {
        private readonly object _lock = new object();
        private readonly Dictionary<Tuple<string, string, string, string>, Group> _groups = new Dictionary<Tuple<string, string, string, string>, Group>();
        private readonly BrowserViewModel _model;
        private readonly Dispatcher _dispatcher;
        private readonly IUserScriptRepository _repository;
        private readonly Func<CefSharpUserScriptHost> _host;
        private bool _disposed;
        private readonly Action _reportFailure;

        public UserScriptMenuService(BrowserViewModel model, Dispatcher dispatcher, IUserScriptRepository repository,
            Func<CefSharpUserScriptHost> host, Action reportFailure = null)
        {
            _model = model;
            _dispatcher = dispatcher;
            _repository = repository;
            _host = host;
            _reportFailure = reportFailure;
        }

        public async Task<IMenuRegistration> RegisterAsync(MenuCommandRequest request,
            Action<MenuCommandInvocation> invoked, CancellationToken token)
        {
            var installation = await _repository.GetAsync(request.ScriptKey, token).ConfigureAwait(false);

            return await _dispatcher.InvokeAsync(() =>
            {
                token.ThrowIfCancellationRequested();

                lock (_lock)
                {
                    if (_disposed || _model.IsClosing)
                        throw new OperationCanceledException();

                    var key = Tuple.Create(request.ScriptKey.ToString(), request.Name, request.Title ?? "", request.AccessKey ?? "");

                    if (!_groups.TryGetValue(key, out var group))
                    {
                        var metadata = installation.Definition.Metadata;
                        var iconUrl = UserScriptItemViewModel.ResolveIconUrl(metadata, installation.SourceOrigin);
                        var title = request.Title?.TrimEnd();
                        var description = string.IsNullOrEmpty(title) ? metadata.Name : metadata.Name + "\n" + title;

                        group = new Group
                        {
                            Command = new UserScriptMenuCommand(
                                key.Item1, metadata.Name, request.Name, description, InvokeToolbar, iconUrl)
                        };

                        _groups.Add(key, group);
                        _model.UserScriptMenus.Add(group.Command);
                    }

                    var subscription = new Subscription
                    {
                        Request = request,
                        Invoked = invoked
                    };

                    group.Subscriptions.Add(subscription);
                    Refresh(group);

                    return (IMenuRegistration)new Registration(() => _dispatcher.BeginInvoke(new Action(() =>
                    {
                        lock (_lock)
                        {
                            group.Subscriptions.Remove(subscription);
                            Refresh(group);

                            if (group.Subscriptions.Count == 0)
                            {
                                _groups.Remove(key);
                                _model.UserScriptMenus.Remove(group.Command);
                            }
                        }
                    })));
                }
            });
        }

        public UserScriptMenuCommand[] GetCommands()
        {
            // Dictionary slots are reused after removal; the toolbar collection preserves registration order.
            lock (_lock)
                return _model.UserScriptMenus.ToArray();
        }

        private static void Refresh(Group group)
        {
            group.Command.InvokeCommand.CanExecute = group.Subscriptions.Any(item => !item.Request.ClickedFrame);
            group.Command.StaysOpenOnClick = group.Subscriptions.Any(item => !item.Request.AutoClose);
        }

        private void InvokeToolbar(UserScriptMenuCommand command)
        {
            Invoke(command, null, CefEventFlags.None);
        }

        public bool Invoke(UserScriptMenuCommand command, IFrame nativeFrame, CefEventFlags flags)
        {
            Subscription[] subscriptions;

            lock (_lock)
            {
                if (_disposed || _model.IsClosing || _model.IsClosed)
                    return false;

                var group = _groups.Values.FirstOrDefault(item => ReferenceEquals(item.Command, command));

                if (group == null)
                    return false;

                subscriptions = group.Subscriptions.ToArray();
            }

            foreach (var subscription in subscriptions)
            {
                // Resolve while still inside the native callback. Never retain the
                // borrowed IFrame for a later WPF click or toolbar invocation.
                var frame = nativeFrame == null ? null : _host()?.ResolveMenuFrame(nativeFrame, subscription.Request);

                if (subscription.Request.ClickedFrame && frame == null)
                    continue;

                var invocation = new MenuCommandInvocation
                {
                    Kind = "unknown",
                    Frame = frame,
                    CtrlKey = nativeFrame != null ? flags.HasFlag(CefEventFlags.ControlDown) : Keyboard.Modifiers.HasFlag(ModifierKeys.Control),
                    ShiftKey = nativeFrame != null ? flags.HasFlag(CefEventFlags.ShiftDown) : Keyboard.Modifiers.HasFlag(ModifierKeys.Shift),
                    AltKey = nativeFrame != null ? flags.HasFlag(CefEventFlags.AltDown) : Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)
                };

                try
                {
                    subscription.Invoked(invocation);
                }
                catch
                {
                    try
                    {
                        _reportFailure?.Invoke();
                    }
                    catch { }
                }
            }

            return true;
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _disposed = true;
                _groups.Clear();
                _model.UserScriptMenus.Clear();
            }
        }

        private sealed class Group
        {
            public UserScriptMenuCommand Command;
            public readonly List<Subscription> Subscriptions = new List<Subscription>();
        }

        private sealed class Subscription
        {
            public MenuCommandRequest Request;
            public Action<MenuCommandInvocation> Invoked;
        }

        private sealed class Registration : IMenuRegistration
        {
            private Action _remove;

            public Registration(Action remove)
            {
                _remove = remove;
            }

            public void Dispose() => Interlocked.Exchange(ref _remove, null)?.Invoke();
        }
    }
}
