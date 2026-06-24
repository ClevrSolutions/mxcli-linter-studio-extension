using Clevr.Acr.Normalizer;

namespace Clevr.AcrSpike;

/// <summary>
/// Reading/writing of <c>$project/.clevr-acr/exclusions.json</c> (spec section 3). The pure
/// (de)serialization + list-ops live in <see cref="ExclusionsJson"/>; this class only handles
/// the file IO around the project directory. The file resides IN the project directory → it is
/// included in version control, so the next developer sees the exclusions + reasons on a pull.
/// </summary>
public sealed class ExclusionStore
{
    private const string Dir = ".clevr-acr";
    private const string File_ = "exclusions.json";

    private static string PathFor(string projectDir)
        => Path.Combine(projectDir, Dir, File_);

    /// <summary>Reads the exclusions as a JSON string ({"exclusions":[...]}) for the webview.
    /// If the file/project directory is missing, returns an empty (but valid) list.</summary>
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

    /// <summary>Adds an exclusion (or replaces the one with the same fingerprint) and writes it out.</summary>
    public void Add(string? projectDir, Exclusion exclusion)
    {
        if (string.IsNullOrWhiteSpace(projectDir))
            throw new InvalidOperationException("Geen projectmap beschikbaar om de exclusion in op te slaan.");
        var list = ExclusionsJson.Upsert(Load(projectDir), exclusion);
        Save(projectDir!, list);
    }

    /// <summary>Adds multiple exclusions at once (upsert per fingerprint → dedup,
    /// one file write). For "Exclude rule": all points under a rule at once.</summary>
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

    /// <summary>Removes multiple exclusions at once (one file write). For
    /// "Remove rule exclusion": restore all entries of a rule at once.</summary>
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
