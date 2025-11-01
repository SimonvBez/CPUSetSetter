using CommunityToolkit.Mvvm.ComponentModel;
using CPUSetSetter.Config.Models;
using CPUSetSetter.Platforms;
using CPUSetSetter.Themes;
using System.Collections.ObjectModel;


namespace CPUSetSetter.Config
{
    public partial class AppConfig : ObservableObject
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
            Theme = theme;

            if (generateDefaultMasks)
            {
                foreach (LogicalProcessorMask coreMask in CpuInfo.DefaultLogicalProcessorMasks)
                {
                    LogicalProcessorMasks.Add(coreMask);
                }
            }
        }

        public void Save()
        {
            AppConfigFile.Save(this);
        }
    }
}
