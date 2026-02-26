# Contributing to Eye-Rest

Thank you for your interest in contributing to Eye-Rest!

## Development Setup

### Prerequisites
- .NET 8.0 SDK
- Windows 10+ or macOS 12+
- Node.js 18+ (for the marketing website only)

### Build & Run

```bash
# Build the solution
dotnet build

# Run the application
dotnet run --project EyeRest.UI

# Run all tests
dotnet test
```

### Project Structure

See [PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md) for a detailed breakdown of the codebase.

## How to Contribute

1. Fork the repository
2. Create a feature branch from `develop`
3. Make your changes
4. Run the full test suite: `dotnet test`
5. Ensure the build succeeds: `dotnet build`
6. Submit a pull request targeting `develop`

## Code Standards

- Follow existing code patterns and naming conventions
- All warnings are treated as errors (`TreatWarningsAsErrors` is enabled)
- Nullable reference types are enabled project-wide
- New services should implement an interface in `EyeRest.Abstractions`
- Platform-specific code goes in `EyeRest.Platform.Windows` or `EyeRest.Platform.macOS`

## Testing

- Write tests for new functionality
- Use the naming convention: `MethodName_StateUnderTest_ExpectedBehavior`
- Follow the Arrange-Act-Assert pattern
- Use `FakeTimer` and `FakeDispatcherService` for timer-related tests

## Reporting Issues

Open an issue on GitHub with:
- Steps to reproduce
- Expected vs actual behavior
- OS and .NET version
- Relevant log entries (from `%APPDATA%\EyeRest\logs\` or `~/.config/EyeRest/logs/`)
