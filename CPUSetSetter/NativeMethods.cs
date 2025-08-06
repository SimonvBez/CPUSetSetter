using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;


namespace CPUSetLib
{
    [Flags]
    public enum ProcessAccessFlags : uint
    {
        PROCESS_QUERY_LIMITED_INFORMATION = 0x00001000,
        PROCESS_SET_LIMITED_INFORMATION = 0x00002000
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GROUP_AFFINITY
    {
        public ulong Mask;
        public ushort Group;
        public ushort Reserved1;
        public ushort Reserved2;
        public ushort Reserved3;
    }

    internal static partial class NativeMethods
    {
        [LibraryImport("kernel32.dll", SetLastError = true)]
        public static partial SafeProcessHandle OpenProcess(ProcessAccessFlags access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetProcessDefaultCpuSetMasks(SafeProcessHandle hProcess, GROUP_AFFINITY[]? cpuSetMasks, uint cpuSetMaskCount);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetProcessDefaultCpuSetMasks(SafeProcessHandle hProcess, [Out] GROUP_AFFINITY[]? cpuSetMasks, uint cpuSetMaskCount, out uint requiredMaskCount);
    }
}
