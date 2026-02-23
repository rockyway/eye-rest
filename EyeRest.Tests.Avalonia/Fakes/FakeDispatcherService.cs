using System;
using System.Threading.Tasks;
using EyeRest.Services;

namespace EyeRest.Tests.Avalonia.Fakes
{
    /// <summary>
    /// Fake dispatcher service for testing that executes actions synchronously on the calling thread.
    /// This avoids needing a real Avalonia UI thread during unit tests.
    /// </summary>
    public class FakeDispatcherService : IDispatcherService
    {
        public void Invoke(Action action) => action();

        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }

        public void BeginInvoke(Action action) => action();

        public bool CheckAccess() => true;
    }
}
