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
        public ObservableCollection<ProgramMaskRule> ProgramMaskRules { get; }
        // Rules with optional wildcards that can automatically create program rules
        public ObservableCollection<ProgramMaskRule> AutomaticMaskRules { get; }

        [ObservableProperty]
        private bool _muteHotkeySound;

        [ObservableProperty]
        private bool _startMinimized;

        [ObservableProperty]
        private bool _disableWelcomeMessage;

        [ObservableProperty]
        private Theme _uiTheme;

        private readonly Lock _saveTaskLock = new();
        private bool _isSaving = false;

        public AppConfig(List<LogicalProcessorMask> logicalProcessorMasks,
            List<ProgramMaskRule> programMaskRules,
            List<ProgramMaskRule> automaticMaskRules,
            bool muteHotkeySound,
            bool startMinimized,
            bool disableWelcomeMessage,
            Theme uiTheme,
            bool generateDefaultMasks)
        {
            if (_exists)
            {
                throw new InvalidOperationException("Only a single AppConfig can be constructed in the app's lifetime");
            }
            _exists = true;

            LogicalProcessorMasks = new(logicalProcessorMasks);
            ProgramMaskRules = new(programMaskRules);
            AutomaticMaskRules = new(automaticMaskRules);
            _muteHotkeySound = muteHotkeySound;
            _startMinimized = startMinimized;
            _disableWelcomeMessage = disableWelcomeMessage;
            _uiTheme = uiTheme;

            if (generateDefaultMasks)
            {
                foreach (LogicalProcessorMask coreMask in CpuInfo.DefaultLogicalProcessorMasks)
                {
                    LogicalProcessorMasks.Add(coreMask);
                }
            }

            SaveOnCollectionChanged(LogicalProcessorMasks);
            SaveOnCollectionChanged(ProgramMaskRules);

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
