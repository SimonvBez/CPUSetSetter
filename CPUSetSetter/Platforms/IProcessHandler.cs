using CPUSetSetter.Config.Models;


namespace CPUSetSetter.Platforms
{
    public interface IProcessHandler : IDisposable
    {
        /// <summary>
        /// Get the CPU usage of the process
        /// </summary>
        /// <returns>Between 0 and 1 on success. -1 on fail</returns>
        double GetCpuUsage();
        void ResetCpuUsageSampling();
        bool ApplyMask(LogicalProcessorMask mask);
        bool ReapplyMask(LogicalProcessorMask mask);
    }
}
