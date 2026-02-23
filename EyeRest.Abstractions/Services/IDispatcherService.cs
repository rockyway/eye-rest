using System;
using System.Threading.Tasks;

namespace EyeRest.Services
{
    public interface IDispatcherService
    {
        void Invoke(Action action);
        Task InvokeAsync(Action action);
        void BeginInvoke(Action action);
        bool CheckAccess();
    }
}
