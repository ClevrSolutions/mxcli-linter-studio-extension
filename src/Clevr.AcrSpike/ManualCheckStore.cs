using Clevr.Acr.Normalizer;

namespace Clevr.AcrSpike;

/// <summary>
/// Lezen/schrijven van <c>$project/.clevr-acr/manual-checks.json</c> — spiegelt
/// <see cref="ExclusionStore"/>. Bewaart alleen ANTWOORDEN (id → ja/nee + toelichting +
/// wie + datum); de vragen zelf staan in de render-laag. Staat IN de projectmap → gaat mee
/// in version control (NIET in .gitignore), zodat het team het antwoord deelt.
/// </summary>
public sealed class ManualCheckStore
{
    private const string Dir = ".clevr-acr";
    private const string File_ = "manual-checks.json";

    private static string PathFor(string projectDir) => Path.Combine(projectDir, Dir, File_);

    public string LoadJson(string? projectDir) => ManualChecksJson.Serialize(Load(projectDir));

    public List<ManualCheckAnswer> Load(string? projectDir)
    {
        if (string.IsNullOrWhiteSpace(projectDir)) return new List<ManualCheckAnswer>();
        var path = PathFor(projectDir);
        return File.Exists(path) ? ManualChecksJson.Parse(File.ReadAllText(path)) : new List<ManualCheckAnswer>();
    }

    /// <summary>Legt een antwoord vast (upsert per id) en schrijft weg.</summary>
    public void Answer(string? projectDir, ManualCheckAnswer answer)
    {
        if (string.IsNullOrWhiteSpace(projectDir))
            throw new InvalidOperationException("Geen projectmap beschikbaar om het antwoord op te slaan.");
        Save(projectDir!, ManualChecksJson.Upsert(Load(projectDir), answer));
    }

    /// <summary>Wist het antwoord van een check (zodat 'ie weer onbeantwoord is).</summary>
    public void Clear(string? projectDir, string id)
    {
        if (string.IsNullOrWhiteSpace(projectDir))
            throw new InvalidOperationException("Geen projectmap beschikbaar om het antwoord te wissen.");
        Save(projectDir!, ManualChecksJson.Remove(Load(projectDir), id));
    }

    private static void Save(string projectDir, IEnumerable<ManualCheckAnswer> answers)
    {
        var dir = Path.Combine(projectDir, Dir);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, File_), ManualChecksJson.Serialize(answers));
    }
}
