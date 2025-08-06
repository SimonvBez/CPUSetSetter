using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows.Threading;


namespace CPUSetSetter.UI
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly Dispatcher _dispatcher;

        [ObservableProperty]
        private ObservableCollection<ProcessInfo> _runningProcesses = new();

        public MainWindowViewModel(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;

            ProcessEvents processEvents = new();
            processEvents.OnNewProcess += (_, e) => OnNewProcess(e.Process);
            processEvents.OnExitedProcess += (_, e) => OnExitedProcess(e.PID);
            processEvents.Start();
        }

        private void OnNewProcess(ProcessInfo pInfo)
        {
            _dispatcher.Invoke(() =>
            {
                if (!RunningProcesses.Any(x => x.PID == pInfo.PID))
                {
                    RunningProcesses.Add(pInfo);
                }
            });
        }

        private void OnExitedProcess(uint exitedPid)
        {
            _dispatcher.Invoke(() =>
            {
                for (int i = RunningProcesses.Count - 1; i >= 0; --i)
                {
                    if (RunningProcesses[i].PID == exitedPid)
                    {
                        RunningProcesses.RemoveAt(i);
                    }
                }
            });
        }
    }
}
