using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CPUSetSetter.Config.Models;
using CPUSetSetter.Core;
using CPUSetSetter.Platforms;
using System.Windows;


namespace CPUSetSetter.UI.Tabs.Masks
{
    public partial class MasksTabViewModel : ObservableObject
    {
        public bool HotkeyInputSelected { get; private set; } = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanRemoveMask))]
        [NotifyPropertyChangedFor(nameof(MaskBits))]
        private LogicalProcessorMask? _selectedMask;

        public bool CanRemoveMask => SelectedMask is not null && !SelectedMask.IsNoMask;

        public IEnumerable<IEnumerable<MaskBitViewModel>> MaskBits
        {
            get
            {
                if (SelectedMask is null)
                    return [];

                // Calculate the number of columns, so there are no columns with more than 16 logical processors
                int div = 2;
                while (SelectedMask.Mask.Count / div > 16)
                {
                    div += 2;
                }
                int columnCount = Math.Max(1, SelectedMask.Mask.Count / div);

                // Convert the mask to a MaskBitViewModel, which also contains the name of the logical processor
                IEnumerable<MaskBitViewModel> maskBits = Enumerable.Range(0, SelectedMask.Mask.Count).Select(i => new MaskBitViewModel(SelectedMask, i));
                return maskBits.Chunk(columnCount);
            }
        }

        [RelayCommand]
        private void CreateNewMask()
        {
            // TODO: Open a new window to create a new mask
            CreateMaskWindow createMaskWindow = new();
            createMaskWindow.ShowDialog();
        }

        [RelayCommand]
        private void RemoveMask()
        {
            if (!CanRemoveMask || SelectedMask is null)
                return;

            if (MaskRuleManager.MaskIsUsedByRules(SelectedMask))
            {
                // Prompt user if they're sure
                MessageBoxResult choice = MessageBox.Show($"The Mask '{SelectedMask.Name}' is currently used by at least one Rule.\nRemoving the Mask will also remove those Rules.\nAre you sure you want to remove the Mask?",
                    "Mask in use by Rule",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (choice != MessageBoxResult.Yes)
                    return;
            }

            SelectedMask.Remove();
            SelectedMask = null;
        }

        [RelayCommand]
        private void ClearHotkey()
        {
            SelectedMask?.Hotkeys.Clear();
        }

        public MasksTabViewModel()
        {
            // Add pressed keys to the selected mask's hotkey list when the input TextBox is focussed 
            HotkeyListener.KeyPressed += (_, e) =>
            {
                if (HotkeyInputSelected && SelectedMask is not null && !SelectedMask.Hotkeys.Contains(e.Key))
                {
                    SelectedMask.Hotkeys.Add(e.Key);
                }
            };
        }

        public void OnHotkeyInputFocusChanged(bool isFocused)
        {
            HotkeyInputSelected = isFocused;
            HotkeyListener.CallbacksEnabled = !isFocused;
        }
    }
}
