using CPUSetSetter.Config.Models;


namespace CPUSetSetter.Platforms
{
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
