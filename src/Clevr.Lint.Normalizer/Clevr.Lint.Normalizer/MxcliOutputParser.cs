using System.Text.Json;

namespace Clevr.Lint.Normalizer;

/// <summary>
/// Parses the stdout of `mxcli lint --format json` into <see cref="MxcliViolation"/>[].
/// Pure: string in, DTOs out.
///
/// mxcli first writes STATUS LINES to stdout (e.g. "Connected to: ...",
/// "Loading cached catalog...", "✓ Catalog ready") and only AFTER that the JSON. The number of
/// status lines differs per run (cache vs full build). Therefore we strip everything before
/// the JSON start: the first line that (after trim) begins with `{` or `[`.
///
/// Tolerant of the envelope: a root array, or a root object with a
/// violations array under a known property name.
/// </summary>
public static class MxcliOutputParser
{
    private static readonly string[] ArrayPropertyNames =
        { "violations", "results", "issues", "diagnostics", "findings" };

    /// <summary>
    /// True if the stdout contains a JSON envelope (a line/position that begins with <c>{</c> or <c>[</c>).
    /// Needed to avoid mxcli's EXITCODE semantics: mxcli returns exit 1 as soon as there are error-severity
    /// findings (CI convention) AND on a real error — the exit code is therefore NOT a reliable
    /// success/failure signal. The difference is in stdout: a successful run (with or without findings)
    /// produces JSON; a real error (e.g. connection error) produces EMPTY stdout + an 'Error …' line on stderr.
    /// (The fixed "vibe-coded PoC" warning is ALWAYS on stderr and must therefore NOT be an error indicator.)
    /// </summary>
    public static bool ContainsJson(string? stdout)
        => !string.IsNullOrWhiteSpace(stdout) && ExtractJson(stdout) is not null;

    public static IReadOnlyList<MxcliViolation> Parse(string stdout)
    {
        if (string.IsNullOrWhiteSpace(stdout)) return Array.Empty<MxcliViolation>();

        var json = ExtractJson(stdout);
        if (json is null) return Array.Empty<MxcliViolation>();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        JsonElement array;
        switch (root.ValueKind)
        {
            case JsonValueKind.Array:
                array = root;
                break;

            case JsonValueKind.Object:
                if (!TryFindArray(root, out array))
                    return Array.Empty<MxcliViolation>();
                break;

            default:
                return Array.Empty<MxcliViolation>();
        }

        var list = JsonSerializer.Deserialize<List<MxcliViolation>>(array.GetRawText());
        return list ?? new List<MxcliViolation>();
    }

    /// <summary>
    /// Strips the leading status lines and returns the JSON (starting from the first
    /// line that begins with `{` or `[`). Null if no JSON start is found.
    /// </summary>
    private static string? ExtractJson(string stdout)
    {
        var lines = stdout.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
                return string.Join("\n", lines[i..]);
        }

        // Fallback: first { or [ somewhere in the text (in case JSON does not start at line beginning).
        var brace = stdout.IndexOf('{');
        var bracket = stdout.IndexOf('[');
        var start = MinNonNegative(brace, bracket);
        return start >= 0 ? stdout[start..] : null;
    }

    private static int MinNonNegative(int a, int b)
    {
        if (a < 0) return b;
        if (b < 0) return a;
        return Math.Min(a, b);
    }

    private static bool TryFindArray(JsonElement obj, out JsonElement array)
    {
        foreach (var name in ArrayPropertyNames)
        {
            if (obj.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Array)
            {
                array = prop;
                return true;
            }
        }
        array = default;
        return false;
    }
}
