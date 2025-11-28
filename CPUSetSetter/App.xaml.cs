using CPUSetSetter.Config.Models;
using CPUSetSetter.Platforms;
using CPUSetSetter.TrayIcon;
using CPUSetSetter.UI;
using CPUSetSetter.UI.Tabs.Processes;
using CPUSetSetter.Util;
using Microsoft.Win32;
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

            // Check if the app was launched as an elevated AutoStart child process
            if (e.Args.Contains(AutoStarter.LaunchArgumentEnable))
            {
                bool success = AutoStarter.Enable();
                Environment.Exit(success ? 0 : 1);
            }
            if (e.Args.Contains(AutoStarter.LaunchArgumentDisable))
            {
                bool success = AutoStarter.Disable();
                Environment.Exit(success ? 0 : 1);
            }

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
            RuleHelpers.OnConfigLoaded();

            // Set the app's culture to the local culture
            SetAppCulture();

            // Set up the tray icon
            using Stream iconStream = GetResourceStream(new Uri("pack://application:,,,/CPUSetSetter;component/tray.ico")).Stream;
            trayIcon = new(iconStream);
            trayIcon.OpenClicked += (_, _) => ShowMainWindow();
            trayIcon.CloseClicked += (_, _) => ExitAppGracefully();

            if (AppConfig.Instance.IsFirstRun)
            {
                // Promote the tray icon directly onto the Taskbar instead of in the "up-arrow" menu
                // Some users did not notice the tray icon because it was hidden by default
                // To respect the user's choice, this is only done the first time the app is ran
                PromoteTrayIcon();
            }

            // Check for updates in the background
            VersionChecker.Instance.RunVersionChecker();

            // Show a warning if Windows Game Mode is enabled
            WindowsGameModeWarning.ShowIfEnabled();

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

        /// <summary>
        /// 'Promote' the app's tray icon, meaning it is unhidden from the 'up-arrow' menu 
        /// </summary>
        private static void PromoteTrayIcon()
        {
            string? appExePath = Environment.ProcessPath;
            if (appExePath is null)
                return;

            using RegistryKey? notifyKey = Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings");
            if (notifyKey is null)
                return;

            foreach (string subkeyName in notifyKey.GetSubKeyNames())
            {
                using RegistryKey? appKey = notifyKey.OpenSubKey(subkeyName, true);
                if (appKey is null)
                    continue;

                object? exePathValue = appKey.GetValue("ExecutablePath");
                if (exePathValue is null || !string.Equals(exePathValue.ToString(), appExePath, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Only promote the tray icon when it has no promote status set yet
                object? isPromoted = appKey.GetValue("IsPromoted");
                if (isPromoted is null)
                    appKey.SetValue("IsPromoted", 1, RegistryValueKind.DWord);
            }
        }

        private void ShowMainWindow()
        {
            MainWindow.Show();
            MainWindow.WindowState = WindowState.Normal;
            MainWindow.Activate();
        }

        private void ExitAppGracefully()
        {
            if (AppConfig.Instance.ClearMasksOnClose)
                RuleHelpers.ClearAllProcessMasksNoSave();

            AppConfig.Instance.WaitForSave();
            ExitApp();
        }

        private void ExitApp()
        {
            trayIcon?.Dispose();
            Shutdown();
        }
    }
}
