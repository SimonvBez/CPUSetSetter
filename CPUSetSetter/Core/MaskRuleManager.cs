using CPUSetSetter.Config.Models;
using CPUSetSetter.UI.Tabs.Processes;
using Microsoft.Extensions.FileSystemGlobbing;
using System.IO;


namespace CPUSetSetter.Core
{
    public static class MaskRuleManager
    {
        public static void OnConfigLoaded()
        {
            // Updates all ProgramRules to have references to the RuleTemplate that matches them
            RefreshAllRules();
        }

        /// <summary>
        /// Get the mask that belongs to a given program path.
        /// This is called when a new process has appeared.
        /// </summary>
        public static LogicalProcessorMask GetMaskFromPath(string imagePath)
        {
            ProgramRule? rule = GetProgramRule(imagePath);
            if (rule is null)
                return LogicalProcessorMask.NoMask;
            return rule.Mask;
        }

        /// <summary>
        /// Update a rule if it exists, or add it if it doesn't.
        /// This is called when the mask of a process in the process list is changed.
        /// </summary>
        /// <returns>false if the mask failed to apply to the process that is being changed, else true. This is used for the hotkey sound</returns>
        public static bool UpdateOrAddProgramRule(string imagePath, LogicalProcessorMask newMask, bool removeRedundantNoMask)
        {
            if (imagePath.Length == 0)
                return false; // Don't add/update any empty strings

            App.EnsureMainThread();

            ProgramRule? existingRule = FindProgramRule(imagePath);
            RuleTemplate? ruleTemplate = FindRuleTemplate(imagePath);
            if (existingRule is not null)
                existingRule.MatchingRuleTemplate = ruleTemplate;

            if (newMask.IsNoMask)
            {
                // If there is no rule template with a mask, remove this NoMask rule from the config
                if (ruleTemplate is null || (ruleTemplate is not null && ruleTemplate.Mask.IsNoMask))
                {
                    // Clear the rule if it exists
                    if (existingRule is not null && removeRedundantNoMask)
                    {
                        AppConfig.Instance.ProgramRules.Remove(existingRule);
                    }
                    return ApplyRulesToPath(imagePath);
                }
                // If there is an rule template that does have a mask, then store this NoMask rule in the config by continuing down
            }

            // Add the rule if it does not exist, or update it if it does
            if (existingRule is null)
            {
                // There is no existing rule yet. Add the new rule
                AppConfig.Instance.ProgramRules.Add(new(imagePath, newMask));
            }
            else
            {
                // Modify the existing rule
                existingRule.Mask = newMask;
            }

            return ApplyRulesToPath(imagePath);
        }

        /// <summary>
        /// User pressed the explicit remove button on a ProgramRule.
        /// Remove the ProgramRule, unless it currently has a running process AND is matching a RuleTemplate.
        /// In that case, the Mask will be set to the RuleTemplate's
        /// </summary>
        public static void RemoveProgramRule(ProgramRule programRule)
        {
            if (programRule.MatchingRuleTemplate is null)
            {
                // Clear the mask and remove the ProgramRule
                programRule.Mask = LogicalProcessorMask.NoMask;
                AppConfig.Instance.ProgramRules.Remove(programRule);
                return;
            }

            foreach (ProcessListEntryViewModel process in ProcessesTabViewModel.RunningProcesses)
            {
                if (PathsEqual(process.ImagePath, programRule.ProgramPath))
                {
                    // This ProgramRule matches a RuleTemplate and has a currently running process
                    // It can't be removed, so it is set to the RuleTemplate instead
                    programRule.Mask = programRule.MatchingRuleTemplate.Mask;
                    return;
                }
            }
        }

        public static void AddRuleTemplate(string ruleGlob, LogicalProcessorMask mask)
        {
            App.EnsureMainThread();

            AppConfig.Instance.RuleTemplates.Add(new(ruleGlob, mask));
            RefreshAllRules();
        }

        public static void RemoveRuleTemplate(RuleTemplate ruleTemplate)
        {
            App.EnsureMainThread();

            AppConfig.Instance.RuleTemplates.Remove(ruleTemplate);
            RefreshAllRules();
        }

        public static void OnRuleTemplateChanged()
        {
            RefreshAllRules();
        }

        public static bool MaskIsUsedByRules(LogicalProcessorMask mask)
        {
            App.EnsureMainThread();

            return AppConfig.Instance.ProgramRules.Any(rule => rule.Mask == mask) ||
                AppConfig.Instance.RuleTemplates.Any(rule => rule.Mask == mask);
        }

        public static void RemoveRulesUsingMask(LogicalProcessorMask mask)
        {
            App.EnsureMainThread();

            List<string> removedRulePaths = [];
            // Remove any program rules that were using this mask
            for (int i = AppConfig.Instance.ProgramRules.Count - 1; i >= 0; --i)
            {
                if (AppConfig.Instance.ProgramRules[i].Mask == mask)
                {
                    removedRulePaths.Add(AppConfig.Instance.ProgramRules[i].ProgramPath);
                    AppConfig.Instance.ProgramRules.RemoveAt(i);
                }
            }

            // Remove any rule templates that were using this mask
            for (int i = AppConfig.Instance.RuleTemplates.Count - 1; i >= 0; --i)
            {
                if (AppConfig.Instance.RuleTemplates[i].Mask == mask)
                    AppConfig.Instance.RuleTemplates.RemoveAt(i);
            }

            // Also remove/update the masks on any processes that were still using it
            foreach (string removedRulePath in removedRulePaths)
            {
                ApplyRulesToPath(removedRulePath);
            }
        }

        /// <summary>
        /// Reapply a rule template. This will overwrite any Program Rules that deviated from this template, to the template's mask
        /// </summary>
        public static void ReapplyRuleTemplate(RuleTemplate ruleTemplate)
        {
            App.EnsureMainThread();

            foreach (ProgramRule rule in AppConfig.Instance.ProgramRules)
            {
                // Find all Program Rules that match with the given Template
                if (FindRuleTemplate(rule.ProgramPath) == ruleTemplate)
                {
                    rule.Mask = ruleTemplate.Mask;
                }
            }
        }

        /// <summary>
        /// Get an already existing Program Rule, otherwise create+add a new Program Rule with the first matching Rule Template.
        /// If no existing Program Rule or matching Rule Template exist, this returns null
        /// </summary>
        private static ProgramRule? GetProgramRule(string imagePath)
        {
            // First try to return a matching program rule
            ProgramRule? rule = FindProgramRule(imagePath);
            if (rule is not null)
                return rule;

            // Then try to get a RuleTemplate for this process
            RuleTemplate? ruleTemplate = FindRuleTemplate(imagePath);
            if (ruleTemplate is not null)
            {
                // A RuleTemplate exists. Create a new ProgramRule based on it
                ProgramRule newRule = new(imagePath, ruleTemplate.Mask);
                newRule.MatchingRuleTemplate = ruleTemplate;
                AppConfig.Instance.ProgramRules.Add(newRule);
                return newRule;
            }

            return null; // No program rule exists
        }

        private static ProgramRule? FindProgramRule(string imagePath)
        {
            return AppConfig.Instance.ProgramRules.FirstOrDefault(rule => PathsEqual(rule!.ProgramPath, imagePath), null);
        }

        private static RuleTemplate? FindRuleTemplate(string imagePath)
        {
            return AppConfig.Instance.RuleTemplates.FirstOrDefault(rule => PathMatchesGlob(rule!.RuleGlob, imagePath), null);
        }

        /// <summary>
        /// Get the mask belonging to the given imagePath, which is either set by a rule, or NoMask if no rule exists
        /// </summary>
        /// <returns>True if the mask was applied successfully to all relevant processes, false if an error occurred</returns>
        private static bool ApplyRulesToPath(string imagePath)
        {
            ProgramRule? programRule = GetProgramRule(imagePath);
            LogicalProcessorMask mask = programRule?.Mask ?? LogicalProcessorMask.NoMask;

            bool result = true;
            foreach (ProcessListEntryViewModel process in ProcessesTabViewModel.RunningProcesses)
            {
                if (PathsEqual(process.ImagePath, imagePath))
                {
                    result = process.SetMask(mask, false) && result;
                }
            }
            return result;
        }

        private static void RefreshAllRules()
        {
            App.EnsureMainThread();

            // The only way to deal with edge cases, such as when an existing RuleTemplate takes priority over a new/changed one is
            // by just iterating over all running processes and re-applying every rule
            foreach (ProcessListEntryViewModel process in ProcessesTabViewModel.RunningProcesses)
            {
                ProgramRule? programRule = GetProgramRule(process.ImagePath);
                LogicalProcessorMask mask = programRule?.Mask ?? LogicalProcessorMask.NoMask;
                process.SetMask(mask, false);
            }

            // Give each ProgramRule a reference to the Rule Template that matches it, to update the actions buttons on the Rules page
            foreach (ProgramRule rule in AppConfig.Instance.ProgramRules)
            {
                rule.MatchingRuleTemplate = FindRuleTemplate(rule.ProgramPath);
            }
        }

        private static bool PathsEqual(string path1, string path2)
        {
            return NormalizePath(path1) == NormalizePath(path2);
        }

        private static string NormalizePath(string path)
        {
            if (OperatingSystem.IsWindows())
                return path.Replace(@"\", "/");
            return path;
        }

        /// <summary>
        /// Checks if an absolute path is part of a glob pattern
        /// </summary>
        private static bool PathMatchesGlob(string pattern, string path)
        {
            if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(path))
                return false;

            string? pathRoot = Path.GetPathRoot(path);
            string? patternRoot = Path.GetPathRoot(pattern);

            if (string.IsNullOrEmpty(pathRoot))
            {
                // The path doesn't have a root
                return false;
            }

            if (pattern.StartsWith("*/"))
            {
                // Remove a starting wildcard(*/) as it will otherwise cause a false negative
                pattern = pattern[2..];
            }
            else if (!pattern.StartsWith("**"))
            {
                if (string.IsNullOrEmpty(patternRoot))
                {
                    // A pattern not starting with a root, **, or */ can never be a match
                    return false;
                }
                else if (patternRoot != pathRoot)
                {
                    // The path's root is different from the pattern's root
                    return false;
                }
                else
                {
                    // Now that the pattern root is checked, strip it. The glob matcher only works with patterns without root
                    pattern = pattern[patternRoot.Length..];
                }
            }

            Matcher matcher = new(OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
            matcher.AddInclude(pattern);

            if (string.IsNullOrEmpty(pathRoot))
                return matcher.Match(path).HasMatches;
            return matcher.Match(pathRoot, path).HasMatches;
        }
    }
}
