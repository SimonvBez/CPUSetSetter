using CPUSetSetter.Config.Models;
using Microsoft.Extensions.FileSystemGlobbing;
using System.IO;


namespace CPUSetSetter.Core
{
    public static class RuleHelpers
    {
        public static void OnConfigLoaded()
        {
            RuleTemplate.OnConfigLoaded();
        }

        /// <summary>
        /// Get an already existing Program Rule, otherwise create+add a new Program Rule with the first matching Rule Template.
        /// If no existing Program Rule or matching Rule Template exist, this returns null
        /// </summary>
        public static ProgramRule? GetProgramRuleOrNull(string imagePath)
        {
            // First try to return a matching program rule
            ProgramRule? rule = FindProgramRuleOrNull(imagePath);
            if (rule is not null)
                return rule;

            // Then try to get a RuleTemplate for this process
            RuleTemplate? ruleTemplate = FindRuleTemplateOrNull(imagePath);
            if (ruleTemplate is not null)
            {
                // A RuleTemplate exists. Create a new ProgramRule based on it
                ProgramRule newRule = new(imagePath, ruleTemplate.Mask, true);
                newRule.MatchingRuleTemplate = ruleTemplate;
                AppConfig.Instance.ProgramRules.Add(newRule);
                return newRule;
            }

            return null; // No program rule exists
        }

        public static bool MaskIsUsedByRules(LogicalProcessorMask mask)
        {
            return AppConfig.Instance.ProgramRules.Any(rule => rule.Mask == mask) ||
                AppConfig.Instance.RuleTemplates.Any(rule => rule.Mask == mask);
        }

        private static ProgramRule? FindProgramRuleOrNull(string imagePath)
        {
            return AppConfig.Instance.ProgramRules.FirstOrDefault(rule => PathsEqual(rule!.ProgramPath, imagePath), null);
        }

        public static RuleTemplate? FindRuleTemplateOrNull(string imagePath)
        {
            return AppConfig.Instance.RuleTemplates.FirstOrDefault(rule => PathMatchesGlob(rule!.RuleGlob, imagePath), null);
        }

        public static bool PathsEqual(string path1, string path2)
        {
            return string.Equals(
                NormalizePath(path1),
                NormalizePath(path2),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal
            );
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
        public static bool PathMatchesGlob(string pattern, string path)
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
