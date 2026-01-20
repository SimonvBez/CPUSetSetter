using CommunityToolkit.Mvvm.ComponentModel;
using CPUSetSetter.Platforms;
using CPUSetSetter.Themes;
using CPUSetSetter.UI.Tabs.Processes;
using System.Collections.ObjectModel;


namespace CPUSetSetter.Config.Models
{
    public partial class AppConfig : ObservableConfigObject
    {
        private static bool _exists = false;
        public static readonly AppConfig Instance = AppConfigFile.Load();
        public static AppConfig Load() => Instance; // Function to more explicitly control when the config gets loaded

        private Task? _saveTask = null;

        // Masks that can be used by program rules and rule templates
        public ObservableCollection<LogicalProcessorMask> LogicalProcessorMasks { get; }
        // Program rules that define which mask should be used on a specific program
        public ObservableCollection<ProgramRule> ProgramRules { get; }
        // Rules with optional wildcards that can automatically create program rules
        public ObservableCollection<RuleTemplate> RuleTemplates { get; }

        [ObservableProperty]
        private bool _muteHotkeySound;

        [ObservableProperty]
        private bool _startMinimized;

        [ObservableProperty]
        private bool _disableWelcomeMessage;

        [ObservableProperty]
        private bool _showGameModePopup;

        [ObservableProperty]
        private bool _showUpdatePopup;

        [ObservableProperty]
        private bool _clearMasksOnClose;

        [ObservableProperty]
        private Theme _uiTheme;

        public bool IsFirstRun { get; }

        private readonly Lock _saveTaskLock = new();
        private bool _isSaving = false;

        public AppConfig(List<LogicalProcessorMask> logicalProcessorMasks,
            List<ProgramRule> programRules,
            List<RuleTemplate> ruleTemplates,
            bool muteHotkeySound,
            bool startMinimized,
            bool disableWelcomeMessage,
            bool showGameModePopup,
            bool showUpdatePopup,
            bool clearMasksOnClose,
            Theme uiTheme,
            bool generateDefaultMasks,
            bool isFirstRun,
            int configVersion)
        {
            if (_exists)
            {
                throw new InvalidOperationException("Only a single AppConfig can be constructed in the app's lifetime");
            }
            _exists = true;

            LogicalProcessorMasks = new(logicalProcessorMasks);
            ProgramRules = new(programRules);
            RuleTemplates = new(ruleTemplates);
            _muteHotkeySound = muteHotkeySound;
            _startMinimized = startMinimized;
            _disableWelcomeMessage = disableWelcomeMessage;
            _showGameModePopup = showGameModePopup;
            _showUpdatePopup = showUpdatePopup;
            _clearMasksOnClose = clearMasksOnClose;
            _uiTheme = uiTheme;
            IsFirstRun = isFirstRun;

            if (!DisableWelcomeMessage)
            {
                WindowLogger.Write(
                    "Welcome! Here you can apply a Core Mask to a process. Changes are also saved and applied automatically the next time it runs.\n" +
                    "Use the Masks tab to customize your Core Masks and Hotkeys. For the advanced, use the Rules tab to create Templates for entire folders.\n" +
                    "I hope this tool may be of use to you! For questions, issues, feedback or just to say Hi, please comment/open an Issue on GitHub!\n");
            }

            if (CpuInfo.DieDetectionFailed && CpuInfo.Manufacturer == Manufacturer.AMD)
            {
                WindowLogger.Write("INFO: CCD detection does not work on Windows 10. If you have a multi-CCD CPU, you need to create the CCD Core Masks manually.");
            }

            bool hasChangesToSave = false;

            if (generateDefaultMasks)
            {
                // Create a default set of masks for this system's CPU
                List<string> names = [];
                foreach ((string name, List<bool> boolMask) in CpuInfo.DefaultLogicalProcessorMasks)
                {
                    names.Add(name);
                    LogicalProcessorMasks.Add(new(name, MaskApplyType.CPUSet, boolMask, []));
                }
                if (names.Count > 0)
                {
                    hasChangesToSave = true;
                    WindowLogger.Write($"The following default Core Masks have been created: {string.Join(", ", names)}");
                }
            }

            if (MigrateConfig(configVersion))
            {
                hasChangesToSave = true;
            }

            if (hasChangesToSave)
            {
                Save();
            }

            SaveOnCollectionChanged(LogicalProcessorMasks);
            SaveOnCollectionChanged(ProgramRules);
            SaveOnCollectionChanged(RuleTemplates);

            AppTheme.ApplyTheme(UiTheme);
        }

        /// <summary>
        /// Initiate a config save. The saving is delayed by a few milliseconds so that bulk changes don't do 30+ saves in a row
        /// </summary>
        public void Save()
        {
            using (_saveTaskLock.EnterScope())
            {
                if (!_isSaving)
                {
                    _isSaving = true;
                    _saveTask = App.Current.Dispatcher.BeginInvoke(DelayedSave).Task;
                }
            }
        }

        private async Task DelayedSave()
        {
            await Task.Delay(30);

            using (_saveTaskLock.EnterScope())
            {
                AppConfigFile.Save(this);
                _isSaving = false;
            }
        }

        public void WaitForSave()
        {
            _saveTask?.Wait();
        }

        partial void OnUiThemeChanged(Theme value)
        {
            AppTheme.ApplyTheme(value);
        }

        private bool MigrateConfig(int loadedConfigVersion)
        {
            bool hasMigrated = loadedConfigVersion < AppConfigFile.ConfigVersion;

            if (loadedConfigVersion < 2)
            {
                // Config 1 -> 2 migration
                // New default masks were added. If they are not present yet, add them to the user's config
                List<string> newNames = [];
                foreach ((string name, List<bool> boolMask) in CpuInfo.DefaultLogicalProcessorMasks)
                {
                    if (!LogicalProcessorMasks.Any(existing => existing.Name == name))
                    {
                        newNames.Add(name);
                        LogicalProcessorMasks.Add(new(name, MaskApplyType.CPUSet, boolMask, []));
                    }
                }
                if (newNames.Count > 0)
                {
                    WindowLogger.Write($"The following new default Core Masks have been created: {string.Join(", ", newNames)}");
                }
            }

            return hasMigrated;
        }
    }
}
