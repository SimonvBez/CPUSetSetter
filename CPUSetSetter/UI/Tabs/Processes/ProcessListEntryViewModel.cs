using CommunityToolkit.Mvvm.ComponentModel;
using CPUSetSetter.Config.Models;
using CPUSetSetter.Core;
using CPUSetSetter.Platforms;


namespace CPUSetSetter.UI.Tabs.Processes
{
    /// <summary>
    /// Represents a row in the Processes list
    /// </summary>
    public partial class ProcessListEntryViewModel : ObservableObject, IDisposable
    {
        private readonly IProcessHandler _processHandler;
        private LogicalProcessorMask _lastAppliedMask = LogicalProcessorMask.NoMask;

        public uint Pid { get; }
        public string Name { get; }
        public string ImagePath { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AverageCpuPercentageStr))]
        private double _averageCpuUsage;

        [ObservableProperty]
        private LogicalProcessorMask _mask;

        [ObservableProperty]
        private bool _failedToOpen = false;

        public string AverageCpuPercentageStr => AverageCpuUsage == -1 ? "" : $"{AverageCpuUsage * 100:F1}%";

        public ProcessListEntryViewModel(ProcessInfo pInfo)
        {
            Pid = pInfo.PID;
            Name = pInfo.Name;
            ImagePath = pInfo.ImagePath;
            _processHandler = pInfo.ProcessHandler;
            Mask = MaskRuleManager.GetMaskFromPath(pInfo.ImagePath);

            AverageCpuUsage = _processHandler.GetAverageCpuUsage();
        }

        public void UpdateCpuUsage()
        {
            AverageCpuUsage = _processHandler.GetAverageCpuUsage();
        }

        /// <summary>
        /// Store the new mask in the config (which in turn may also set the mask of other processes with the same path), and apply it to the process
        /// </summary>
        /// <returns>true if the mask was successfully applied to the process, false if not</returns>
        public bool SetMask(LogicalProcessorMask mask)
        {
            if (mask == _lastAppliedMask) // Return the previous status if the mask is still the same
                return !FailedToOpen;

            _lastAppliedMask = mask;
            Mask = mask;
            // Save the new mask to the config, which also applies it to all other processes of the same path
            MaskRuleManager.UpdateOrAddProgramRule(ImagePath, mask);

            // Apply the mask to the actual process
            bool success = _processHandler.ApplyMask(mask);
            if (!success)
                FailedToOpen = true;
            return success;
        }

        /// <summary>
        /// The UI changed the mask
        /// </summary>
        partial void OnMaskChanged(LogicalProcessorMask value)
        {
            SetMask(value);
        }

        public void Dispose()
        {
            _processHandler.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
