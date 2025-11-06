using CommunityToolkit.Mvvm.ComponentModel;
using CPUSetSetter.Core;


namespace CPUSetSetter.Config.Models
{
    /// <summary>
    /// A rule indicating which program paths should be affected by a certain LogicalProcessorMask
    /// </summary>
    public partial class ProgramRule : ObservableConfigObject
    {
        public string ProgramPath { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDeviatingFromRuleTemplate))]
        private LogicalProcessorMask _mask;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDeviatingFromRuleTemplate))]
        private RuleTemplate? _matchingRuleTemplate;

        public bool IsDeviatingFromRuleTemplate => MatchingRuleTemplate is not null && MatchingRuleTemplate.Mask != Mask;

        public ProgramRule(string programPath, LogicalProcessorMask mask)
        {
            ProgramPath = programPath;
            _mask = mask;
        }

        /// <summary>
        /// The mask was just set by either the Rules tab, or was set to a non-NoMask by something else
        /// The mask can only have been set to NoMask from the Rules tab, either directly or with a Template reapply
        /// </summary>
        partial void OnMaskChanged(LogicalProcessorMask value)
        {
            MaskRuleManager.UpdateOrAddProgramRule(ProgramPath, value, false);
        }

        partial void OnMatchingRuleTemplateChanged(RuleTemplate? oldValue, RuleTemplate? newValue)
        {
            if (oldValue is not null)
                oldValue.MaskChanged -= OnMatchingRuleTemplateMaskChanged;

            if (newValue is not null)
                newValue.MaskChanged += OnMatchingRuleTemplateMaskChanged;
        }

        private void OnMatchingRuleTemplateMaskChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(IsDeviatingFromRuleTemplate));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && MatchingRuleTemplate is not null)
                MatchingRuleTemplate.MaskChanged -= OnMatchingRuleTemplateMaskChanged;

            base.Dispose(disposing);
        }
    }
}
