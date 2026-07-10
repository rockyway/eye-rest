using Xunit;

namespace EyeRest.Tests.Avalonia
{
    /// <summary>
    /// TimerService keeps its break/eye-rest processing guards in process-wide <c>static</c>
    /// fields, so any test class that instantiates a real <see cref="EyeRest.Services.TimerService"/>
    /// shares that state and must not run concurrently with another such class.
    ///
    /// Grouping those classes into this single non-parallel collection serializes just them
    /// (the rest of the suite still runs in parallel), replacing the earlier assembly-wide
    /// <c>parallelizeTestCollections:false</c> hammer. If the static guards are ever made
    /// instance-scoped, this collection can be removed.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class TimerServiceStaticStateCollection
    {
        public const string Name = "TimerService static state";
    }
}
