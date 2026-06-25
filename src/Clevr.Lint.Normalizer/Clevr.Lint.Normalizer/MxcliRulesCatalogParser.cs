using System.Text.RegularExpressions;

namespace Clevr.Lint.Normalizer;

/// <summary>Name + mxcli category of a single rule from the mxcli catalog.</summary>
public sealed record MxcliRuleInfo(string Name, string Category);

/// <summary>
/// Parses the output of `mxcli lint --list-rules` into a map ruleId → (name, category).
/// Pure: string in, map out. The lint JSON itself contains neither the name nor the category
/// (only ruleId); this catalog supplies both, e.g. CONV011 → ("NoCommitInLoop", "performance").
///
/// Format in the output (two lines per rule):
///   "  CONV011 (NoCommitInLoop) - Commit actions should not be inside loops ..."
///   "      Category: performance, Severity: warning"
/// Status lines ("Connected to: ...", "✓ Catalog ready") do not match.
/// </summary>
public static class MxcliRulesCatalogParser
{
    private static readonly Regex RuleLine =
        new(@"^\s*([A-Za-z][\w]*)\s+\(([^)]+)\)\s*-\s*", RegexOptions.Compiled);

    private static readonly Regex CategoryLine =
        new(@"^\s*Category:\s*([A-Za-z]+)", RegexOptions.Compiled);

    public static IReadOnlyDictionary<string, MxcliRuleInfo> Parse(string listRulesOutput)
    {
        var map = new Dictionary<string, MxcliRuleInfo>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(listRulesOutput)) return map;

        string? currentId = null;
        foreach (var line in listRulesOutput.Split('\n'))
        {
            var rule = RuleLine.Match(line);
            if (rule.Success)
            {
                var ruleId = rule.Groups[1].Value.Trim();
                var name = rule.Groups[2].Value.Trim();
                if (!map.ContainsKey(ruleId)) map[ruleId] = new MxcliRuleInfo(name, "");
                currentId = ruleId; // category follows on the next line
                continue;
            }

            var cat = CategoryLine.Match(line);
            if (cat.Success && currentId is not null)
            {
                map[currentId] = map[currentId] with { Category = cat.Groups[1].Value.Trim() };
                currentId = null;
            }
        }
        return map;
    }
}
