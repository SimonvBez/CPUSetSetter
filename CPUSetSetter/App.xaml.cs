using CPUSetSetter.Config.Models;
using CPUSetSetter.Platforms;
using CPUSetSetter.TrayIcon;
using CPUSetSetter.UI;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;


namespace CPUSetSetter
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private AppTrayIcon? trayIcon;
        private Mutex? singleInstanceMutex;
        private const string mutexName = "CPUSetSetterLock";

        protected override void OnStartup(StartupEventArgs e)
        {
            // Show unhandled exceptions in an error dialog box
            AddDialogExceptionHandler();

            // Set the working directory to the directory of the executable, so the config .json file will always be in the right place
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

            // Quit when this CPU is not supported
            try
            {
                if (!CpuInfo.IsSupported)
                {
                    throw new UnsupportedCpu();
                }
            }
            catch (UnsupportedCpu ex)
            {
                MessageBox.Show($"This system's CPU is unfortunately not supported: {ex.Message}", "CPU Set Setter", MessageBoxButton.OK, MessageBoxImage.Error);
                ExitApp();
                return;
            }

            base.OnStartup(e);

            // Check if the app is already running
            singleInstanceMutex = new(true, mutexName, out bool isOwned);
            if (!isOwned)
            {
                MessageBox.Show("Failed to open: App is already running", "CPU Set Setter", MessageBoxButton.OK, MessageBoxImage.Error);
                ExitApp();
                return;
            }

            // Load the config, which also loads the app's UI theme
            AppConfig.Load();

            // Set the app's culture to the local culture
            SetAppCulture();

            // Set up the tray icon
            using Stream iconStream = GetResourceStream(new Uri("pack://application:,,,/CPUSetSetter;component/tray.ico")).Stream;
            trayIcon = new(iconStream);
            trayIcon.OpenClicked += (_, _) => ShowMainWindow();
            trayIcon.CloseClicked += (_, _) => ExitApp();

            // Create the rest of the app
            MainWindow = new MainWindow();
            if (!AppConfig.Instance.StartMinimized)
            {
                ShowMainWindow();
            }
        }

        private void AddDialogExceptionHandler()
        {
            DispatcherUnhandledException += (_, e) =>
            {
                MessageBox.Show($"An error occurred: {e.Exception}\n{e.Exception.StackTrace}", "Unhandled error", MessageBoxButton.OK, MessageBoxImage.Error);
            };
        }

        private static void SetAppCulture()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.CurrentCulture;

            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag))
            );
        }

        private void ShowMainWindow()
        {
            MainWindow.Show();
            MainWindow.WindowState = WindowState.Normal;
            MainWindow.Activate();
        }

        private void ExitApp()
        {
            trayIcon?.Dispose();
            Shutdown();
        }
    }
}
