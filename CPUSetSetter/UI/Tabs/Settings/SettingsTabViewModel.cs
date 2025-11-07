using CommunityToolkit.Mvvm.ComponentModel;
using CPUSetSetter.Platforms;
using CPUSetSetter.Themes;


namespace CPUSetSetter.UI.Tabs.Settings
{
    public partial class SettingsTabViewModel : ObservableObject
    {
        public static List<Theme> AvailableThemes { get; } = new(Enum.GetValues(typeof(Theme)).Cast<Theme>());

        [ObservableProperty]
        private bool _autoStartEnabled = AutoStarter.IsEnabled;

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
