using CommunityToolkit.Mvvm.ComponentModel;
using CPUSetSetter.Config.Models;
using CPUSetSetter.Platforms;
using CPUSetSetter.Util;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;


namespace CPUSetSetter.UI.Tabs.Processes
{
    public partial class ProcessesTabViewModel : ObservableObject
    {
        public static ProcessesTabViewModel? Instance { get; private set; }

        private readonly Dispatcher _dispatcher;
        private readonly ListCollectionView runningProcessesView;

        public static PausableObservableCollection<ProcessListEntryViewModel> RunningProcesses { get; } = [];

        [ObservableProperty]
        private string _processNameFilter = string.Empty;

        public ProcessesTabViewModel(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            Instance = this;

            ProcessEvents.Default.ProcessCreated += (_, e) => OnNewProcess(e.Info);
            ProcessEvents.Default.ProcessExited += (_, e) => OnExitedProcess(e.PID);
            ProcessEvents.Default.Start();

            runningProcessesView = (ListCollectionView)CollectionViewSource.GetDefaultView(RunningProcesses);
            runningProcessesView.SortDescriptions.Add(new(nameof(ProcessListEntryViewModel.AverageCpuUsage), ListSortDirection.Descending));
            runningProcessesView.IsLiveSorting = true;
            runningProcessesView.LiveSortingProperties.Add(nameof(ProcessListEntryViewModel.AverageCpuUsage));
            runningProcessesView.Filter = item => ((ProcessListEntryViewModel)item).Name.Contains(ProcessNameFilter, StringComparison.OrdinalIgnoreCase);

            Task.Run(ProcessCpuUsageUpdateLoop);
        }

        /// <summary>
        /// Triggered by a LogicalProcessorMask when its hotkeys are pressed
        /// </summary>
        public void OnMaskHotkeyPressed(LogicalProcessorMask mask)
        {
            ProcessListEntryViewModel? foregroundProcess = GetCurrentForegroundProcess();
            if (foregroundProcess is not null)
            {
                bool success = foregroundProcess.SetMask(mask, true);
                if (success)
                {
                    if (mask.IsNoMask)
                        HotkeySoundPlayer.Default.PlayCleared();
                    else
                        HotkeySoundPlayer.Default.PlayApplied();
                }
                else
                {
                    HotkeySoundPlayer.PlayError();
                }
            }
        }

        /// <summary>
        /// Pause the live sorting of the Processes list
        /// </summary>
        public void PauseListUpdates()
        {
            if (runningProcessesView != null)
            {
                runningProcessesView.IsLiveSorting = false;
                RunningProcesses.SuppressNotifications(true);
            }
        }

        /// <summary>
        /// Resume the live sorting of the Processes list
        /// </summary>
        public void ResumeListUpdates()
        {
            if (runningProcessesView != null)
            {
                RunningProcesses.SuppressNotifications(false);
                runningProcessesView.IsLiveSorting = true;
            }
        }

        partial void OnProcessNameFilterChanged(string value)
        {
            runningProcessesView.Refresh();
        }

        private void OnNewProcess(ProcessInfo pInfo)
        {
            _dispatcher.Invoke(() =>
            {
                if (!RunningProcesses.Any(x => x.Pid == pInfo.PID))
                {
                    RunningProcesses.Add(new ProcessListEntryViewModel(pInfo));
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
                        RunningProcesses[i].Dispose();
                        RunningProcesses.RemoveAt(i);
                    }
                }
            });
        }

        private ProcessListEntryViewModel? GetCurrentForegroundProcess()
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
            while (true)
            {
                bool windowIsVisible = false;
                await _dispatcher.InvokeAsync(() =>
                {
                    windowIsVisible = App.Current.MainWindow.Visibility == Visibility.Visible;
                    foreach (ProcessListEntryViewModel pEntry in RunningProcesses)
                    {
                        pEntry.UpdateCpuUsage();
                    }
                });
                int delayTime = windowIsVisible ? 1000 : 5000; // Poll the CPU usage less often when not visible
                await Task.Delay(delayTime);
            }
        }
    }
}
