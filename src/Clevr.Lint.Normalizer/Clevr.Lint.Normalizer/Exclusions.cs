using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clevr.Lint.Normalizer;

/// <summary>
/// One exclusion (spec section 3): a deliberately suppressed improvement WITH a mandatory reason.
/// Stored in <c>$project/.clevr-lint/exclusions.json</c> (checked into version control → the
/// team shares the same exclusions). Matching is done on <see cref="Fingerprint"/> =
/// sha1(ruleId | documentQualifiedName | elementName) — already calculated on every Violation.
/// The document fields are retained so that a STALE exclusion (no longer matching) can still
/// be shown in a readable form.
/// </summary>
public sealed record Exclusion
{
    [JsonPropertyName("fingerprint")] public string Fingerprint { get; init; } = "";
    [JsonPropertyName("ruleId")] public string RuleId { get; init; } = "";
    [JsonPropertyName("documentQualifiedName")] public string DocumentQualifiedName { get; init; } = "";
    [JsonPropertyName("elementName")] public string ElementName { get; init; } = "";
    [JsonPropertyName("reason")] public string Reason { get; init; } = "";
    [JsonPropertyName("excludedBy")] public string ExcludedBy { get; init; } = "";
    [JsonPropertyName("date")] public string Date { get; init; } = "";
}

/// <summary>
/// Pure (de)serialization + list operations for exclusions.json. No IO — the
/// extension project handles reading/writing (see ExclusionStore). Tolerant: empty/invalid
/// input → empty list.
/// </summary>
public static class ExclusionsJson
{
    private sealed class Doc
    {
        [JsonPropertyName("exclusions")] public List<Exclusion>? Exclusions { get; set; }
    }

    private static readonly JsonSerializerOptions ReadOpts = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonSerializerOptions WriteOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static List<Exclusion> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<Exclusion>();
        try
        {
            var doc = JsonSerializer.Deserialize<Doc>(json, ReadOpts);
            return doc?.Exclusions ?? new List<Exclusion>();
        }
        catch (JsonException)
        {
            return new List<Exclusion>(); // corrupt file → treat as empty (do not crash)
        }
    }

    public static string Serialize(IEnumerable<Exclusion> exclusions)
        => JsonSerializer.Serialize(new Doc { Exclusions = exclusions.ToList() }, WriteOpts);

    /// <summary>Adds or replaces the exclusion with the same fingerprint (idempotent).</summary>
    public static List<Exclusion> Upsert(IEnumerable<Exclusion> existing, Exclusion add)
    {
        var list = existing.Where(e => e.Fingerprint != add.Fingerprint).ToList();
        list.Add(add);
        return list;
    }

    public static List<Exclusion> Remove(IEnumerable<Exclusion> existing, string fingerprint)
        => existing.Where(e => e.Fingerprint != fingerprint).ToList();
}
