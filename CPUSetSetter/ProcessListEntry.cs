using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Win32.SafeHandles;


namespace CPUSetSetter
{
    public partial class ProcessListEntry : ObservableObject
    {
        [ObservableProperty]
        private uint _pid;

        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private string _path;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AverageCpuPercentageStr))]
        private double _averageCpuUsage;

        [ObservableProperty]
        private DateTime _creationTime;

        [ObservableProperty]
        private CPUSet _cpuSet;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FailedToOpen))]
        public SafeProcessHandle? _setLimitedInfoHandle;

        [ObservableProperty]
        public SafeProcessHandle _queryLimitedInfoHandle;

        public string AverageCpuPercentageStr => QueryLimitedInfoHandle.IsInvalid ? "" : $"{AverageCpuUsage * 100:F1}%";

        public bool FailedToOpen => SetLimitedInfoHandle?.IsInvalid == true;

        private readonly Queue<CpuTimeTimestamp> _cpuTimeMovingAverageBuffer = new();

        public ProcessListEntry(ProcessInfo pInfo)
        {
            Pid = pInfo.PID;
            Name = pInfo.Name;
            Path = pInfo.ImagePath;
            CreationTime = pInfo.CreationTime;
            CpuSet = GetConfiguredCpuSet();
            QueryLimitedInfoHandle = pInfo.QueryHandle;
        }

        partial void OnCpuSetChanged(CPUSet? oldValue, CPUSet newValue)
        {
            oldValue?.RemoveProcess(this);
            newValue.AddProcess(this, oldValue is not null || !newValue.IsUnset); // Don't apply the Unset CPUSet when the program is first started

            if (newValue == CPUSet.Unset)
            {
                Config.Default.RemoveProcessCpuSet(Name, Path); // Cpu set was cleared
            }
            else
            {
                Config.Default.SetProcessCpuSet(Name, Path, CpuSet.Name); // Find the process CPU Set definition and change its CPU set
            }
        }

        public CPUSet GetConfiguredCpuSet()
        {
            ProcessCPUSet? processCPUSet = Config.Default.GetProcessCpuSetByName(Name, Path);
            if (processCPUSet is null)
            {
                return CPUSet.Unset; // No process definition, go with Unset
            }
            else
            {
                // Process CPU Set definition exists, take the belonging CPUSet
                return Config.Default.GetCpuSetByName(processCPUSet.CpuSetName) ?? CPUSet.Unset;
            }
        }

        public void UpdateCpuUsage()
        {
            if (QueryLimitedInfoHandle.IsInvalid)
            {
                return;
            }

            DateTime now = DateTime.Now;
            // Remove datapoints older than 1 minute from the moving average buffer
            while (_cpuTimeMovingAverageBuffer.Count > 0)
            {
                TimeSpan datapointAge = now - _cpuTimeMovingAverageBuffer.Peek().Timestamp;
                if (datapointAge.TotalSeconds > 60)
                {
                    _cpuTimeMovingAverageBuffer.Dequeue();
                }
                else
                {
                    break;
                }
            }

            // Get the current total CPU time of the process
            bool success = NativeMethods.GetProcessTimes(QueryLimitedInfoHandle, out FILETIME _, out FILETIME _, out FILETIME kernelTime, out FILETIME userTime);
            if (!success)
            {
                return;
            }
            TimeSpan totalCpuTime = TimeSpan.FromTicks((long)(kernelTime.ULong + userTime.ULong));
            _cpuTimeMovingAverageBuffer.Enqueue(new() { Timestamp = now, TotalCpuTime = totalCpuTime });

            // Take the CPU time from now and (up to) a minute ago, and get the average usage %
            CpuTimeTimestamp startDatapoint = _cpuTimeMovingAverageBuffer.Peek();
            TimeSpan deltaTime = now - startDatapoint.Timestamp;
            TimeSpan deltaCpuTime = totalCpuTime - startDatapoint.TotalCpuTime;

            if (deltaCpuTime.Ticks == 0)
                AverageCpuUsage = 0;
            else
                AverageCpuUsage = (double)deltaCpuTime.Ticks / deltaTime.Ticks / Environment.ProcessorCount;
        }
    }

    internal class CpuTimeTimestamp
    {
        public DateTime Timestamp { get; init; }
        public TimeSpan TotalCpuTime { get; init; }
    }
}
