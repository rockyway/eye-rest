using System;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace EyeRest.Services
{
    public class AvaloniaDispatcherService : IDispatcherService
    {
        public void Invoke(Action action) => Dispatcher.UIThread.Invoke(action);

        public Task InvokeAsync(Action action) => Dispatcher.UIThread.InvokeAsync(action).GetTask();

        public void BeginInvoke(Action action) => Dispatcher.UIThread.Post(action);

        public bool CheckAccess() => Dispatcher.UIThread.CheckAccess();
    }
}
