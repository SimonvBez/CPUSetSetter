using CommunityToolkit.Mvvm.ComponentModel;
using CPUSetSetter.Core;
using CPUSetSetter.UI.Tabs.Processes;


namespace CPUSetSetter.Config.Models
{
    /// <summary>
    /// A rule indicating which program paths should be affected by a certain LogicalProcessorMask
    /// </summary>
    public partial class ProgramRule : ObservableConfigObject
    {
        private readonly HashSet<ProcessListEntryViewModel> runningRuleProcesses = [];
        private bool _isSettingMask = false;
        private bool _isRemoved = false;

        public string ProgramPath { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDeviatingFromRuleTemplate))]
        private LogicalProcessorMask _mask;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDeviatingFromRuleTemplate))]
        private RuleTemplate? _matchingRuleTemplate;

        public bool HasRunningProcesses => runningRuleProcesses.Count >= 1;
        public bool IsDeviatingFromRuleTemplate => MatchingRuleTemplate is not null && MatchingRuleTemplate.Mask != Mask;

        public ProgramRule(string programPath, LogicalProcessorMask mask)
        {
            ProgramPath = programPath;
            _mask = mask;
            AddAllRunningProcesses();
        }

        protected override void MarkConfigSaveExcludeProperties(ICollection<string> ignoredProperties)
        {
            base.MarkConfigSaveExcludeProperties(ignoredProperties);
            ignoredProperties.Add(nameof(MatchingRuleTemplate));
            ignoredProperties.Add(nameof(HasRunningProcesses));
            ignoredProperties.Add(nameof(IsDeviatingFromRuleTemplate));
        }

        public void AddRunningProcess(ProcessListEntryViewModel process)
        {
            runningRuleProcesses.Add(process);
            OnPropertyChanged(nameof(HasRunningProcesses));
        }

        /// <summary>
        /// Add all processes that are currently running that match the ProgramRule to its Set
        /// </summary>
        private void AddAllRunningProcesses()
        {
            foreach (ProcessListEntryViewModel process in ProcessesTabViewModel.RunningProcesses)
            {
                if (RuleHelpers.PathsEqual(ProgramPath, process.ImagePath))
                    runningRuleProcesses.Add(process);
            }
            OnPropertyChanged(nameof(HasRunningProcesses));
        }

        public void RemoveRunningProcess(ProcessListEntryViewModel process)
        {
            if (!runningRuleProcesses.Remove(process))
                WindowLogger.Write($"WARNING: {process.Name} could not be removed from ProgramRule as it did not exist");
            OnPropertyChanged(nameof(HasRunningProcesses));
        }

        public bool SetMask(LogicalProcessorMask newMask, bool shouldRemoveWhenNoMask)
        {
            if (_isSettingMask)
                return true; // SetMask was called recursively, probably by OnMaskChanged. Ignore it

            _isSettingMask = true;
            bool allSuccess = true;
            try
            {
                Mask = newMask;
                // Apply the new Mask to every process of this Program Rule
                foreach (ProcessListEntryViewModel process in runningRuleProcesses)
                {
                    if (process.SetMask(newMask, false))
                        allSuccess = false;
                }

                // When requested, this is a NoMask and there is no matching RuleTemplate, remove this ProgramRule
                if (shouldRemoveWhenNoMask && newMask.IsNoMask && MatchingRuleTemplate is null)
                {
                    Remove(false);
                }
            }
            finally
            {
                _isSettingMask = false;
            }
            return allSuccess;
        }

        /// <summary>
        /// Try to remove itself. Removing is not allowed when there is both a matching RuleTemplate and at least one process of this Rule.
        /// When removing is not allowed, set the Rule processes to the mask of the RuleTemplate.
        /// When removing is allowed, set the Rule processes to NoMask and remove this Rule.
        /// </summary>
        public bool TryRemove()
        {
            if (MatchingRuleTemplate is not null && runningRuleProcesses.Count >= 1)
            {
                // Removing this ProgramRule is not allowed. Set it to the RuleTemplate's mask instead
                SetMask(MatchingRuleTemplate.Mask, false);
                return false;
            }

            // Removing is allowed
            Remove(true);
            return true;
        }

        /// <summary>
        /// Set every process to NoMask before removing itself
        /// </summary>
        private void Remove(bool setToNoMask)
        {
            if (setToNoMask)
            {
                foreach (ProcessListEntryViewModel process in runningRuleProcesses)
                {
                    process.SetMask(LogicalProcessorMask.NoMask, false);
                }
            }
            _isRemoved = true;
            AppConfig.Instance.ProgramRules.Remove(this); // Remove (and so also Dispose) itself
        }

        partial void OnMaskChanged(LogicalProcessorMask value)
        {
            SetMask(value, false);
        }

        partial void OnMatchingRuleTemplateChanged(RuleTemplate? oldValue, RuleTemplate? newValue)
        {
            if (oldValue is not null)
            {
                oldValue.MaskChanged -= OnMatchingRuleTemplateMaskChanged;
                oldValue.MaskReapplied -= OnMatchingRuleTemplateReapplied;
            }

            if (newValue is not null)
            {
                newValue.MaskChanged += OnMatchingRuleTemplateMaskChanged;
                newValue.MaskReapplied += OnMatchingRuleTemplateReapplied;
            }
        }

        private void OnMatchingRuleTemplateMaskChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(IsDeviatingFromRuleTemplate));
        }

        private void OnMatchingRuleTemplateReapplied(object? sender, EventArgs e)
        {
            if (MatchingRuleTemplate is not null)
                Mask = MatchingRuleTemplate.Mask;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!_isRemoved)
                    throw new InvalidOperationException("ProgramRule is only allowed to be Disposed by TryRemove()!");

                runningRuleProcesses.Clear();
                if (MatchingRuleTemplate is not null)
                {
                    MatchingRuleTemplate.MaskChanged -= OnMatchingRuleTemplateMaskChanged;
                    MatchingRuleTemplate.MaskReapplied -= OnMatchingRuleTemplateReapplied;
                }
            }
            base.Dispose(disposing);
        }
    }
}
