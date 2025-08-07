using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;


namespace CPUSetSetter.UI
{
    public partial class ObservableSettings : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<string> _definedCpuSets = ["", "Cache", "Freq"];
    }

    public static class Settings
    {
        public static ObservableSettings Default { get; } = new ObservableSettings();
    }
}
