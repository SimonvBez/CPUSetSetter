using Microsoft.Win32.SafeHandles;
using System.Management;
using System.Runtime.InteropServices;


namespace CPUSetLib
{
    public class CPUSetSetter
    {
        private Dictionary<uint, ProcessInfo> _currentProcesses = new();


        public event EventHandler<NewProcessEventArgs>? OnNewProcessSpawned;

        public CPUSetSetter()
        {
            
        }

        public void Start()
        {
            ListCurrentProcesses();
            StartNewProcessListener();
        }

        private void ListCurrentProcesses()
        {
            ManagementObjectSearcher searcher = new("SELECT Name, ExecutablePath, ProcessId, CreationDate FROM Win32_Process");

            foreach (ManagementBaseObject process in searcher.Get())
            {
                ProcessInfo pInfo = ParseManegementProcess(process);
                OnNewProcessSpawned?.Invoke(this, new NewProcessEventArgs { Process = pInfo });
            }
        }

        private void StartNewProcessListener()
        {
            string query = "SELECT * FROM __InstanceCreationEvent WITHIN 5 WHERE TargetInstance ISA 'Win32_Process'";
            ManagementEventWatcher watcher = new(new WqlEventQuery(query));

            watcher.EventArrived += (_, e) =>
            {
                var process = (ManagementBaseObject)e.NewEvent["TargetInstance"];
                ProcessInfo pInfo = ParseManegementProcess(process);

                OnNewProcessSpawned?.Invoke(this, new NewProcessEventArgs { Process = pInfo });
            };

            watcher.Start();
        }

        private static ProcessInfo ParseManegementProcess(ManagementBaseObject process)
        {
            string name = (string)process["Name"];
            string exePath = (string)process["ExecutablePath"];
            uint pid = (uint)process["ProcessId"];
            DateTime creationTime = ManagementDateTimeConverter.ToDateTime((string)process["CreationDate"]);
            return new ProcessInfo { ExecutableName = name, FullPath = exePath, PID = pid, CreationTime = creationTime };
        }

        private static bool ApplyCpuSetMaskToProcess(int pid, ulong coreMask)
        {
            using SafeProcessHandle hProcess = NativeMethods.OpenProcess(ProcessAccessFlags.PROCESS_SET_LIMITED_INFORMATION, false, pid);
            if (hProcess.IsInvalid)
            {
                return false;
            }

            var affinity = new GROUP_AFFINITY
            {
                Group = 0,
                Mask = coreMask
            };

            return NativeMethods.SetProcessDefaultCpuSetMasks(hProcess.DangerousGetHandle(), ref affinity, 1);
        }

        private static bool GetCpuSetMask(int pid, out CPUSet cpuSet)
        {
            using SafeProcessHandle hProcess = NativeMethods.OpenProcess(ProcessAccessFlags.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProcess.IsInvalid)
            {
                throw new UnauthorizedAccessException();
            }

            // First call to get how many entries we need
            bool success = NativeMethods.GetProcessDefaultCpuSetMasks(hProcess, null, 0, out uint requiredCount);
            if (!success)
            {
                throw new InvalidOperationException($"Failed to get CPU ");
            }

            if (requiredCount == 0)
            {
                cpuSet = new CPUSet { State = CPUSetState.Unset };
                return true;
            }

            // CHATGPT CODE
            //var masks = new NativeMethods.GROUP_AFFINITY[requiredCount];

            //bool success = NativeMethods.GetProcessDefaultCpuSetMasks(hProcess, masks, requiredCount, out _);

            //if (!success)
            //{
            //    Console.WriteLine($"[!] Failed to get CPU set masks for PID {pid}. Error: {Marshal.GetLastWin32Error()}");
            //    return;
            //}

            //for (int i = 0; i < requiredCount; i++)
            //{
            //    var m = masks[i];
            //    Console.WriteLine($"Group: {m.Group}, Mask: 0x{m.Mask:X}");
            //}
        }
    }

    public enum CPUSetState
    {
        Set,
        Unset
    }

    public class CPUSet
    {
        public CPUSetState State { get; set; }
        public ulong Mask { get; set; }
    }

    public class ProcessInfo
    {
        public required string ExecutableName { get; set; }
        public string? FullPath { get; set; }
        public uint PID { get; set; }
        public DateTime CreationTime { get; set; }
        public required CPUSet WantedSet { get; set; }
        public required CPUSet ActualSet { get; set; }
    }

    public class NewProcessEventArgs : EventArgs
    {
        public required ProcessInfo Process { get; set; }
    }
}
