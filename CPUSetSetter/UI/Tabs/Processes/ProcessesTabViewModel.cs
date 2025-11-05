using CommunityToolkit.Mvvm.ComponentModel;
using CPUSetSetter.Config.Models;
using CPUSetSetter.Platforms;
using CPUSetSetter.UI.Tabs.Processes.CoreUsage;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
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

        // Core usage collection
        public ObservableCollection<CPUSetSetter.UI.Tabs.Processes.CoreUsage.CoreUsage> CoreUsages { get; } = new();

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

            // Initialize per-core collection
            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                CoreUsages.Add(new CPUSetSetter.UI.Tabs.Processes.CoreUsage.CoreUsage(i));
            }

            Task.Run(ForegroundProcessUpdateLoop);
            Task.Run(ProcessCpuUsageUpdateLoop);
            Task.Run(PerCoreUsageUpdateLoop);
        }

        /// <summary>
        /// Triggered by a LogicalProcessorMask when its hotkeys are pressed
        /// </summary>
        public void OnMaskHotkeyPressed(LogicalProcessorMask mask)
        {
            UpdateCurrentForegroundProcess();
            var foregroundProcess = CurrentForegroundProcess;
            if (foregroundProcess is not null)
            {
                bool success = foregroundProcess.SetMask(mask, true);
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

        /// <summary>
        /// Pause the live sorting of the Processes list
        /// </summary>
        public void PauseListUpdates()
        {
            if (RunningProcessesView != null)
            {
                RunningProcessesView.IsLiveSorting = false;
                RunningProcesses.SuppressNotifications(true);
            }
        }

        /// <summary>
        /// Resume the live sorting of the Processes list
        /// </summary>
        public void ResumeListUpdates()
        {
            if (RunningProcessesView != null)
            {
                RunningProcesses.SuppressNotifications(false);
                RunningProcessesView.IsLiveSorting = true;
            }
        }

        partial void OnProcessNameFilterChanged(string value)
        {
            RunningProcessesView.Refresh();
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

        private async Task PerCoreUsageUpdateLoop()
        {
            // Use Windows per-processor performance counters: "Processor", "% Processor Time"; and parking from "Processor Information", "Parking Status".
            var usageCounters = new System.Diagnostics.PerformanceCounter[Environment.ProcessorCount];
            System.Diagnostics.PerformanceCounter[]? parkingCounters = null;
            try
            {
                for (int i = 0; i < usageCounters.Length; i++)
                {
                    usageCounters[i] = new System.Diagnostics.PerformanceCounter("Processor", "% Processor Time", i.ToString());
                    _ = usageCounters[i].NextValue();
                }
            }
            catch
            {
                // ignore
            }

            try
            {
                var cat = new System.Diagnostics.PerformanceCounterCategory("Processor Information");
                var instanceNames = cat.GetInstanceNames()
                .Where(n => n != "_Total" && Regex.IsMatch(n, @"^\d+,\d+$"))
                .Select(n =>
                {
                    var parts = n.Split(',');
                    return new { Name = n, Node = int.Parse(parts[0]), Cpu = int.Parse(parts[1]) };
                })
                .OrderBy(x => x.Node).ThenBy(x => x.Cpu)
                .Select(x => x.Name)
                .ToArray();

                int count = Math.Min(instanceNames.Length, CoreUsages.Count);
                parkingCounters = new System.Diagnostics.PerformanceCounter[count];
                for (int i = 0; i < count; i++)
                {
                    parkingCounters[i] = new System.Diagnostics.PerformanceCounter("Processor Information", "Parking Status", instanceNames[i]);
                    _ = parkingCounters[i].NextValue();
                }
            }
            catch (Exception ex)
            {
                // Parking counters may not exist; leave null
            }

            while (true)
            {
                float[] usageValues = new float[CoreUsages.Count];
                bool[] parkedValues = new bool[CoreUsages.Count];

                for (int i = 0; i < usageValues.Length; i++)
                {
                    float v = 0f;
                    try { v = usageCounters[i]?.NextValue() ?? 0f; } catch { }
                    usageValues[i] = Math.Clamp(v, 0f, 100f);

                    if (parkingCounters is not null && i < parkingCounters.Length)
                    {
                        try
                        {
                            float p = parkingCounters[i]?.NextValue() ?? 0f; // Typically 0 or 1
                            parkedValues[i] = p > 0.5f; // treat >0.5 as parked
                        }
                        catch
                        {
                            parkedValues[i] = false;
                        }
                    }
                }

                await _dispatcher.InvokeAsync(() =>
                {
                    for (int i = 0; i < CoreUsages.Count; i++)
                    {
                        CoreUsages[i].UsagePercent = usageValues[i];
                        if (i < parkedValues.Length)
                            CoreUsages[i].IsParked = parkedValues[i];
                        else
                            CoreUsages[i].IsParked = false;
                    }
                });

                await Task.Delay(1000);
            }
        }
    }
}
