namespace CPUSetSetter.Platforms
{
    public static class AutoStarter
    {
        public const string LaunchArgumentEnable = "--create-task";
        public const string LaunchArgumentDisable = "--remove-task";

        public static bool Enable() => Default.Enable();
        public static bool Disable() => Default.Disable();
        public static bool IsEnabled => Default.IsEnabled;

        private static IAutoStarter? _default;

#if WINDOWS
        public static IAutoStarter Default => _default ??= new AutoStarterWindows();
#endif
    }

    public interface IAutoStarter
    {
        /// <returns>true if the AutoStart task was created successfully</returns>
        bool Enable();
        /// <returns>true if the AutoStart task was removed successfully</returns>
        bool Disable();
        bool IsEnabled { get; }
    }
}
