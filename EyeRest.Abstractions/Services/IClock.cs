using System;

namespace EyeRest.Services
{
    /// <summary>
    /// Wall-clock abstraction so time-dependent code paths (timer pause/resume,
    /// elapsed-duration calculations) can be deterministically driven from tests.
    /// </summary>
    public interface IClock
    {
        DateTime Now { get; }
        DateTime UtcNow { get; }
    }
}
