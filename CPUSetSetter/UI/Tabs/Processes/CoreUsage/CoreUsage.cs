using CommunityToolkit.Mvvm.ComponentModel;

namespace CPUSetSetter.UI.Tabs.Processes.CoreUsage
{
    public partial class CoreUsage(int coreIndex) : ObservableObject
    {
        public int CoreIndex { get; } = coreIndex;

        [ObservableProperty]
        private double _usagePercent;

        [ObservableProperty]
        private bool _isParked;
    }
}