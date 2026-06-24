namespace Clevr.Acr.Normalizer;

/// <summary>Regelstatus (spec sectie 4). Alleen Verified telt in het ACR-hoofdrapport.</summary>
public enum RuleStatus
{
    Verified,
    NeedsThreshold,
    Approximate,
    Todo,
    OutOfReach,
}

/// <summary>
/// Eén ACR-registry-entry (spec sectie 4A): bindt een engine-regel aan ACR-metadata.
/// `EngineRuleKey` is de id waarmee de engine de regel rapporteert (matchsleutel).
/// </summary>
public sealed record AcrRuleEntry
{
    public required string RuleId { get; init; }        // CLEVR-...
    public required string AcrCode { get; init; }
    public required string Engine { get; init; }        // "star" | "rego"
    public required string EngineRuleKey { get; init; }
    public required string Category { get; init; }      // ACR-categorie (sectie 1)
    public required string Severity { get; init; }      // ACR-severity (sectie 1)
    public required RuleStatus Status { get; init; }
}

/// <summary>
/// In-memory ACR-registry met opzoeken op engineRuleKey. Dwingt de gouden regel
/// (sectie 4) licht af: geen dubbele ruleId, geen dubbele engineRuleKey.
/// Het laden uit rules.json (bestand-IO) zit bewust BUITEN deze klasse en buiten
/// de normalizer, zodat beide puur en los testbaar blijven.
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
            throw new ArgumentException($"Dubbele ruleId in registry: '{dupRuleId.Key}'.");

        var dupKey = list.GroupBy(e => e.EngineRuleKey, StringComparer.Ordinal)
            .FirstOrDefault(g => g.Count() > 1);
        if (dupKey is not null)
            throw new ArgumentException($"Dubbele engineRuleKey in registry: '{dupKey.Key}'.");

        _byEngineRuleKey = list.ToDictionary(e => e.EngineRuleKey, StringComparer.Ordinal);
    }

    /// <summary>Vindt de ACR-entry die deze engine-regel claimt, of null als niet geclaimd.</summary>
    public AcrRuleEntry? FindByEngineRuleKey(string engineRuleKey)
        => _byEngineRuleKey.TryGetValue(engineRuleKey, out var entry) ? entry : null;
}
