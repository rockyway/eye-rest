using System;
using System.Threading.Tasks;
using EyeRest.Services;

namespace EyeRest.Tests.Fakes
{
    /// <summary>
    /// Fake dispatcher service for testing that executes actions synchronously on the calling thread
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
