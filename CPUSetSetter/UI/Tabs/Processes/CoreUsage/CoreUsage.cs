using CommunityToolkit.Mvvm.ComponentModel;

namespace CPUSetSetter.UI.Tabs.Processes.CoreUsage
{
    public class CoreUsage : ObservableObject
    {
        public int CoreIndex { get; }

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

        public CoreUsage(int coreIndex)
        {
            CoreIndex = coreIndex;
        }
    }
}
