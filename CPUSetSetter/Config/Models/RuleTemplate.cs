using CommunityToolkit.Mvvm.ComponentModel;
using CPUSetSetter.Core;
using CPUSetSetter.UI.Tabs.Processes;


namespace CPUSetSetter.Config.Models
{
    /// <summary>
    /// A rule indicating which programs should automatically have a program rule created based on a glob pattern, with a given LogicalProcessorMask
    /// </summary>
    public partial class RuleTemplate : ObservableConfigObject
    {
        private static bool _isBulkChanging = false;

        [ObservableProperty]
        private string _ruleGlob;

        [ObservableProperty]
        private LogicalProcessorMask _Mask;

        public event EventHandler<EventArgs>? MaskChanged;
        public event EventHandler<EventArgs>? MaskReapplied;

        public RuleTemplate(string ruleGlob, LogicalProcessorMask mask)
        {
            _ruleGlob = ruleGlob;
            _Mask = mask;
        }

        partial void OnRuleGlobChanged(string value)
        {
            RefreshAllTemplates();
        }

        partial void OnMaskChanged(LogicalProcessorMask value)
        {
            MaskChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Reapply the mask of this Template to all ProgramRules that falls under it
        /// </summary>
        public void Reapply()
        {
            MaskReapplied?.Invoke(this, EventArgs.Empty);
        }

        public static void RemoveAllUsingMask(LogicalProcessorMask maskToBeRemoved)
        {
            _isBulkChanging = true;
            for (int i = AppConfig.Instance.RuleTemplates.Count - 1; i >= 0; --i)
            {
                if (AppConfig.Instance.RuleTemplates[i].Mask == maskToBeRemoved)
                    AppConfig.Instance.RuleTemplates.RemoveAt(i);
            }
            _isBulkChanging = false;
            RefreshAllTemplates();
        }

        public static void OnConfigLoaded()
        {
            // Updates all ProgramRules to have references to the RuleTemplate that matches them
            RefreshAllTemplates();

            AppConfig.Instance.RuleTemplates.CollectionChanged += (_, _) => RefreshAllTemplates();
        }

        private static void RefreshAllTemplates()
        {
            if (_isBulkChanging)
                return; // Don't do anything if there currently is a bulk add/remove going on

            // Iterate over each process, and get/create ProgramRules where they apply
            foreach (ProcessListEntryViewModel process in ProcessesTabViewModel.RunningProcesses)
            {
                ProgramRule? programRule = RuleHelpers.GetProgramRuleOrNull(process.ImagePath);
                LogicalProcessorMask mask = programRule?.Mask ?? LogicalProcessorMask.NoMask;
                process.SetMask(mask, false);
            }

            // Refresh the Templates of the ProgramRules, so they know which Template they fall under
            foreach (ProgramRule programRule in AppConfig.Instance.ProgramRules)
            {
                programRule.MatchingRuleTemplate = RuleHelpers.FindRuleTemplateOrNull(programRule.ProgramPath);
            }
        }
    }
}
