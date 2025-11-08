using CPUSetSetter.Config.Models;


namespace CPUSetSetter.Platforms
{
    public interface IProcessHandler : IDisposable
    {
        /// <summary>
        /// Get the average CPU usage of the process of the recent past (~30 seconds)
        /// </summary>
        /// <returns>Between 0 and 1 on success. -1 on fail</returns>
        double GetAverageCpuUsage();
        bool ApplyMask(LogicalProcessorMask mask);
    }
}
