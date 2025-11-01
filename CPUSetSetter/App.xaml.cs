using CPUSetSetter.Platforms;
using System.Globalization;
using System.IO;
using System.Security.Principal;
using System.Windows;
using System.Windows.Markup;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;


namespace CPUSetSetter
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private NotifyIcon? _trayIcon;
        private Mutex? singleInstanceMutex;
        private const string mutexName = "CPUSetSetterLock";

        public static bool IsElevated { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
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

            // Set the working directory to the directory of the executable, so the config .json file will always be in the right place
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new(identity);
                IsElevated = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }

            // Show unhandled exceptions in an error dialog box
            AddDialogExceptionHandler();

            // Check if the app is already running
            singleInstanceMutex = new(true, mutexName, out bool isOwned);
            if (!isOwned)
            {
                MessageBox.Show("Failed to open: App is already running", "CPU Set Setter", MessageBoxButton.OK, MessageBoxImage.Error);
                ExitApp();
                return;
            }

            // Set the app's culture to the local culture
            SetAppCulture();

            // Set up the tray icon
            ContextMenuStrip trayMenu = new();
            trayMenu.Items.Add("Open", null, (_, _) => ShowMainWindow());
            trayMenu.Items.Add("Close", null, (_, _) => ExitApp());
            using Stream iconStream = GetResourceStream(new Uri("pack://application:,,,/CPUSetSetter;component/tray.ico")).Stream;
            _trayIcon = new()
            {
                Icon = new Icon(iconStream),
                Visible = true,
                ContextMenuStrip = trayMenu,
                Text = "CPU Set Setter"
            };

            // Show the app when clicking the tray icon
            _trayIcon.MouseClick += (_, e) =>
            {
                switch (e.Button)
                {
                    case MouseButtons.Left:
                        ShowMainWindow();
                        break;
                    case MouseButtons.Middle:
                        ExitApp();
                        break;
                }
            };

            // Create the rest of the app
            MainWindow = new MainWindow();
            if (!ConfigOld.Default.StartMinimized)
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
            if (_trayIcon is not null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
            Shutdown();
        }
    }
}

/*
 * TODO:
 * - Low priority: Add list of saved process settings
 */
