using CPUSetSetter.Config.Models;


namespace CPUSetSetter.Platforms
{
    /// <summary>
    /// Provides information on the system's CPU. It analyzes the core/die structure of the CPU.
    /// It uses this information to provide a list of names for each logical processor, and a collection of default masks that may be common in use.
    /// </summary>
    public static class CpuInfo
    {
        public static Manufacturer Manufacturer => Default.Manufacturer;

        public static IReadOnlyList<string> LogicalProcessorNames => Default.LogicalProcessorNames;

        public static int LogicalProcessorCount { get; } = Default.LogicalProcessorNames.Count;

        public static IReadOnlyList<LogicalProcessorMask> DefaultLogicalProcessorMasks => Default.DefaultLogicalProcessorMasks;

        public static bool IsSupported => Default.IsSupported;


        private static ICpuInfo? _default;

#if WINDOWS
        public static ICpuInfo Default => _default ??= new CpuInfoWindows();
#endif
    }

    public interface ICpuInfo
    {
        Manufacturer Manufacturer { get; }
        IReadOnlyList<string> LogicalProcessorNames { get; }
        IReadOnlyList<LogicalProcessorMask> DefaultLogicalProcessorMasks { get; }
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
