using CPUSetSetter.Platforms.Windows;
using Microsoft.Win32.SafeHandles;
using System.Management;


namespace CPUSetSetter.Platforms
{
    public class ProcessEventsWindows : IProcessEvents
    {
        private bool _hasStarted = false;

        public event EventHandler<NewProcessEventArgs>? ProcessCreated;
        public event EventHandler<ExitedProcessEventArgs>? ProcessExited;

        public void Start()
        {
            if (_hasStarted)
                return;
            _hasStarted = true;

            StartNewProcessListener();
            StartExitedProcessListener();
            ListCurrentProcesses();
        }

        private void ListCurrentProcesses()
        {
            ManagementObjectSearcher searcher = new("SELECT Name, ProcessId, CreationDate FROM Win32_Process");

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
                ProcessExited?.Invoke(this, new(pid));
            };

            watcher.Start();
        }

        private void AddNewProcess(ManagementBaseObject process)
        {
            ProcessInfo pInfo = ParseManagementProcess(process);
            ProcessCreated?.Invoke(this, new NewProcessEventArgs(pInfo));
        }

        private static ProcessInfo ParseManagementProcess(ManagementBaseObject process)
        {
            string name = (string)process["Name"];
            uint pid = (uint)process["ProcessId"];

            SafeProcessHandle hProcess = NativeMethods.OpenProcess(ProcessAccessFlags.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            string exePath;
            if (!hProcess.IsInvalid)
            {
                char[] buffer = new char[1024];
                uint size = 1024;
                bool success = NativeMethods.QueryFullProcessImageNameW(hProcess, 0, buffer, ref size);
                exePath = success ? new string(buffer[..(int)size]) : "";
            }
            else
            {
                exePath = "";
            }

            return new(name, exePath, pid, new ProcessHandlerWindows(name, pid, hProcess));
        }
    }
}
