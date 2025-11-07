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
                    themePath = "Themes/LightThemeColors.xaml";
                    break;
                case Theme.Dark:
                    themePath = "Themes/DarkThemeColors.xaml";
                    break;
                case Theme.System:
                    ApplySystemTheme();
                    return;
                default:
                    throw new ArgumentException("Invalid theme");
            }

            var mergedDicts = App.Current.Resources.MergedDictionaries;
            ResourceDictionary colorDict = new() { Source = new(themePath, UriKind.Relative) };
            if (mergedDicts.Count == 0)
            {
                // Theme is being set for the first time, set the theme colors and the Styles that use them
                mergedDicts.Add(colorDict);
                mergedDicts.Add(new() { Source = new("Themes/Styles.xaml", UriKind.Relative) });
            }
            else if (mergedDicts.Count == 2)
            {
                // Theme is being hot-switched. Only change the colors
                mergedDicts[0] = colorDict;
            }
            else
            {
                throw new InvalidOperationException($"Unexpected MergedDictionaries count: {mergedDicts.Count}");
            }
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
