using System.Management;


namespace CPUSetSetter.UI
{
    public class ProcessEvents
    {
        public event EventHandler<NewProcessEventArgs>? OnNewProcess;
        public event EventHandler<ExitedProcessEventArgs>? OnExitedProcess;

        public void Start()
        {
            ListCurrentProcesses();
            StartNewProcessListener();
            StartExitedProcessListener();
        }

        private void ListCurrentProcesses()
        {
            ManagementObjectSearcher searcher = new("SELECT Name, ExecutablePath, ProcessId, CreationDate FROM Win32_Process");

            foreach (ManagementBaseObject process in searcher.Get())
            {
                AddNewProcess(process);
            }
        }

        private void StartNewProcessListener()
        {
            string query = "SELECT * FROM __InstanceCreationEvent WITHIN 5 WHERE TargetInstance ISA 'Win32_Process'";
            ManagementEventWatcher watcher = new(new WqlEventQuery(query));

            watcher.EventArrived += (_, e) =>
            {
                var process = (ManagementBaseObject)e.NewEvent["TargetInstance"];
                AddNewProcess(process);
            };

            watcher.Start();
        }

        private void StartExitedProcessListener()
        {
            string query = "SELECT * FROM __InstanceDeletionEvent WITHIN 5 WHERE TargetInstance ISA 'Win32_Process'";
            ManagementEventWatcher watcher = new(new WqlEventQuery(query));

            watcher.EventArrived += (_, e) =>
            {
                var process = (ManagementBaseObject)e.NewEvent["TargetInstance"];
                uint pid = (uint)process["ProcessId"];
                OnExitedProcess?.Invoke(this, new ExitedProcessEventArgs { PID = pid });
            };

            watcher.Start();
        }

        private void AddNewProcess(ManagementBaseObject process)
        {
            ProcessInfo pInfo = ParseManagementProcess(process);
            OnNewProcess?.Invoke(this, new NewProcessEventArgs { Process = pInfo });
        }

        private static ProcessInfo ParseManagementProcess(ManagementBaseObject process)
        {
            string name = (string)process["Name"];
            string? exePath = (string?)process["ExecutablePath"];
            uint pid = (uint)process["ProcessId"];
            DateTime creationTime = ManagementDateTimeConverter.ToDateTime((string)process["CreationDate"]);
            return new ProcessInfo
            {
                Name = name,
                ImagePath = exePath,
                PID = pid,
                CreationTime = creationTime
            };
        }
    }

    public class ProcessInfo
    {
        public required string Name { get; set; }
        public string? ImagePath { get; set; }
        public uint PID { get; set; }
        public DateTime CreationTime { get; set; }
    }

    public class NewProcessEventArgs : EventArgs
    {
        public required ProcessInfo Process { get; set; }
    }

    public class ExitedProcessEventArgs : EventArgs
    {
        public uint PID { get; set; }
    }
}
