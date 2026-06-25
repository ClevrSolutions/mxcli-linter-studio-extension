using Clevr.Lint.Normalizer;

namespace Clevr.Lint.Extension;

/// <summary>
/// Reading/writing of <c>$project/.clevr-lint/manual-checks.json</c> — mirrors
/// <see cref="ExclusionStore"/>. Stores only ANSWERS (id → yes/no + explanation +
/// who + date); the questions themselves live in the render layer. Resides IN the project folder → is
/// included in version control (NOT in .gitignore), so the team shares the answer.
/// </summary>
public sealed class ManualCheckStore
{
    private const string Dir = ".clevr-lint";
    private const string File_ = "manual-checks.json";

    private static string PathFor(string projectDir) => Path.Combine(projectDir, Dir, File_);

    public string LoadJson(string? projectDir) => ManualChecksJson.Serialize(Load(projectDir));

    public List<ManualCheckAnswer> Load(string? projectDir)
    {
        if (string.IsNullOrWhiteSpace(projectDir)) return new List<ManualCheckAnswer>();
        var path = PathFor(projectDir);
        return File.Exists(path) ? ManualChecksJson.Parse(File.ReadAllText(path)) : new List<ManualCheckAnswer>();
    }

    /// <summary>Records an answer (upsert by id) and writes it to disk.</summary>
    public void Answer(string? projectDir, ManualCheckAnswer answer)
    {
        if (string.IsNullOrWhiteSpace(projectDir))
            throw new InvalidOperationException("No project folder available to save the answer.");
        Save(projectDir!, ManualChecksJson.Upsert(Load(projectDir), answer));
    }

    /// <summary>Clears the answer of a check (so it becomes unanswered again).</summary>
    public void Clear(string? projectDir, string id)
    {
        if (string.IsNullOrWhiteSpace(projectDir))
            throw new InvalidOperationException("No project folder available to clear the answer.");
        Save(projectDir!, ManualChecksJson.Remove(Load(projectDir), id));
    }

    private static void Save(string projectDir, IEnumerable<ManualCheckAnswer> answers)
    {
        var dir = Path.Combine(projectDir, Dir);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, File_), ManualChecksJson.Serialize(answers));
    }
}
