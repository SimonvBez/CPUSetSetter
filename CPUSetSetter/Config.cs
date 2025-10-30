using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Management;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace CPUSetSetter
{
    public partial class Config : ObservableObject
    {
        private static JsonSerializerOptions JsonOptions { get; } = CreateJsonOptions();
        private static JsonSerializerOptions CreateJsonOptions()
        {
            JsonSerializerOptions options = new() { WriteIndented = true };
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }

        // Config variables
        public ObservableCollection<CPUSet> CpuSets { get; init; } = [];

        public ObservableCollection<ProcessCPUSet> ProcessCPUSets { get; init; } = [];

        [ObservableProperty]
        private bool _matchWholePath = true;

        [ObservableProperty]
        private bool _muteHotkeySound = false;

        [ObservableProperty]
        private bool _startMinimized = false;

        [ObservableProperty]
        private bool _disableWelcomeMessage = false;

        // Static getting for singleton instance
        public static Config Default { get; } = Load();

        private bool _isLoading = true;

        [JsonConstructor]
        private Config() { }

        private void SetupListener()
        {
            // Save changes to CpuSets
            CpuSets.CollectionChanged += (_, e) =>
            {
                Save();
            };

            // When there is a change to a saved process CPU Set, apply it immediately to the running processes
            ProcessCPUSets.CollectionChanged += (_, e) =>
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        foreach (ProcessCPUSet processCPUSet in e.NewItems!)
                        {
                            processCPUSet.ApplyCpuSet();
                        }
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        foreach (ProcessCPUSet processCPUSet in e.OldItems!)
                        {
                            // Update every ProcessListEntry now that this process CPU Set definition has been removed
                            foreach (ProcessListEntry pEntry in MainWindowViewModel.RunningProcesses)
                            {
                                pEntry.CpuSet = pEntry.GetConfiguredCpuSet();
                            }
                        }
                        break;
                    case NotifyCollectionChangedAction.Reset:
                    case NotifyCollectionChangedAction.Replace:
                    case NotifyCollectionChangedAction.Move:
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
                FileStream fileStream = File.Create("CPUSetSetter_config_new.json");
                JsonSerializer.Serialize(fileStream, this, options: JsonOptions);
                fileStream.Dispose();
                File.Move("CPUSetSetter_config_new.json", "CPUSetSetter_config.json", true);
            }
            catch (Exception ex)
            {
                WindowLogger.Default.Write($"Failed to write config: {ex}");
            }
        }

        private static Config Load()
        {
            Config config;
            bool isExisting;
            try
            {
                using FileStream fileStream = File.OpenRead("CPUSetSetter_config.json");
                config = JsonSerializer.Deserialize<Config>(fileStream, options: JsonOptions) ?? throw new NullReferenceException();
                isExisting = true;
            }
            catch (Exception)
            {
                // The config file does not exist yet or was corrupt
                config = new();
                isExisting = false;
            }
            config._isLoading = false;
            config.SetupListener();

            if (!config.DisableWelcomeMessage)
            {
                WindowLogger.Default.Write(
                    "Welcome! To start, head to the Settings tab to define a CPU Set. This Set can then be applied to processes.\n" +
                    "To apply a CPU Set, choose it in the Processes list, or configure a Hotkey to apply it to the current foreground process.");
            }

            if (isExisting)
            {
                config.ValidateCPUSets();
            }
            else
            {
                config.PopulateDefaultConfig();
            }
            return config;
        }

        public CPUSet? GetCpuSetByName(string name)
        {
            return CpuSets.FirstOrDefault(x => x!.Name == name, null);
        }

        public ProcessCPUSet? GetProcessCpuSetByName(string processName, string executablePath)
        {
            return ProcessCPUSets.FirstOrDefault(x =>
            {
                return x!.Name.Equals(processName, StringComparison.OrdinalIgnoreCase) &&
                    (!MatchWholePath || x.Path.Equals(executablePath, StringComparison.OrdinalIgnoreCase));
            },
            null);
        }

        public void SetProcessCpuSet(string processName, string executablePath, string cpuSetName)
        {
            ProcessCPUSet? processCPUSet = GetProcessCpuSetByName(processName, executablePath);
            if (processCPUSet is not null)
            {
                processCPUSet.CpuSetName = cpuSetName;
            }
            else
            {
                // There is no existing process CPU Set rule, create a new one
                ProcessCPUSets.Add(new ProcessCPUSet(processName, executablePath, cpuSetName));
            }
        }

        public void RemoveProcessCpuSet(string processName, string executablePath)
        {
            for (int i = ProcessCPUSets.Count - 1; i >= 0; --i)
            {
                if (ProcessCPUSets[i].Name.Equals(processName, StringComparison.OrdinalIgnoreCase) &&
                    !MatchWholePath || ProcessCPUSets[i].Path.Equals(executablePath, StringComparison.OrdinalIgnoreCase))
                {
                    ProcessCPUSets.RemoveAt(i);
                }
            }
        }

        private void PopulateDefaultConfig()
        {
            // Add the special <unset> Set
            CpuSets.Add(CPUSet.CreateUnset());

            ManagementObjectSearcher searcher = new("root\\CIMV2", "SELECT * FROM Win32_Processor");
            int resultCount = 0;
            string cpuName = "";
            string manufacturer = "";

            foreach (ManagementBaseObject cpu in searcher.Get())
            {
                cpuName = (string)cpu["Name"] ?? "";
                manufacturer = (string)cpu["Manufacturer"] ?? "";
                resultCount++;
            }

            string[] cpuNameParts = cpuName.Split(' ');
            bool isIntel = cpuName.Contains("Intel", StringComparison.OrdinalIgnoreCase) ||
                           manufacturer.Contains("Intel", StringComparison.OrdinalIgnoreCase);
            bool isAMD = cpuName.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                         manufacturer.Contains("AMD", StringComparison.OrdinalIgnoreCase);

            if (resultCount == 1)
            {
                // Intel hybrid P/E core CPUs with thread counts (P-cores with HT, E-cores without)
                if (isIntel)
                {
                    // Dictionary: Model -> (P-core threads, E-core threads)
                    Dictionary<string, (int pThreads, int eThreads)> intelPECoreLayouts = new()
                    {
                        // 12th Gen Alder Lake Desktop
                        ["12900KS"] = (16, 8),  // 8P+8E
                        ["12900K"] = (16, 8),   // 8P+8E
                        ["12900KF"] = (16, 8),  // 8P+8E
                        ["12900"] = (16, 8),    // 8P+8E
                        ["12900F"] = (16, 8),   // 8P+8E
                        ["12900T"] = (16, 8),   // 8P+8E
                        ["12700K"] = (16, 4),   // 8P+4E
                        ["12700KF"] = (16, 4),  // 8P+4E
                        ["12700"] = (16, 4),    // 8P+4E
                        ["12700F"] = (16, 4),   // 8P+4E
                        ["12700T"] = (16, 4),   // 8P+4E
                        ["12600K"] = (12, 4),   // 6P+4E
                        ["12600KF"] = (12, 4),  // 6P+4E
                        ["12600"] = (12, 0),    // 6P+0E
                        ["12600T"] = (12, 0),   // 6P+0E
                        ["12490F"] = (12, 0),   // 6P+0E
                        ["12400"] = (12, 0),    // 6P+0E
                        ["12400F"] = (12, 0),   // 6P+0E
                        ["12400T"] = (12, 0),   // 6P+0E
                        ["12100"] = (8, 0),     // 4P+0E
                        ["12100F"] = (8, 0),    // 4P+0E
                        ["12100T"] = (8, 0),    // 4P+0E

                        // 12th Gen Mobile
                        ["12900HK"] = (14, 6),  // 7P+6E (some 6P+8E)
                        ["12900H"] = (14, 6),   // 7P+6E
                        ["12800H"] = (14, 6),   // 7P+6E (some 6P+8E)
                        ["12700H"] = (14, 6),   // 7P+6E
                        ["12650H"] = (10, 4),   // 5P+4E
                        ["12600H"] = (12, 4),   // 6P+4E
                        ["12950HX"] = (16, 8),  // 8P+8E
                        ["12900HX"] = (16, 8),  // 8P+8E
                        ["12850HX"] = (16, 8),  // 8P+8E
                        ["12800HX"] = (16, 8),  // 8P+8E
                        ["12650HX"] = (14, 6),  // 7P+6E
                        ["1280P"] = (12, 8),    // 6P+8E
                        ["1270P"] = (12, 8),    // 6P+8E
                        ["1260P"] = (12, 8),    // 6P+8E
                        ["1250P"] = (12, 8),    // 6P+8E
                        ["1240P"] = (12, 8),    // 6P+8E
                        ["1265U"] = (10, 8),    // 5P+8E (some 2P+8E)
                        ["1255U"] = (10, 8),    // 5P+8E (some 2P+8E)
                        ["1245U"] = (10, 8),    // 5P+8E (some 2P+8E)
                        ["1240U"] = (10, 8),    // 5P+8E (some 2P+8E)
                        ["1235U"] = (10, 8),    // 5P+8E
                        ["1230U"] = (10, 8),    // 5P+8E

                        // 13th Gen Raptor Lake Desktop
                        ["13900KS"] = (16, 16), // 8P+16E
                        ["13900K"] = (16, 16),  // 8P+16E
                        ["13900KF"] = (16, 16), // 8P+16E
                        ["13900"] = (16, 16),   // 8P+16E
                        ["13900F"] = (16, 16),  // 8P+16E
                        ["13900T"] = (16, 16),  // 8P+16E
                        ["13790F"] = (16, 16),  // 8P+16E
                        ["13700K"] = (16, 8),   // 8P+8E
                        ["13700KF"] = (16, 8),  // 8P+8E
                        ["13700"] = (16, 8),    // 8P+8E
                        ["13700F"] = (16, 8),   // 8P+8E
                        ["13700T"] = (16, 8),   // 8P+8E
                        ["13600K"] = (12, 8),   // 6P+8E
                        ["13600KF"] = (12, 8),  // 6P+8E
                        ["13600"] = (12, 8),    // 6P+8E
                        ["13600T"] = (12, 8),   // 6P+8E
                        ["13500"] = (12, 8),    // 6P+8E
                        ["13500T"] = (12, 8),   // 6P+8E
                        ["13490F"] = (10, 6),   // 5P+6E
                        ["13400"] = (10, 6),    // 5P+6E
                        ["13400F"] = (10, 6),   // 5P+6E
                        ["13400T"] = (10, 6),   // 5P+6E
                        ["13100"] = (8, 0),     // 4P+0E
                        ["13100F"] = (8, 0),    // 4P+0E
                        ["13100T"] = (8, 0),    // 4P+0E

                        // 13th Gen Mobile
                        ["13980HX"] = (16, 16), // 8P+16E
                        ["13950HX"] = (16, 16), // 8P+16E
                        ["13900HX"] = (16, 16), // 8P+16E
                        ["13850HX"] = (12, 8),  // 6P+8E
                        ["13700HX"] = (16, 8),  // 8P+8E
                        ["13650HX"] = (14, 6),  // 7P+6E
                        ["13600HX"] = (12, 8),  // 6P+8E
                        ["13500HX"] = (12, 8),  // 6P+8E
                        ["13450HX"] = (10, 6),  // 5P+6E
                        ["13900HK"] = (14, 6),  // 7P+6E
                        ["13900H"] = (14, 6),   // 7P+6E
                        ["13800H"] = (14, 6),   // 7P+6E
                        ["13700H"] = (14, 6),   // 7P+6E
                        ["13620H"] = (10, 6),   // 5P+6E
                        ["13600H"] = (12, 8),   // 6P+8E
                        ["13500H"] = (8, 8),    // 4P+8E
                        ["13420H"] = (8, 4),    // 4P+4E
                        ["1370P"] = (12, 8),    // 6P+8E
                        ["1360P"] = (8, 8),     // 4P+8E
                        ["1350P"] = (12, 8),    // 6P+8E
                        ["1340P"] = (12, 8),    // 6P+8E

                        // 14th Gen Raptor Lake Refresh Desktop
                        ["14900KS"] = (16, 16), // 8P+16E
                        ["14900K"] = (16, 16),  // 8P+16E
                        ["14900KF"] = (16, 16), // 8P+16E
                        ["14900"] = (16, 16),   // 8P+16E
                        ["14900F"] = (16, 16),  // 8P+16E
                        ["14900T"] = (16, 16),  // 8P+16E
                        ["14790F"] = (16, 16),  // 8P+16E
                        ["14700K"] = (16, 12),  // 8P+12E
                        ["14700KF"] = (16, 12), // 8P+12E
                        ["14700"] = (16, 12),   // 8P+12E
                        ["14700F"] = (16, 12),  // 8P+12E
                        ["14700T"] = (16, 12),  // 8P+12E
                        ["14600K"] = (12, 8),   // 6P+8E
                        ["14600KF"] = (12, 8),  // 6P+8E
                        ["14600"] = (12, 8),    // 6P+8E
                        ["14600T"] = (12, 8),   // 6P+8E
                        ["14500"] = (14, 6),    // 7P+6E
                        ["14500T"] = (14, 6),   // 7P+6E
                        ["14490F"] = (10, 6),   // 5P+6E
                        ["14400"] = (10, 6),    // 5P+6E
                        ["14400F"] = (10, 6),   // 5P+6E
                        ["14400T"] = (10, 6),   // 5P+6E

                        // 14th Gen Mobile
                        ["14900HX"] = (16, 16), // 8P+16E
                        ["14700HX"] = (16, 12), // 8P+12E
                        ["14650HX"] = (16, 8),  // 8P+8E
                        ["14500HX"] = (14, 8),  // 7P+8E
                        ["14450HX"] = (10, 6),  // 5P+6E

                        // 15th Gen Arrow Lake (no hyperthreading on P-cores)
                        ["285K"] = (8, 16),     // 8P+16E (Lion Cove + Skymont)
                        ["285KF"] = (8, 16),    // 8P+16E
                        ["285"] = (8, 16),      // 8P+16E
                        ["265K"] = (8, 12),     // 8P+12E
                        ["265KF"] = (8, 12),    // 8P+12E
                        ["265"] = (8, 12),      // 8P+12E
                        ["245K"] = (6, 8),      // 6P+8E
                        ["245KF"] = (6, 8),     // 6P+8E
                        ["245"] = (6, 8)        // 6P+8E
                    };

                    foreach (var kvp in intelPECoreLayouts)
                    {
                        if (cpuNameParts.Contains(kvp.Key))
                        {
                            int logicalProcessorCount = Environment.ProcessorCount;
                            int pThreads = kvp.Value.pThreads;
                            int eThreads = kvp.Value.eThreads;

                            // Verify the total matches
                            if (pThreads + eThreads != logicalProcessorCount)
                            {
                                WindowLogger.Default.Write($"Warning: Expected {pThreads + eThreads} threads but detected {logicalProcessorCount}");
                                //return;
                            }

                            // TODO: Also apply the P and E suffixes when creating a new CPUSet through the UI
                            bool[] pOnlyMask = new bool[logicalProcessorCount];
                            bool[] eOnlyMask = new bool[logicalProcessorCount];
                            string[] cpuNames = new string[logicalProcessorCount];

                            // P-cores are typically indexed first
                            for (int i = 0; i < logicalProcessorCount; ++i)
                            {
                                string suffix = i < pThreads ? "P" : "E";
                                pOnlyMask[i] = i < pThreads;
                                eOnlyMask[i] = i >= pThreads;
                                cpuNames[i] = $"CPU {i}{suffix}";
                            }

                            CpuSets.Add(new("P-Cores", pOnlyMask, cpuNames));
                            CpuSets.Add(new("E-Cores", eOnlyMask, cpuNames));

                            WindowLogger.Default.Write($"Detected Intel hybrid CPU ({kvp.Key}), added P-Cores and E-Cores CPU Sets");
                            return;
                        }
                    }
                }

                else if (isAMD)
                {
                    // AMD dual CCD detection
                    string[] knownDuoCcdCpus = [
                        "3950X", "3900XT", "3900X", "3900",
                        "5950X", "5900XT", "5900X", "5900",
                        "7950X3D", "7950X", "7900X3D", "7900X", "7900",
                        "9950X3D", "9950X", "9900X3D", "9900X"
                    ];

                    foreach (string knownCpu in knownDuoCcdCpus)
                    {
                        if (cpuNameParts.Contains(knownCpu))
                        {
                            int logicalProcessorCount = Environment.ProcessorCount;
                            IEnumerable<bool> ccd0Mask = Enumerable.Repeat(true, logicalProcessorCount / 2)
                                                                   .Concat(Enumerable.Repeat(false, logicalProcessorCount / 2));
                            IEnumerable<bool> ccd1Mask = Enumerable.Repeat(false, logicalProcessorCount / 2)
                                                                   .Concat(Enumerable.Repeat(true, logicalProcessorCount / 2));
                            if (knownCpu.EndsWith("X3D", StringComparison.Ordinal))
                            {
                                CpuSets.Add(new("Cache", ccd0Mask));
                                CpuSets.Add(new("Freq", ccd1Mask));
                                WindowLogger.Default.Write($"Detected a hybrid cache CPU ({knownCpu}), added a default Cache and Freq CPU Set");
                            }
                            else
                            {
                                CpuSets.Add(new("CCD0", ccd0Mask));
                                CpuSets.Add(new("CCD1", ccd1Mask));
                                WindowLogger.Default.Write($"Detected a dual CCD CPU ({knownCpu}), added a default CCD0 and CCD1 CPU Set");
                            }
                            break;
                        }
                    }
                }
                // Not Supported or no special sets detected, just add the <all> Set
                WindowLogger.Default.Write("No special CPU Sets detected for this CPU");
            }
        }

        private void ValidateCPUSets()
        {
            for (int i = CpuSets.Count - 1; i >= 0; --i)
            {
                if (!CpuSets[i].IsUnset && CpuSets[i].Mask.Count != Environment.ProcessorCount)
                {
                    WindowLogger.Default.Write($"Set '{CpuSets[i].Name}' had an invalid number of processors and has been removed");
                    CpuSets.RemoveAt(i);
                }
            }
        }
    }
}
