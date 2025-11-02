using CPUSetSetter.Config.Models;
using CPUSetSetter.Platforms;
using CPUSetSetter.Themes;
using CPUSetSetter.UI.Tabs.Processes;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace CPUSetSetter.Config
{
    public static class AppConfigFile
    {
        private static readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };

        private const string fileName = "CPUSetSetter_config.json";
        private const string saveTempName = "CPUSetSetter_config_new.json";
        private const string backupNameTemplate = "CPUSetSetter_config_backup{0}.json";
        private const int configVersion = 1;

        public static void Save(AppConfig config)
        {
            ConfigJson configJson = new(config);
            try
            {
                // Save the new config to a temp file before overwriting the config, in case the serialization fails and clears the config
                FileStream fileStream = File.Create(saveTempName);
                JsonSerializer.Serialize(fileStream, configJson, options: jsonOptions);
                fileStream.Dispose();
                File.Move(saveTempName, fileName, true);
            }
            catch (Exception ex)
            {
                WindowLogger.Write($"Failed to write config: {ex}");
            }
        }

        public static AppConfig Load()
        {
            if (!File.Exists(fileName))
            {
                // The config file does not exist yet, use the defaults
                return JsonToConfig(ConfigJson.Default, true, out bool _);
            }

            try
            {
                using FileStream fileStream = File.OpenRead(fileName);
                ConfigJson configJson = JsonSerializer.Deserialize<ConfigJson>(fileStream, options: jsonOptions) ?? throw new NullReferenceException();
                AppConfig config = JsonToConfig(configJson, false, out bool hadSoftError);

                if (hadSoftError)
                {
                    try
                    {
                        string backupName = BackupConfig();
                        WindowLogger.Write($"Your config contained an error. The old config was backed up to '{backupName}'");
                    }
                    catch (Exception backupEx)
                    {
                        WindowLogger.Write($"Unable to make a backup of your old config: {backupEx}\n");
                        WindowLogger.Write("Your config contained an error. What did you do to make even the backup fail??");
                    }
                }

                return config;
            }
            catch (Exception readEx)
            {
                // The config file was likely corrupt
                WindowLogger.Write($"Failed to read config: {readEx}\n");
                try
                {
                    string backupName = BackupConfig();
                    WindowLogger.Write($"Your config has been reset. The old config was backed up to '{backupName}'");
                }
                catch (Exception backupEx)
                {
                    WindowLogger.Write($"Unable to make a backup of your old config: {backupEx}\n");
                    WindowLogger.Write("Your config has been reset. What did you do to make even the backup fail??");
                }
                // Use the defaults
                return JsonToConfig(ConfigJson.Default, true, out bool _);
            }
        }

        private static string BackupConfig()
        {
            int i = 0;
            while (true)
            {
                string backupName = string.Format(backupNameTemplate, i++);
                try
                {
                    File.Copy(fileName, backupName, false);
                    return backupName;
                }
                catch (IOException)
                {
                    // Continue when the backup name already exists
                    continue;
                }
            }
        }

        private static AppConfig JsonToConfig(ConfigJson configJson, bool generateDefaultMasks, out bool hadSoftError)
        {
            hadSoftError = false;

            List<VKey> noMaskHotkeys = configJson.NoMaskHotkeys.Select(hotkey => Enum.Parse<VKey>(hotkey)).ToList();
            // Put the NoMask Mask at the front of the logicalProcessorMasks
            List<LogicalProcessorMask> logicalProcessorMasks = [LogicalProcessorMask.InitNoMask(noMaskHotkeys)];

            // Construct the LogicalProcessorMask models from the config
            foreach (LogicalProcessorMaskJson jsonMask in configJson.LogicalProcessorMasks)
            {
                if (logicalProcessorMasks.Any(existingMask => existingMask.Name == jsonMask.Name))
                {
                    WindowLogger.Write($"Config file contained multiple masks with the same name '{jsonMask.Name}'. The duplicate was removed.");
                    hadSoftError = true;
                    continue;
                }
                if (jsonMask.Mask.Count != CpuInfo.LogicalProcessorCount)
                {
                    WindowLogger.Write($"Config file contained incorrect mask length in mask '{jsonMask.Name}'. The invalid mask was removed.");
                    hadSoftError = true;
                    continue;
                }

                List<VKey> hotkeys = jsonMask.Hotkeys.Select(hotkey => Enum.Parse<VKey>(hotkey)).ToList();
                logicalProcessorMasks.Add(new(jsonMask.Name, jsonMask.Mask, hotkeys));
            }

            // Construct the ProgramMaskRule models from the config
            List<ProgramMaskRule> programMaskRules = configJson.ProgramMaskRules.Select(jsonProgramRule =>
            {
                LogicalProcessorMask mask = logicalProcessorMasks.Single(mask => mask.Name == jsonProgramRule.LogicalProcessorMaskName);
                return new ProgramMaskRule(jsonProgramRule.ProgramPath, mask);
            }).ToList();

            // Construct the AutomaticMaskRule models from the config
            List<ProgramMaskRule> automaticMaskRules = configJson.AutomaticMaskRules.Select(jsonAutomaticRule =>
            {
                LogicalProcessorMask mask = logicalProcessorMasks.Single(mask => mask.Name == jsonAutomaticRule.LogicalProcessorMaskName);
                return new ProgramMaskRule(jsonAutomaticRule.ProgramPath, mask);
            }).ToList();

            // Construct the AppConfig
            return new(logicalProcessorMasks,
                programMaskRules,
                automaticMaskRules,
                configJson.MuteHotKeySound,
                configJson.StartMinimized,
                configJson.DisableWelcomeMessage,
                Enum.Parse<Theme>(configJson.UiTheme),
                generateDefaultMasks);
        }

        private class ConfigJson
        {
            public List<string> NoMaskHotkeys { get; init; }
            public List<LogicalProcessorMaskJson> LogicalProcessorMasks { get; init; }
            public List<ProgramMaskRuleJson> ProgramMaskRules { get; init; }
            public List<ProgramMaskRuleJson> AutomaticMaskRules { get; init; }
            public bool MuteHotKeySound { get; init; }
            public bool StartMinimized { get; init; }
            public bool DisableWelcomeMessage { get; init; }
            public string UiTheme { get; init; }
            public int ConfigVersion { get; init; } // Can be used in the future to migrate config files

            public static ConfigJson Default => new();

            // Default constructor for JSON Deserialization
            [JsonConstructor]
            private ConfigJson()
            {
                NoMaskHotkeys = [];
                LogicalProcessorMasks = [];
                ProgramMaskRules = [];
                AutomaticMaskRules = [];
                MuteHotKeySound = false;
                StartMinimized = false;
                DisableWelcomeMessage = false;
                UiTheme = Theme.System.ToString();
                ConfigVersion = 0;
            }

            public ConfigJson(AppConfig config)
            {
                // Get the Hotkeys for the NoMask
                var noMaskHotkeysVKeys = config.LogicalProcessorMasks.Single(mask => mask.IsNoMask).Hotkeys;
                NoMaskHotkeys = noMaskHotkeysVKeys.Select(hotkey => hotkey.ToString()).ToList();

                // Filter out the NoMask from the list of logicalProcessorMasks
                var userDefinedMasks = config.LogicalProcessorMasks.Where(mask => !mask.IsNoMask);

                // Convert the LogicalProcessorMask models to JSON objects
                LogicalProcessorMasks = userDefinedMasks.Select(mask =>
                {
                    List<string> hotkeys = mask.Hotkeys.Select(hotkey => hotkey.ToString()).ToList();
                    return new LogicalProcessorMaskJson(mask.Name, new(mask.Mask), hotkeys);
                }).ToList();

                // Convert the ProgramMaskRules models to JSON objects
                ProgramMaskRules = config.ProgramMaskRules.Select(programRule =>
                    new ProgramMaskRuleJson(programRule.ProgramPath, programRule.LogicalProcessorMask.Name)
                ).ToList();

                // Convert the AutomaticMaskRules models to JSON objects
                AutomaticMaskRules = config.AutomaticMaskRules.Select(automaticRule =>
                    new ProgramMaskRuleJson(automaticRule.ProgramPath, automaticRule.LogicalProcessorMask.Name)
                ).ToList();

                // Set the remainder of the settings to the JSON object
                MuteHotKeySound = config.MuteHotkeySound;
                StartMinimized = config.StartMinimized;
                DisableWelcomeMessage = config.DisableWelcomeMessage;
                UiTheme = config.UiTheme.ToString();
                ConfigVersion = configVersion;
            }
        }

        private class LogicalProcessorMaskJson
        {
            public string Name { get; init; }
            public List<bool> Mask { get; init; }
            public List<string> Hotkeys { get; init; }

            [JsonConstructor]
            private LogicalProcessorMaskJson()
            {
                Name = string.Empty;
                Mask = [];
                Hotkeys = [];
            }

            public LogicalProcessorMaskJson(string name, List<bool> mask, List<string> hotkeys)
            {
                Name = name;
                Mask = mask;
                Hotkeys = hotkeys;
            }
        }

        private class ProgramMaskRuleJson
        {
            public string ProgramPath { get; init; }
            public string LogicalProcessorMaskName { get; init; }

            [JsonConstructor]
            private ProgramMaskRuleJson()
            {
                ProgramPath = string.Empty;
                LogicalProcessorMaskName = string.Empty;
            }

            public ProgramMaskRuleJson(string programPath, string logicalProcessorMaskName)
            {
                ProgramPath = programPath;
                LogicalProcessorMaskName = logicalProcessorMaskName;
            }
        }
    }
}
