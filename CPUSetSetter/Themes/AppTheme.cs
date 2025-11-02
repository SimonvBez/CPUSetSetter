using Microsoft.Win32;
using System.Windows;


namespace CPUSetSetter.Themes
{
    public static class AppTheme
    {
        public static void ApplyTheme(Theme theme)
        {
            string themePath;
            switch (theme)
            {
                case Theme.Light:
                    themePath = "Themes/LightTheme.xaml";
                    break;
                case Theme.Dark:
                    themePath = "Themes/DarkTheme.xaml";
                    break;
                case Theme.System:
                    ApplySystemTheme();
                    return;
                default:
                    throw new ArgumentException("Invalid theme");
            }

            App.Current.Resources.MergedDictionaries.Clear();
            App.Current.Resources.MergedDictionaries.Add(
                new() { Source = new(themePath, UriKind.Relative) }
            );
        }

        private static void ApplySystemTheme()
        {
            RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            object? value = key?.GetValue("AppsUseLightTheme");
            if (value is int intValue && intValue == 0)
            {
                ApplyTheme(Theme.Dark);
                return;
            }
            ApplyTheme(Theme.Light);
        }
    }

    public enum Theme
    {
        Light,
        Dark,
        System
    }
}
