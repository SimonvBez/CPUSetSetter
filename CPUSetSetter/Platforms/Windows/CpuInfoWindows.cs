using CPUSetSetter.Config.Models;
using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;


namespace CPUSetSetter.Platforms
{
    public class CpuInfoWindows : ICpuInfo
    {
        public Manufacturer Manufacturer { get; }

        public IReadOnlyCollection<string> LogicalProcessorNames { get; }

        // Default masks are loaded on demand, so that they are only created when needed
        public IReadOnlyCollection<LogicalProcessorMask> DefaultLogicalProcessorMasks => GetDefaultLogicalProcessorMasks();

        // If the CPU is not supported, an error will raise during construction. So if this property can be retrieved, support is already guaranteed
        public bool IsSupported { get; } = true;

        private List<ProcessorRelationship> _coreRelations = [];
        private bool _hasECores = false;

        public CpuInfoWindows()
        {
            Manufacturer = GetManufacturer();
            LogicalProcessorNames = GetLogicalProcessorNames();
        }

        private static Manufacturer GetManufacturer()
        {
            using ManagementObjectSearcher searcher = new("root\\CIMV2", "SELECT * FROM Win32_Processor");
            var cpus = searcher.Get().Cast<ManagementBaseObject>().ToList();

            if (cpus.Count != 1)
                throw new UnsupportedCpu($"Only single CPU systems are supported. {cpus.Count} detected.");

            string name = cpus[0]["Name"] as string ?? string.Empty;
            string manufacturer = cpus[0]["Manufacturer"] as string ?? string.Empty;

            // Get the CPU manufacturer
            if (name.Contains("AMD", StringComparison.OrdinalIgnoreCase) || manufacturer.Contains("AMD", StringComparison.OrdinalIgnoreCase))
                return Manufacturer.AMD;
            else if (name.Contains("Intel", StringComparison.OrdinalIgnoreCase) || manufacturer.Contains("Intel", StringComparison.OrdinalIgnoreCase))
                return Manufacturer.Intel;
            else
                return Manufacturer.Other;
        }

        private List<string> GetLogicalProcessorNames()
        {
            // Get information about the cores, like if they have SMT or are P/E cores
            _coreRelations = GetCoreRelationships(); // Store the core relations so they can be reused by GetDefaultCoreMasks()
            if (_coreRelations[0].Affinities.Count != 1)
            {
                throw new UnsupportedCpu($"Only systems with a single processor group are supported. {_coreRelations[0].Affinities.Count} detected.\n" +
                    "(Do you have more than 64 CPU threads/logical cores?)");
            }

            _hasECores = _coreRelations.Any(core => core.EfficiencyClass >= 1);

            List<string> logicalProcessorNames = [];
            int logicalProcessorsNum = 0;
            // Count up the logicalProcessorsNum, find what core it belongs to and if there should be any prefix/suffix to the name
            while (true)
            {
                UIntPtr logicalProcessorMask = (UIntPtr)1 << logicalProcessorsNum;
                int coreNum = _coreRelations.FindIndex(core => (core.Affinities[0] & logicalProcessorMask) != 0);
                if (coreNum == -1)
                {
                    break;
                }

                // Prefix with P- or E- on Intel
                string prefix = string.Empty;
                if (Manufacturer == Manufacturer.Intel && _hasECores)
                {
                    prefix = _coreRelations[coreNum].EfficiencyClass >= 1 ? "P-" : "E-";
                }

                // Suffix with T0 or T1 if it is an SMT/HT core
                string suffix = string.Empty;
                if (_coreRelations[coreNum].IsSMT)
                {
                    // Count what logical processor number this is on this core
                    int logicalProcessorIndexOnCore = 0;
                    for (int i = 0; i < logicalProcessorsNum; ++i)
                    {
                        UIntPtr checkMask = (UIntPtr)1 << i;
                        logicalProcessorIndexOnCore += (_coreRelations[coreNum].Affinities[0] & checkMask) > 0 ? 1 : 0;
                    }
                    suffix = $" T{logicalProcessorIndexOnCore}";
                }

                logicalProcessorNames.Add($"{prefix}Core {coreNum}{suffix}");
                logicalProcessorsNum++;
            }

            return logicalProcessorNames;
        }

        private List<LogicalProcessorMask> GetDefaultLogicalProcessorMasks()
        {
            List<LogicalProcessorMask> result = [];

            // Add a P-Cores and E-Cores default core mask for supported Intel CPUs
            if (Manufacturer == Manufacturer.Intel && _hasECores)
            {
                UIntPtr pMask = 0;
                UIntPtr eMask = 0;
                foreach (ProcessorRelationship core in _coreRelations)
                {
                    if (core.EfficiencyClass >= 1)
                        pMask |= core.Affinities[0];
                    else
                        eMask |= core.Affinities[0];
                }
                result.Add(new("P-Cores", BitMaskToBoolMask(pMask, LogicalProcessorNames.Count), []));
                result.Add(new("E-Cores", BitMaskToBoolMask(eMask, LogicalProcessorNames.Count), []));
            }

            // Add CCD default core masks for multi-CCD CPUs
            List<ProcessorRelationship> dieRelations = GetDieRelationships();
            if (dieRelations.Count >= 2)
            {
                List<CacheRelationship> cacheRelations = GetCacheRelationships();
                List<CacheRelationship> l3Caches = cacheRelations.Where(cache => cache.Level == 3).ToList();

                // Get the L3 cache size for each Die
                List<long> cachePerDie = dieRelations.Select(die =>
                    l3Caches.Where(cache => cache.Affinities[0] == die.Affinities[0])
                    .Sum(cache => cache.CacheSize)
                ).ToList();

                long minDieCacheSize = cachePerDie.Min();

                // Split the Cache from the Freq dies if there are any
                List<ProcessorRelationship> cacheDies = dieRelations.Zip(cachePerDie, (die, cacheSize) => (die, cacheSize))
                    .Where(item => item.cacheSize > minDieCacheSize * 2)
                    .Select(item => item.die).ToList();

                // If there are no dies with extra cache, every die will be in the freqDies list
                List<ProcessorRelationship> freqDies = dieRelations.Where(die => !cacheDies.Contains(die)).ToList();

                // Add the Cache dies, if there are any
                for (int i = 0; i < cacheDies.Count; ++i)
                {
                    string maskName = "Cache";
                    if (cacheDies.Count >= 2)
                        maskName += i.ToString(CultureInfo.InvariantCulture);
                    result.Add(new(maskName, BitMaskToBoolMask(cacheDies[i].Affinities[0], LogicalProcessorNames.Count), []));
                }

                // Add the remaining dies, calling them Freq if there were any Cache dies
                for (int i = 0; i < freqDies.Count; ++i)
                {
                    string maskName = cacheDies.Count >= 1 ? "Freq" : "CCD";
                    if (freqDies.Count >= 2)
                        maskName += i.ToString(CultureInfo.InvariantCulture);
                    result.Add(new(maskName, BitMaskToBoolMask(freqDies[i].Affinities[0], LogicalProcessorNames.Count), []));
                }
            }

            return result;
        }

        private static List<bool> BitMaskToBoolMask(UIntPtr mask, int maskLength)
        {
            if (maskLength > UIntPtr.Size * 8)
                throw new ArgumentException($"maskLength cannot be greater than {UIntPtr.Size * 8} (the bit size of UIntPtr)");

            return Enumerable.Range(0, maskLength).Select(i => (mask & ((UIntPtr)1 << i)) != 0).ToList();
        }

        private static List<ProcessorRelationship> GetCoreRelationships()
        {
            List<object> cores = GetProcessorInfo(LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore);
            return cores.Cast<ProcessorRelationship>().ToList();
        }

        private static List<ProcessorRelationship> GetDieRelationships()
        {
            List<object> dies = GetProcessorInfo(LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorDie);
            return dies.Cast<ProcessorRelationship>().ToList();
        }

        private static List<CacheRelationship> GetCacheRelationships()
        {
            List<object> caches = GetProcessorInfo(LOGICAL_PROCESSOR_RELATIONSHIP.RelationCache);
            return caches.Cast<CacheRelationship>().ToList();
        }

        /// <summary>
        /// Allocate memory and get ready to call GetLogicalProcessorInformationEx
        /// </summary>
        private static List<object> GetProcessorInfo(LOGICAL_PROCESSOR_RELATIONSHIP relationship)
        {
            // Get the required buffer length to receive the processor information
            uint length = 0;
            NativeMethods.GetLogicalProcessorInformationEx(relationship, IntPtr.Zero, ref length);

            // Create the buffer and get the processor information
            IntPtr buffer = Marshal.AllocHGlobal((int)length);
            try
            {
                return ReadProcessorInfoBuffer(relationship, buffer, length);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        /// <summary>
        /// Call GetLogicalProcessorInformationEx and parse the modified memory buffer into the requested type
        /// </summary>
        private static List<object> ReadProcessorInfoBuffer(LOGICAL_PROCESSOR_RELATIONSHIP relationship, IntPtr buffer, uint bufferLength)
        {
            if (!NativeMethods.GetLogicalProcessorInformationEx(relationship, buffer, ref bufferLength))
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }

            List<object> result = [];

            IntPtr current = buffer;
            long remaining = bufferLength;
            int headerSize = Marshal.SizeOf<SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX_Header>();
            int processRelationSize = Marshal.SizeOf<PROCESSOR_RELATIONSHIP>();
            // Iterate over the modified buffer
            while (remaining >= headerSize)
            {
                SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX_Header header = Marshal.PtrToStructure<SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX_Header>(current);
                // Make sure that the sizes and received relationship are all as expected
                if (header.Size < headerSize || header.Size > remaining)
                {
                    throw new InvalidCastException("Invalid block size encountered; aborting");
                }
                if (header.Relationship != relationship || header.Relationship == LOGICAL_PROCESSOR_RELATIONSHIP.RelationAll)
                {
                    throw new InvalidCastException("Invalid data type encountered; aborting");
                }

                IntPtr payloadPtr = current + headerSize;
                long payloadSize = header.Size - headerSize;
                if (payloadSize < processRelationSize)
                {
                    throw new InvalidCastException("Invalid block size encountered; aborting");
                }

                IntPtr groupMasksPtr;
                List<UIntPtr> affinityMasks;
                switch (header.Relationship)
                {
                    case LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore:
                    case LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorDie:
                        PROCESSOR_RELATIONSHIP pRelation = Marshal.PtrToStructure<PROCESSOR_RELATIONSHIP>(payloadPtr);
                        // Read the group affinity masks right after the PROCESSOR_RELATIONSHIP structure
                        groupMasksPtr = payloadPtr + Marshal.SizeOf<PROCESSOR_RELATIONSHIP>();
                        affinityMasks = ReadGroupMasks(groupMasksPtr, pRelation.GroupCount);
                        result.Add(new ProcessorRelationship(pRelation.Flags == 1, pRelation.EfficiencyClass, affinityMasks));
                        break;

                    case LOGICAL_PROCESSOR_RELATIONSHIP.RelationCache:
                        CACHE_RELATIONSHIP cRelation = Marshal.PtrToStructure<CACHE_RELATIONSHIP>(payloadPtr);
                        // Read the group affinity masks right after the CACHE_RELATIONSHIP structure
                        groupMasksPtr = payloadPtr + Marshal.SizeOf<CACHE_RELATIONSHIP>();
                        affinityMasks = ReadGroupMasks(groupMasksPtr, cRelation.GroupCount);
                        result.Add(new CacheRelationship(cRelation.Level, cRelation.CacheSize, affinityMasks));
                        break;

                    default:
                        throw new NotImplementedException($"Parsing of {header.Relationship} is not implemented");
                }

                current += (IntPtr)header.Size;
                remaining -= header.Size;
            }

            return result;
        }

        /// <summary>
        /// Read the GROUP_AFFINITY GroupMask[GroupCount] array at a given memory address
        /// </summary>
        private static List<UIntPtr> ReadGroupMasks(IntPtr groupMasksPtr, int groupCount)
        {
            List<UIntPtr> affinityMasks = [];
            int affinitySize = Marshal.SizeOf<GROUP_AFFINITY>();
            for (int i = 0; i < groupCount; ++i)
            {
                GROUP_AFFINITY affinity = Marshal.PtrToStructure<GROUP_AFFINITY>(groupMasksPtr + i * affinitySize);
                affinityMasks.Add(affinity.Mask);
            }
            return affinityMasks;
        }


        private class ProcessorRelationship
        {
            /// <summary>
            /// Whether this physical core has more than one logical processor (only set on RelationProcessorCore)
            /// </summary>
            public bool IsSMT { get; }

            /// <summary>
            /// The efficiency class of this core, where a higher number means the core has more performance (only set on RelationProcessorCore)
            /// </summary>
            public int EfficiencyClass { get; }

            /// <summary>
            /// Affinity mask of which logical processor is part of this relationship
            /// </summary>
            public List<UIntPtr> Affinities { get; }

            public ProcessorRelationship(bool isSMT, int efficiencyClass, List<UIntPtr> affinities)
            {
                IsSMT = isSMT;
                EfficiencyClass = efficiencyClass;
                Affinities = affinities;
            }
        }

        private class CacheRelationship
        {
            /// <summary>
            /// The level of the cache
            /// </summary>
            public int Level { get; }

            /// <summary>
            /// The size of the cache
            /// </summary>
            public uint CacheSize { get; }

            /// <summary>
            /// Affinity mask of which logical processors have access to this cache
            /// </summary>
            public List<UIntPtr> Affinities { get; }

            public CacheRelationship(int level, uint cacheSize, List<UIntPtr> affinities)
            {
                Level = level;
                CacheSize = cacheSize;
                Affinities = affinities;
            }
        }
    }
}
