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

        private const string oldConfigPath = "CPUSetSetter_config.json";

        private static readonly string configDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CPU Set Setter");
        private static readonly string configPath = Path.Combine(configDirectory, "CPUSetSetter_config.json");
        private static readonly string saveTempPath = Path.Combine(configDirectory, "CPUSetSetter_config_new.json");
        private static readonly string backupNameTemplate = Path.Combine(configDirectory, "CPUSetSetter_config_backup{0}.json");
        public const int ConfigVersion = 2;

        static AppConfigFile()
        {
            Directory.CreateDirectory(configDirectory);
        }

        public static void Save(AppConfig config)
        {
            ConfigJson configJson = new(config);
            try
            {
                // Save the new config to a temp file before overwriting the config, in case the serialization fails and clears the config
                FileStream fileStream = File.Create(saveTempPath);
                JsonSerializer.Serialize(fileStream, configJson, options: jsonOptions);
                fileStream.Dispose();
                File.Move(saveTempPath, configPath, true);
            }
            catch (Exception ex)
            {
                WindowLogger.Write($"Failed to write config: {ex}");
            }
        }

        public static AppConfig Load()
        {
            // Copy the old config to the new location, if the new one doesn't exist yet
            if (File.Exists(oldConfigPath) && !File.Exists(configPath))
            {
                WindowLogger.Write($"INFO: The config file location has been migrated to '{configPath}'\n");
                File.Copy(oldConfigPath, configPath);
            }

            if (!File.Exists(configPath))
            {
                // The config file does not exist yet, use the defaults
                return JsonToConfig(ConfigJson.Default, true, true, out bool _);
            }

            try
            {
                using FileStream fileStream = File.OpenRead(configPath);
                ConfigJson configJson = JsonSerializer.Deserialize<ConfigJson>(fileStream, options: jsonOptions) ?? throw new NullReferenceException();
                AppConfig config = JsonToConfig(configJson, false, false, out bool hadSoftError);

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
                return JsonToConfig(ConfigJson.Default, true, false, out bool _);
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
                    File.Copy(configPath, backupName, false);
                    return backupName;
                }
                catch (IOException)
                {
                    // Continue when the backup name already exists
                    continue;
                }
            }
        }

        private static AppConfig JsonToConfig(ConfigJson configJson, bool generateDefaultMasks, bool isFirstRun, out bool hadSoftError)
        {
            hadSoftError = false;

            List<VKey> noMaskHotkeys = configJson.NoMaskHotkeys.Select(hotkey => Enum.Parse<VKey>(hotkey)).ToList();
            // Put the NoMask Mask at the front of the logicalProcessorMasks
            List<LogicalProcessorMask> logicalProcessorMasks = [LogicalProcessorMask.InitNoMask(noMaskHotkeys)];

            // Construct the LogicalProcessorMask models from the config
            foreach (LogicalProcessorMaskJson jsonMask in configJson.Masks)
            {
                if (logicalProcessorMasks.Any(existingMask => existingMask.Name == jsonMask.Name))
                {
                    WindowLogger.Write($"Config file contained multiple masks with the same name '{jsonMask.Name}'. The duplicate was removed.");
                    hadSoftError = true;
                    continue;
                }
                if (jsonMask.BoolMask.Count != CpuInfo.LogicalProcessorCount)
                {
                    WindowLogger.Write($"Config file contained incorrect mask length in mask '{jsonMask.Name}'. The invalid mask was removed.");
                    hadSoftError = true;
                    continue;
                }

                List<VKey> hotkeys = jsonMask.Hotkeys.Select(hotkey => Enum.Parse<VKey>(hotkey)).ToList();
                logicalProcessorMasks.Add(new(jsonMask.Name, jsonMask.BoolMask, hotkeys));
            }

            // Construct the ProgramRule models from the config
            List<ProgramRule> programRules = configJson.ProgramRules.Select(jsonProgramRule =>
            {
                LogicalProcessorMask mask = logicalProcessorMasks.Single(mask => mask.Name == jsonProgramRule.LogicalProcessorMaskName);
                return new ProgramRule(jsonProgramRule.ProgramPath, mask, true);
            }).ToList();

            // Construct the RuleTemplate models from the config
            List<RuleTemplate> ruleTemplates = configJson.RuleTemplates.Select(jsonRuleTemplate =>
            {
                LogicalProcessorMask mask = logicalProcessorMasks.Single(mask => mask.Name == jsonRuleTemplate.LogicalProcessorMaskName);
                return new RuleTemplate(jsonRuleTemplate.RuleGlob, mask);
            }).ToList();

            // Construct the AppConfig
            return new(logicalProcessorMasks,
                programRules,
                ruleTemplates,
                configJson.MuteHotKeySound,
                configJson.StartMinimized,
                configJson.DisableWelcomeMessage,
                configJson.ShowGameModePopup,
                configJson.ShowUpdatePopup,
                configJson.ClearMasksOnClose,
                Enum.Parse<Theme>(configJson.UiTheme),
                generateDefaultMasks,
                isFirstRun,
                configJson.ConfigVersion);
        }

        private class ConfigJson
        {
            public List<string> NoMaskHotkeys { get; init; }
            public List<LogicalProcessorMaskJson> Masks { get; init; }
            public List<ProgramRuleJson> ProgramRules { get; init; }
            public List<RuleTemplateJson> RuleTemplates { get; init; }
            public bool MuteHotKeySound { get; init; }
            public bool StartMinimized { get; init; }
            public bool DisableWelcomeMessage { get; init; }
            public bool ShowGameModePopup { get; init; }
            public bool ShowUpdatePopup { get; init; }
            public bool ClearMasksOnClose { get; init; }
            public string UiTheme { get; init; }
            public int ConfigVersion { get; init; } // Can be used in the future to migrate config files

            public static ConfigJson Default => new();

            // Default constructor for JSON Deserialization
            [JsonConstructor]
            private ConfigJson()
            {
                NoMaskHotkeys = [];
                Masks = [];
                ProgramRules = [];
                RuleTemplates = [];
                MuteHotKeySound = false;
                StartMinimized = false;
                DisableWelcomeMessage = false;
                ShowGameModePopup = true;
                ShowUpdatePopup = true;
                ClearMasksOnClose = false;
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
                Masks = userDefinedMasks.Select(mask =>
                {
                    List<string> hotkeys = mask.Hotkeys.Select(hotkey => hotkey.ToString()).ToList();
                    return new LogicalProcessorMaskJson(mask.Name, new(mask.BoolMask), hotkeys);
                }).ToList();

                // Convert the ProgramRules models to JSON objects
                ProgramRules = config.ProgramRules.Select(programRule =>
                    new ProgramRuleJson(programRule.ProgramPath, programRule.Mask.Name)
                ).ToList();

                // Convert the RuleTemplates models to JSON objects
                RuleTemplates = config.RuleTemplates.Select(ruleTemplate =>
                    new RuleTemplateJson(ruleTemplate.RuleGlob, ruleTemplate.Mask.Name)
                ).ToList();

                // Set the remainder of the settings to the JSON object
                MuteHotKeySound = config.MuteHotkeySound;
                StartMinimized = config.StartMinimized;
                DisableWelcomeMessage = config.DisableWelcomeMessage;
                ShowGameModePopup = config.ShowGameModePopup;
                ShowUpdatePopup = config.ShowUpdatePopup;
                ClearMasksOnClose = config.ClearMasksOnClose;
                UiTheme = config.UiTheme.ToString();
                ConfigVersion = AppConfigFile.ConfigVersion;
            }
        }

        private class LogicalProcessorMaskJson
        {
            public string Name { get; init; }
            public List<bool> BoolMask { get; init; }
            public List<string> Hotkeys { get; init; }

            [JsonConstructor]
            private LogicalProcessorMaskJson()
            {
                Name = string.Empty;
                BoolMask = [];
                Hotkeys = [];
            }

            public LogicalProcessorMaskJson(string name, List<bool> boolMask, List<string> hotkeys)
            {
                Name = name;
                BoolMask = boolMask;
                Hotkeys = hotkeys;
            }
        }

        private class ProgramRuleJson
        {
            public string ProgramPath { get; init; }
            public string LogicalProcessorMaskName { get; init; }

            [JsonConstructor]
            private ProgramRuleJson()
            {
                ProgramPath = string.Empty;
                LogicalProcessorMaskName = string.Empty;
            }

            public ProgramRuleJson(string programPath, string logicalProcessorMaskName)
            {
                ProgramPath = programPath;
                LogicalProcessorMaskName = logicalProcessorMaskName;
            }
        }

        private class RuleTemplateJson
        {
            public string RuleGlob { get; init; }
            public string LogicalProcessorMaskName { get; init; }

            [JsonConstructor]
            private RuleTemplateJson()
            {
                RuleGlob = string.Empty;
                LogicalProcessorMaskName = string.Empty;
            }

            public RuleTemplateJson(string ruleGlob, string logicalProcessorMaskName)
            {
                RuleGlob = ruleGlob;
                LogicalProcessorMaskName = logicalProcessorMaskName;
            }
        }
    }
}
