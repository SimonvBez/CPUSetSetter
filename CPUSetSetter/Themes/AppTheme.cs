using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;


namespace CPUSetSetter.Themes
{
    public static class AppTheme
    {
        public static void ApplyTheme(Theme theme)
        {
            string themePath;
            bool isDark = false;
            switch (theme)
            {
                case Theme.Light:
                    themePath = "Themes/LightThemeColors.xaml";
                    break;
                case Theme.Dark:
                    themePath = "Themes/DarkThemeColors.xaml";
                    isDark = true;
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

            UpdateTitleBarStatus(isDark);
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

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        // Windows 10 1903/1909 (builds 18362-18363) used attribute 19.
        private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;
        // Windows 10 20H1 (build 19041) and later use attribute 20.
        private const int DwmwaUseImmersiveDarkMode = 20;

        private static bool _isDarkTheme;
        private static bool _isWindowLoadedRegistered;

        private static void UpdateTitleBarStatus(bool isDarkTheme)
        {
            if (Application.Current == null) return;
            Application.Current.Dispatcher.VerifyAccess();

            _isDarkTheme = isDarkTheme;

            if (!_isWindowLoadedRegistered)
            {
                EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(Window_Loaded));
                _isWindowLoadedRegistered = true;
            }

            foreach (Window window in Application.Current.Windows)
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero)
                {
                    window.SourceInitialized -= OnSourceInitialized;
                    window.SourceInitialized += OnSourceInitialized;
                }
                else
                {
                    ApplyDarkMode(hwnd, _isDarkTheme);
                }
            }
        }

        private static void OnSourceInitialized(object? sender, EventArgs e)
        {
            if (sender is Window window)
            {
                window.SourceInitialized -= OnSourceInitialized;
                var hwnd = new WindowInteropHelper(window).Handle;
                ApplyDarkMode(hwnd, _isDarkTheme);
            }
        }

        private static void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is Window window)
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    ApplyDarkMode(hwnd, _isDarkTheme);
                }
            }
        }

        private static void ApplyDarkMode(IntPtr hwnd, bool isDarkTheme)
        {
            int useImmersiveDarkMode = isDarkTheme ? 1 : 0;

            // Try the modern attribute first (Windows 10 20H1+).
            if (DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref useImmersiveDarkMode, sizeof(int)) != 0)
            {
                // If it failed, fall back to the pre-20H1 attribute (Windows 10 1903/1909).
                // On versions older than 1903, this will also fail silently with no effect.
                DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeBefore20H1, ref useImmersiveDarkMode, sizeof(int));
            }
        }
    }

    public enum Theme
    {
        Light,
        Dark,
        System
    }
}
