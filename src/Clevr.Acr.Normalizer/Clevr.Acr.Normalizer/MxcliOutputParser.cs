using System.Text.Json;

namespace Clevr.Acr.Normalizer;

/// <summary>
/// Parseert de stdout van `mxcli lint --format json` naar <see cref="MxcliViolation"/>[].
/// Puur: string in, DTO's uit.
///
/// mxcli schrijft eerst STATUSREGELS naar stdout (bv. "Connected to: ...",
/// "Loading cached catalog...", "✓ Catalog ready") en pas DAARNA de JSON. Het aantal
/// statusregels verschilt per run (cache vs full build). Daarom knippen we alles vóór
/// het JSON-begin weg: de eerste regel die (na trim) met `{` of `[` begint.
///
/// Tolerant voor de envelope: een root-array, of een root-object met een
/// violations-array onder een bekende property-naam.
/// </summary>
public static class MxcliOutputParser
{
    private static readonly string[] ArrayPropertyNames =
        { "violations", "results", "issues", "diagnostics", "findings" };

    /// <summary>
    /// True als de stdout een JSON-envelope bevat (een regel/positie die met <c>{</c> of <c>[</c> begint).
    /// Nodig om mxcli's EXITCODE-semantiek te ontwijken: mxcli geeft exit 1 zodra er error-severity-
    /// findings zijn (CI-conventie) ÉN bij een echte fout — de exitcode is dus GEEN betrouwbaar
    /// succes/faal-signaal. Het verschil zit in de stdout: een geslaagde run (met of zonder findings)
    /// levert JSON; een echte fout (bv. connect-fout) levert LEGE stdout + een 'Error …'-regel op stderr.
    /// (De vaste "vibe-coded PoC"-waarschuwing staat ALTIJD op stderr en mag dus géén foutindicator zijn.)
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
    /// Knipt de leidende statusregels weg en geeft de JSON terug (vanaf de eerste
    /// regel die met `{` of `[` begint). Null als er geen JSON-begin gevonden wordt.
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

        // Fallback: eerste { of [ ergens in de tekst (mocht JSON niet aan regelstart staan).
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
