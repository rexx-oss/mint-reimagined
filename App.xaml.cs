using System.Threading;
using System.Windows;

namespace Mint
{
    public partial class App : Application
    {
        private static Mutex? _mutex;
        private const string MutexId = "MINT_LAUNCHER_SINGLE_INSTANCE_MUTEX";

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            _mutex = new Mutex(true, MutexId, out bool isNewInstance);
            if (!isNewInstance)
            {
                MessageBox.Show("Mint is already running in your system tray.", "Mint", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            var mainWindow = new MainWindow();
            mainWindow.Hide();
        }
    }
}