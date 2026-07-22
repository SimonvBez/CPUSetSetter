using CPUSetSetter.UI.Tabs.Processes;
using Microsoft.Win32;
using System.Windows.Threading;


namespace CPUSetSetter.Platforms.Windows
{
    internal sealed class WindowsGameModeService : IGameModeService
    {
        private const string GameBarKey = @"Software\Microsoft\GameBar";
        private const string ValueName = "AutoGameModeEnabled";
        private static readonly TimeSpan MonitorInterval = TimeSpan.FromSeconds(1);

        private readonly Dispatcher _dispatcher;
        private readonly Lock _stateLock = new();
        private bool _isEnabled;

        public bool IsEnabled
        {
            get
            {
                using (_stateLock.EnterScope())
                {
                    return _isEnabled;
                }
            }
        }

        public event EventHandler<GameModeStateChangedEventArgs>? StateChanged;

        public WindowsGameModeService(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            TryRefreshState(logFailure: true, publishChanges: false);
        }

        public bool TrySetEnabled(bool isEnabled)
        {
            bool stateChanged = false;
            string? failureMessage = null;

            using (_stateLock.EnterScope())
            {
                try
                {
                    using RegistryKey? key = Registry.CurrentUser.CreateSubKey(GameBarKey);
                    if (key is null)
                    {
                        failureMessage = "Unable to toggle Windows Game Mode: Registry key could not be created or opened.";
                    }
                    else
                    {
                        key.SetValue(ValueName, isEnabled ? 1 : 0, RegistryValueKind.DWord);
                        bool confirmedState = ReadState(key);
                        if (confirmedState != isEnabled)
                        {
                            failureMessage = "Unable to toggle Windows Game Mode: Registry did not retain the requested value.";
                        }
                        else
                        {
                            stateChanged = SetCachedStateLocked(confirmedState);
                            if (stateChanged)
                            {
                                PublishStateChanged(confirmedState);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    failureMessage = $"Unable to toggle Windows Game Mode: {ex.Message}";
                }
            }

            if (failureMessage is not null)
            {
                WindowLogger.Write(failureMessage);
                return false;
            }

            return true;
        }

        public async Task MonitorAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (true)
                {
                    await Task.Delay(MonitorInterval, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    TryRefreshState(logFailure: true, publishChanges: true);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Application shutdown is expected.
            }
            catch (Exception ex)
            {
                WindowLogger.Write($"Windows Game Mode monitoring stopped unexpectedly: {ex}");
            }
        }

        private bool TryRefreshState(bool logFailure, bool publishChanges)
        {
            bool currentState = false;
            bool stateChanged = false;
            string? failureMessage = null;

            using (_stateLock.EnterScope())
            {
                try
                {
                    using RegistryKey? key = Registry.CurrentUser.OpenSubKey(GameBarKey);
                    currentState = ReadState(key);
                    stateChanged = SetCachedStateLocked(currentState);
                    if (stateChanged && publishChanges)
                    {
                        PublishStateChanged(currentState);
                    }
                }
                catch (Exception ex)
                {
                    failureMessage = $"Unable to read Windows Game Mode state: {ex.Message}";
                }
            }

            if (failureMessage is not null)
            {
                if (logFailure)
                {
                    WindowLogger.Write(failureMessage);
                }
                return false;
            }

            return true;
        }

        private static bool ReadState(RegistryKey? key)
        {
            return key?.GetValue(ValueName, 0) is int intValue && intValue != 0;
        }

        private bool SetCachedStateLocked(bool isEnabled)
        {
            if (_isEnabled == isEnabled)
            {
                return false;
            }

            _isEnabled = isEnabled;
            return true;
        }

        private void PublishStateChanged(bool isEnabled)
        {
            if (_dispatcher.HasShutdownStarted)
            {
                return;
            }

            _dispatcher.InvokeAsync(() =>
                StateChanged?.Invoke(this, new GameModeStateChangedEventArgs(isEnabled)));
        }
    }
}
