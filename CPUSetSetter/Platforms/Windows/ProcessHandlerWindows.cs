using CPUSetSetter.Config.Models;
using CPUSetSetter.UI.Tabs.Processes;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;


namespace CPUSetSetter.Platforms.Windows
{
    public class ProcessHandlerWindows : IProcessHandler
    {
        private readonly static Dictionary<int, uint> _setIdPerLogicalProcessor;
        private readonly Queue<CpuTimeTimestamp> _cpuTimeMovingAverageBuffer = new();

        private readonly string _executableName;
        private readonly uint _pid;
        private readonly SafeProcessHandle _queryLimitedInfoHandle;
        private SafeProcessHandle? _setLimitedInfoHandle;

        static ProcessHandlerWindows()
        {
            _setIdPerLogicalProcessor = GetCpuSetIdPerLogicalProcessor();
        }

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
            int error;

            if (_setLimitedInfoHandle is null)
            {
                _setLimitedInfoHandle = NativeMethods.OpenProcess(ProcessAccessFlags.PROCESS_SET_LIMITED_INFORMATION, false, _pid);
                if (_setLimitedInfoHandle.IsInvalid)
                {
                    error = Marshal.GetLastWin32Error();
                    string extraHelpString = (error == 5 && !Environment.IsPrivilegedProcess) ? " Try restarting as Admin" : "";
                    WindowLogger.Write($"ERROR: Could not open process '{_executableName}': {new Win32Exception(error).Message}{extraHelpString}");
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
                success = NativeMethods.SetProcessDefaultCpuSets(_setLimitedInfoHandle, null, 0);
                if (success)
                {
                    WindowLogger.Write($"Cleared CPU Set of '{_executableName}'");
                    return true;
                }

                error = Marshal.GetLastWin32Error();
                WindowLogger.Write($"ERROR: Could not clear CPU Set of '{_executableName}': {new Win32Exception(error).Message}");
                return false;
            }

            // Get an array of active CPU Set Ids for this mask
            List<uint> cpuSetIds = [];
            for (int i = 0; i < mask.BoolMask.Count; ++i)
            {
                try
                {
                    if (mask.BoolMask[i])
                        cpuSetIds.Add(_setIdPerLogicalProcessor[i]);
                }
                catch (KeyNotFoundException)
                {
                    WindowLogger.Write($"WARNING: Unable to include '{CpuInfo.LogicalProcessorNames[i]}' in Core Mask. It does not have a CPU Set ID");
                }
            }
            uint[] cpuSetIdsArray = cpuSetIds.ToArray();
            success = NativeMethods.SetProcessDefaultCpuSets(_setLimitedInfoHandle, cpuSetIdsArray, (uint)cpuSetIdsArray.Length);
            if (success)
            {
                WindowLogger.Write($"Applied CPU Set '{mask.Name}' to '{_executableName}'");
                return true;
            }

            error = Marshal.GetLastWin32Error();
            string errorMessage = $"ERROR: Could not apply CPU Set to '{_executableName}': {new Win32Exception(error).Message}";
            if (error == 5)
                errorMessage += " Likely due to anti-cheat";
            WindowLogger.Write(errorMessage);
            return false;
        }

        /// <summary>
        /// Get the CPU Set Id of each logical processor 
        /// </summary>
        private static Dictionary<int, uint> GetCpuSetIdPerLogicalProcessor()
        {
            uint bufferLength = 0;
            if (!NativeMethods.GetSystemCpuSetInformation(IntPtr.Zero, 0, ref bufferLength, new(), 0))
            {
                int error = Marshal.GetLastWin32Error();
                if (error != 0x7A) // ERROR_INSUFFICIENT_BUFFER
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            Dictionary<int, uint> cpuSets = [];
            // Create the buffer and get the CPU Set information
            IntPtr buffer = Marshal.AllocHGlobal((int)bufferLength);
            try
            {
                if (!NativeMethods.GetSystemCpuSetInformation(buffer, bufferLength, ref bufferLength, new(), 0))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                IntPtr current = buffer;
                IntPtr bufferEnd = buffer + (IntPtr)bufferLength;
                int itemSize = Marshal.SizeOf<SYSTEM_CPU_SET_INFORMATION>();
                while (current < bufferEnd)
                {
                    SYSTEM_CPU_SET_INFORMATION item = Marshal.PtrToStructure<SYSTEM_CPU_SET_INFORMATION>(current);
                    
                    if (item.Type != CPU_SET_INFORMATION_TYPE.CpuSetInformation)
                    {
                        throw new InvalidCastException("Invalid data type encounted; aborting");
                    }

                    cpuSets.Add(item.LogicalProcessorIndex, item.Id);

                    current += (IntPtr)item.Size;
                }
                return cpuSets;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
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
