using CommunityToolkit.Mvvm.ComponentModel;
using CPUSetSetter.Platforms;
using CPUSetSetter.Themes;
using System.Collections.ObjectModel;


namespace CPUSetSetter.Config.Models
{
    public partial class AppConfig : ObservableConfigObject
    {
        private static bool _exists = false;
        public static readonly AppConfig Instance = AppConfigFile.Load();
        public static AppConfig Load() => Instance; // Function to more explicitly control when the config gets loaded

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
            Theme uiTheme,
            bool generateDefaultMasks,
            bool isFirstRun)
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
            _uiTheme = uiTheme;
            IsFirstRun = isFirstRun;

            if (generateDefaultMasks)
            {
                // Create a default set of masks for this system's CPU
                foreach ((string name, List<bool> boolMask) in CpuInfo.DefaultLogicalProcessorMasks)
                {
                    LogicalProcessorMasks.Add(new(name, boolMask, []));
                }
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
                    App.Current.Dispatcher.BeginInvoke(DelayedSave);
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

        partial void OnUiThemeChanged(Theme value)
        {
            AppTheme.ApplyTheme(value);
        }
    }
}
