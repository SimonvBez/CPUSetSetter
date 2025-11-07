using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CPUSetSetter.Config.Models;
using CPUSetSetter.Core;
using Microsoft.Win32;


namespace CPUSetSetter.UI.Tabs.Rules
{
    public partial class CreateProgramRuleWindowViewModel : ObservableObject
    {
        private readonly Action CloseWindow;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanCreate))]
        private string _rulePath = string.Empty;

        [ObservableProperty]
        private LogicalProcessorMask _selectedMask = LogicalProcessorMask.NoMask;

        public bool CanCreate => RulePath.Length > 0 && !AppConfig.Instance.ProgramRules.Any(existingRule => RuleHelpers.PathsEqual(existingRule.ProgramPath, RulePath));

        [RelayCommand]
        private void OpenBrowseDialog()
        {
            OpenFileDialog dialog = new();
            dialog.Filter = "All Files (*.*)|*.*";

            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                RulePath = dialog.FileName;
            }
        }

        [RelayCommand]
        private void CreateProgramRule()
        {
            // Create, add and apply the new ProgramRule
            ProgramRule programRule = new(RulePath, SelectedMask, false);
            AppConfig.Instance.ProgramRules.Add(programRule);
            programRule.SetMask(SelectedMask, false);
            CloseWindow();
        }

        public CreateProgramRuleWindowViewModel(Action closeWindow)
        {
            CloseWindow = closeWindow;
        }
    }
}
