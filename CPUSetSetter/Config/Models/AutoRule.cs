using CommunityToolkit.Mvvm.ComponentModel;
using CPUSetSetter.Core;


namespace CPUSetSetter.Config.Models
{
    /// <summary>
    /// A rule indicating which programs should automatically have a program rule created based on a glob pattern, with a given LogicalProcessorMask
    /// </summary>
    public partial class AutoRule : ObservableConfigObject
    {
        [ObservableProperty]
        private string _ruleGlob;

        [ObservableProperty]
        private LogicalProcessorMask _logicalProcessorMask;

        public AutoRule(string ruleGlob, LogicalProcessorMask logicalProcessorMask)
        {
            _ruleGlob = ruleGlob;
            _logicalProcessorMask = logicalProcessorMask;
        }

        partial void OnRuleGlobChanged(string value)
        {
            // Apply the auto rule to the currently running processes
            MaskRuleManager.OnAutoRulesChanged();
        }

        partial void OnLogicalProcessorMaskChanged(LogicalProcessorMask value)
        {
            // Apply the auto rule to the currently running processes
            MaskRuleManager.OnAutoRulesChanged();
        }
    }
}
