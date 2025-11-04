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
        private LogicalProcessorMask _logicalProcessorMask;

        public ProgramRule(string programPath, LogicalProcessorMask logicalProcessorMask)
        {
            ProgramPath = programPath;
            _logicalProcessorMask = logicalProcessorMask;
        }

        partial void OnLogicalProcessorMaskChanged(LogicalProcessorMask value)
        {
            MaskRuleManager.UpdateOrAddProgramRule(ProgramPath, value);
        }
    }
}
