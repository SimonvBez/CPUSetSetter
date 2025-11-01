using CPUSetSetter.Config.Models;
using CPUSetSetter.Themes;
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
                return JsonToConfig(ConfigJson.Default, true);
            }

            try
            {
                using FileStream fileStream = File.OpenRead(fileName);
                ConfigJson configJson = JsonSerializer.Deserialize<ConfigJson>(fileStream, options: jsonOptions) ?? throw new NullReferenceException();
                return JsonToConfig(configJson, false); // The config file was loaded successfully
            }
            catch (Exception readEx)
            {
                // The config file was likely corrupt
                WindowLogger.Write($"Failed to read config: {readEx}");
                try
                {
                    string backupName = BackupConfig();
                    WindowLogger.Write($"Your config has been reset. The old config was backed up to '{backupName}'");
                }
                catch (Exception backupEx)
                {
                    WindowLogger.Write($"Unable to make a backup of your old config: {backupEx}");
                    WindowLogger.Write("Your config has been reset. What did you do to even make the backup fail??");
                }
                // Use the defaults
                return JsonToConfig(ConfigJson.Default, true);
            }
        }

        private static string BackupConfig()
        {
            int i = 0;
            while (true) {
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

        private static AppConfig JsonToConfig(ConfigJson configJson, bool generateDefaultMasks)
        {
            List<VKey> clearMaskHotkeys = configJson.ClearMaskHotkey.Select(hotkey => Enum.Parse<VKey>(hotkey)).ToList();
            List<CoreMask> coreMasks = [CoreMask.InitClearMask(clearMaskHotkeys)]; // Put the ClearMask CoreMask at the front of the CoreMasks

            coreMasks.AddRange(configJson.CoreMasks.Select(jsonMask =>
            {
                List<VKey> hotkeys = jsonMask.Hotkeys.Select(hotkey => Enum.Parse<VKey>(hotkey)).ToList();
                return new CoreMask(jsonMask.Name, jsonMask.Mask, hotkeys);
            }));

            List<ProgramCoreMaskRule> programCoreMaskRules = configJson.ProgramRules.Select(jsonProgramRule =>
            {
                CoreMask coreMask = coreMasks.Single(coreMask => coreMask.Name == jsonProgramRule.CoreMaskName);
                return new ProgramCoreMaskRule(jsonProgramRule.ProgramPath, coreMask);
            }).ToList();

            return new(coreMasks,
                programCoreMaskRules,
                configJson.MatchWholePath,
                configJson.MuteHotKeySound,
                configJson.StartMinimized,
                configJson.DisableWelcomeMessage,
                Enum.Parse<ThemeMode>(configJson.Theme),
                generateDefaultMasks);
        }

        private class ConfigJson
        {
            public List<string> ClearMaskHotkey { get; init; } = [];
            public List<CoreMaskJson> CoreMasks { get; init; } = [];
            public List<ProgramCoreMaskRuleJson> ProgramRules { get; init; } = [];
            public bool MatchWholePath { get; init; } = true;
            public bool MuteHotKeySound { get; init; } = false;
            public bool StartMinimized { get; init; } = false;
            public bool DisableWelcomeMessage { get; init; } = false;
            public string Theme { get; init; } = ThemeMode.System.ToString();
            public int ConfigVersion { get; init; } = 0; // Can be used in the future to migrate config files

            [JsonConstructor]
            private ConfigJson() { } // Default constructor for JSON Deserialization

            public static ConfigJson Default => new ConfigJson();

            public ConfigJson(AppConfig config)
            {
                var clearMaskHotkeyVKeys = config.CoreMasks.Single(coreMask => coreMask.IsClearMask).Hotkeys;
                ClearMaskHotkey = clearMaskHotkeyVKeys.Select(hotkey => hotkey.ToString()).ToList();

                // Filter out the ClearMask from the list of CoreMasks
                var userDefinedMasks = config.CoreMasks.Where(coreMask => !coreMask.IsClearMask);
                CoreMasks = userDefinedMasks.Select(coreMask => {
                    List<string> hotkeys = coreMask.Hotkeys.Select(hotkey => hotkey.ToString()).ToList();
                    return new CoreMaskJson(coreMask.Name, new(coreMask.Mask), hotkeys);
                }).ToList();

                ProgramRules = config.ProgramCoreMaskRules.Select(programRule =>
                    new ProgramCoreMaskRuleJson(programRule.ProgramPath, programRule.CoreMask.Name)
                ).ToList();

                MatchWholePath = config.MatchWholePath;
                MuteHotKeySound = config.MuteHotkeySound;
                StartMinimized = config.StartMinimized;
                DisableWelcomeMessage = config.DisableWelcomeMessage;
                Theme = config.Theme.ToString();
                ConfigVersion = configVersion;
            }
        }

        private class CoreMaskJson
        {
            public string Name { get; init; } = string.Empty;
            public List<bool> Mask { get; init; } = [];
            public List<string> Hotkeys { get; init; } = [];

            [JsonConstructor]
            private CoreMaskJson() { }

            public CoreMaskJson(string name, List<bool> mask, List<string> hotkeys)
            {
                Name = name;
                Mask = mask;
                Hotkeys = hotkeys;
            }
        }

        private class ProgramCoreMaskRuleJson
        {
            public string ProgramPath { get; init; } = string.Empty;
            public string CoreMaskName { get; init; } = string.Empty;

            [JsonConstructor]
            private ProgramCoreMaskRuleJson() { }

            public ProgramCoreMaskRuleJson(string programPath, string coreMaskName)
            {
                ProgramPath = programPath;
                CoreMaskName = coreMaskName;
            }
        }
    }
}
