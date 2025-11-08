using CPUSetSetter.Config.Models;
using CPUSetSetter.UI.Tabs.Processes;
using Microsoft.Win32;
using System.Diagnostics;
using System.Windows;


namespace CPUSetSetter.Platforms
{
    /// <summary>
    /// Windows Game Mode is abysmal to gaming performance when combined with CPU Set Setter.
    /// Warn the user, and encourage them to turn it off, or experiment with it on.
    /// </summary>
    public static class WindowsGameModeWarning
    {
        public static void ShowIfEnabled()
        {
            if (!GameModeEnabled())
                return;

            WindowLogger.Write("WARNING: Windows Game Mode is enabled. This might have a severe negative impact on gaming performance/stability " +
                "when also using CPU Set Setter on games.");

            if (AppConfig.Instance.ShowGameModePopup)
            {
                App.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBoxResult result = MessageBox.Show(
                        "Windows Game Mode is currently enabled.\n" +
                        "On AMD CPUs, Game Mode is known to conflict with CPU Set Setter, leading to lower FPS and game crashes.\n" +
                        "I am not sure if Intel CPUs are affected too. So if you have one, please share your findings with me on GitHub!\n\n" +
                        "Would you like to open the Game Mode Settings page?\n\n" +
                        "(This popup can be disabled in the Settings tab)",
                        "CPU Set Setter: Windows Game Mode warning",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        OpenGameModeSettings();
                    }
                });
            }
        }

        private static bool GameModeEnabled()
        {
            using RegistryKey? gameBarKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\GameBar");
            if (gameBarKey is null)
                return false;

            object? gameModeValue = gameBarKey.GetValue("AutoGameModeEnabled");
            if (gameModeValue is int value)
                return value != 0;

            return false;
        }

        private static void OpenGameModeSettings()
        {
            Process.Start(new ProcessStartInfo { FileName = "ms-settings:gaming-gamemode", UseShellExecute = true });
        }
    }
}
