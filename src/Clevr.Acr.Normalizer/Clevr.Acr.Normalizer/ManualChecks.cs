using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clevr.Acr.Normalizer;

/// <summary>
/// Antwoord op een "manual check" (controlevraag die de developer zelf beantwoordt). Generiek:
/// C# kent de VRAGEN niet — alleen het antwoord per check-id. De definities (vraag/categorie/
/// severity/context) en de 30-dagen-verloop-logica leven in de render-laag. Opslag in
/// <c>$project/.clevr-acr/manual-checks.json</c> (mee in version control, NIET in .gitignore —
/// net als exclusions), zodat het team het antwoord deelt.
/// </summary>
public sealed record ManualCheckAnswer
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    /// <summary>"yes" | "no".</summary>
    [JsonPropertyName("answer")] public string Answer { get; init; } = "";
    /// <summary>Verplichte toelichting (ja: wat gedaan/afgewogen) of reden (nee: waarom nog niet).</summary>
    [JsonPropertyName("note")] public string Note { get; init; } = "";
    [JsonPropertyName("answeredBy")] public string AnsweredBy { get; init; } = "";
    [JsonPropertyName("date")] public string Date { get; init; } = "";
}

/// <summary>Pure (de)serialisatie + lijst-ops voor manual-checks.json. Geen IO (zie ManualCheckStore).</summary>
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
            return new List<ManualCheckAnswer>(); // corrupt → leeg (niet crashen)
        }
    }

    public static string Serialize(IEnumerable<ManualCheckAnswer> answers)
        => JsonSerializer.Serialize(new Doc { Answers = answers.ToList() }, WriteOpts);

    /// <summary>Voegt toe of vervangt het antwoord met hetzelfde id (één antwoord per check).</summary>
    public static List<ManualCheckAnswer> Upsert(IEnumerable<ManualCheckAnswer> existing, ManualCheckAnswer add)
    {
        var list = existing.Where(a => a.Id != add.Id).ToList();
        list.Add(add);
        return list;
    }

    public static List<ManualCheckAnswer> Remove(IEnumerable<ManualCheckAnswer> existing, string id)
        => existing.Where(a => a.Id != id).ToList();
}
