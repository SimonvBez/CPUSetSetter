using CommunityToolkit.Mvvm.ComponentModel;


namespace CPUSetSetter.Config.Models
{
    public partial class ProgramCoreMaskRule : ObservableObject
    {
        [ObservableProperty]
        public string _programPath;

        [ObservableProperty]
        public CoreMask _coreMask;

        public ProgramCoreMaskRule(string programPath, CoreMask coreMask)
        {
            _programPath = programPath;
            _coreMask = coreMask;
        }
    }
}
