using System.Text.Json;
using Clevr.Lint.Normalizer;

namespace Clevr.Lint.Extension;

public sealed record BaselineEntry
{
    public required string Id { get; init; }
    public required DateTimeOffset SavedAt { get; init; }
    public string? GitRevision { get; init; }
    public required Violation[] Violations { get; init; }

    /// <summary>Modules excluded from scanning when this baseline was saved. Null/empty on baselines saved before this field existed.</summary>
    public string[]? ExcludedModules { get; init; }

    /// <summary>Rule IDs disabled when this baseline was saved. Null/empty on baselines saved before this field existed.</summary>
    public string[]? DisabledRuleIds { get; init; }
}

file sealed record BaselinesFile
{
    public List<BaselineEntry> Baselines { get; init; } = [];
}

public sealed class BaselineStore
{
    private const string Dir = ".clevr-lint";
    private const string File_ = "baselines.json";
    private const int MaxBaselines = 5;

    private static string PathFor(string projectDir)
        => Path.Combine(projectDir, Dir, File_);

    public List<BaselineEntry> Load(string? projectDir)
    {
        if (string.IsNullOrWhiteSpace(projectDir)) return [];
        var path = PathFor(projectDir);
        if (!File.Exists(path)) return [];
        try
        {
            var json = File.ReadAllText(path);
            var file = JsonSerializer.Deserialize<BaselinesFile>(json, LintScanService.JsonOut);
            return file?.Baselines ?? [];
        }
        catch (Exception ex)
        {
            DebugLog.Write(projectDir, $"baselines.json could not be parsed — treating as empty: {ex.Message}", LogLevel.Error);
            return [];
        }
    }

    public void Save(string projectDir, BaselineEntry entry)
    {
        var existing = Load(projectDir);
        var next = new List<BaselineEntry> { entry };
        next.AddRange(existing.Where(b => b.Id != entry.Id));
        if (next.Count > MaxBaselines)
            next = next.Take(MaxBaselines).ToList();
        Write(projectDir, next);
    }

    public void Delete(string projectDir, string id)
    {
        var list = Load(projectDir).Where(b => b.Id != id).ToList();
        Write(projectDir, list);
    }

    private static void Write(string projectDir, List<BaselineEntry> list)
    {
        var dir = Path.Combine(projectDir, Dir);
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, File_);
        var tmpPath = filePath + ".tmp";
        var file = new BaselinesFile { Baselines = list };
        File.WriteAllText(tmpPath, JsonSerializer.Serialize(file, LintScanService.JsonOut));
        File.Move(tmpPath, filePath, overwrite: true);
    }

    public static string? GetGitRevision(string? projectDir)
    {
        if (string.IsNullOrWhiteSpace(projectDir)) return null;
        try
        {
            var r = ProcessRunner.Run("git", $"-C \"{projectDir}\" rev-parse --short HEAD", timeoutMs: 3000);
            var rev = r.StdOut?.Trim();
            return string.IsNullOrWhiteSpace(rev) ? null : rev;
        }
        catch
        {
            return null;
        }
    }
}
