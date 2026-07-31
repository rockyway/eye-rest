using System.Collections.Generic;
using System.Linq;
using EyeRest.UI.Views;
using Xunit;

namespace EyeRest.Tests.Avalonia.Views
{
    /// <summary>
    /// Tests for the popup shell pool's per-scaling selection.
    ///
    /// A pooled shell carries the render scaling it was established at. Avalonia only sets a
    /// window's scaling when the native window is created and on WM_DPICHANGED, which Windows does
    /// not deliver to a hidden window that is moved and re-shown — so re-showing a shell on a
    /// monitor with a different scale renders its content at one scale inside a frame sized for
    /// another, clipping it. The pool must therefore hand back only shells matching the target
    /// monitor's scaling, while keeping the others for their own monitors.
    /// </summary>
    public class PopupWindowShellPoolTests
    {
        /// <summary>Stand-in for a pooled shell: PopupWindow itself needs an Avalonia platform.</summary>
        private sealed class Shell(double scaling)
        {
            public double Scaling { get; } = scaling;
        }

        private static Stack<Shell> Pool(params double[] topToBottom)
        {
            // Stack<T> pushes bottom-first, so reverse to get the requested top-to-bottom order.
            var pool = new Stack<Shell>();
            foreach (var s in Enumerable.Reverse(topToBottom))
                pool.Push(new Shell(s));
            return pool;
        }

        private static Shell? Take(Stack<Shell> pool, double target, int maxPoolSize = 4) =>
            PopupWindow.TakeMatching(pool, s => s.Scaling, target, maxPoolSize);

        [Fact]
        public void TakeMatching_ReturnsShellAtTargetScaling()
        {
            var pool = Pool(1.5);

            var match = Take(pool, 1.5);

            Assert.NotNull(match);
            Assert.Equal(1.5, match!.Scaling);
            Assert.Empty(pool);
        }

        [Fact]
        public void TakeMatching_RefusesShellFromDifferentScalingMonitor()
        {
            // The reported bug: a shell last sized on a 100% monitor re-shown on the 150% primary.
            var pool = Pool(1.0);

            Assert.Null(Take(pool, 1.5)); // caller must build a fresh shell
        }

        [Fact]
        public void TakeMatching_KeepsRejectedShellsInThePool()
        {
            // Rejecting a shell must not discard it: it is still correct for its own monitor.
            var pool = Pool(1.0);

            Take(pool, 1.5);

            Assert.Single(pool);
            Assert.Equal(1.0, pool.Peek().Scaling);
        }

        [Fact]
        public void TakeMatching_SkipsPastNonMatchingShellsToFindAMatch()
        {
            var pool = Pool(1.0, 1.75, 1.5);

            var match = Take(pool, 1.5);

            Assert.NotNull(match);
            Assert.Equal(1.5, match!.Scaling);
            // The two skipped shells are preserved, in their original order.
            Assert.Equal(new[] { 1.0, 1.75 }, pool.Select(s => s.Scaling));
        }

        [Fact]
        public void TakeMatching_OnEmptyPoolReturnsNull()
        {
            Assert.Null(Take(Pool(), 1.5));
        }

        [Theory]
        [InlineData(1.0, 1.25)]
        [InlineData(1.25, 1.5)]
        [InlineData(1.5, 1.75)]
        [InlineData(1.75, 2.0)]
        public void TakeMatching_DistinguishesAdjacentWindowsScaleFactors(double pooled, double target)
        {
            // Windows scale factors step in 25% increments; neighbours must never be confused.
            Assert.Null(Take(Pool(pooled), target));
        }

        [Fact]
        public void TakeMatching_ToleratesFloatingPointNoiseInTheSameScaling()
        {
            // RenderScaling is computed as dpi/96.0, so an exact == would be brittle.
            var pool = Pool(150 / 100.0);

            var match = Take(pool, 144 / 96.0);

            Assert.NotNull(match);
        }

        [Fact]
        public void TakeMatching_DoesNotGrowThePoolBeyondItsCap()
        {
            // Every candidate is rejected, so all of them are pushed back — but never past the cap.
            var pool = Pool(1.0, 1.0, 1.0, 1.0);

            Assert.Null(Take(pool, 1.5, maxPoolSize: 2));
            Assert.Equal(2, pool.Count);
        }
    }
}
