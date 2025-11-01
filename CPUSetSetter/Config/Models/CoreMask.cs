using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;


namespace CPUSetSetter.Config.Models
{
    public partial class CoreMask : ObservableObject
    {
        private static CoreMask? _clearMask = null;

        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private string _settingsName;

        public ObservableCollection<bool> Mask { get; init; }

        public ObservableCollection<VKey> Hotkeys { get; init; }

        public bool IsClearMask;

        private CoreMask(List<VKey> hotkeys)
        {
            _name = string.Empty;
            _settingsName = "<clear mask>";
            Mask = [];
            Hotkeys = new(hotkeys);
            IsClearMask = true;
        }

        public CoreMask(string name, List<bool> mask, List<VKey> hotkeys)
        {
            _name = name;
            _settingsName = name;
            Mask = new(mask);
            Hotkeys = new(hotkeys);
            IsClearMask = false;
        }

        public static CoreMask InitClearMask(List<VKey> hotkeys)
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
