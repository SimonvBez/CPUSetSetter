using CPUSetSetter.Config.Models;
using CPUSetSetter.UI.Tabs.Processes;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;


namespace CPUSetSetter.Platforms.Windows
{
    public class ProcessHandlerWindows : IProcessHandler
    {
        private readonly Queue<CpuTimeTimestamp> _cpuTimeMovingAverageBuffer = new();

        private readonly string _executableName;
        private readonly uint _pid;
        private readonly SafeProcessHandle _queryLimitedInfoHandle;
        private SafeProcessHandle? _setLimitedInfoHandle;

        public ProcessHandlerWindows(string executableName, uint pid, SafeProcessHandle queryHandle)
        {
            _executableName = executableName;
            _pid = pid;
            _queryLimitedInfoHandle = queryHandle;
        }

        public double GetAverageCpuUsage()
        {
            if (_queryLimitedInfoHandle.IsInvalid)
            {
                return -1;
            }

            DateTime now = DateTime.Now;
            // Remove datapoints older than 30 seconds from the moving average buffer
            while (_cpuTimeMovingAverageBuffer.Count > 0)
            {
                TimeSpan datapointAge = now - _cpuTimeMovingAverageBuffer.Peek().Timestamp;
                if (datapointAge.TotalSeconds > 30)
                {
                    _cpuTimeMovingAverageBuffer.Dequeue();
                }
                else
                {
                    break;
                }
            }

            // Get the current total CPU time of the process
            bool success = NativeMethods.GetProcessTimes(_queryLimitedInfoHandle, out FILETIME _, out FILETIME _, out FILETIME kernelTime, out FILETIME userTime);
            if (!success)
            {
                return -1;
            }
            TimeSpan totalCpuTime = TimeSpan.FromTicks((long)(kernelTime.ULong + userTime.ULong));
            _cpuTimeMovingAverageBuffer.Enqueue(new() { Timestamp = now, TotalCpuTime = totalCpuTime });

            // Take the CPU time from now and (up to) a minute ago, and get the average usage %
            CpuTimeTimestamp startDatapoint = _cpuTimeMovingAverageBuffer.Peek();
            TimeSpan deltaTime = now - startDatapoint.Timestamp;
            TimeSpan deltaCpuTime = totalCpuTime - startDatapoint.TotalCpuTime;

            if (deltaCpuTime.Ticks == 0)
                return 0;
            else
                return (double)deltaCpuTime.Ticks / deltaTime.Ticks / CpuInfo.LogicalProcessorCount;
        }

        public bool ApplyMask(LogicalProcessorMask mask)
        {
            return ApplyCpuSet(mask);
        }

        /// <summary>
        /// Apply a given mask as a CPU Set
        /// </summary>
        private bool ApplyCpuSet(LogicalProcessorMask mask)
        {
            if (_setLimitedInfoHandle is null)
            {
                _setLimitedInfoHandle = NativeMethods.OpenProcess(ProcessAccessFlags.PROCESS_SET_LIMITED_INFORMATION, false, _pid);
                if (_setLimitedInfoHandle.IsInvalid)
                {
                    int error = Marshal.GetLastWin32Error();
                    string extraHelpString = (error == 5 && !Environment.IsPrivilegedProcess) ? " Try restarting as Admin" : "";
                    WindowLogger.Write($"ERROR: Could not open process '{_executableName}': {new System.ComponentModel.Win32Exception(error).Message}{extraHelpString}");
                    return false;
                }
            }
            else if (_setLimitedInfoHandle.IsInvalid)
            {
                // The handle was already made previously, don't bother trying again
                return false;
            }

            bool success;
            if (mask.IsNoMask)
            {
                // Clear the CPU Set
                success = NativeMethods.SetProcessDefaultCpuSetMasks(_setLimitedInfoHandle, null, 0);
                if (success)
                {
                    WindowLogger.Write($"Cleared CPU Set of '{_executableName}'");
                    return true;
                }
                else
                {
                    int error = Marshal.GetLastWin32Error();
                    WindowLogger.Write($"ERROR: Could not clear CPU Set of '{_executableName}': {new System.ComponentModel.Win32Exception(error).Message}");
                    return false;
                }
            }
            else
            {
                UIntPtr bitMask = 0;
                for (int i = 0; i < mask.BoolMask.Count; ++i)
                {
                    if (mask.BoolMask[i])
                        bitMask |= (UIntPtr)1 << i;
                }

                GROUP_AFFINITY[] affinity =
                [
                    new GROUP_AFFINITY
                    {
                        Group = 0,
                        Mask = bitMask
                    }
                ];

                success = NativeMethods.SetProcessDefaultCpuSetMasks(_setLimitedInfoHandle, affinity, 1);
                if (success)
                {
                    WindowLogger.Write($"Applied CPU Set '{mask.Name}' to '{_executableName}'");
                    return true;
                }
                else
                {
                    int error = Marshal.GetLastWin32Error();
                    string errorMessage = $"ERROR: Could not apply CPU Set to '{_executableName}': {new Win32Exception(error).Message}";
                    if (error == 5)
                        errorMessage += " Likely due to anti-cheat";
                    WindowLogger.Write(errorMessage);
                    return false;
                }
            }
        }

        public void Dispose()
        {
            _queryLimitedInfoHandle.Dispose();
            _setLimitedInfoHandle?.Dispose();
            _cpuTimeMovingAverageBuffer.Clear();
        }

        private class CpuTimeTimestamp
        {
            public DateTime Timestamp { get; init; }
            public TimeSpan TotalCpuTime { get; init; }
        }
    }
}
