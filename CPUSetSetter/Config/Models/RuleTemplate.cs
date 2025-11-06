using CommunityToolkit.Mvvm.ComponentModel;
using CPUSetSetter.Core;


namespace CPUSetSetter.Config.Models
{
    /// <summary>
    /// A rule indicating which programs should automatically have a program rule created based on a glob pattern, with a given LogicalProcessorMask
    /// </summary>
    public partial class RuleTemplate : ObservableConfigObject
    {
        [ObservableProperty]
        private string _ruleGlob;

        [ObservableProperty]
        private LogicalProcessorMask _Mask;

        public event EventHandler<EventArgs>? MaskChanged;

        public RuleTemplate(string ruleGlob, LogicalProcessorMask mask)
        {
            _ruleGlob = ruleGlob;
            _Mask = mask;
        }

        partial void OnRuleGlobChanged(string value)
        {
            MaskRuleManager.OnRuleTemplateChanged();
        }

        partial void OnMaskChanged(LogicalProcessorMask value)
        {
            MaskChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
