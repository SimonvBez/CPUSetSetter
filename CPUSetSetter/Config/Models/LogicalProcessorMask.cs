using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;


namespace CPUSetSetter.Config.Models
{
    /// <summary>
    /// Represents a boolean mask of the logical processors, indicating which ones should be enabled, and which ones disabled
    /// </summary>
    public partial class LogicalProcessorMask : ObservableObject
    {
        private static readonly LogicalProcessorMask _clearMask = new([]);

        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private string _settingsName;

        public ObservableCollection<bool> Mask { get; init; }

        public ObservableCollection<VKey> Hotkeys { get; init; }

        public bool IsClearMask { get; }

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

        public static LogicalProcessorMask PrepareClearMask(List<VKey> hotkeys)
        {
            // Overwrite the existing hotkeys, in case the config was corrupt and got reset to the defaults
            _clearMask.Hotkeys.Clear();
            foreach (VKey key in hotkeys)
            {
                _clearMask.Hotkeys.Add(key);
            }
            return _clearMask;
        }
    }
}
