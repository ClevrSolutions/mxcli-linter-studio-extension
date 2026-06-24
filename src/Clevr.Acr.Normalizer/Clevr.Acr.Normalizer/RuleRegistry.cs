namespace Clevr.Acr.Normalizer;

/// <summary>Rule status (spec section 4). Only Verified counts in the ACR main report.</summary>
public enum RuleStatus
{
    Verified,
    NeedsThreshold,
    Approximate,
    Todo,
    OutOfReach,
}

/// <summary>
/// One ACR registry entry (spec section 4A): binds an engine rule to ACR metadata.
/// `EngineRuleKey` is the id with which the engine reports the rule (match key).
/// </summary>
public sealed record AcrRuleEntry
{
    public required string RuleId { get; init; }        // CLEVR-...
    public required string AcrCode { get; init; }
    public required string Engine { get; init; }        // "star" | "rego"
    public required string EngineRuleKey { get; init; }
    public required string Category { get; init; }      // ACR category (section 1)
    public required string Severity { get; init; }      // ACR severity (section 1)
    public required RuleStatus Status { get; init; }
}

/// <summary>
/// In-memory ACR registry with lookup by engineRuleKey. Lightly enforces the golden rule
/// (section 4): no duplicate ruleId, no duplicate engineRuleKey.
/// Loading from rules.json (file IO) is intentionally kept OUTSIDE this class and outside
/// the normalizer, so that both remain pure and independently testable.
/// </summary>
public sealed class RuleRegistry
{
    private readonly Dictionary<string, AcrRuleEntry> _byEngineRuleKey;

    public RuleRegistry(IEnumerable<AcrRuleEntry> entries)
    {
        var list = entries.ToList();

        var dupRuleId = list.GroupBy(e => e.RuleId, StringComparer.Ordinal)
            .FirstOrDefault(g => g.Count() > 1);
        if (dupRuleId is not null)
            throw new ArgumentException($"Duplicate ruleId in registry: '{dupRuleId.Key}'.");

        var dupKey = list.GroupBy(e => e.EngineRuleKey, StringComparer.Ordinal)
            .FirstOrDefault(g => g.Count() > 1);
        if (dupKey is not null)
            throw new ArgumentException($"Duplicate engineRuleKey in registry: '{dupKey.Key}'.");

        _byEngineRuleKey = list.ToDictionary(e => e.EngineRuleKey, StringComparer.Ordinal);
    }

    /// <summary>Finds the ACR entry that claims this engine rule, or null if not claimed.</summary>
    public AcrRuleEntry? FindByEngineRuleKey(string engineRuleKey)
        => _byEngineRuleKey.TryGetValue(engineRuleKey, out var entry) ? entry : null;
}
