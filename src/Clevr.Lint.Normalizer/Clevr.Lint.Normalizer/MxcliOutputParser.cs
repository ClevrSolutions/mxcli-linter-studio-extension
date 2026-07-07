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
///
/// Tolerant of malformed content too: mxcli can be killed mid-write (timeout) and leave stdout
/// ending in truncated JSON (e.g. `{"ruleId": "MPR0`), or one violation in an otherwise-valid
/// array can carry a wrong-typed field (e.g. `"severity": 3`). Neither case should discard an
/// entire scan's worth of findings, so parsing never throws: the envelope is parsed leniently
/// (unparseable → no result) and array elements are deserialized one at a time (a bad element
/// is skipped, not fatal to its siblings).
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

    /// <summary>
    /// Extracts and deserializes the violation array from mxcli stdout, never throwing.
    /// Returns an empty list when stdout has no JSON envelope, when the envelope itself is
    /// malformed/truncated, or when no element in the array deserializes cleanly.
    /// </summary>
    public static IReadOnlyList<MxcliViolation> Parse(string stdout)
    {
        TryParse(stdout, out var violations);
        return violations;
    }

    /// <summary>
    /// Combines the "does stdout contain JSON at all" check with parsing, extracting the JSON
    /// substring only once (<see cref="ContainsJson"/> followed by <see cref="Parse"/> would
    /// otherwise scan stdout for the JSON start twice per call). Returns false when no JSON
    /// envelope is present at all — callers use that to distinguish "mxcli produced no output"
    /// (a real failure) from "mxcli produced JSON, but some/all of it was malformed" (tolerated,
    /// yields whatever could be salvaged).
    /// </summary>
    public static bool TryParse(string? stdout, out IReadOnlyList<MxcliViolation> violations)
    {
        violations = Array.Empty<MxcliViolation>();

        var json = string.IsNullOrWhiteSpace(stdout) ? null : ExtractJson(stdout);
        if (json is null) return false;

        violations = ParseEnvelope(json);
        return true;
    }

    /// <summary>
    /// Parses the JSON envelope and deserializes its violation array element-by-element.
    /// Swallows JsonException at every level: a truncated/corrupt envelope yields an empty
    /// list, and an individual element that fails to deserialize (e.g. a wrong-typed field)
    /// is skipped without discarding the rest of the array.
    /// </summary>
    private static IReadOnlyList<MxcliViolation> ParseEnvelope(string json)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return Array.Empty<MxcliViolation>();
        }

        using (doc)
        {
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

            var result = new List<MxcliViolation>(array.GetArrayLength());
            foreach (var element in array.EnumerateArray())
            {
                try
                {
                    var violation = element.Deserialize<MxcliViolation>();
                    if (violation is not null) result.Add(violation);
                }
                catch (JsonException)
                {
                    // One malformed violation (e.g. "severity": 3 instead of a string) must not
                    // discard the rest of an otherwise-valid array — skip it and keep going.
                }
            }

            return result;
        }
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
