﻿using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Management;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using Application = System.Windows.Application;


namespace CPUSetSetter
{
    public partial class Config : ObservableObject
    {
        private static JsonSerializerOptions JsonOptions { get; } = CreateJsonOptions();
        private static JsonSerializerOptions CreateJsonOptions()
        {
            JsonSerializerOptions options = new() { WriteIndented = true };
            options.Converters.Add(new JsonStringEnumConverter());
            options.Converters.Add(new ThemeModeJsonConverter());
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

        [ObservableProperty]
        private ThemeMode _theme = ThemeMode.System;

        [ObservableProperty]
        private bool _runInBackground = true;

        public ObservableCollection<ThemeMode> AvailableThemes { get; } =
        [
            ThemeMode.Light,
            ThemeMode.Dark,
            ThemeMode.System
        ];

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

        partial void OnThemeChanged(ThemeMode value)
        {
            if(Application.Current is not null)
                Application.Current.ThemeMode = value;
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
            config.OnThemeChanged(config.Theme);
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
            foreach (ManagementBaseObject cpu in searcher.Get())
            {
                cpuName = (string)cpu["Name"] ?? "";
                resultCount++;
            }

            string[] cpuNameParts = cpuName.Split(' ');

            if (resultCount == 1)
            {
                string[] knownDuoCcdCpus = [
                    "3950X", "3900XT", "3900X", "3900",
                    "5950X", "5900XT", "5900X", "5900",
                    "7950X3D", "7950X", "7900X3D", "7900X", "7900",
                    "9950X3D", "9950X", "9900X3D", "9900X"
                ];

                // If this CPU is known to have 2 CCDs, add a default CCD0/Cache and CCD1/Freq Cpu Set
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
                            WindowLogger.Default.Write("Detected a hybrid cache CPU, added a default Cache and Freq CPU Set");
                        }
                        else
                        {
                            CpuSets.Add(new("CCD0", ccd0Mask));
                            CpuSets.Add(new("CCD1", ccd1Mask));
                            WindowLogger.Default.Write("Detected a dual CCD CPU, added a default CCD0 and CCD1 CPU Set");
                        }
                        break;
                    }
                }
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
