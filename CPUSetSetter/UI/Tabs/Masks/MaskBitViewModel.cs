using CommunityToolkit.Mvvm.ComponentModel;
using CPUSetSetter.Platforms;


namespace CPUSetSetter.UI.Tabs.Masks
{
    /// <summary>
    /// Represents a single logical processor in a mask. It contains a processor name and a bool state.
    /// Used by the MaskEditorControl.
    /// </summary>
    public partial class MaskBitViewModel : ObservableObject
    {
        private readonly int _logicalProcessorIndex;

        public string LogicalProcessorName { get; }

        [ObservableProperty]
        private bool _isEnabled;

        public event EventHandler<MaskBitChangedEventArgs>? MaskChanged;

        public MaskBitViewModel(int logicalProcessorIndex)
        {
            _logicalProcessorIndex = logicalProcessorIndex;
            LogicalProcessorName = CpuInfo.LogicalProcessorNames[logicalProcessorIndex];
            _isEnabled = false;
        }

        partial void OnIsEnabledChanged(bool value)
        {
            MaskChanged?.Invoke(this, new(_logicalProcessorIndex, value));
        }
    }

    public class MaskBitChangedEventArgs : EventArgs
    {
        public int MaskBitIndex { get; }
        public bool IsEnabled { get; }

        public MaskBitChangedEventArgs(int maskBitIndex, bool isEnabled)
        {
            MaskBitIndex = maskBitIndex;
            IsEnabled = isEnabled;
        }
    }
}
