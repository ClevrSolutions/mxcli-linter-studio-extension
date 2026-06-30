using System.Text.Json;
using System.Text.RegularExpressions;

namespace Clevr.Lint.Normalizer;

/// <summary>
/// Pure parser for the JSON produced by `mx diff BASE MINE OUT` (Mendix mx-toolset).
/// Returns the semantically-changed elements within the describe-route scope:
/// microflow unit GUIDs (to be resolved to qualified names via CATALOG.MICROFLOWS)
/// and entity qualified names (extracted directly from the changeDescription field,
/// e.g. "Entity 'TRB.Bank'").
///
/// mx diff structure (verified against Mendix 11.10): top-level <c>unitDifferences[]</c>,
/// per unit <c>type</c> (Microflows$Microflow / DomainModels$DomainModel / …),
/// <c>id</c> (unit GUID), <c>change</c> (Modified/Added/Deleted) and nested
/// <c>changeDetails[]</c> with per-property <c>baseValue</c>/<c>mineValue</c>
/// and an <c>onlyVisualChanges</c> flag.
///
/// Rules: only Microflows$Microflow (→ GUID) and DomainModels$DomainModel → "Entity 'QN'"
/// are included (the describe route runs on microflows + entities only). Deleted elements
/// cannot be scanned and are skipped. Purely cosmetic changes (onlyVisualChanges=true
/// everywhere in the subtree, e.g. a repositioned activity) are skipped and tallied in
/// <see cref="ChangedElements.VisualOnlySkipped"/> for transparency.
///
/// Pure: JSON string in, <see cref="ChangedElements"/> out. No IO.
/// </summary>
public static class MxDiffParser
{
    private static readonly Regex EntityDesc    = new(@"^Entity '(.+)'$",               RegexOptions.Compiled);
    // "Attribute 'Module.Entity.AttrName'" — entity = everything before the last dot
    private static readonly Regex AttributeDesc = new(@"^Attribute '(.+)\.[^.']+?'$",   RegexOptions.Compiled);
    // "Access rule of entity 'Module.Entity'", "Validation rule of entity '...'" etc.
    private static readonly Regex EntityOfDesc  = new(@"of entity '([^']+)'",            RegexOptions.Compiled);

    public static ChangedElements Parse(string diffJson)
    {
        var mfIds = new List<string>();
        var entityQns = new List<string>();
        var visualSkipped = 0;

        if (string.IsNullOrWhiteSpace(diffJson)) return Empty;

        JsonDocument doc;
        try { doc = JsonDocument.Parse(diffJson); }
        catch { return Empty; }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("unitDifferences", out var units)
                || units.ValueKind != JsonValueKind.Array)
                return Empty;

            foreach (var unit in units.EnumerateArray())
            {
                var type = Str(unit, "type");
                if (string.Equals(Str(unit, "change"), "Deleted", StringComparison.OrdinalIgnoreCase))
                    continue; // deleted element — nothing to scan

                if (type == "Microflows$Microflow")
                {
                    var id = Str(unit, "id");
                    if (id.Length == 0) continue;
                    if (HasRealChange(unit)) mfIds.Add(id); else visualSkipped++;
                }
                else if (type == "DomainModels$DomainModel")
                {
                    if (!unit.TryGetProperty("changeDetails", out var details)
                        || details.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var d in details.EnumerateArray())
                    {
                        if (string.Equals(Str(d, "change"), "Deleted", StringComparison.OrdinalIgnoreCase)) continue;
                        var qn = TryEntityQn(Str(d, "changeDescription"));
                        if (qn is null) continue; // association, security config, etc. — not in describe scope
                        if (HasRealChange(d)) entityQns.Add(qn); else visualSkipped++;
                    }
                }
                // other unit types (Security, Pages, …) are outside the describe-route scope — ignored
            }
        }

        return new ChangedElements(
            mfIds.Distinct(StringComparer.Ordinal).ToList(),
            entityQns.Distinct(StringComparer.Ordinal).ToList(),
            visualSkipped);
    }

    private static readonly ChangedElements Empty =
        new(Array.Empty<string>(), Array.Empty<string>(), 0);

    /// Returns the entity qualified name from a changeDescription string, or null if the
    /// description does not reference an entity (e.g. "Association '...'").
    /// Handles: "Entity 'M.E'", "Attribute 'M.E.A'", "Access rule of entity 'M.E'", etc.
    private static string? TryEntityQn(string desc)
    {
        var m = EntityDesc.Match(desc);
        if (m.Success) return m.Groups[1].Value;

        m = AttributeDesc.Match(desc);
        if (m.Success) return m.Groups[1].Value;

        m = EntityOfDesc.Match(desc);
        if (m.Success) return m.Groups[1].Value;

        return null;
    }

    /// <summary>
    /// "Real" change = somewhere in the subtree onlyVisualChanges == false.
    /// If all flags are true → purely cosmetic → skip.
    /// If no flag is present → treat conservatively as real (over-include is safer than
    /// missing a genuine change).
    /// </summary>
    private static bool HasRealChange(JsonElement node)
    {
        var sawFlag = false;
        var anyFalse = false;
        Walk(node, ref sawFlag, ref anyFalse);
        return !sawFlag || anyFalse;
    }

    private static void Walk(JsonElement node, ref bool sawFlag, ref bool anyFalse)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in node.EnumerateObject())
            {
                if (p.NameEquals("onlyVisualChanges")
                    && p.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    sawFlag = true;
                    if (p.Value.ValueKind == JsonValueKind.False) anyFalse = true;
                }
                else
                {
                    Walk(p.Value, ref sawFlag, ref anyFalse);
                }
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in node.EnumerateArray()) Walk(e, ref sawFlag, ref anyFalse);
        }
    }

    private static string Str(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? ""
            : "";
}

/// <summary>
/// Parsed result of a single <c>mx diff</c> run: changed microflow unit GUIDs,
/// changed entity qualified names, and the number of visual-only changes skipped.
/// </summary>
public sealed record ChangedElements(
    IReadOnlyList<string> MicroflowUnitIds,
    IReadOnlyList<string> EntityQualifiedNames,
    int VisualOnlySkipped);
