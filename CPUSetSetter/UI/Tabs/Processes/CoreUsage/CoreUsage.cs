using CommunityToolkit.Mvvm.ComponentModel;


namespace CPUSetSetter.UI.Tabs.Processes.CoreUsage
{
    /// <summary>
    /// Represents the utility and parking state of a single logical processor.
    /// </summary>
    public partial class CoreUsage : ObservableObject
    {
        [ObservableProperty]
        private double _utility;

        [ObservableProperty]
        private bool _isParked;

        public string Name { get; }

        public CoreUsage(string cpuName)
        {
            _utility = 0;
            _isParked = false;
            Name = cpuName;
        }
    }
}
