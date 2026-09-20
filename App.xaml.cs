using System;
using System.Threading;
using System.Windows;

namespace Mint;

public partial class App : Application
{
    private static Mutex? _mutex;
    private const string MutexName = "MINT_LAUNCHER_SINGLE_INSTANCE_MUTEX";

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            MessageBox.Show($"Mint startup error:\n{args.ExceptionObject}", "Mint Error", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        DispatcherUnhandledException += (s, args) =>
        {
            MessageBox.Show($"Mint UI error:\n{args.Exception.Message}", "Mint Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        _mutex = new Mutex(true, MutexName, out bool isNewInstance);
        if (!isNewInstance)
        {
            MessageBox.Show("Mint is already running in your system tray.", "Mint", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // Always launches directly to the tray.
        // The main window only opens when the tray icon is double-clicked.
        var mainWindow = new MainWindow();
    }
}
