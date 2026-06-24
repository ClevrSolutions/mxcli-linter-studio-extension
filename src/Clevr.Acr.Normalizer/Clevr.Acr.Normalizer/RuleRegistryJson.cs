using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clevr.Acr.Normalizer;

/// <summary>
/// Parses rules.json (spec section 4A) into a <see cref="RuleRegistry"/>.
/// Pure: string in, registry out — no file IO (the caller reads the file).
/// Unknown top-level keys (_comment, _ruleId_schema, ...) are ignored.
/// </summary>
public static class RuleRegistryJson
{
    private sealed class Root
    {
        [JsonPropertyName("rules")] public List<Entry> Rules { get; set; } = new();
    }

    private sealed class Entry
    {
        [JsonPropertyName("ruleId")] public string RuleId { get; set; } = "";
        [JsonPropertyName("acrCode")] public string AcrCode { get; set; } = "";
        [JsonPropertyName("engine")] public string Engine { get; set; } = "";
        [JsonPropertyName("engineRuleKey")] public string EngineRuleKey { get; set; } = "";
        [JsonPropertyName("category")] public string Category { get; set; } = "";
        [JsonPropertyName("severity")] public string Severity { get; set; } = "";
        [JsonPropertyName("status")] public string Status { get; set; } = "";
    }

    public static RuleRegistry Parse(string json)
    {
        var root = JsonSerializer.Deserialize<Root>(json) ?? new Root();
        var entries = root.Rules.Select(e => new AcrRuleEntry
        {
            RuleId = e.RuleId,
            AcrCode = e.AcrCode,
            Engine = e.Engine,
            EngineRuleKey = e.EngineRuleKey,
            Category = e.Category,
            Severity = e.Severity,                 // free text (incl. "TODO-confirm")
            Status = ParseStatus(e.Status),
        });
        return new RuleRegistry(entries); // reuses the validated golden-rule guards
    }

    private static RuleStatus ParseStatus(string status) => status.Trim().ToLowerInvariant() switch
    {
        "verified" => RuleStatus.Verified,
        "needs-threshold" => RuleStatus.NeedsThreshold,
        "approximate" => RuleStatus.Approximate,
        "todo" => RuleStatus.Todo,
        "out-of-reach" => RuleStatus.OutOfReach,
        _ => throw new ArgumentException($"Unknown status '{status}' in rules.json."),
    };
}
