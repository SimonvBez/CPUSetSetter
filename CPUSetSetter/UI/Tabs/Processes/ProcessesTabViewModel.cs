using CommunityToolkit.Mvvm.ComponentModel;
using CPUSetSetter.Config.Models;
using CPUSetSetter.Platforms;
using CPUSetSetter.Platforms.Windows;
using CPUSetSetter.Util;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;


namespace CPUSetSetter.UI.Tabs.Processes
{
    [Flags]
    internal enum ProcessListPauseReason
    {
        None = 0,
        UserInput = 1,
        WindowHidden = 2
    }

    public partial class ProcessesTabViewModel : ObservableObject, IDisposable
    {
        private const int UpdateDelayVisibleMs = 1000;
        private const int UpdateDelayHiddenMs = 2000;
        private static readonly TimeSpan ReapplyInterval = TimeSpan.FromSeconds(15);

        public static ProcessesTabViewModel? Instance { get; private set; }

        private readonly Dispatcher _dispatcher;
        private readonly IGameModeService _gameModeService;
        private readonly CancellationToken _cancellationToken;
        private readonly ListCollectionView _runningProcessesView;
        private readonly EventHandler<NewProcessEventArgs> _processCreatedHandler;
        private readonly EventHandler<ExitedProcessEventArgs> _processExitedHandler;
        private readonly PropertyChangedEventHandler _configChangedHandler;
        private readonly EventHandler<GameModeStateChangedEventArgs> _gameModeChangedHandler;
        private ProcessListPauseReason _pauseReasons;
        private bool _isDisposed;

        public static PausableObservableCollection<ProcessListEntryViewModel> RunningProcesses { get; } = [];

        [ObservableProperty]
        private string _processNameFilter = string.Empty;

        internal ProcessesTabViewModel(
            Dispatcher dispatcher,
            IGameModeService gameModeService,
            CancellationToken cancellationToken)
        {
            _dispatcher = dispatcher;
            _gameModeService = gameModeService;
            _cancellationToken = cancellationToken;
            Instance = this;

            _runningProcessesView = (ListCollectionView)CollectionViewSource.GetDefaultView(RunningProcesses);
            _runningProcessesView.SortDescriptions.Add(new(nameof(ProcessListEntryViewModel.CpuUsage), ListSortDirection.Descending));
            _runningProcessesView.IsLiveSorting = true;
            _runningProcessesView.LiveSortingProperties.Add(nameof(ProcessListEntryViewModel.CpuUsage));
            _runningProcessesView.Filter = item =>
                ((ProcessListEntryViewModel)item).Name.Contains(ProcessNameFilter, StringComparison.OrdinalIgnoreCase);

            _processCreatedHandler = (_, e) => OnNewProcess(e.Info);
            _processExitedHandler = (_, e) => OnExitedProcess(e.PID);
            _configChangedHandler = OnConfigChanged;
            _gameModeChangedHandler = OnGameModeStateChanged;

            ProcessEvents.Default.ProcessCreated += _processCreatedHandler;
            ProcessEvents.Default.ProcessExited += _processExitedHandler;
            AppConfig.Instance.PropertyChanged += _configChangedHandler;
            _gameModeService.StateChanged += _gameModeChangedHandler;
            ProcessEvents.Default.Start();

            _ = Task.Run(ProcessCpuUsageUpdateLoop);
            _ = Task.Run(ProcessReapplyLoop);
        }

        public void OnMaskHotkeyPressed(LogicalProcessorMask mask)
        {
            if (GameModeForcesNoMask)
            {
                return;
            }

            ProcessListEntryViewModel? foregroundProcess = GetCurrentForegroundProcess();
            if (foregroundProcess is not null)
            {
                bool success = foregroundProcess.SetMask(mask, true);
                if (success)
                {
                    if (mask.MaskType == MaskApplyType.NoMask)
                    {
                        HotkeySoundPlayer.Default.PlayCleared();
                    }
                    else
                    {
                        HotkeySoundPlayer.Default.PlayApplied();
                    }
                }
                else
                {
                    HotkeySoundPlayer.PlayError();
                }
            }
        }

        internal void SetListPauseReason(ProcessListPauseReason reason, bool enabled)
        {
            _dispatcher.VerifyAccess();

            ProcessListPauseReason previousReasons = _pauseReasons;
            if (enabled)
            {
                _pauseReasons |= reason;
            }
            else
            {
                _pauseReasons &= ~reason;
            }

            if (previousReasons == ProcessListPauseReason.None && _pauseReasons != ProcessListPauseReason.None)
            {
                _runningProcessesView.IsLiveSorting = false;
                RunningProcesses.SuppressNotifications(true);
            }
            else if (previousReasons != ProcessListPauseReason.None && _pauseReasons == ProcessListPauseReason.None)
            {
                RunningProcesses.SuppressNotifications(false);
                _runningProcessesView.IsLiveSorting = true;
            }
        }

        partial void OnProcessNameFilterChanged(string value)
        {
            _runningProcessesView.Refresh();
        }

        private bool GameModeForcesNoMask =>
            _gameModeService.IsEnabled && !AppConfig.Instance.KeepMasksDuringGameMode;

        private void OnConfigChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppConfig.KeepMasksDuringGameMode) && _gameModeService.IsEnabled)
            {
                DispatchGameModeRefresh(applyMask: true);
            }
        }

        private void OnGameModeStateChanged(object? sender, GameModeStateChangedEventArgs e)
        {
            DispatchGameModeRefresh(applyMask: !AppConfig.Instance.KeepMasksDuringGameMode);
        }

        private void DispatchGameModeRefresh(bool applyMask)
        {
            void Refresh()
            {
                foreach (ProcessListEntryViewModel process in RunningProcesses)
                {
                    if (applyMask)
                    {
                        process.RefreshGameModeApply();
                    }
                    process.UpdateVisualMask();
                }
            }

            if (_dispatcher.CheckAccess())
            {
                Refresh();
            }
            else if (!_dispatcher.HasShutdownStarted)
            {
                _dispatcher.InvokeAsync(Refresh);
            }
        }

        private void OnNewProcess(ProcessInfo pInfo)
        {
            if (_isDisposed || _dispatcher.HasShutdownStarted)
            {
                pInfo.ProcessHandler.Dispose();
                return;
            }

            _dispatcher.InvokeAsync(() =>
            {
                if (!_isDisposed && !RunningProcesses.Any(x => x.Pid == pInfo.PID))
                {
                    RunningProcesses.Add(new ProcessListEntryViewModel(pInfo, _gameModeService));
                }
                else
                {
                    pInfo.ProcessHandler.Dispose();
                }
            });
        }

        private void OnExitedProcess(uint exitedPid)
        {
            if (_isDisposed || _dispatcher.HasShutdownStarted)
            {
                return;
            }

            _dispatcher.InvokeAsync(() =>
            {
                for (int i = RunningProcesses.Count - 1; i >= 0; --i)
                {
                    if (RunningProcesses[i].Pid == exitedPid)
                    {
                        RunningProcesses[i].Dispose();
                        RunningProcesses.RemoveAt(i);
                    }
                }
            });
        }

        private static ProcessListEntryViewModel? GetCurrentForegroundProcess()
        {
            IntPtr hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == 0)
            {
                return null;
            }

            NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
            return RunningProcesses.FirstOrDefault(x => x!.Pid == pid, null);
        }

        private async Task ProcessCpuUsageUpdateLoop()
        {
            try
            {
                bool wasVisible = true;
                while (true)
                {
                    _cancellationToken.ThrowIfCancellationRequested();
                    bool windowIsVisible = false;
                    await _dispatcher.InvokeAsync(() =>
                    {
                        windowIsVisible = Application.Current.MainWindow?.Visibility == Visibility.Visible;
                    });

                    if (!windowIsVisible)
                    {
                        if (wasVisible)
                        {
                            await _dispatcher.InvokeAsync(() =>
                                SetListPauseReason(ProcessListPauseReason.WindowHidden, true));
                            wasVisible = false;
                        }
                        await Task.Delay(UpdateDelayHiddenMs, _cancellationToken);
                        continue;
                    }

                    await _dispatcher.InvokeAsync(() =>
                    {
                        if (!wasVisible)
                        {
                            foreach (ProcessListEntryViewModel process in RunningProcesses)
                            {
                                process.ResetCpuUsageSampling();
                            }
                            SetListPauseReason(ProcessListPauseReason.WindowHidden, false);
                            wasVisible = true;
                        }

                        foreach (ProcessListEntryViewModel process in RunningProcesses)
                        {
                            process.UpdateCpuUsage();
                        }
                    });
                    await Task.Delay(UpdateDelayVisibleMs, _cancellationToken);
                }
            }
            catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested || _dispatcher.HasShutdownStarted)
            {
                // Application shutdown is expected.
            }
            catch (Exception ex)
            {
                WindowLogger.Write($"Process CPU-usage loop stopped unexpectedly: {ex}");
            }
        }

        private async Task ProcessReapplyLoop()
        {
            try
            {
                while (true)
                {
                    await Task.Delay(ReapplyInterval, _cancellationToken);
                    await _dispatcher.InvokeAsync(() =>
                    {
                        foreach (ProcessListEntryViewModel process in RunningProcesses)
                        {
                            if (process.AutoReapply)
                            {
                                process.ReapplyMask();
                            }
                        }
                    });
                }
            }
            catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested || _dispatcher.HasShutdownStarted)
            {
                // Application shutdown is expected.
            }
            catch (Exception ex)
            {
                WindowLogger.Write($"Automatic-reapply loop stopped unexpectedly: {ex}");
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            ProcessEvents.Default.ProcessCreated -= _processCreatedHandler;
            ProcessEvents.Default.ProcessExited -= _processExitedHandler;
            AppConfig.Instance.PropertyChanged -= _configChangedHandler;
            _gameModeService.StateChanged -= _gameModeChangedHandler;

            foreach (ProcessListEntryViewModel process in RunningProcesses.ToList())
            {
                process.Dispose();
            }
            RunningProcesses.Clear();
            Instance = null;
            GC.SuppressFinalize(this);
        }
    }
}
