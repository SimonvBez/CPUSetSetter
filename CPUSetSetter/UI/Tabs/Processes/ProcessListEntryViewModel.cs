using CommunityToolkit.Mvvm.ComponentModel;
using CPUSetSetter.Config.Models;
using CPUSetSetter.Core;
using CPUSetSetter.Platforms;
using System.Collections.Specialized;


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

            ProgramRule? programRule = RuleHelpers.GetProgramRuleOrNull(pInfo.ImagePath);
            programRule?.AddRunningProcess(this);

            _mask = programRule?.Mask ?? LogicalProcessorMask.NoMask;

            AverageCpuUsage = _processHandler.GetAverageCpuUsage();
        }

        public void UpdateCpuUsage()
        {
            AverageCpuUsage = _processHandler.GetAverageCpuUsage();
        }

        public bool SetMask(LogicalProcessorMask newMask, bool updateRule)
        {
            if (newMask == _lastAppliedMask) // Return the previous status if the mask is still the same
                return !FailedToOpen;

            _lastAppliedMask = newMask;
            Mask = newMask;

            bool ruleSuccess = true;
            if (updateRule)
            {
                // SetMask was called from the Processes tab UI, so the ProgramRule needs to be updated or created too
                ProgramRule? programRule = RuleHelpers.GetProgramRuleOrNull(ImagePath);
                if (programRule is null)
                {
                    programRule = new(ImagePath, newMask);
                    AppConfig.Instance.ProgramRules.Add(programRule);
                }
                ruleSuccess = programRule.SetMask(newMask, true);
            }
            bool success = _processHandler.ApplyMask(newMask);
            if (!success)
                FailedToOpen = true;
            return success && ruleSuccess;
        }

        ///// <summary>
        ///// Store the new mask in the config (which in turn may also set the mask of other processes with the same path), and apply it to the process
        ///// </summary>
        ///// <param name="shouldUpdateRules">Normally true. Only false if this mask is being applied BY a rule already to prevent recursion</param>
        ///// <returns>true if the mask was successfully applied to all processes of this ImagePath, false if not</returns>
        //public bool SetMask(LogicalProcessorMask mask, bool shouldUpdateRules)
        //{
        //    if (mask == _lastAppliedMask) // Return the previous status if the mask is still the same
        //        return !FailedToOpen;

        //    _lastAppliedMask = mask;
        //    Mask = mask;

        //    bool ruleSuccess;
        //    if (shouldUpdateRules)
        //    {
        //        // Save the new mask to the config, which also applies it to all other processes of the same path
        //        ruleSuccess = MaskRuleManager.UpdateOrAddProgramRule(ImagePath, mask, true);
        //    }
        //    else
        //    {
        //        ruleSuccess = true;
        //    }

        //    // Apply the mask to the actual process
        //    bool success = _processHandler.ApplyMask(mask);
        //    if (!success)
        //        FailedToOpen = true;
        //    return success && ruleSuccess;
        //}

        /// <summary>
        /// The UI picked a different mask
        /// </summary>
        partial void OnMaskChanged(LogicalProcessorMask? oldValue, LogicalProcessorMask newValue)
        {
            if (oldValue is not null)
                oldValue.BoolMask.CollectionChanged -= OnMaskBitsChanged;
            newValue.BoolMask.CollectionChanged += OnMaskBitsChanged;
            SetMask(newValue, true);
        }

        private void OnMaskBitsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Replace)
                throw new ArgumentException("Only Replace actions are allowed for mask bits");

            // One of the logical processors in the mask has changed, apply it
            _processHandler.ApplyMask(Mask);
        }

        /// <summary>
        /// The process has exited
        /// </summary>
        public void Dispose()
        {
            RuleHelpers.GetProgramRuleOrNull(ImagePath)?.RemoveRunningProcess(this);
            Mask.BoolMask.CollectionChanged -= OnMaskBitsChanged;
            _processHandler.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
