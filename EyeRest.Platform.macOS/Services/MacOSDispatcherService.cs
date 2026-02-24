using System;
using System.Threading;
using System.Threading.Tasks;

namespace EyeRest.Services
{
    /// <summary>
    /// macOS implementation of <see cref="IDispatcherService"/> using SynchronizationContext.
    /// Wraps the UI thread synchronization context (provided by Avalonia on macOS)
    /// to marshal actions to the main thread.
    /// </summary>
    public class MacOSDispatcherService : IDispatcherService
    {
        private readonly SynchronizationContext? _syncContext;

        public MacOSDispatcherService()
        {
            _syncContext = SynchronizationContext.Current;
        }

        public void Invoke(Action action)
        {
            if (_syncContext != null && SynchronizationContext.Current != _syncContext)
            {
                var tcs = new TaskCompletionSource();
                _syncContext.Post(_ =>
                {
                    try
                    {
                        action();
                        tcs.SetResult();
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }, null);
                tcs.Task.Wait();
            }
            else
            {
                action();
            }
        }

        public Task InvokeAsync(Action action)
        {
            if (_syncContext != null && SynchronizationContext.Current != _syncContext)
            {
                var tcs = new TaskCompletionSource();
                _syncContext.Post(_ =>
                {
                    try
                    {
                        action();
                        tcs.SetResult();
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }, null);
                return tcs.Task;
            }
            action();
            return Task.CompletedTask;
        }

        public void BeginInvoke(Action action)
        {
            if (_syncContext != null)
                _syncContext.Post(_ => action(), null);
            else
                action();
        }

        public bool CheckAccess()
        {
            return _syncContext == null || SynchronizationContext.Current == _syncContext;
        }
    }
}
