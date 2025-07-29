@echo off
echo ========================================
echo EyeRest UI Test Suite Runner
echo ========================================
echo.

:: Set console title
title EyeRest UI Tests

:: Check if dotnet is available
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: .NET SDK not found. Please install .NET 8 SDK.
    pause
    exit /b 1
)

echo .NET SDK found: 
dotnet --version
echo.

:: Build the project first
echo Building EyeRest application...
dotnet build --configuration Debug
if errorlevel 1 (
    echo.
    echo ERROR: Build failed. Please fix compilation errors first.
    pause
    exit /b 1
)

echo.
echo Build completed successfully.
echo.

:: Run the UI tests
echo Starting UI test execution...
echo.

dotnet run --project . -- RunUITests --build

:: Check exit code
if errorlevel 1 (
    echo.
    echo WARNING: Some tests failed or encountered errors.
    echo Check the test report for details.
) else (
    echo.
    echo SUCCESS: All UI tests passed!
)

echo.
echo UI test execution completed.
echo Check the TestReports folder for detailed HTML report.
echo.
pause