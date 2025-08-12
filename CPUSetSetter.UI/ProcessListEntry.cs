using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Win32.SafeHandles;


namespace CPUSetSetter.UI
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
        private DateTime _creationTime;

        [ObservableProperty]
        private CPUSet _cpuSet;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FailedToOpen))]
        public SafeProcessHandle? _handle;

        public bool FailedToOpen => Handle?.IsInvalid == true;

        public ProcessListEntry(ProcessInfo pInfo)
        {
            Pid = pInfo.PID;
            Name = pInfo.Name;
            Path = pInfo.ImagePath;
            CreationTime = pInfo.CreationTime;
            CpuSet = GetConfiguredCpuSet();
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
    }
}
