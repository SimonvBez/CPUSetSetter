using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CPUSetSetter.Platforms;
using CPUSetSetter.Themes;
using CPUSetSetter.Util;


namespace CPUSetSetter.UI.Tabs.Settings
{
    public partial class SettingsTabViewModel : ObservableObject
    {
        public static List<Theme> AvailableThemes { get; } = new(Enum.GetValues(typeof(Theme)).Cast<Theme>());

        [ObservableProperty]
        private bool _autoStartEnabled = AutoStarter.IsEnabled;

        [RelayCommand]
        private static void OpenReleasePage()
        {
            VersionChecker.OpenLatestReleasePage();
        }

        partial void OnAutoStartEnabledChanged(bool value)
        {
            if (value && !AutoStarter.IsEnabled)
            {
                AutoStartEnabled = AutoStarter.Enable();
            }
            else if (!value && AutoStarter.IsEnabled)
            {
                AutoStarter.Disable();
                AutoStartEnabled = AutoStarter.IsEnabled;
            }
        }
    }
}
