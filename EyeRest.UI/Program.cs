using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia;

namespace EyeRest.UI;

class Program
{
    private const string MutexName = "EyeRest_SingleInstance_7A3F2B1E-4D5C-6E8F-9A0B-C1D2E3F4A5B6";
    private const string PipeName = "EyeRest_ActivationPipe";
    private static Mutex? _instanceMutex;
    private static FileStream? _instanceLockFile;

#if PLATFORM_WINDOWS
    private const int ATTACH_PARENT_PROCESS = -1;

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);
#endif

    [STAThread]
    public static void Main(string[] args)
    {
#if PLATFORM_WINDOWS
        // EyeRest.UI is a WinExe (GUI subsystem) and therefore has no console of its own,
        // so stdout is swallowed when launched from a terminal. When started from an existing
        // console (e.g. `dotnet run`), attach to the parent's console so Console.WriteLine and
        // the Serilog console sink are visible. No-op (returns false, harmless) when there is
        // no parent console — e.g. launched from Explorer or as a packaged/tray app.
        AttachConsole(ATTACH_PARENT_PROCESS);
#endif

        // Velopack startup hook — MUST be first, before any other initialization.
        // During install/update/uninstall, Velopack launches the exe with special
        // arguments and this call handles them, then exits immediately.
#if !STORE_BUILD
        Velopack.VelopackApp.Build().Run();
#endif

        if (!TryAcquireSingleInstance())
        {
            // Another instance is already running — signal it to restore its window
            SignalExistingInstance();
            return;
        }

        try
        {
            // Parse debug flags
            App.ForceShowDonationBanner = Array.Exists(args, a =>
                a.Equals("--show-donation", StringComparison.OrdinalIgnoreCase));
            Console.WriteLine($"[EyeRest] Args: [{string.Join(", ", args)}], ForceShowDonationBanner={App.ForceShowDonationBanner}");

            // Start the named pipe listener for activation signals from future instances
            StartActivationListener();

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            _instanceMutex?.ReleaseMutex();
            _instanceMutex?.Dispose();
            _instanceLockFile?.Dispose();
        }
    }

    /// <summary>
    /// Claims the single-instance guard. Returns false when another instance already
    /// holds it. Windows/macOS use a named mutex. On Linux, named mutexes are scoped
    /// to the login session (/tmp/.dotnet/shm/session&lt;id&gt;/), so a launch from another
    /// session (autostart vs terminal, SSH) would not see the first instance — use an
    /// advisory file lock in the user's config directory instead: per-user, cross-session,
    /// and released by the kernel even on a hard crash.
    /// </summary>
    private static bool TryAcquireSingleInstance()
    {
        if (OperatingSystem.IsLinux())
        {
            var configDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EyeRest");
            try
            {
                Directory.CreateDirectory(configDir);
                _instanceLockFile = new FileStream(
                    Path.Combine(configDir, ".instance.lock"),
                    FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                return true;
            }
            catch (IOException)
            {
                // Lock held by the running instance
                return false;
            }
        }

        _instanceMutex = new Mutex(true, MutexName, out var createdNew);
        return createdNew;
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static void SignalExistingInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(timeout: 2000);
            using var writer = new StreamWriter(client);
            writer.Write("activate");
            writer.Flush();
        }
        catch
        {
            // If pipe connection fails, the existing instance may not be listening — just exit
        }
    }

    private static void StartActivationListener()
    {
        var thread = new Thread(ListenForActivation)
        {
            IsBackground = true,
            Name = "SingleInstancePipeListener"
        };
        thread.Start();
    }

    private static void ListenForActivation()
    {
        while (true)
        {
            try
            {
                using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1);
                server.WaitForConnection();

                using var reader = new StreamReader(server);
                var message = reader.ReadToEnd();

                if (message == "activate")
                {
                    App.RestoreMainWindow();
                }
            }
            catch (IOException)
            {
                // Pipe broken — restart listener
            }
            catch (ObjectDisposedException)
            {
                // App shutting down
                break;
            }
        }
    }
}
