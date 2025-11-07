using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CPUSetSetter.Config.Models;
using CPUSetSetter.Core;
using System.Windows.Data;


namespace CPUSetSetter.UI.Tabs.Rules
{
    public partial class RulesTabViewModel : ObservableObject
    {
        private readonly ListCollectionView programRulesView;

        [ObservableProperty]
        private string _programRuleFilter = string.Empty;

        [ObservableProperty]
        private bool _showOnlyDeviatingProgramRule = false;

        /// <summary>
        /// User pressed Program Rule Remove button
        /// </summary>
        [RelayCommand]
        private static void ProgramRuleRemove(ProgramRule programRule)
        {
            // Remove the ProgramRule, unless it currently has a running process AND is matching a RuleTemplate
            // In that case, the Mask will be set to the RuleTemplate's
            programRule.TryRemove();
        }

        [RelayCommand]
        private static void ProgramRuleReapply(ProgramRule programRule)
        {
            if (programRule.MatchingRuleTemplate is not null)
                programRule.Mask = programRule.MatchingRuleTemplate.Mask;
        }

        [RelayCommand]
        private static void CreateProgramRule()
        {
            CreateProgramRuleWindow window = new() { Owner = App.Current.MainWindow };
            window.ShowDialog();
        }

        /// <summary>
        /// User pressed Rule Template Reapply button.
        /// This will overwrite every deviating ProgramRule with the mask of the RuleTemplate
        /// </summary>
        [RelayCommand]
        private static void RuleTemplateReapply(RuleTemplate ruleTemplate)
        {
            ruleTemplate.Reapply();
        }

        /// <summary>
        /// User pressed Rule Template Remove button.
        /// Removes the RuleTemplate. This will also refresh all ProgramRules to find their first matching RuleTemplate
        /// </summary>
        [RelayCommand]
        private static void RuleTemplateRemove(RuleTemplate ruleTemplate)
        {
            AppConfig.Instance.RuleTemplates.Remove(ruleTemplate);
        }

        [RelayCommand]
        private static void CreateRuleTemplate()
        {
            CreateRuleTemplateWindow window = new() { Owner = App.Current.MainWindow };
            window.ShowDialog();
        }

        public RulesTabViewModel()
        {
            programRulesView = (ListCollectionView)CollectionViewSource.GetDefaultView(AppConfig.Instance.ProgramRules);
            programRulesView.Filter = item =>
            {
                ProgramRule rule = (ProgramRule)item;
                return rule.ProgramPath.Contains(ProgramRuleFilter, StringComparison.OrdinalIgnoreCase) &&
                    (!ShowOnlyDeviatingProgramRule || rule.IsDeviatingFromRuleTemplate);
            };
        }

        partial void OnProgramRuleFilterChanged(string value)
        {
            programRulesView.Refresh();
        }

        partial void OnShowOnlyDeviatingProgramRuleChanged(bool value)
        {
            programRulesView.Refresh();
        }
    }
}
