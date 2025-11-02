using CommunityToolkit.Mvvm.ComponentModel;
using CPUSetSetter.Config.Models;
using CPUSetSetter.Platforms;
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

        public static PausableObservableCollection<ProcessListEntryViewModel> RunningProcesses { get; } = [];
        public ListCollectionView RunningProcessesView;

        [ObservableProperty]
        private string _processNameFilter = string.Empty;

        [ObservableProperty]
        private ProcessListEntryViewModel? _currentForegroundProcess;

        public ProcessesTabViewModel(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            Instance = this;

            ProcessEvents.Default.ProcessCreated += (_, e) => OnNewProcess(e.Info);
            ProcessEvents.Default.ProcessExited += (_, e) => OnExitedProcess(e.PID);
            ProcessEvents.Default.Start();

            RunningProcessesView = (ListCollectionView)CollectionViewSource.GetDefaultView(RunningProcesses);
            RunningProcessesView.SortDescriptions.Add(new(nameof(ProcessListEntryViewModel.AverageCpuUsage), ListSortDirection.Descending));
            RunningProcessesView.IsLiveSorting = true;
            RunningProcessesView.LiveSortingProperties.Add(nameof(ProcessListEntryViewModel.AverageCpuUsage));
            RunningProcessesView.Filter = item => ((ProcessListEntryViewModel)item).Name.Contains(ProcessNameFilter, StringComparison.OrdinalIgnoreCase);

            Task.Run(ForegroundProcessUpdateLoop);
            Task.Run(ProcessCpuUsageUpdateLoop);
        }

        public void OnMaskHotkeyPressed(LogicalProcessorMask mask)
        {
            UpdateCurrentForegroundProcess();
            var foregroundProcess = CurrentForegroundProcess;
            if (foregroundProcess is not null)
            {
                bool success = foregroundProcess.SetMask(mask);
                if (success)
                {
                    if (mask.IsNoMask)
                        HotkeySoundPlayer.Instance.PlayCleared();
                    else
                        HotkeySoundPlayer.Instance.PlayApplied();
                }
                else
                {
                    HotkeySoundPlayer.PlayError();
                }
            }
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

            _dispatcher.BeginInvoke(() =>
            {
                CurrentForegroundProcess = RunningProcesses.FirstOrDefault(x => x!.Pid == pid, null);
            });
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
