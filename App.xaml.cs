using System;
using System.Linq;
using System.Threading;
using System.Windows;

namespace Mint
{
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

            bool startMinimized = e.Args.Any(a => 
                a.Equals("--minimized", StringComparison.OrdinalIgnoreCase) || 
                a.Equals("-min", StringComparison.OrdinalIgnoreCase));

            var mainWindow = new MainWindow();

            // Only show the window if opened manually; stay in tray if auto-started
            if (!startMinimized)
            {
                mainWindow.Show();
            }
        }
    }
}
