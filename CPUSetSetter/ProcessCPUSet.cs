using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;


namespace CPUSetSetter
{
    public partial class ProcessCPUSet : ObservableObject
    {
        public string Name { get; init; }

        public string Path { get; init; }

        [ObservableProperty]
        private string _cpuSetName;

        [JsonConstructor]
        private ProcessCPUSet()
        {
            Name = "";
            Path = "";
            _cpuSetName = "";
        }

        public ProcessCPUSet(string name, string path, string cpuSetName)
        {
            Name = name;
            Path = path;
            _cpuSetName = cpuSetName;
        }

        partial void OnCpuSetNameChanged(string value)
        {
            ApplyCpuSet();
        }

        /// <summary>
        /// Apply the CPU set of this process definition to every running process, if there are any
        /// </summary>
        public void ApplyCpuSet()
        {
            if (!MainWindowViewModel.IsRunning)
                return;

            try
            {
                CPUSet newCpuSet = ConfigOld.Default.GetCpuSetByName(CpuSetName) ?? throw new NullReferenceException();
                bool matchPath = ConfigOld.Default.MatchWholePath;

                foreach (ProcessListEntry pEntry in MainWindowViewModel.RunningProcesses)
                {
                    if (Name.Equals(pEntry.Name, StringComparison.OrdinalIgnoreCase) &&
                        (!matchPath || Path.Equals(pEntry.Path, StringComparison.OrdinalIgnoreCase)))
                    {
                        pEntry.CpuSet = newCpuSet;
                    }
                }
            }
            catch (NullReferenceException)
            {
                WindowLogger.Write($"Unable to apply CPU Set '{CpuSetName}', as it does not exist");
            }
        }
    }
}
