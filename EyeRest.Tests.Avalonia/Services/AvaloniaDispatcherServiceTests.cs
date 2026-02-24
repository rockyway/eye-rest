using System;
using System.Threading.Tasks;
using EyeRest.Services;
using EyeRest.Tests.Avalonia.Fakes;
using Xunit;

namespace EyeRest.Tests.Avalonia.Services
{
    /// <summary>
    /// Tests for the dispatcher service contract using FakeDispatcherService.
    /// The real AvaloniaDispatcherService requires a running Avalonia UI thread,
    /// so we test the interface contract using the fake implementation.
    /// </summary>
    public class AvaloniaDispatcherServiceTests
    {
        private readonly FakeDispatcherService _dispatcher;

        public AvaloniaDispatcherServiceTests()
        {
            _dispatcher = new FakeDispatcherService();
        }

        [Fact]
        public void Invoke_ExecutesAction()
        {
            // Arrange
            var executed = false;

            // Act
            _dispatcher.Invoke(() => executed = true);

            // Assert
            Assert.True(executed);
        }

        [Fact]
        public async Task InvokeAsync_ExecutesAction()
        {
            // Arrange
            var executed = false;

            // Act
            await _dispatcher.InvokeAsync(() => executed = true);

            // Assert
            Assert.True(executed);
        }

        [Fact]
        public async Task InvokeAsync_ReturnsCompletedTask()
        {
            // Act
            var task = _dispatcher.InvokeAsync(() => { });

            // Assert
            Assert.True(task.IsCompleted);
            await task; // Should not throw
        }

        [Fact]
        public void BeginInvoke_ExecutesAction()
        {
            // Arrange
            var executed = false;

            // Act
            _dispatcher.BeginInvoke(() => executed = true);

            // Assert
            Assert.True(executed);
        }

        [Fact]
        public void CheckAccess_ReturnsTrue()
        {
            // Act
            var result = _dispatcher.CheckAccess();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void Invoke_WithException_PropagatesException()
        {
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                _dispatcher.Invoke(() => throw new InvalidOperationException("Test exception")));
        }

        [Fact]
        public void BeginInvoke_WithException_PropagatesException()
        {
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                _dispatcher.BeginInvoke(() => throw new InvalidOperationException("Test exception")));
        }

        [Fact]
        public void FakeDispatcher_ImplementsIDispatcherService()
        {
            // Assert
            Assert.IsAssignableFrom<IDispatcherService>(_dispatcher);
        }

        [Fact]
        public void Invoke_ModifiesSharedState_Synchronously()
        {
            // Arrange
            var counter = 0;

            // Act
            _dispatcher.Invoke(() => counter++);
            _dispatcher.Invoke(() => counter++);
            _dispatcher.Invoke(() => counter++);

            // Assert
            Assert.Equal(3, counter);
        }
    }
}
