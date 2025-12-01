using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CPUSetSetter.Config.Models;
using CPUSetSetter.Util;
using System.Windows;


namespace CPUSetSetter.UI.Tabs.Masks
{
    public partial class MasksTabViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanRemoveMask))]
        private LogicalProcessorMask? _selectedMask;

        public bool CanRemoveMask => SelectedMask is not null && SelectedMask.MaskType != MaskApplyType.NoMask;

        [RelayCommand]
        private void CreateNewMask()
        {
            // Open a CreateMaskWindow, which will be able to add a new Mask directly to the config 
            CreateMaskWindow createMaskWindow = new() { Owner = App.Current.MainWindow };
            bool? hasCreated = createMaskWindow.ShowDialog();
            if (hasCreated == true)
            {
                SelectedMask = AppConfig.Instance.LogicalProcessorMasks[^1];
            }
        }

        [RelayCommand]
        private void RemoveMask()
        {
            if (!CanRemoveMask || SelectedMask is null)
                return;

            if (RuleHelpers.MaskIsUsedByRules(SelectedMask))
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
    }
}
