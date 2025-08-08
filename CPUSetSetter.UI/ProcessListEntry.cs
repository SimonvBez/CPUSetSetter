using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Win32.SafeHandles;


namespace CPUSetSetter.UI
{
    public partial class ProcessListEntry : ObservableObject
    {
        private bool _isUpdatingOtherProcesses = false;

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
            CpuSet = CPUSet.Unset;
        }

        partial void OnCpuSetChanged(CPUSet? oldValue, CPUSet newValue)
        {
            oldValue?.RemoveProcess(this);
            newValue.AddProcess(this, oldValue is not null || !newValue.IsUnset); // Don't apply the Unset CPUSet when the program is first started

            // Prevent deep recursion when applying this set to other processes
            if (_isUpdatingOtherProcesses)
                return;

            // Apply this set to any process with the same name and path
            _isUpdatingOtherProcesses = true;
            foreach (ProcessListEntry pEntry in MainWindowViewModel.RunningProcesses)
            {
                if (Name.Equals(pEntry.Name, StringComparison.OrdinalIgnoreCase) &&
                    Path.Equals(pEntry.Path, StringComparison.OrdinalIgnoreCase))
                {
                    pEntry.CpuSet = newValue;
                }
            }
            _isUpdatingOtherProcesses = false;
        }
    }
}
