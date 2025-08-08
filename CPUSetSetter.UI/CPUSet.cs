using CommunityToolkit.Mvvm.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;


namespace CPUSetSetter.UI
{
    public partial class CPUSet : ObservableObject, IJsonOnDeserialized
    {
        public static string UnsetName { get; } = "";
        public static CPUSet Unset { get; } = new CPUSet(UnsetName, []) { IsUnset = true };

        [ObservableProperty]
        private string _name = "";

        public List<CPUSetCore> Mask { get; init; } = [];

        [JsonIgnore]
        public IEnumerable<IEnumerable<CPUSetCore>> SettingsTabMask => Mask.Chunk(8);

        [JsonIgnore]
        public bool IsUnset { get; private set; } = false;

        private List<ProcessListEntry> _processesUsingSet = [];


        // Private constructor for Json loading
        [JsonConstructor]
        private CPUSet() { }

        public void OnDeserialized()
        {
            foreach (CPUSetCore core in Mask)
            {
                core.Parent = this;
            }
        }

        public CPUSet(string name)
        {
            _name = name;
            Mask = new(Enumerable.Range(0, Environment.ProcessorCount).Select(i => new CPUSetCore { Name = $"Core {i}", IsEnabled = true, Parent = this }));
        }

        public CPUSet(string name, IEnumerable<bool> mask)
        {
            _name = name;
            Mask = new(mask.Select((bool coreEnabled, int i) => new CPUSetCore { Name = $"Core {i}", IsEnabled = coreEnabled, Parent = this }));
        }

        public void Remove()
        {
            // First unset every process using this CPU Set
            foreach (ProcessListEntry pEntry in new List<ProcessListEntry>(_processesUsingSet))
            {
                pEntry.CpuSet = Unset;
            }
            // Then remove this Set from the config
            Config.Default.CpuSets.Remove(this);
        }

        public void AddProcess(ProcessListEntry pEntry, bool applyNow)
        {
            _processesUsingSet.Add(pEntry);
            if (applyNow)
            {
                ApplyToProcess(pEntry);
            }
        }

        public void RemoveProcess(ProcessListEntry pEntry)
        {
            _processesUsingSet.Remove(pEntry);
        }

        public void ApplyToAllProcesses()
        {
            foreach (ProcessListEntry pEntry in _processesUsingSet)
            {
                ApplyToProcess(pEntry);
            }
        }

        private void ApplyToProcess(ProcessListEntry pEntry)
        {
            // Open the process if it does not have a handle yet
            if (pEntry.Handle is null)
            {
                pEntry.Handle = NativeMethods.OpenProcess(ProcessAccessFlags.PROCESS_SET_LIMITED_INFORMATION, false, pEntry.Pid);
                if (pEntry.Handle.IsInvalid)
                {
                    int error = Marshal.GetLastWin32Error();
                    WindowLogger.Default.Write($"ERROR: Could not open process '{pEntry.Name}': {new System.ComponentModel.Win32Exception(error).Message}");
                    return;
                }
            }
            else if (pEntry.Handle.IsInvalid)
            {
                // The handle was already made previously, don't bother trying again
                return;
            }

            bool success;
            if (IsUnset)
            {
                success = NativeMethods.SetProcessDefaultCpuSetMasks(pEntry.Handle, null, 0);
                if (success)
                {
                    WindowLogger.Default.Write($"Cleared CPU Set of '{pEntry.Name}'");
                }
                else
                {
                    int error = Marshal.GetLastWin32Error();
                    WindowLogger.Default.Write($"ERROR: Could not clear CPU Set of '{pEntry.Name}': {new System.ComponentModel.Win32Exception(error).Message}");
                }
            }
            else
            {
                ulong bitMask = 0;
                for (int i = 0; i < Mask.Count; ++i)
                {
                    if (Mask[i].IsEnabled)
                        bitMask |= 1UL << i;
                }

                GROUP_AFFINITY[] affinity =
                [
                    new GROUP_AFFINITY
                    {
                        Group = 0,
                        Mask = bitMask
                    }
                ];

                success = NativeMethods.SetProcessDefaultCpuSetMasks(pEntry.Handle, affinity, 1);
                if (success)
                {
                    WindowLogger.Default.Write($"Applied CPU Set of '{pEntry.Name}' to {Name}");
                }
                else
                {
                    int error = Marshal.GetLastWin32Error();
                    WindowLogger.Default.Write($"ERROR: Could not apply CPU Set to '{pEntry.Name}': {new System.ComponentModel.Win32Exception(error).Message}");
                }
            }
        }
    }

    public partial class CPUSetCore : ObservableObject
    {
        [ObservableProperty]
        private string _name = "";

        [ObservableProperty]
        private bool _isEnabled;

        [JsonIgnore]
        public CPUSet? Parent { get; set; }

        partial void OnIsEnabledChanged(bool value)
        {
            Config.Default?.Save(); // Config.Default has not been set yet while the Config is still loading
            Parent?.ApplyToAllProcesses();
        }
    }
}
