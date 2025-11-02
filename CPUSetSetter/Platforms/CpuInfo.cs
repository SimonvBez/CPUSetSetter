using CPUSetSetter.Config.Models;


namespace CPUSetSetter.Platforms
{
    public static class CpuInfo
    {
        public static Manufacturer Manufacturer => Default.Manufacturer;

        public static IReadOnlyCollection<string> LogicalProcessorNames => Default.LogicalProcessorNames;

        public static int LogicalProcessorCount = Default.LogicalProcessorNames.Count;

        public static IReadOnlyCollection<LogicalProcessorMask> DefaultLogicalProcessorMasks => Default.DefaultLogicalProcessorMasks;

        public static bool IsSupported => Default.IsSupported;


        private static ICpuInfo? _default;

#if WINDOWS
        public static ICpuInfo Default => _default ??= new CpuInfoWindows();
#endif
    }

    public interface ICpuInfo
    {
        Manufacturer Manufacturer { get; }
        IReadOnlyCollection<string> LogicalProcessorNames { get; }
        IReadOnlyCollection<LogicalProcessorMask> DefaultLogicalProcessorMasks { get; }
        bool IsSupported { get; }
    }

    public enum Manufacturer
    {
        Intel,
        AMD,
        Other
    }

    public class UnsupportedCpu : Exception
    {
        public UnsupportedCpu() { }
        public UnsupportedCpu(string message) : base(message) { }
    }
}
