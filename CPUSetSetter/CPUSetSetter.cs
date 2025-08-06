using Microsoft.Win32.SafeHandles;
using System.Management;


namespace CPUSetLib
{
    public class CPUSetSetter
    {
        public event EventHandler<ProcessEventArgs>? OnNewProcess;
        public event EventHandler<ProcessEventArgs>? OnExitedProcess;

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
                ProcessInfo pInfo = ParseManagementProcess(process);
                OnExitedProcess?.Invoke(this, new ProcessEventArgs { Process = pInfo });
            };

            watcher.Start();
        }

        private void AddNewProcess(ManagementBaseObject process)
        {
            ProcessInfo pInfo = ParseManagementProcess(process);
            OnNewProcess?.Invoke(this, new ProcessEventArgs { Process = pInfo });
        }

        private static ProcessInfo ParseManagementProcess(ManagementBaseObject process)
        {
            string name = (string)process["Name"];
            string? exePath = (string?)process["ExecutablePath"];
            uint pid = (uint)process["ProcessId"];
            DateTime creationTime = ManagementDateTimeConverter.ToDateTime((string)process["CreationDate"]);
            return new ProcessInfo
            {
                ExecutableName = name.ToLower(),
                FullPath = exePath?.ToLower(),
                PID = pid,
                CreationTime = creationTime
            };
        }

        public static bool ApplyCpuSetMaskToProcess(uint pid, ulong coreMask)
        {
            using SafeProcessHandle hProcess = NativeMethods.OpenProcess(ProcessAccessFlags.PROCESS_SET_LIMITED_INFORMATION, false, pid);
            if (hProcess.IsInvalid)
            {
                return false;
            }

            if (coreMask == 0)
            {
                return NativeMethods.SetProcessDefaultCpuSetMasks(hProcess, null, 0);
            }

            GROUP_AFFINITY[] affinity = [
                new GROUP_AFFINITY {
                    Group = 0,
                    Mask = coreMask
                }
            ];

            return NativeMethods.SetProcessDefaultCpuSetMasks(hProcess, affinity, 1);
        }

        //private static bool GetCpuSetMask(uint pid, out CPUSet cpuSet)
        //{
        //    using SafeProcessHandle hProcess = NativeMethods.OpenProcess(ProcessAccessFlags.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        //    if (hProcess.IsInvalid)
        //    {
        //        throw new UnauthorizedAccessException();
        //    }

        //    // First call to get how many entries we need
        //    bool success = NativeMethods.GetProcessDefaultCpuSetMasks(hProcess, null, 0, out uint requiredCount);
        //    if (!success)
        //    {
        //        throw new InvalidOperationException($"Failed to get CPU Set masks");
        //    }

        //    if (requiredCount == 0)
        //    {
        //        // No CPU Set has been configured for this process
        //        cpuSet = CPUSet.Unset;
        //        return true;
        //    }
        //    else if (requiredCount > 1)
        //    {
        //        // More than 64 CPUs are not supported
        //        throw new NotImplementedException("Only single CPU Set masks are supported (max 64 CPUs)");
        //    }

        //    // Get the CPU Set of this process
        //    var masks = new GROUP_AFFINITY[requiredCount];
        //    success = NativeMethods.GetProcessDefaultCpuSetMasks(hProcess, masks, requiredCount, out _);
        //    if (!success)
        //    {
        //        throw new InvalidOperationException($"Failed to get CPU Set masks");
        //    }

        //    cpuSet = new CPUSet { State = CPUSetState.Set, Mask = masks[0].Mask };
        //    return true;
        //}
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

        public static CPUSet Unset => new() { State = CPUSetState.Unset, Mask = 0 };

        public override bool Equals(object? obj)
        {
            if (obj is not CPUSet other)
            {
                return false;
            }

            if (State == other.State)
            {
                // Always return true if both States are Unset, disregarding of Mask
                return State == CPUSetState.Unset || Mask == other.Mask;
            }
            return false;
        }

        public override int GetHashCode() => (State, Mask).GetHashCode();

        public static bool operator ==(CPUSet lhs, CPUSet rhs)
        {
            if (lhs is null)
            {
                if (rhs is null)
                {
                    return true;
                }

                // Only the left side is null.
                return false;
            }
            // Equals handles case of null on right side.
            return lhs.Equals(rhs);
        }

        public static bool operator !=(CPUSet lhs, CPUSet rhs) => !(lhs == rhs);
    }

    public class ProcessInfo
    {
        public required string ExecutableName { get; set; }
        public string? FullPath { get; set; }
        public uint PID { get; set; }
        public DateTime CreationTime { get; set; }
    }

    public class ProcessEventArgs : EventArgs
    {
        public required ProcessInfo Process { get; set; }
    }
}
