using CommunityToolkit.Mvvm.ComponentModel;
using CPUSetSetter.Util;
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

        [ObservableProperty]
        private MaskApplyType _maskType;

        public ObservableCollection<bool> BoolMask { get; init; }

        public ObservableCollection<VKey> Hotkeys { get; init; }

        private LogicalProcessorMask(string name, string displayName, MaskApplyType maskType, List<bool> mask, List<VKey> hotkeys)
        {
            _name = name;
            _displayName = displayName;
            _maskType = maskType;
            BoolMask = new(mask);
            Hotkeys = new(hotkeys);

            SaveOnCollectionChanged(BoolMask);
            SaveOnCollectionChanged(Hotkeys);

            _hotkeyCallback = new(hotkeys.ToArray(), false);
            _hotkeyCallback.Pressed += OnHotkeyPressed;

            Hotkeys.CollectionChanged += OnHotkeysCollectionChanged;

            HotkeyListener.Default.AddCallback(_hotkeyCallback);
        }

        /// <summary>
        /// Private constructor for creating the NoMask 
        /// </summary>
        private LogicalProcessorMask(List<VKey> hotkeys) : this("<no mask>", string.Empty, MaskApplyType.NoMask, [], hotkeys) { }

        /// <summary>
        /// Constructor for creating a new logical processor mask
        /// </summary>
        public LogicalProcessorMask(string name, MaskApplyType maskType, List<bool> mask, List<VKey> hotkeys) : this(name, name, maskType, mask, hotkeys) { }

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
            // Remove all RuleTemplates that are using this Mask
            RuleTemplate.RemoveAllUsingMask(this);

            // Remove any program rules that were using this mask
            for (int i = AppConfig.Instance.ProgramRules.Count - 1; i >= 0; --i)
            {
                if (AppConfig.Instance.ProgramRules[i].Mask == this)
                {
                    AppConfig.Instance.ProgramRules[i].TryRemove();
                }
            }

            // Finally, also remove the mask from any processes where the ImagePath couldn't be read (and so there was no ProgramRule to clear it)
            foreach (ProcessListEntryViewModel process in ProcessesTabViewModel.RunningProcesses)
            {
                if (process.Mask == this)
                    process.SetMask(NoMask, false);
            }

            // Remove self from config, which in turn calls Dispose()
            AppConfig.Instance.LogicalProcessorMasks.Remove(this);
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

    public enum MaskApplyType
    {
        NoMask,
        CPUSet,
        Affinity
    }
}
