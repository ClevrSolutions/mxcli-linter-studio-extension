using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clevr.Acr.Normalizer;

/// <summary>
/// Eén exclusion (spec sectie 3): een bewust onderdrukte improvement MET verplichte reden.
/// Opgeslagen in <c>$project/.clevr-acr/exclusions.json</c> (mee in version control → het
/// team deelt dezelfde exclusions). De match gebeurt op <see cref="Fingerprint"/> =
/// sha1(ruleId | documentQualifiedName | elementName) — al berekend op elke Violation.
/// De document-velden worden meebewaard zodat een STALE exclusion (geen match meer) tóch
/// leesbaar getoond kan worden.
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
/// Pure (de)serialisatie + lijst-operaties voor exclusions.json. Geen IO — het
/// extensieproject doet het lezen/schrijven (zie ExclusionStore). Tolerant: lege/ongeldige
/// input → lege lijst.
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
            return new List<Exclusion>(); // corrupt bestand → behandel als leeg (niet crashen)
        }
    }

    public static string Serialize(IEnumerable<Exclusion> exclusions)
        => JsonSerializer.Serialize(new Doc { Exclusions = exclusions.ToList() }, WriteOpts);

    /// <summary>Voegt toe of vervangt de exclusion met dezelfde fingerprint (idempotent).</summary>
    public static List<Exclusion> Upsert(IEnumerable<Exclusion> existing, Exclusion add)
    {
        var list = existing.Where(e => e.Fingerprint != add.Fingerprint).ToList();
        list.Add(add);
        return list;
    }

    public static List<Exclusion> Remove(IEnumerable<Exclusion> existing, string fingerprint)
        => existing.Where(e => e.Fingerprint != fingerprint).ToList();
}
