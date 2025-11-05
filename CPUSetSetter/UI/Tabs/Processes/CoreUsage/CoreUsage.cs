using CommunityToolkit.Mvvm.ComponentModel;

namespace CPUSetSetter.UI.Tabs.Processes.CoreUsage
{
    public class CoreUsage(int coreIndex) : ObservableObject
    {
        public int CoreIndex { get; } = coreIndex;

        private double _usagePercent;
        public double UsagePercent
        {
            get => _usagePercent;
            set => SetProperty(ref _usagePercent, value);
        }

        private bool _isParked;
        public bool IsParked
        {
            get => _isParked;
            set => SetProperty(ref _isParked, value);
        }
    }
}