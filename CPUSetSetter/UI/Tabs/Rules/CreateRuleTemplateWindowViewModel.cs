using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CPUSetSetter.Config.Models;


namespace CPUSetSetter.UI.Tabs.Rules
{
    public partial class CreateRuleTemplateWindowViewModel : ObservableObject
    {
        private readonly Action CloseWindow;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanCreate))]
        private string _ruleGlob = string.Empty;

        [ObservableProperty]
        private LogicalProcessorMask _selectedMask = LogicalProcessorMask.NoMask;

        public bool CanCreate => RuleGlob.Length > 0;

        [RelayCommand]
        private void CreateRuleTemplate()
        {
            AppConfig.Instance.RuleTemplates.Add(new(RuleGlob, SelectedMask));
            CloseWindow();
        }

        public CreateRuleTemplateWindowViewModel(Action closeWindow)
        {
            CloseWindow = closeWindow;
        }
    }
}
