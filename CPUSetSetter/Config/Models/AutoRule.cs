using CommunityToolkit.Mvvm.ComponentModel;


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
    }
}
