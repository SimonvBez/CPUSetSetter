using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CPUSetSetter.Config.Models;
using CPUSetSetter.Platforms;
using System.Collections.ObjectModel;


namespace CPUSetSetter.UI.Tabs.Masks
{
    public partial class CreateMaskWindowViewModel : ObservableObject
    {
        private readonly Action _closeWindowAction;

        [ObservableProperty]
        private ObservableCollection<bool> _boolMask;

        [ObservableProperty]
        private MaskApplyType _maskType = MaskApplyType.CPUSet;

        [ObservableProperty]
        private ObservableCollection<VKey> _hotkeys;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanCreateMask))]
        private string _name;

        public bool CanCreateMask => Name.Length > 0 && !AppConfig.Instance.LogicalProcessorMasks.Any(mask => mask.Name == Name);

        [RelayCommand]
        private void CreateMask()
        {
            if (!CanCreateMask)
            {
                // This should not be able to happen, but just in case we'll let the UI update
                OnPropertyChanged(nameof(CanCreateMask));
                return;
            }

            AppConfig.Instance.LogicalProcessorMasks.Add(new(Name, MaskType, new(BoolMask), new(Hotkeys)));
            _closeWindowAction();
        }

        public CreateMaskWindowViewModel(Action closeWindowAction)
        {
            _closeWindowAction = closeWindowAction;
            _boolMask = new(Enumerable.Repeat(true, CpuInfo.LogicalProcessorCount));
            _hotkeys = [];
            _name = string.Empty;
        }
    }
}
