using CPUSetSetter.Config.Models;


namespace CPUSetSetter.Platforms
{
    public static class CpuInfo
    {
        public static Manufacturer Manufacturer => Default.Manufacturer;

        public static IReadOnlyCollection<string> ThreadNames => Default.ThreadNames;

        public static IReadOnlyCollection<CoreMask> DefaultCoreMasks => Default.DefaultCoreMasks;

        public static bool IsSupported => Default.IsSupported;


        private static ICpuInfo? _default;

#if WINDOWS
        public static ICpuInfo Default => _default ??= new CpuInfoWindows();
#endif
    }
}
