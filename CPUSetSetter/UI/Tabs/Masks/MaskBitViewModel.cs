using CommunityToolkit.Mvvm.ComponentModel;
using CPUSetSetter.Config.Models;
using CPUSetSetter.Platforms;


namespace CPUSetSetter.UI.Tabs.Masks
{
    public partial class MaskBitViewModel : ObservableObject
    {
        private readonly LogicalProcessorMask _mask;
        private readonly int _logicalProcessorIndex;

        [ObservableProperty]
        private string _logicalProcessorName;

        [ObservableProperty]
        private bool _isEnabled;

        public MaskBitViewModel(LogicalProcessorMask mask, int logicalProcessorIndex)
        {
            _mask = mask;
            _logicalProcessorIndex = logicalProcessorIndex;
            _logicalProcessorName = CpuInfo.LogicalProcessorNames[logicalProcessorIndex];
            _isEnabled = mask.Mask[logicalProcessorIndex];
        }

        /// <summary>
        /// Save the changed mask bit to the underlying LogicalProcessorMask config object
        /// </summary>
        partial void OnIsEnabledChanged(bool value)
        {
            _mask.Mask[_logicalProcessorIndex] = value;
        }
    }
}
