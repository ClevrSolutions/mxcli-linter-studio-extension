using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clevr.Acr.Normalizer;

/// <summary>
/// Answer to a "manual check" (a control question answered by the developer themselves). Generic:
/// C# does not know the QUESTIONS — only the answer per check id. The definitions (question/category/
/// severity/context) and the 30-day-expiry logic live in the render layer. Stored in
/// <c>$project/.clevr-acr/manual-checks.json</c> (included in version control, NOT in .gitignore —
/// just like exclusions), so that the team shares the answer.
/// </summary>
public sealed record ManualCheckAnswer
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    /// <summary>"yes" | "no".</summary>
    [JsonPropertyName("answer")] public string Answer { get; init; } = "";
    /// <summary>Mandatory explanation (yes: what was done/considered) or reason (no: why not yet).</summary>
    [JsonPropertyName("note")] public string Note { get; init; } = "";
    [JsonPropertyName("answeredBy")] public string AnsweredBy { get; init; } = "";
    [JsonPropertyName("date")] public string Date { get; init; } = "";
}

/// <summary>Pure (de)serialization + list operations for manual-checks.json. No IO (see ManualCheckStore).</summary>
public static class ManualChecksJson
{
    private sealed class Doc
    {
        [JsonPropertyName("answers")] public List<ManualCheckAnswer>? Answers { get; set; }
    }

    private static readonly JsonSerializerOptions ReadOpts = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonSerializerOptions WriteOpts = new() { WriteIndented = true };

    public static List<ManualCheckAnswer> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<ManualCheckAnswer>();
        try
        {
            var doc = JsonSerializer.Deserialize<Doc>(json, ReadOpts);
            return doc?.Answers ?? new List<ManualCheckAnswer>();
        }
        catch (JsonException)
        {
            return new List<ManualCheckAnswer>(); // corrupt → empty (do not crash)
        }
    }

    public static string Serialize(IEnumerable<ManualCheckAnswer> answers)
        => JsonSerializer.Serialize(new Doc { Answers = answers.ToList() }, WriteOpts);

    /// <summary>Adds or replaces the answer with the same id (one answer per check).</summary>
    public static List<ManualCheckAnswer> Upsert(IEnumerable<ManualCheckAnswer> existing, ManualCheckAnswer add)
    {
        var list = existing.Where(a => a.Id != add.Id).ToList();
        list.Add(add);
        return list;
    }

    public static List<ManualCheckAnswer> Remove(IEnumerable<ManualCheckAnswer> existing, string id)
        => existing.Where(a => a.Id != id).ToList();
}
