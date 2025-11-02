using CommunityToolkit.Mvvm.ComponentModel;
using CPUSetSetter.Platforms;
using CPUSetSetter.Themes;
using System.Collections.ObjectModel;
using System.Windows;


namespace CPUSetSetter.Config.Models
{
    public partial class AppConfig : ObservableConfigObject
    {
        private static bool _exists = false;
        public static readonly AppConfig Instance = AppConfigFile.Load();
        public static AppConfig Load() => Instance; // Function to more explicitly control when the config gets loaded

        public ObservableCollection<LogicalProcessorMask> LogicalProcessorMasks { get; }

        public ObservableCollection<ProgramMaskRule> ProgramMaskRules { get; }

        [ObservableProperty]
        private bool _matchWholePath;

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
            bool matchWholePath,
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
            _matchWholePath = matchWholePath;
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
                    Application.Current.Dispatcher.BeginInvoke(DelayedSave);
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
