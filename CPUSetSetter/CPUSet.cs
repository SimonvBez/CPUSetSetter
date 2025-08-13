using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;


namespace CPUSetSetter
{
    public partial class CPUSet : ObservableObject, IJsonOnDeserialized
    {
        public static string UnsetName { get; } = "";
        public static string UnsetSettingsName { get; } = "<unset>";
        public static CPUSet Unset => Config.Default.GetCpuSetByName(UnsetName)!;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SettingsName))]
        private string _name = "";

        public List<CPUSetCore> Mask { get; init; } = [];

        public ObservableCollection<VKey> Hotkey { get; init; } = [];
        public bool IsUnset { get; init; } = false;

        [JsonIgnore]
        public string SettingsName => IsUnset ? UnsetSettingsName : Name;

        [JsonIgnore]
        public IEnumerable<IEnumerable<CPUSetCore>> SettingsTabMask
        {
            get
            {
                int div = 2;
                while (Mask.Count / div > 16)
                {
                    div += 2;
                }
                return Mask.Chunk(Mask.Count / div);
            }
        }

        [JsonIgnore]
        public string SettingsHotkeyString => string.Join("+", Hotkey);

        private readonly List<ProcessListEntry> _processesUsingSet = [];
        private HotkeyCallback? _hotkeyCallback;


        // Private constructor for Json loading
        [JsonConstructor]
        private CPUSet() { }

        public void OnDeserialized()
        {
            foreach (CPUSetCore core in Mask)
            {
                core.Parent = this;
            }
            SetupHotkeyListener();
        }

        public CPUSet(string name)
        {
            _name = name;
            Mask = new(Enumerable.Range(0, Environment.ProcessorCount).Select(i => new CPUSetCore { Name = $"Core {i}", IsEnabled = true, Parent = this }));
            SetupHotkeyListener();
        }

        public CPUSet(string name, IEnumerable<bool> mask)
        {
            _name = name;
            Mask = new(mask.Select((bool coreEnabled, int i) => new CPUSetCore { Name = $"Core {i}", IsEnabled = coreEnabled, Parent = this }));
            SetupHotkeyListener();
        }

        public static CPUSet CreateUnset()
        {
            return new CPUSet(UnsetName, []) { IsUnset = true };
        }

        private void SetupHotkeyListener()
        {
            _hotkeyCallback = new(Hotkey, (_, _) => OnHotkeyPressed());

            Hotkey.CollectionChanged += (_, e) =>
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                    case NotifyCollectionChangedAction.Remove:
                        _hotkeyCallback.VKeys = [.. Hotkey]; // Apply the new hotkey to the callback
                        break;
                    case NotifyCollectionChangedAction.Reset:
                        _hotkeyCallback.VKeys = [];
                        break;
                    case NotifyCollectionChangedAction.Replace:
                    case NotifyCollectionChangedAction.Move:
                    default:
                        throw new NotImplementedException();
                }
                OnPropertyChanged(nameof(SettingsHotkeyString));
                Config.Default?.Save();
            };

            HotkeyListener.Instance.AddCallback(_hotkeyCallback);
        }

        private void OnHotkeyPressed()
        {
            MainWindowViewModel.Instance?.OnCpuSetHotkeyPressed(this);
        }

        public void Remove()
        {
            // First unset every process using this CPU Set
            foreach (ProcessListEntry pEntry in new List<ProcessListEntry>(_processesUsingSet))
            {
                pEntry.CpuSet = Unset;
            }
            // Then remove this Set from the config
            HotkeyListener.Instance.RemoveCallback(_hotkeyCallback!);
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

        public void ApplyToAllBoundProcesses()
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
            Parent?.ApplyToAllBoundProcesses();
        }
    }
}
