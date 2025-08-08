using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Management;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace CPUSetSetter.UI
{
    public partial class Config : ObservableObject, IJsonOnDeserialized
    {
        // Config variables
        [JsonIgnore]
        public ObservableCollection<CPUSet> CpuSetPickable { get; } = [CPUSet.Unset];

        public ObservableCollection<CPUSet> CpuSets { get; init; } = [];

        [ObservableProperty]
        private bool _matchWholePath = true;

        // Static getting for singleton instance
        public static Config Default { get; } = Load();

        private bool _isLoading = true;

        // Private constructor to force usage of Load()
        [JsonConstructor]
        private Config() { }

        public void OnDeserialized()
        {
            // Keep CpuSetPickableNames up-to-date when CpuSets changes
            CpuSets.CollectionChanged += (_, e) =>
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        foreach (CPUSet newSet in e.NewItems!)
                        {
                            CpuSetPickable.Add(newSet);
                        }
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        foreach (CPUSet newSet in e.OldItems!)
                        {
                            CpuSetPickable.Remove(newSet);
                        }
                        break;
                    case NotifyCollectionChangedAction.Reset:
                        throw new NotImplementedException();
                    case NotifyCollectionChangedAction.Replace:
                        throw new NotImplementedException();
                    case NotifyCollectionChangedAction.Move:
                        throw new NotImplementedException();
                    default:
                        throw new NotImplementedException();
                }
                Save();
            };
        }


        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            Save();
        }

        public void Save()
        {
            // Don't save the Config when properties are changing while it is still being loaded by Load()
            if (_isLoading)
                return;

            try
            {
                using FileStream fileStream = File.Create("CPUSetSetter_config.json");
                JsonSerializer.Serialize(fileStream, this);
            }
            catch (Exception ex)
            {
                WindowLogger.Default.Write($"Failed to write config: {ex}");
            }
        }

        public static Config Load()
        {
            try
            {
                Config loadedConfig;
                {
                    using FileStream fileStream = File.OpenRead("CPUSetSetter_config.json");
                    loadedConfig = JsonSerializer.Deserialize<Config>(fileStream) ?? throw new NullReferenceException();
                    loadedConfig._isLoading = false;
                }
                loadedConfig.InitCpuSetNames();
                loadedConfig.ValidateCPUSets();
                return loadedConfig;
            }
            catch (Exception ex)
            {
                Config newConfig = new() { _isLoading = false };
                newConfig.OnDeserialized();
                newConfig.PopulateDefaultConfig();
                return newConfig;
            }
        }

        public CPUSet GetCpuSetByName(string name)
        {
            return CpuSetPickable.First(x => x.Name == name);
        }

        private void PopulateDefaultConfig()
        {
            string[] knownDuoHybridCpus = ["7950X3D", "7900X3D", "9950X3D", "9900X3D"];

            ManagementObjectSearcher searcher = new("root\\CIMV2", "SELECT * FROM Win32_Processor");
            int resultCount = 0;
            string cpuName = "";
            foreach (ManagementBaseObject cpu in searcher.Get())
            {
                cpuName = (string)cpu["Name"] ?? "";
                resultCount++;
            }

            if (resultCount == 1)
            {
                // If this CPU is known to have 2 CCDs, and CCD0 has extra cache, add a default Cache and Freq Cpu Set
                if (knownDuoHybridCpus.Any(knownCpu => cpuName.Contains(knownCpu)))
                {
                    int logicalCoreCount = Environment.ProcessorCount;
                    IEnumerable<bool> cacheMask = Enumerable.Repeat(true, logicalCoreCount / 2)
                                                            .Concat(Enumerable.Repeat(false, logicalCoreCount / 2));
                    IEnumerable<bool> freqMask = Enumerable.Repeat(false, logicalCoreCount / 2)
                                                           .Concat(Enumerable.Repeat(true, logicalCoreCount / 2));
                    CpuSets.Add(new("Cache", cacheMask));
                    CpuSets.Add(new("Freq", freqMask));
                    WindowLogger.Default.Write("Detected a hybrid cache CPU, added a default Cache and Freq Set");
                }
            }
        }

        private void InitCpuSetNames()
        {
            foreach (CPUSet cpuSet in CpuSets)
            {
                CpuSetPickable.Add(cpuSet);
            }
        }

        private void ValidateCPUSets()
        {
            for (int i = CpuSets.Count - 1; i >= 0; --i)
            {
                if (CpuSets[i].Mask.Count != Environment.ProcessorCount)
                {
                    WindowLogger.Default.Write($"Set '{CpuSets[i].Name}' had an invalid number of cores and has been removed");
                    CpuSets.RemoveAt(i);
                }
            }
        }
    }
}
