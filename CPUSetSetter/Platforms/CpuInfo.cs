using CPUSetSetter.Config.Models;


namespace CPUSetSetter.Platforms
{
    public static class CpuInfo
    {
        public static Manufacturer Manufacturer => Default.Manufacturer;

        public static IReadOnlyCollection<string> LogicalProcessorNames => Default.LogicalProcessorNames;

        public static IReadOnlyCollection<LogicalProcessorMask> DefaultLogicalProcessorMasks => Default.DefaultLogicalProcessorMasks;

        public static bool IsSupported => Default.IsSupported;


        private static ICpuInfo? _default;

#if WINDOWS
        public static ICpuInfo Default => _default ??= new CpuInfoWindows();
#endif
    }
}
