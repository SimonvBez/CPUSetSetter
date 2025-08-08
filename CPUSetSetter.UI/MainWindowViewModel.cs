using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Threading;


namespace CPUSetSetter.UI
{
    public partial class MainWindowViewModel : ObservableObject
    {
        public static ObservableCollection<ProcessListEntry> RunningProcesses { get; } = [];
        private readonly Dispatcher _dispatcher;

        [ObservableProperty]
        private ICollectionView _runningProcessesView;

        [ObservableProperty]
        private string _processNameFilter = "";

        [ObservableProperty]
        private ProcessListEntry? _currentForegroundProcess;

        [ObservableProperty]
        private CPUSet? _settingsSelectedCpuSet;

        [ObservableProperty]
        private string _settingsNewCpuSetName = "";

        [RelayCommand]
        private void AddNewSet()
        {
            string newName = SettingsNewCpuSetName;
            if (newName.Length == 0 || Config.Default.CpuSets.Any(s => s.Name == newName))
            {
                return;
            }
            Config.Default.CpuSets.Add(new CPUSet(SettingsNewCpuSetName));
            SettingsNewCpuSetName = "";
        }

        [RelayCommand]
        private void RemoveSet()
        {
            SettingsSelectedCpuSet?.Remove();
        }

        public MainWindowViewModel(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;

            ProcessEvents processEvents = new();
            processEvents.OnNewProcess += (_, e) => OnNewProcess(e.Process);
            processEvents.OnExitedProcess += (_, e) => OnExitedProcess(e.PID);
            processEvents.Start();

            RunningProcessesView = CollectionViewSource.GetDefaultView(RunningProcesses);
            RunningProcessesView.SortDescriptions.Add(new SortDescription(nameof(ProcessListEntry.CreationTime), ListSortDirection.Descending));

            RunningProcessesView.Filter = item => ((ProcessListEntry)item).Name.Contains(ProcessNameFilter, StringComparison.OrdinalIgnoreCase);

            Task.Run(ForegroundProcessUpdateLoop);
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
                Inner();
                await Task.Delay(2000);
            }

            void Inner()
            {
                IntPtr hwnd = NativeMethods.GetForegroundWindow();
                if (hwnd == 0)
                {
                    return;
                }

                NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);

                _dispatcher.Invoke(() =>
                {
                    CurrentForegroundProcess = RunningProcesses.FirstOrDefault(x => x!.Pid == pid, null);
                });
            }
        }
    }
}
