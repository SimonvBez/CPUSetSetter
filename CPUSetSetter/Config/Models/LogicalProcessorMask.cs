using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;


namespace CPUSetSetter.Config.Models
{
    /// <summary>
    /// Represents a boolean mask of the logical processors, indicating which ones should be enabled, and which ones disabled
    /// </summary>
    public partial class LogicalProcessorMask : ObservableConfigObject
    {
        private static LogicalProcessorMask? _clearMask;

        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private string _settingsName;

        public ObservableCollection<bool> Mask { get; init; }

        public ObservableCollection<VKey> Hotkeys { get; init; }

        public bool IsClearMask { get; }

        private LogicalProcessorMask(string name, string settingsName, List<bool> mask, List<VKey> hotkeys, bool isClearMask)
        {
            _name = name;
            _settingsName = settingsName;
            Mask = new(mask);
            Hotkeys = new(hotkeys);
            IsClearMask = isClearMask;

            SaveOnCollectionChanged(Mask);
            SaveOnCollectionChanged(Hotkeys);
        }

        /// <summary>
        /// Private constructor for creating the clear mask 
        /// </summary>
        private LogicalProcessorMask(List<VKey> hotkeys) : this(string.Empty, "<clear mask>", [], hotkeys, true) { }

        /// <summary>
        /// Constructor for creating a new logical processor mask
        /// </summary>
        public LogicalProcessorMask(string name, List<bool> mask, List<VKey> hotkeys) : this(name, name, mask, hotkeys, false) { }

        public static LogicalProcessorMask InitClearMask(List<VKey> hotkeys)
        {
            _clearMask?.Dispose(); // Dispose the old clearMask in case it already existed
            _clearMask = new(hotkeys); // Create a new one
            return _clearMask;
        }
    }
}
