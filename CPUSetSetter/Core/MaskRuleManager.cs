using CPUSetSetter.Config.Models;
using CPUSetSetter.UI.Tabs.Processes;
using System.Text.RegularExpressions;


namespace CPUSetSetter.Core
{
    public static class MaskRuleManager
    {
        /// <summary>
        /// Get the mask that belongs to a given program path.
        /// This is called when a new process has appeared.
        /// </summary>
        public static LogicalProcessorMask GetMaskFromPath(string imagePath)
        {
            ProgramMaskRule? rule = GetProgramRule(imagePath);
            if (rule is null)
                return LogicalProcessorMask.NoMask;
            return rule.LogicalProcessorMask;
        }

        /// <summary>
        /// Update a rule if it exists, or add it if it doesn't.
        /// This is called when the mask of a process in the process list is changed.
        /// </summary>
        public static void UpdateOrAddProgramRule(string imagePath, LogicalProcessorMask newMask)
        {
            App.EnsureMainThread();

            ProgramMaskRule? existingRule = FindProgramRule(imagePath);

            if (newMask.IsNoMask)
            {
                // If there is no auto rule with a mask, remove this NoMask rule from the config
                ProgramMaskRule? autoRule = FindAutoRule(imagePath);
                if (autoRule is null || (autoRule is not null && autoRule.LogicalProcessorMask.IsNoMask))
                {
                    // Clear the rule if it exists
                    if (existingRule is not null)
                    {
                        AppConfig.Instance.ProgramMaskRules.Remove(existingRule);
                    }
                    ApplyProgramRulesToAllProcesses();
                    return;
                }
                // If there is an auto rule that does have a mask, then store this NoMask rule in the config by continuing down
            }

            // Add the rule if it does not exist, or update it if it does
            if (existingRule is null)
            {
                // There is no existing rule yet. Add the new rule
                AppConfig.Instance.ProgramMaskRules.Add(new(imagePath, newMask));
            }
            else
            {
                // Modify the existing rule
                existingRule.LogicalProcessorMask = newMask;
            }

            ApplyProgramRulesToAllProcesses();
        }

        // Notes for later:
        // - If there is going to be a "Program rules" UI tab, add an AddRule/RemoveRule function. And make a ProgramMaskRule be able to be edited
        // - If there is going to be a "Auto rule" UI tab, add Add/Remove functions, and prune any NoMask program rules that are no longer needed

        private static ProgramMaskRule? GetProgramRule(string imagePath)
        {
            App.EnsureMainThread();

            // First try to return a matching program rule
            ProgramMaskRule? rule = FindProgramRule(imagePath);
            if (rule is not null)
                return rule;

            // Then try to get an auto rule
            ProgramMaskRule? autoRule = FindAutoRule(imagePath);
            if (autoRule is not null && !autoRule.LogicalProcessorMask.IsNoMask)
            {
                // An auto rule with a mask exists. Create a new program rules based on it
                ProgramMaskRule newRule = new(imagePath, autoRule.LogicalProcessorMask);
                AppConfig.Instance.ProgramMaskRules.Add(newRule);
                return newRule;
            }

            return null; // No program rule exists
        }

        private static ProgramMaskRule? FindProgramRule(string imagePath)
        {
            return AppConfig.Instance.ProgramMaskRules.FirstOrDefault(rule => PathsEqual(rule!.ProgramPath, imagePath), null);
        }

        private static ProgramMaskRule? FindAutoRule(string imagePath)
        {
            return AppConfig.Instance.AutomaticMaskRules.FirstOrDefault(rule => PathMatchesGlob(rule!.ProgramPath, imagePath), null);
        }

        private static void ApplyProgramRulesToAllProcesses()
        {
            foreach (ProcessListEntryViewModel process in ProcessesTabViewModel.RunningProcesses)
            {
                ProgramMaskRule? rule = AppConfig.Instance.ProgramMaskRules.FirstOrDefault(rule => PathsEqual(rule!.ProgramPath, process.ImagePath), null);
                process.Mask = rule is null ? LogicalProcessorMask.NoMask : rule.LogicalProcessorMask;
            }
        }

        private static bool PathsEqual(string path1, string path2)
        {
            return NormalizePath(path1) == NormalizePath(path2);
        }

        private static bool PathMatchesGlob(string pattern, string imagePath)
        {
            if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(imagePath))
                return false;

            // Normalize paths (consistent separators and absolute form)
            string normalizedPattern = NormalizePath(pattern);
            string normalizedCandidate = NormalizePath(imagePath);

            // Convert glob to regex
            string regexPattern = "^" + Regex.Escape(normalizedPattern)
                .Replace(@"\*", ".*")     // *: any sequence
                .Replace(@"\?", ".")      // ?: single character
                + "$";

            var comparison = OperatingSystem.IsWindows() ? RegexOptions.IgnoreCase : RegexOptions.None;

            return Regex.IsMatch(normalizedCandidate, regexPattern, comparison);
        }

        private static string NormalizePath(string path)
        {
            if (OperatingSystem.IsWindows())
                return path.Replace(@"\", "/");
            return path;
        }
    }
}
