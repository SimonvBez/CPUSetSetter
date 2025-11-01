using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;


namespace CPUSetSetter.Config.Models
{
    /// <summary>
    /// Represents a boolean mask of the logical processors, indicating which ones should be enabled, and which ones disabled
    /// </summary>
    public partial class LogicalProcessorMask : ObservableObject
    {
        private static LogicalProcessorMask? _clearMask = null;

        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private string _settingsName;

        public ObservableCollection<bool> Mask { get; init; }

        public ObservableCollection<VKey> Hotkeys { get; init; }

        public bool IsClearMask;

        private LogicalProcessorMask(List<VKey> hotkeys)
        {
            _name = string.Empty;
            _settingsName = "<clear mask>";
            Mask = [];
            Hotkeys = new(hotkeys);
            IsClearMask = true;
        }

        public LogicalProcessorMask(string name, List<bool> mask, List<VKey> hotkeys)
        {
            _name = name;
            _settingsName = name;
            Mask = new(mask);
            Hotkeys = new(hotkeys);
            IsClearMask = false;
        }

        public static LogicalProcessorMask InitClearMask(List<VKey> hotkeys)
        {
            if (_clearMask is not null)
            {
                throw new InvalidOperationException("A ClearMask can only be created once");
            }
            _clearMask = new(hotkeys);
            return _clearMask;
        }
    }
}
