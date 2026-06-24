using Clevr.Acr.Normalizer;

namespace Clevr.AcrSpike;

/// <summary>
/// Lezen/schrijven van <c>$project/.clevr-acr/exclusions.json</c> (spec sectie 3). De pure
/// (de)serialisatie + lijst-ops zitten in <see cref="ExclusionsJson"/>; deze klasse doet
/// alleen de bestand-IO rond de projectmap. Het bestand staat IN de projectmap → gaat mee in
/// version control, zodat de volgende developer de exclusions + redenen bij een pull ziet.
/// </summary>
public sealed class ExclusionStore
{
    private const string Dir = ".clevr-acr";
    private const string File_ = "exclusions.json";

    private static string PathFor(string projectDir)
        => Path.Combine(projectDir, Dir, File_);

    /// <summary>Leest de exclusions als JSON-string ({"exclusions":[...]}) voor de webview.
    /// Ontbreekt het bestand/de projectmap, dan een lege (maar geldige) lijst.</summary>
    public string LoadJson(string? projectDir)
    {
        var list = Load(projectDir);
        return ExclusionsJson.Serialize(list);
    }

    public List<Exclusion> Load(string? projectDir)
    {
        if (string.IsNullOrWhiteSpace(projectDir)) return new List<Exclusion>();
        var path = PathFor(projectDir);
        return File.Exists(path) ? ExclusionsJson.Parse(File.ReadAllText(path)) : new List<Exclusion>();
    }

    /// <summary>Voegt een exclusion toe (of vervangt die met dezelfde fingerprint) en schrijft weg.</summary>
    public void Add(string? projectDir, Exclusion exclusion)
    {
        if (string.IsNullOrWhiteSpace(projectDir))
            throw new InvalidOperationException("Geen projectmap beschikbaar om de exclusion in op te slaan.");
        var list = ExclusionsJson.Upsert(Load(projectDir), exclusion);
        Save(projectDir!, list);
    }

    /// <summary>Voegt meerdere exclusions in één keer toe (upsert per fingerprint → dedup,
    /// één bestand-write). Voor "Exclude rule": alle punten onder een regel tegelijk.</summary>
    public void AddMany(string? projectDir, IEnumerable<Exclusion> exclusions)
    {
        if (string.IsNullOrWhiteSpace(projectDir))
            throw new InvalidOperationException("Geen projectmap beschikbaar om de exclusions in op te slaan.");
        var list = Load(projectDir);
        foreach (var e in exclusions) list = ExclusionsJson.Upsert(list, e);
        Save(projectDir!, list);
    }

    public void Remove(string? projectDir, string fingerprint)
    {
        if (string.IsNullOrWhiteSpace(projectDir))
            throw new InvalidOperationException("Geen projectmap beschikbaar om de exclusion te verwijderen.");
        var list = ExclusionsJson.Remove(Load(projectDir), fingerprint);
        Save(projectDir!, list);
    }

    /// <summary>Verwijdert meerdere exclusions in één keer (één bestand-write). Voor
    /// "Remove rule exclusion": alle entries van een regel tegelijk terugzetten.</summary>
    public void RemoveMany(string? projectDir, IEnumerable<string> fingerprints)
    {
        if (string.IsNullOrWhiteSpace(projectDir))
            throw new InvalidOperationException("Geen projectmap beschikbaar om de exclusions te verwijderen.");
        var drop = new HashSet<string>(fingerprints, StringComparer.Ordinal);
        var list = Load(projectDir).Where(e => !drop.Contains(e.Fingerprint)).ToList();
        Save(projectDir!, list);
    }

    private static void Save(string projectDir, IEnumerable<Exclusion> list)
    {
        var dir = Path.Combine(projectDir, Dir);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, File_), ExclusionsJson.Serialize(list));
    }
}
