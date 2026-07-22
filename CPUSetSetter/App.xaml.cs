using CPUSetSetter.Config.Models;
using CPUSetSetter.Platforms;
using CPUSetSetter.Platforms.Windows;
using CPUSetSetter.TrayIcon;
using CPUSetSetter.UI;
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
        private const string MutexName = "CPUSetSetterLock";

        private readonly CancellationTokenSource _applicationLifetimeCancellation = new();
        private AppTrayIcon? _trayIcon;
        private Mutex? _singleInstanceMutex;
        private WindowsGameModeWarning? _gameModeWarning;
        private EventHandler<bool>? _trayGameModeToggleRequestedHandler;
        private EventHandler<GameModeStateChangedEventArgs>? _gameModeStateChangedHandler;
        private bool _ownsSingleInstanceMutex;
        private bool _shutdownStarted;

        internal IGameModeService? GameModeService { get; private set; }
        internal CancellationToken ApplicationLifetimeToken => _applicationLifetimeCancellation.Token;

        protected override void OnStartup(StartupEventArgs e)
        {
            AddDialogExceptionHandler();

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

            _singleInstanceMutex = new(true, MutexName, out bool isOwned);
            _ownsSingleInstanceMutex = isOwned;
            if (!isOwned)
            {
                MessageBox.Show("Failed to open: App is already running", "CPU Set Setter", MessageBoxButton.OK, MessageBoxImage.Error);
                ExitApp();
                return;
            }

            AppConfig.Load();
            RuleHelpers.OnConfigLoaded();
            SetAppCulture();

            WindowsGameModeService gameModeService = new(Dispatcher);
            GameModeService = gameModeService;
            _ = Task.Run(() => gameModeService.MonitorAsync(ApplicationLifetimeToken));

            using Stream iconStream = GetResourceStream(new Uri("pack://application:,,,/CPUSetSetter;component/tray.ico")).Stream;
            _trayIcon = new(iconStream);
            _trayIcon.OpenClicked += OnTrayOpenClicked;
            _trayIcon.CloseClicked += OnTrayCloseClicked;
            _trayGameModeToggleRequestedHandler = OnTrayGameModeToggleRequested;
            _trayIcon.GameModeToggleRequested += _trayGameModeToggleRequestedHandler;

            _gameModeStateChangedHandler = OnGameModeStateChanged;
            gameModeService.StateChanged += _gameModeStateChangedHandler;
            UpdateTrayGameModeState(gameModeService.IsEnabled);

            if (AppConfig.Instance.IsFirstRun)
            {
                PromoteTrayIcon();
            }

            VersionChecker.Instance.RunVersionChecker(ApplicationLifetimeToken);

            _gameModeWarning = new(gameModeService);
            _gameModeWarning.ShowIfEnabled();

            MainWindow = new MainWindow(gameModeService, ApplicationLifetimeToken);
            if (!AppConfig.Instance.StartMinimized)
            {
                ShowMainWindow();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            BeginShutdown();

            if (MainWindow is MainWindow mainWindow)
            {
                mainWindow.Dispose();
            }

            _gameModeWarning?.Dispose();
            _gameModeWarning = null;

            if (GameModeService is not null && _gameModeStateChangedHandler is not null)
            {
                GameModeService.StateChanged -= _gameModeStateChangedHandler;
            }
            _gameModeStateChangedHandler = null;

            if (_trayIcon is not null)
            {
                _trayIcon.OpenClicked -= OnTrayOpenClicked;
                _trayIcon.CloseClicked -= OnTrayCloseClicked;
                if (_trayGameModeToggleRequestedHandler is not null)
                {
                    _trayIcon.GameModeToggleRequested -= _trayGameModeToggleRequestedHandler;
                }
                _trayIcon.Dispose();
                _trayIcon = null;
            }
            _trayGameModeToggleRequestedHandler = null;

            if (_ownsSingleInstanceMutex)
            {
                _singleInstanceMutex?.ReleaseMutex();
                _ownsSingleInstanceMutex = false;
            }
            _singleInstanceMutex?.Dispose();
            _singleInstanceMutex = null;

            _applicationLifetimeCancellation.Dispose();
            base.OnExit(e);
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
            {
                return;
            }

            using RegistryKey? notifyKey = Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings");
            if (notifyKey is null)
            {
                return;
            }

            foreach (string subkeyName in notifyKey.GetSubKeyNames())
            {
                using RegistryKey? appKey = notifyKey.OpenSubKey(subkeyName, true);
                if (appKey is null)
                {
                    continue;
                }

                object? exePathValue = appKey.GetValue("ExecutablePath");
                if (exePathValue is null || !string.Equals(exePathValue.ToString(), appExePath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                object? isPromoted = appKey.GetValue("IsPromoted");
                if (isPromoted is null)
                {
                    appKey.SetValue("IsPromoted", 1, RegistryValueKind.DWord);
                }
            }
        }

        private void OnTrayOpenClicked(object? sender, EventArgs e)
        {
            ShowMainWindow();
        }

        private void OnTrayCloseClicked(object? sender, EventArgs e)
        {
            ExitAppGracefully();
        }

        private void OnTrayGameModeToggleRequested(object? sender, bool isEnabled)
        {
            if (GameModeService is null)
            {
                return;
            }

            GameModeService.TrySetEnabled(isEnabled);
            UpdateTrayGameModeState(GameModeService.IsEnabled);
        }

        private void OnGameModeStateChanged(object? sender, GameModeStateChangedEventArgs e)
        {
            if (Dispatcher.CheckAccess())
            {
                UpdateTrayGameModeState(e.IsEnabled);
            }
            else if (!Dispatcher.HasShutdownStarted)
            {
                Dispatcher.InvokeAsync(() => UpdateTrayGameModeState(e.IsEnabled));
            }
        }

        private void UpdateTrayGameModeState(bool isEnabled)
        {
            if (_trayIcon is not null)
            {
                _trayIcon.IsGameModeEnabled = isEnabled;
            }
        }

        private void ShowMainWindow()
        {
            if (MainWindow is null)
            {
                return;
            }

            MainWindow.Show();
            MainWindow.WindowState = WindowState.Normal;
            MainWindow.Activate();
        }

        private void ExitAppGracefully()
        {
            BeginShutdown();
            if (AppConfig.Instance.ClearMasksOnClose)
            {
                RuleHelpers.ClearAllProcessMasksNoSave();
            }

            AppConfig.Instance.WaitForSave();
            Shutdown();
        }

        private void ExitApp()
        {
            BeginShutdown();
            Shutdown();
        }

        private void BeginShutdown()
        {
            if (_shutdownStarted)
            {
                return;
            }

            _shutdownStarted = true;
            _applicationLifetimeCancellation.Cancel();
        }
    }
}
