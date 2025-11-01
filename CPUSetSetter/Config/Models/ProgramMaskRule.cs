using CommunityToolkit.Mvvm.ComponentModel;


namespace CPUSetSetter.Config.Models
{
    /// <summary>
    /// A rule indicating which program paths should be affected by a certain LogicalProcessorMask
    /// </summary>
    public partial class ProgramMaskRule : ObservableConfigObject
    {
        [ObservableProperty]
        public string _programPath;

        [ObservableProperty]
        public LogicalProcessorMask _logicalProcessorMask;

        public ProgramMaskRule(string programPath, LogicalProcessorMask logicalProcessorMask)
        {
            _programPath = programPath;
            _logicalProcessorMask = logicalProcessorMask;
        }
    }
}
