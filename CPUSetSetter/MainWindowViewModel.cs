using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using Application = System.Windows.Application;


namespace CPUSetSetter
{
    public partial class MainWindowViewModel : ObservableObject
    {
        public static MainWindowViewModel? Instance { get; private set; }

        public static PausableObservableCollection<ProcessListEntry> RunningProcesses { get; } = [];
        public static bool IsRunning { get; private set; } = false;
        private readonly Dispatcher _dispatcher;

        public bool HotkeyInputSelected { get; private set; } = false;

        [ObservableProperty]
        private ListCollectionView _runningProcessesView;

        [ObservableProperty]
        private string _processNameFilter = "";

        [ObservableProperty]
        private ProcessListEntry? _currentForegroundProcess;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SettingsCanRemoveSet))]
        private CPUSet? _settingsSelectedCpuSet;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SettingsCanAddNewSet))]
        private string _settingsNewCpuSetName = "";

        public bool SettingsCanAddNewSet => SettingsNewCpuSetName.Length > 0 && !Config.Default.CpuSets.Any(s => s.SettingsName == SettingsNewCpuSetName);
        public bool SettingsCanRemoveSet => SettingsSelectedCpuSet is not null && !SettingsSelectedCpuSet.IsUnset;

        [RelayCommand]
        private void AddNewSet()
        {
            // Verify that this name is not invalid or already in use
            if (!SettingsCanAddNewSet)
            {
                return;
            }
            CPUSet newCpuSet = new(SettingsNewCpuSetName);
            Config.Default.CpuSets.Add(newCpuSet);
            SettingsSelectedCpuSet = newCpuSet;
            SettingsNewCpuSetName = "";
        }

        [RelayCommand]
        private void RemoveSet()
        {
            if (!SettingsCanRemoveSet)
                return;
            SettingsSelectedCpuSet?.Remove();
        }

        [RelayCommand]
        private void ClearHotkey()
        {
            SettingsSelectedCpuSet?.Hotkey.Clear();
        }

        public MainWindowViewModel(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            Instance = this;

            ProcessEvents processEvents = new();
            processEvents.OnNewProcess += (_, e) => OnNewProcess(e.Process);
            processEvents.OnExitedProcess += (_, e) => OnExitedProcess(e.PID);
            processEvents.Start();

            RunningProcessesView = (ListCollectionView)CollectionViewSource.GetDefaultView(RunningProcesses);
            RunningProcessesView.SortDescriptions.Add(new(nameof(ProcessListEntry.AverageCpuUsage), ListSortDirection.Descending));
            RunningProcessesView.IsLiveSorting = true; 
            RunningProcessesView.LiveSortingProperties.Add(nameof(ProcessListEntry.AverageCpuUsage));

            RunningProcessesView.Filter = item => ((ProcessListEntry)item).Name.Contains(ProcessNameFilter, StringComparison.OrdinalIgnoreCase);

            // Set up the key listener to enter new keystrokes in the hotkey TextBox when it is selected
            HotkeyListener.Instance.KeyDown += (_, e) =>
            {
                if (HotkeyInputSelected && SettingsSelectedCpuSet is not null && !SettingsSelectedCpuSet.Hotkey.Contains(e.Key))
                {
                    SettingsSelectedCpuSet.Hotkey.Add(e.Key);
                }
            };

            // Update the SettingsCanAddNewSet property when CPU Sets are added/removed
            Config.Default.CpuSets.CollectionChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(SettingsCanAddNewSet));
            };

            Task.Run(ForegroundProcessUpdateLoop);
            Task.Run(ProcessCpuUsageUpdateLoop);

            IsRunning = true;
        }

        public void OnHotkeyInputFocusChanged(bool isFocused)
        {
            HotkeyInputSelected = isFocused;
            HotkeyListener.Instance.CallbacksEnabled = !isFocused;
        }

        public void OnCpuSetHotkeyPressed(CPUSet cpuSet)
        {
            UpdateCurrentForegroundProcess();
            if (CurrentForegroundProcess is not null)
            {
                CurrentForegroundProcess.CpuSet = cpuSet;
            }
        }

        private void OnNewProcess(ProcessInfo pInfo)
        {
            _dispatcher.Invoke(() =>
            {
                if (!RunningProcesses.Any(x => x.Pid == pInfo.PID))
                {
                    RunningProcesses.Add(new ProcessListEntry(pInfo));
                }
            });
        }

        private void OnExitedProcess(uint exitedPid)
        {
            _dispatcher.Invoke(() =>
            {
                for (int i = RunningProcesses.Count - 1; i >= 0; --i)
                {
                    if (RunningProcesses[i].Pid == exitedPid)
                    {
                        RunningProcesses.RemoveAt(i);
                    }
                }
            });
        }

        partial void OnProcessNameFilterChanged(string value)
        {
            RunningProcessesView.Refresh();
        }

        private async Task ForegroundProcessUpdateLoop()
        {
            while (true)
            {
                UpdateCurrentForegroundProcess();
                await Task.Delay(2000);
            }
        }

        private void UpdateCurrentForegroundProcess()
        {
            IntPtr hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == 0)
            {
                return;
            }

            NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);

            CurrentForegroundProcess = RunningProcesses.FirstOrDefault(x => x!.Pid == pid, null);
        }

        private async Task ProcessCpuUsageUpdateLoop()
        {
            while (true)
            {
                bool windowIsVisible = false;
                await _dispatcher.InvokeAsync(() =>
                {
                    windowIsVisible = Application.Current.MainWindow.Visibility == Visibility.Visible;
                    foreach (ProcessListEntry pEntry in RunningProcesses)
                    {
                        pEntry.UpdateCpuUsage();
                    }
                });
                int delayTime = windowIsVisible ? 1000 : 5000; // Poll the CPU usage less often when not visible
                await Task.Delay(delayTime);
            }
        }

        public void PauseListUpdates()
        {
            if (RunningProcessesView != null)
            {
                RunningProcessesView.IsLiveSorting = false;
                RunningProcesses.SuppressNotifications(true);
            }
        }

        public void ResumeListUpdates()
        {
            if (RunningProcessesView != null)
            {
                RunningProcesses.SuppressNotifications(false);
                RunningProcessesView.IsLiveSorting = true;
            }
        }

    }
}
