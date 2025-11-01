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

        public ObservableCollection<CoreMask> CoreMasks { get; }

        public ObservableCollection<ProgramCoreMaskRule> ProgramCoreMaskRules { get; }

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

        public AppConfig(List<CoreMask> coreMasks,
            List<ProgramCoreMaskRule> programCoreMaskRules,
            bool matchWholePath,
            bool muteHotkeySound,
            bool startMinimized,
            bool disableWelcomeMessage,
            ThemeMode theme,
            bool generateDefaultMasks)
        {
            CoreMasks = new(coreMasks);
            ProgramCoreMaskRules = new(programCoreMaskRules);
            _matchWholePath = matchWholePath;
            _muteHotkeySound = muteHotkeySound;
            _startMinimized = startMinimized;
            _disableWelcomeMessage = disableWelcomeMessage;
            Theme = theme;

            if (generateDefaultMasks)
            {
                foreach (CoreMask coreMask in CpuInfo.DefaultCoreMasks)
                {
                    CoreMasks.Add(coreMask);
                }
            }
        }
    }
}
