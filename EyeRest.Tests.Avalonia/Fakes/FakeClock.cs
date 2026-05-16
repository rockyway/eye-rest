using System;
using EyeRest.Services;

namespace EyeRest.Tests.Avalonia.Fakes
{
    /// <summary>
    /// Deterministic clock for tests. Defaults to the real DateTime.Now at construction
    /// (so tests that don't manipulate the clock behave like before), and exposes
    /// <see cref="Advance"/> + a settable <see cref="Now"/> for tests that do.
    /// </summary>
    public sealed class FakeClock : IClock
    {
        public DateTime Now { get; set; }

        public DateTime UtcNow => Now.Kind == DateTimeKind.Utc ? Now : Now.ToUniversalTime();

        public FakeClock()
        {
            Now = DateTime.Now;
        }

        public FakeClock(DateTime initial)
        {
            Now = initial;
        }

        public void Advance(TimeSpan span) => Now = Now.Add(span);
    }
}
