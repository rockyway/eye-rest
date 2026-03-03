using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using Avalonia;

namespace EyeRest.UI;

class Program
{
    private const string MutexName = "EyeRest_SingleInstance_7A3F2B1E-4D5C-6E8F-9A0B-C1D2E3F4A5B6";
    private const string PipeName = "EyeRest_ActivationPipe";
    private static Mutex? _instanceMutex;

    [STAThread]
    public static void Main(string[] args)
    {
        // Velopack startup hook — MUST be first, before any other initialization.
        // During install/update/uninstall, Velopack launches the exe with special
        // arguments and this call handles them, then exits immediately.
#if !STORE_BUILD
        Velopack.VelopackApp.Build().Run();
#endif

        _instanceMutex = new Mutex(true, MutexName, out var createdNew);

        if (!createdNew)
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
            _instanceMutex.ReleaseMutex();
            _instanceMutex.Dispose();
        }
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
