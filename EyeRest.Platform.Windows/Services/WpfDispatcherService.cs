using System;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace EyeRest.Services
{
    /// <summary>
    /// WPF implementation of IDispatcherService that delegates to the WPF Dispatcher
    /// </summary>
    public class WpfDispatcherService : IDispatcherService
    {
        private readonly Dispatcher _dispatcher;

        public WpfDispatcherService(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public void Invoke(Action action) => _dispatcher.Invoke(action);

        public Task InvokeAsync(Action action)
        {
            return _dispatcher.InvokeAsync(action).Task;
        }

        public void BeginInvoke(Action action) => _dispatcher.BeginInvoke(action);

        public bool CheckAccess() => _dispatcher.CheckAccess();
    }
}
