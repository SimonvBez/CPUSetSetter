using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Threading;


namespace CPUSetSetter.UI
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly Dispatcher _dispatcher;
        private readonly ObservableCollection<ProcessListEntry> _runningProcesses = [];

        [ObservableProperty]
        private ICollectionView _runningProcessesView;

        [ObservableProperty]
        private string _processNameFilter = "";

        public MainWindowViewModel(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;

            ProcessEvents processEvents = new();
            processEvents.OnNewProcess += (_, e) => OnNewProcess(e.Process);
            processEvents.OnExitedProcess += (_, e) => OnExitedProcess(e.PID);
            processEvents.Start();

            RunningProcessesView = CollectionViewSource.GetDefaultView(_runningProcesses);
            RunningProcessesView.SortDescriptions.Add(new SortDescription(nameof(ProcessListEntry.CreationTime), ListSortDirection.Descending));

            RunningProcessesView.Filter = item => ((ProcessListEntry)item).Name.Contains(ProcessNameFilter, StringComparison.OrdinalIgnoreCase);
        }

        private void OnNewProcess(ProcessInfo pInfo)
        {
            _dispatcher.Invoke(() =>
            {
                if (!_runningProcesses.Any(x => x.Pid == pInfo.PID))
                {
                    _runningProcesses.Add(new ProcessListEntry(pInfo));
                }
            });
        }

        private void OnExitedProcess(uint exitedPid)
        {
            _dispatcher.Invoke(() =>
            {
                for (int i = _runningProcesses.Count - 1; i >= 0; --i)
                {
                    if (_runningProcesses[i].Pid == exitedPid)
                    {
                        _runningProcesses.RemoveAt(i);
                    }
                }
            });
        }

        partial void OnProcessNameFilterChanged(string value)
        {
            RunningProcessesView.Refresh();
        }
    }


    public partial class ProcessListEntry : ObservableObject
    {
        [ObservableProperty]
        private uint _pid;

        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private string _path;

        [ObservableProperty]
        private DateTime _creationTime;

        [ObservableProperty]
        private string _cpuSetName;

        public ProcessListEntry(ProcessInfo pInfo)
        {
            Pid = pInfo.PID;
            Name = pInfo.Name;
            Path = pInfo.ImagePath ?? "";
            CreationTime = pInfo.CreationTime;
            CpuSetName = "";
        }

        partial void OnCpuSetNameChanged(string value)
        {
            
        }
    }
}
