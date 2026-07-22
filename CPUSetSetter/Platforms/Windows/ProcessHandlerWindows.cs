using CPUSetSetter.Config.Models;
using CPUSetSetter.UI.Tabs.Processes;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;


namespace CPUSetSetter.Platforms.Windows
{
    public class ProcessHandlerWindows : IProcessHandler
    {
        private readonly static Dictionary<int, uint> _logicalProcessorToSetId;
        private readonly static Dictionary<uint, int> _setIdToLogicalProcessor;

        private readonly string _executableName;
        private readonly uint _pid;
        private readonly SafeProcessHandle _queryLimitedInfoHandle;
        private SafeProcessHandle? _setLimitedInfoHandle;
        private SafeProcessHandle? _setInfoHandle;
        private MaskApplyType _previousMaskType = MaskApplyType.NoMask;

        static ProcessHandlerWindows()
        {
            _logicalProcessorToSetId = GetCpuSetIdPerLogicalProcessor();
            _setIdToLogicalProcessor = _logicalProcessorToSetId.ToDictionary(x => x.Value, x => x.Key);
        }

        public ProcessHandlerWindows(string executableName, uint pid, SafeProcessHandle queryHandle)
        {
            _executableName = executableName;
            _pid = pid;
            _queryLimitedInfoHandle = queryHandle;
        }

        private bool _isFirstCall = true;
        private long _lastTimeStamp = 0;
        private TimeSpan _lastTotalCpuTime = TimeSpan.Zero;

        public double GetCpuUsage()
        {
            if (_queryLimitedInfoHandle.IsInvalid)
            {
                return -1;
            }

            // Get the current CPU time using GetProcessTimes
            bool success = NativeMethods.GetProcessTimes(_queryLimitedInfoHandle, out FILETIME _, out FILETIME _, out FILETIME kernelTime, out FILETIME userTime);
            if (!success)
            {
                return -1;
            }

            long now = Stopwatch.GetTimestamp();
            TimeSpan totalCpuTime = TimeSpan.FromTicks((long)(kernelTime.ULong + userTime.ULong));

            if (_isFirstCall)
            {
                _isFirstCall = false;
                _lastTimeStamp = now;
                _lastTotalCpuTime = totalCpuTime;
                return 0; // There is no delta on the first call
            }

            TimeSpan deltaTime = Stopwatch.GetElapsedTime(_lastTimeStamp, now);
            TimeSpan deltaCpuTime = totalCpuTime - _lastTotalCpuTime;

            _lastTimeStamp = now;
            _lastTotalCpuTime = totalCpuTime;

            if (deltaCpuTime.Ticks <= 0 || deltaTime.Ticks <= 0)
                return 0;
            else if (CpuInfo.LogicalProcessorCount <= 0)
                return 0;
            return Math.Clamp((double)deltaCpuTime.Ticks / deltaTime.Ticks / CpuInfo.LogicalProcessorCount, 0, 1);
        }

        public void ResetCpuUsageSampling()
        {
            _isFirstCall = true;
            _lastTimeStamp = 0;
            _lastTotalCpuTime = TimeSpan.Zero;
        }

        public bool ApplyMask(LogicalProcessorMask mask)
        {
            bool result;

            switch (mask.MaskType)
            {
                case MaskApplyType.NoMask:
                    // Clear the previous mask
                    if (_previousMaskType == MaskApplyType.CPUSet)
                        result = ApplyCpuSet(mask);
                    else if (_previousMaskType == MaskApplyType.Affinity)
                        result = ApplyAffinity(mask);
                    else if (_previousMaskType == MaskApplyType.NoMask)
                        result = true;
                    else
                        throw new NotImplementedException();
                    break;

                case MaskApplyType.CPUSet:
                    if (_previousMaskType == MaskApplyType.Affinity)
                        ApplyAffinity(LogicalProcessorMask.NoMask); // Clear the previous Affinity if the MaskType has changed
                    result = ApplyCpuSet(mask);
                    break;

                case MaskApplyType.Affinity:
                    if (_previousMaskType == MaskApplyType.CPUSet)
                        ApplyCpuSet(LogicalProcessorMask.NoMask); // Clear the previous CPU Set if the MaskType has changed
                    result = ApplyAffinity(mask);
                    break;

                default:
                    throw new NotImplementedException();
            }

            _previousMaskType = mask.MaskType;
            return result;
        }

        public bool ReapplyMask(LogicalProcessorMask mask)
        {
            if (_previousMaskType != mask.MaskType)
                return true;

            bool[] actualMask;
            try
            {
                actualMask = mask.MaskType switch
                {
                    MaskApplyType.CPUSet => GetCpuSetMask(),
                    MaskApplyType.Affinity => GetAffinityMask(),
                    _ => throw new NotImplementedException(),
                };
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (Win32Exception)
            {
                return false;
            }

            if (actualMask.SequenceEqual(mask.BoolMask))
                return true;

            return mask.MaskType switch
            {
                MaskApplyType.CPUSet => ApplyCpuSet(mask),
                MaskApplyType.Affinity => ApplyAffinity(mask),
                _ => throw new NotImplementedException(),
            };
        }

        /// <summary>
        /// Apply a given mask as a CPU Set
        /// </summary>
        private bool ApplyCpuSet(LogicalProcessorMask mask)
        {
            int error;
            string extraHelpString;

            if (!AquireSetLimitedInfoHandle(out SafeProcessHandle setLimitedInfoHandle))
                return false;

            bool success;
            if (mask.MaskType == MaskApplyType.NoMask)
            {
                // Clear the CPU Set
                success = NativeMethods.SetProcessDefaultCpuSets(setLimitedInfoHandle, null, 0);
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
                        cpuSetIds.Add(_logicalProcessorToSetId[i]);
                }
                catch (KeyNotFoundException)
                {
                    WindowLogger.Write($"WARNING: Unable to include '{CpuInfo.LogicalProcessorNames[i]}' in Core Mask. It does not have a CPU Set ID");
                }
            }
            uint[] cpuSetIdsArray = cpuSetIds.ToArray();
            success = NativeMethods.SetProcessDefaultCpuSets(setLimitedInfoHandle, cpuSetIdsArray, (uint)cpuSetIdsArray.Length);
            if (success)
            {
                WindowLogger.Write($"Applied CPU Set '{mask.Name}' to '{_executableName}'");
                return true;
            }

            error = Marshal.GetLastWin32Error();
            extraHelpString = (error == 5 && !Environment.IsPrivilegedProcess) ? " Try restarting CPU Set Setter as Admin" : " Likely due to anti-cheat";
            WindowLogger.Write($"ERROR: Could not apply CPU Set to '{_executableName}': {new Win32Exception(error).Message}{extraHelpString}");
            return false;
        }

        private bool[] GetCpuSetMask()
        {
            if (_queryLimitedInfoHandle.IsInvalid)
            {
                throw new InvalidOperationException("Cannot get process CPU Sets due to invalid handle");
            }

            uint requiredIdCount = 0;
            if (!NativeMethods.GetProcessDefaultCpuSets(_queryLimitedInfoHandle, null, 0, ref requiredIdCount))
            {
                int error = Marshal.GetLastWin32Error();
                if (error != 0x7A) // ERROR_INSUFFICIENT_BUFFER
                    throw new Win32Exception(error);
            }

            uint[] cpuSetIds = new uint[requiredIdCount];
            if (!NativeMethods.GetProcessDefaultCpuSets(_queryLimitedInfoHandle, cpuSetIds, (uint)cpuSetIds.Length, ref requiredIdCount))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            bool[] result = new bool[CpuInfo.LogicalProcessorCount];
            foreach (uint cpuSetId in cpuSetIds)
            {
                int logicalProcessor = _setIdToLogicalProcessor[cpuSetId];
                result[logicalProcessor] = true;
            }
            return result;
        }

        private bool AquireSetLimitedInfoHandle(out SafeProcessHandle setLimitedInfoHandle)
        {
            if (_setLimitedInfoHandle is null)
            {
                _setLimitedInfoHandle = NativeMethods.OpenProcess(ProcessAccessFlags.PROCESS_SET_LIMITED_INFORMATION, false, _pid);
                if (_setLimitedInfoHandle.IsInvalid)
                {
                    int error = Marshal.GetLastWin32Error();
                    string extraHelpString = (error == 5 && !Environment.IsPrivilegedProcess) ? " Try restarting CPU Set Setter as Admin" : "";
                    WindowLogger.Write($"ERROR: Could not open process '{_executableName}': {new Win32Exception(error).Message}{extraHelpString}");
                }
            }
            setLimitedInfoHandle = _setLimitedInfoHandle;
            return !setLimitedInfoHandle.IsInvalid;
        }

        private bool ApplyAffinity(LogicalProcessorMask mask)
        {
            int error;
            string extraHelpString;

            if (!AquireSetInfoHandle(out SafeProcessHandle setInfoHandle))
                return false;

            bool success;
            if (mask.MaskType == MaskApplyType.NoMask)
            {
                UIntPtr allMask = 0;
                for (int i = 0; i < CpuInfo.LogicalProcessorCount; ++i)
                {
                    allMask |= (UIntPtr)1 << i;
                }
                success = NativeMethods.SetProcessAffinityMask(setInfoHandle, allMask);
                if (success)
                {
                    WindowLogger.Write($"Cleared Affinity of '{_executableName}'");
                    return true;
                }

                error = Marshal.GetLastWin32Error();
                WindowLogger.Write($"ERROR: Could not clear Affinity of '{_executableName}': {new Win32Exception(error).Message}");
                return false;
            }

            UIntPtr bitMask = 0;
            for (int i = 0; i < mask.BoolMask.Count; ++i)
            {
                if (mask.BoolMask[i])
                    bitMask |= (UIntPtr)1 << i;
            }

            success = NativeMethods.SetProcessAffinityMask(setInfoHandle, bitMask);
            if (success)
            {
                WindowLogger.Write($"Applied Affinity '{mask.Name}' to '{_executableName}'");
                return true;
            }

            error = Marshal.GetLastWin32Error();
            extraHelpString = (error == 5 && !Environment.IsPrivilegedProcess) ? " Try restarting CPU Set Setter as Admin" : " Likely due to anti-cheat";
            WindowLogger.Write($"ERROR: Could not apply Affinity to '{_executableName}': {new Win32Exception(error).Message}{extraHelpString}");
            return false;
        }

        private bool[] GetAffinityMask()
        {
            if (_queryLimitedInfoHandle.IsInvalid)
            {
                throw new InvalidOperationException("Cannot get process Affinity due to invalid handle");
            }

            UIntPtr bitMaskProcess = 0;
            UIntPtr bitMaskSystem = 0;
            if (!NativeMethods.GetProcessAffinityMask(_queryLimitedInfoHandle, ref bitMaskProcess, ref bitMaskSystem))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            if (bitMaskProcess == 0)
            {
                throw new InvalidOperationException("GetProcessAffinityMask returned a mask of 0");
            }

            bool[] result = new bool[CpuInfo.LogicalProcessorCount];
            for (int i = 0; i < result.Length; ++i)
            {
                if ((bitMaskProcess & ((UIntPtr)1 << i)) != 0)
                    result[i] = true;
            }
            return result;
        }

        private bool AquireSetInfoHandle(out SafeProcessHandle setInfoHandle)
        {
            if (_setInfoHandle is null)
            {
                _setInfoHandle = NativeMethods.OpenProcess(ProcessAccessFlags.PROCESS_SET_INFORMATION, false, _pid);
                if (_setInfoHandle.IsInvalid)
                {
                    int error = Marshal.GetLastWin32Error();
                    string extraHelpString = (error == 5 && !Environment.IsPrivilegedProcess) ? " Try restarting CPU Set Setter as Admin" : "";
                    WindowLogger.Write($"ERROR: Could not open process '{_executableName}': {new Win32Exception(error).Message}{extraHelpString}");
                }
            }
            setInfoHandle = _setInfoHandle;
            return !setInfoHandle.IsInvalid;
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
                    throw new Win32Exception(error);
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
                        throw new InvalidCastException("Invalid data type encountered; aborting");
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
            _setInfoHandle?.Dispose();
        }

    }
}
