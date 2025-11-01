using CPUSetSetter.Config.Models;
using System.Management;
using System.Runtime.InteropServices;


namespace CPUSetSetter.Platforms
{
    public class CpuInfoWindows : ICpuInfo
    {
        public Manufacturer Manufacturer { get; }

        public IReadOnlyCollection<string> ThreadNames { get; }

        public IReadOnlyCollection<CoreMask> DefaultCoreMasks { get; }

        public bool IsSupported { get; } = true;

        public CpuInfoWindows()
        {
            Manufacturer = GetManufacturer();
            ThreadNames = GetThreadNames();
            DefaultCoreMasks = GetDefaultCoreMasks();
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

        private List<string> GetThreadNames()
        {
            // Get information about the cores, like if they have SMT or are P/E cores
            List<ProcessorRelationship> coreRelations = GetCoreRelationships();
            if (coreRelations[0].Affinities.Count != 1)
            {
                throw new UnsupportedCpu($"Only systems with a single processor group are supported. {coreRelations[0].Affinities.Count} detected.\n" +
                    "(Do you have more than 64 CPU threads?)");
            }

            bool hasECores = coreRelations.Any(core => core.EfficiencyClass >= 1);

            List<string> threadNames = [];
            int threadNum = 0;
            while (true)
            {
                UIntPtr threadMask = (UIntPtr)1 << threadNum;
                int coreNum = coreRelations.FindIndex(core => (core.Affinities[0] & threadMask) != 0);
                if (coreNum == -1)
                {
                    break;
                }

                string prefix = string.Empty;
                if (Manufacturer == Manufacturer.Intel && hasECores)
                {
                    prefix = coreRelations[coreNum].EfficiencyClass >= 1 ? "P-" : "E-";
                }

                string suffix = string.Empty;
                if (coreRelations[coreNum].IsSMT)
                {
                    // Count what thread number this is on this core
                    int threadIndexOnCore = 0;
                    for (int i = 0; i < threadNum; ++i)
                    {
                        UIntPtr checkMask = (UIntPtr)1 << i;
                        threadIndexOnCore += (coreRelations[coreNum].Affinities[0] & checkMask) > 0 ? 1 : 0;
                    }
                    suffix = $" T{threadIndexOnCore}";
                }

                threadNames.Add($"{prefix}Core {coreNum}{suffix}");
                threadNum++;
            }

            return threadNames;
        }

        private static List<CoreMask> GetDefaultCoreMasks()
        {
            List<ProcessorRelationship> dieRelations = GetDieRelationships();
            List<CacheRelationship> cacheRelations = GetCacheRelationships();

            return [];
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
