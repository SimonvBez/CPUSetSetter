using CommunityToolkit.Mvvm.ComponentModel;
using CPUSetSetter.Core;
using CPUSetSetter.Platforms;
using CPUSetSetter.UI.Tabs.Processes;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Collections.Specialized;


namespace CPUSetSetter.Config.Models
{
    /// <summary>
    /// Represents a boolean mask of the logical processors, indicating which ones should be enabled, and which ones disabled
    /// </summary>
    public partial class LogicalProcessorMask : ObservableConfigObject
    {
        public static LogicalProcessorMask NoMask { get; private set; } = new([]);

        private readonly HotkeyCallback _hotkeyCallback;

        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private string _displayName;

        public ObservableCollection<bool> Mask { get; init; }

        public ObservableCollection<VKey> Hotkeys { get; init; }

        public bool IsNoMask { get; }

        private LogicalProcessorMask(string name, string displayName, List<bool> mask, List<VKey> hotkeys, bool isNoMask)
        {
            _name = name;
            _displayName = displayName;
            Mask = new(mask);
            Hotkeys = new(hotkeys);
            IsNoMask = isNoMask;

            SaveOnCollectionChanged(Mask);
            SaveOnCollectionChanged(Hotkeys);

            _hotkeyCallback = new(hotkeys.ToArray(), false);
            _hotkeyCallback.Pressed += OnHotkeyPressed;

            Hotkeys.CollectionChanged += OnHotkeysCollectionChanged;

            HotkeyListener.Default.AddCallback(_hotkeyCallback);
        }

        /// <summary>
        /// Private constructor for creating the NoMask 
        /// </summary>
        private LogicalProcessorMask(List<VKey> hotkeys) : this("<no mask>", string.Empty, [], hotkeys, true) { }

        /// <summary>
        /// Constructor for creating a new logical processor mask
        /// </summary>
        public LogicalProcessorMask(string name, List<bool> mask, List<VKey> hotkeys) : this(name, name, mask, hotkeys, false) { }

        /// <summary>
        /// Create a new NoMask with a given hotkey.
        /// THIS SHOULD ONLY EVER BE CALLED DURING CONFIG LOADING
        /// </summary>
        public static LogicalProcessorMask InitNoMask(List<VKey> hotkeys)
        {
            NoMask?.Dispose(); // Dispose the old NoMask in case it already existed
            NoMask = new(hotkeys); // Create a new one
            return NoMask;
        }

        public void Remove()
        {
            // Remove any rules that are using this mask, updating any processes by that rule as well
            MaskRuleManager.RemoveRulesUsingMask(this);
            // Remove from any remaining processes (like ones where the path couldn't be read)
            foreach (ProcessListEntryViewModel process in ProcessesTabViewModel.RunningProcesses)
            {
                if (process.Mask == this)
                    process.Mask = NoMask;
            }
            // Remove self from config
            AppConfig.Instance.LogicalProcessorMasks.Remove(this);
            Dispose();
        }

        private void OnHotkeysCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            _hotkeyCallback.VKeys = ImmutableArray.Create(Hotkeys.ToArray());
        }

        private void OnHotkeyPressed(object? sender, EventArgs e)
        {
            ProcessesTabViewModel.Instance?.OnMaskHotkeyPressed(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Hotkeys.CollectionChanged -= OnHotkeysCollectionChanged;
                _hotkeyCallback.Pressed -= OnHotkeyPressed;
                HotkeyListener.Default.RemoveCallback(_hotkeyCallback);
            }
            base.Dispose(disposing);
        }
    }
}
