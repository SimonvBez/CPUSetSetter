using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CPUSetSetter.Config.Models;
using CPUSetSetter.Core;


namespace CPUSetSetter.UI.Tabs.Rules
{
    public partial class RulesTabViewModel : ObservableObject
    {
        public static string RuleTemplatesHeaderText
        {
            get
            {
                if (AppConfig.Instance.RuleTemplates.Count == 0)
                    return "Rule Templates";
                if (AppConfig.Instance.RuleTemplates.Count == 1)
                    return "Rule Templates (double click to edit)";
                return "Rule Templates (double click to edit, drag-and-drop to reorder)";
            }
        }

        /// <summary>
        /// User pressed Program Rule Remove button
        /// </summary>
        [RelayCommand]
        private static void ProgramRuleRemove(ProgramRule programRule)
        {
            // Remove the ProgramRule, unless it currently has a running process AND is matching a RuleTemplate
            // In that case, the Mask will be set to the RuleTemplate's
            MaskRuleManager.RemoveProgramRule(programRule);
        }

        [RelayCommand]
        private static void ProgramRuleReapply(ProgramRule programRule)
        {
            if (programRule.MatchingRuleTemplate is not null)
                programRule.Mask = programRule.MatchingRuleTemplate.Mask;
        }

        /// <summary>
        /// User pressed Rule Template Reapply button
        /// </summary>
        [RelayCommand]
        private static void RuleTemplateReapply(RuleTemplate ruleTemplate)
        {
            MaskRuleManager.ReapplyRuleTemplate(ruleTemplate);
        }

        /// <summary>
        /// User pressed Rule Template Remove button
        /// </summary>
        [RelayCommand]
        private static void RuleTemplateRemove(RuleTemplate ruleTemplate)
        {
            // Rule Templates don't have any remove-logic, so they can just be removed
            MaskRuleManager.RemoveRuleTemplate(ruleTemplate);
        }

        [RelayCommand]
        private static void CreateRuleTemplate()
        {
            new CreateRuleTemplateWindow().ShowDialog();
        }

        public RulesTabViewModel()
        {
            // Update the Rule Template DataGrid header when the Rule Template collection changes
            AppConfig.Instance.RuleTemplates.CollectionChanged += (_, _) => OnPropertyChanged(nameof(RuleTemplatesHeaderText));
        }

        // TODO: Disable Remove button for Program Rules that fall under a Rule Template
    }
}
