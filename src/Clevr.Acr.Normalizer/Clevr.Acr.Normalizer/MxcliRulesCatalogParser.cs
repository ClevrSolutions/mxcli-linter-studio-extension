using System.Text.RegularExpressions;

namespace Clevr.Acr.Normalizer;

/// <summary>Naam + mxcli-categorie van één regel uit de mxcli-catalogus.</summary>
public sealed record MxcliRuleInfo(string Name, string Category);

/// <summary>
/// Parseert de output van `mxcli lint --list-rules` naar een map ruleId → (naam, categorie).
/// Puur: string in, map uit. De lint-JSON zelf bevat noch de naam noch de categorie
/// (alleen ruleId); deze catalogus levert beide, bv. CONV011 → ("NoCommitInLoop", "performance").
///
/// Vorm in de output (twee regels per regel):
///   "  CONV011 (NoCommitInLoop) - Commit actions should not be inside loops ..."
///   "      Category: performance, Severity: warning"
/// Statusregels ("Connected to: ...", "✓ Catalog ready") matchen niet.
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
                currentId = ruleId; // categorie volgt op de volgende regel
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
