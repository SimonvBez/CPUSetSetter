using CommunityToolkit.Mvvm.ComponentModel;
using CPUSetSetter.Platforms;
using CPUSetSetter.Themes;
using System.Collections.ObjectModel;
using Application = System.Windows.Application;


namespace CPUSetSetter.Config.Models
{
    public partial class AppConfig : ObservableConfigObject
    {
        public static readonly AppConfig Instance = AppConfigFile.Load();

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
        private ThemeMode _theme;

        private readonly Lock _saveTaskLock = new();
        private bool _isSaving = false;

        public AppConfig(List<LogicalProcessorMask> logicalProcessorMasks,
            List<ProgramMaskRule> programMaskRules,
            bool matchWholePath,
            bool muteHotkeySound,
            bool startMinimized,
            bool disableWelcomeMessage,
            ThemeMode theme,
            bool generateDefaultMasks)
        {
            LogicalProcessorMasks = new(logicalProcessorMasks);
            ProgramMaskRules = new(programMaskRules);
            _matchWholePath = matchWholePath;
            _muteHotkeySound = muteHotkeySound;
            _startMinimized = startMinimized;
            _disableWelcomeMessage = disableWelcomeMessage;
            _theme = theme;

            if (generateDefaultMasks)
            {
                foreach (LogicalProcessorMask coreMask in CpuInfo.DefaultLogicalProcessorMasks)
                {
                    LogicalProcessorMasks.Add(coreMask);
                }
            }

            SaveOnCollectionChanged(LogicalProcessorMasks);
            SaveOnCollectionChanged(ProgramMaskRules);

            AppTheme.ApplyTheme(Theme);
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

        partial void OnThemeChanged(ThemeMode value)
        {
            AppTheme.ApplyTheme(value);
        }
    }
}
