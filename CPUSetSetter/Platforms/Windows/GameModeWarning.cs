using CPUSetSetter.Config.Models;
using CPUSetSetter.UI.Tabs.Processes;
using System.Windows;


namespace CPUSetSetter.Platforms.Windows
{
    /// <summary>
    /// Warns when Windows Game Mode can conflict with CPU Set Setter masks.
    /// </summary>
    internal sealed class WindowsGameModeWarning : IDisposable
    {
        private const string LogWarningMessage = "WARNING: While Windows Game Mode is enabled, no masks are applied. (This behavior can be disabled in the Settings tab)";

        private readonly IGameModeService _gameModeService;
        private readonly EventHandler<GameModeStateChangedEventArgs> _stateChangedHandler;
        private bool _isDisposed;

        public WindowsGameModeWarning(IGameModeService gameModeService)
        {
            _gameModeService = gameModeService;
            _stateChangedHandler = OnGameModeStateChanged;
            _gameModeService.StateChanged += _stateChangedHandler;
        }

        public void ShowIfEnabled()
        {
            if (!_gameModeService.IsEnabled)
            {
                return;
            }

            if (AppConfig.Instance.ShowGameModePopup)
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBoxResult result = MessageBox.Show(
                        "Windows Game Mode is currently enabled.\n" +
                        "On AMD CPUs, Game Mode is known to conflict with CPU Set Setter, leading to lower FPS and game crashes.\n" +
                        "I am not sure if Intel CPUs are affected too. So if you have one, please share your findings with me on GitHub!\n\n" +
                        "Would you like CPU Set Setter to disable Game Mode for you?\n\n" +
                        "(This popup can be disabled in the Settings tab)",
                        "CPU Set Setter: Windows Game Mode warning",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        _gameModeService.TrySetEnabled(false);
                    }
                    else if (!AppConfig.Instance.KeepMasksDuringGameMode)
                    {
                        WindowLogger.Write(LogWarningMessage);
                    }
                });
            }
            else if (!AppConfig.Instance.KeepMasksDuringGameMode)
            {
                WindowLogger.Write(LogWarningMessage);
            }
        }

        private static void OnGameModeStateChanged(object? sender, GameModeStateChangedEventArgs e)
        {
            if (e.IsEnabled && !AppConfig.Instance.KeepMasksDuringGameMode)
            {
                WindowLogger.Write(LogWarningMessage);
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _gameModeService.StateChanged -= _stateChangedHandler;
            GC.SuppressFinalize(this);
        }
    }
}
