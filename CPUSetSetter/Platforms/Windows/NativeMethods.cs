using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;


namespace CPUSetSetter.Platforms
{
    internal static partial class NativeMethods
    {
        [LibraryImport("kernel32.dll", SetLastError = true)]
        public static partial SafeProcessHandle OpenProcess(ProcessAccessFlags access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetSystemCpuSetInformation(IntPtr Information, uint BufferLength, ref uint ReturnedLength, SafeProcessHandle Process, uint Flags);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetProcessDefaultCpuSets(SafeProcessHandle Process, uint[]? CpuSetIds, uint CpuSetIdCount);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetProcessAffinityMask(SafeProcessHandle hProcess, UIntPtr dwProcessAffinityMask);

        [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool QueryFullProcessImageNameW(SafeProcessHandle hProcess, uint dwFlags, [Out] char[] lpExeName, ref uint lpdwSize);

        [LibraryImport("user32.dll")]
        public static partial IntPtr GetForegroundWindow();

        [LibraryImport("user32.dll")]
        public static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [LibraryImport("user32.dll", SetLastError = true)]
        public static partial IntPtr SetWindowsHookExW(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool UnhookWindowsHookEx(IntPtr hhk);

        [LibraryImport("user32.dll", SetLastError = true)]
        public static partial IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        public static partial IntPtr GetModuleHandleW(string lpModuleName);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetProcessTimes(SafeProcessHandle hProcess, out FILETIME lpCreationTime, out FILETIME lpExitTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

        [LibraryImport("user32.dll")]
        public static partial short GetAsyncKeyState(int vKey);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetLogicalProcessorInformationEx(LOGICAL_PROCESSOR_RELATIONSHIP RelationshipType, IntPtr Buffer, ref uint ReturnedLength);
    }

    [Flags]
    public enum ProcessAccessFlags : uint
    {
        PROCESS_SET_INFORMATION = 0x00000200,
        PROCESS_QUERY_LIMITED_INFORMATION = 0x00001000,
        PROCESS_SET_LIMITED_INFORMATION = 0x00002000
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct GROUP_AFFINITY
    {
        public UIntPtr Mask;
        public ushort Group;
        public fixed ushort Reserved[3];
    }

    [Flags]
    public enum KBDLLHOOKSTRUCTFlags : uint
    {
        LLKHF_EXTENDED = 0x01,
        LLKHF_INJECTED = 0x10,
        LLKHF_ALTDOWN = 0x20,
        LLKHF_UP = 0x80,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public KBDLLHOOKSTRUCTFlags flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct FILETIME
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;

        public readonly ulong ULong => (((ulong)dwHighDateTime) << 32) + dwLowDateTime;
    }

    public enum LOGICAL_PROCESSOR_RELATIONSHIP : int
    {
        RelationProcessorCore = 0,
        RelationNumaNode = 1,
        RelationCache = 2,
        RelationProcessorPackage = 3,
        RelationGroup = 4,
        RelationProcessorDie = 5,
        RelationNumaNodeEx = 6,
        RelationProcessorModule = 7,
        RelationAll = 0xffff // sometimes used as a query value
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX_Header
    {
        public LOGICAL_PROCESSOR_RELATIONSHIP Relationship;
        public uint Size; // size of this block in bytes
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct PROCESSOR_RELATIONSHIP
    {
        public byte Flags;                          // LTP_PC_SMT flag if SMT is enabled
        public byte EfficiencyClass;                // Efficiency class (0–15)
        public fixed byte Reserved[20];             // Reserved[20]
        public ushort GroupCount;                   // Number of entries in GroupMask[]
        // Followed by GROUP_AFFINITY GroupMask[GroupCount] (variable length)
    }

    public enum PROCESSOR_CACHE_TYPE : int
    {
        CacheUnified = 0,
        CacheInstruction = 1,
        CacheData = 2,
        CacheTrace = 3
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct CACHE_RELATIONSHIP
    {
        public byte Level;                          // Cache level (1 = L1, 2 = L2, etc.)
        public byte Associativity;                  // Associativity (0xFF = fully associative)
        public ushort LineSize;                     // Cache line size in bytes
        public uint CacheSize;                      // Total size in bytes
        public PROCESSOR_CACHE_TYPE Type;           // Data / Instruction / Unified
        public fixed byte Reserved[18];             // Reserved[18]
        public ushort GroupCount;                   // Number of entries in GroupMask[]
        // Followed by GROUP_AFFINITY GroupMask[GroupCount] (variable length)
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEM_CPU_SET_INFORMATION
    {
        public uint Size;
        public CPU_SET_INFORMATION_TYPE Type;
        public uint Id;
        public ushort Group;
        public byte LogicalProcessorIndex;
        public byte CoreIndex;
        public byte LastLevelCacheIndex;
        public byte NumaNodeIndex;
        public byte EfficiencyClass;
        public byte AllFlags;
        public uint Reserved; // union with `byte SchedulingClass`
        public ulong AllocationTag;
    }

    public enum CPU_SET_INFORMATION_TYPE : int
    {
        CpuSetInformation = 0
    }
}
