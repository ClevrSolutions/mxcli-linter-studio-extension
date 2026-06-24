using System.Text.Json;
using System.Text.Json.Nodes;

namespace Clevr.Acr.Normalizer;

/// <summary>
/// PoC "derde weg" (kompas Fase 7): één eigen, DETERMINISTISCHE flow-regel rechtstreeks op de
/// getypeerde microflow-AST van <c>mxcli bson dump --type microflow --object &lt;naam&gt; --format json</c>.
/// Regel: "commit-in-loop" — een CommitAction (of een Change/Create-action met Commit ≠ "No")
/// die BINNEN een LoopedActivity zit. Dat commit per iteratie i.p.v. één keer na de loop.
///
/// BSON-JSON-vorm (vastgesteld op TRB): een "node" = JSON-array van { "Key", "Value" }-paren;
/// het type staat in de "$Type"-pair. Een collectie-property (bv. "Objects") is
/// <c>[ markerInt, [node], [node], ... ]</c>: eerste element is een marker-getal, daarna elk een
/// node. Een enkelvoudige node-property (bv. "Action") is direct een array van paren.
/// Structuur: Microflow → ObjectCollection (MicroflowObjectCollection) → Objects → activiteiten;
/// een LoopedActivity heeft zélf een ObjectCollection → Objects = de loop-body (recursief).
///
/// Puur: JSON in, Violation[] uit. Geen IO. De microflow-qualified-name komt van buiten.
/// </summary>
public static class BsonMicroflowParser
{
    public const string RuleId = "CLEVR-PERF-COMMIT-IN-LOOP";
    public const string AcrCode = "CommitInLoop";

    public static IReadOnlyList<Violation> DetectCommitInLoop(string microflowBsonJson, string documentQualifiedName)
    {
        var result = new List<Violation>();
        if (string.IsNullOrWhiteSpace(microflowBsonJson)) return result;

        JsonNode? root;
        try { root = JsonNode.Parse(microflowBsonJson); }
        catch (JsonException) { return result; } // corrupte dump → geen violations (niet crashen)
        if (root is not JsonArray microflow) return result;

        // Microflow → ObjectCollection → Objects = de top-level activiteiten (niet in een loop).
        var topObjects = ObjectsOf(microflow);
        foreach (var activity in topObjects)
            WalkActivity(activity, inLoop: false, documentQualifiedName, result);

        return result;
    }

    private static void WalkActivity(JsonArray activity, bool inLoop, string dqn, List<Violation> result)
    {
        switch (LocalType(activity))
        {
            case "LoopedActivity":
                // De body van de loop staat IN een loop (ook geneste loops blijven inLoop).
                foreach (var inner in ObjectsOf(activity))
                    WalkActivity(inner, inLoop: true, dqn, result);
                break;

            case "ActionActivity":
                if (!inLoop) break;
                if (GetValue(activity, "Action") is JsonArray action && IsCommitting(action))
                    result.Add(BuildViolation(action, dqn));
                break;
        }
    }

    /// <summary>CommitAction commit altijd; een Change/Create-action commit als Commit ≠ "No".</summary>
    private static bool IsCommitting(JsonArray action)
    {
        if (LocalType(action) == "CommitAction") return true;
        var commit = GetString(action, "Commit");
        return commit is not null && !commit.Equals("No", StringComparison.OrdinalIgnoreCase);
    }

    private static Violation BuildViolation(JsonArray action, string dqn)
    {
        var actionType = LocalType(action);
        var varName = GetString(action, "CommitVariableName")
                   ?? GetString(action, "ChangeVariableName")
                   ?? GetString(action, "CreateVariableName")
                   ?? GetString(action, "ResultVariableName")
                   ?? "";
        var elementName = varName;
        var on = varName.Length > 0 ? $" on '{varName}'" : "";
        return new Violation
        {
            RuleId = RuleId,
            Kind = ViolationKind.Acr,
            Source = "clevr-acr",
            AcrCode = AcrCode,
            Engine = "bson",
            Category = "Performance",
            Severity = "Major",
            DocumentType = "Microflow",
            DocumentQualifiedName = dqn,
            ElementName = elementName,
            Reason = $"Commit inside a loop ({actionType}{on}). Commit once after the loop instead.",
            Suggestion = "Move the commit out of the loop: collect the changed objects and commit the list once after the loop.",
            Fingerprint = Fingerprint.Compute(RuleId, dqn, elementName),
        };
    }

    // ---- Expressie-route: redundante empty-string-check ------------------------------------

    /// <summary>
    /// Detecteert redundante empty-string-checks in de expressies van een microflow. Plumbing:
    /// loopt de hele bson-AST af en pakt elke <c>ExpressionSplitCondition.Expression</c> (split-
    /// condities) en <c>ChangeActionItem.Value</c> (toekenningen); de pure
    /// <see cref="ExpressionAnalysis"/> bepaalt de redundantie. Eén violation per (microflow, pad)
    /// — duplicaten van dezelfde redundante check binnen de microflow worden samengevoegd.
    /// </summary>
    public static IReadOnlyList<Violation> DetectRedundantEmptyStringChecks(string microflowBsonJson, string documentQualifiedName)
        => ExpressionRules.RedundantEmptyString(
            ExtractExpressions(microflowBsonJson).Select(e => (documentQualifiedName, e)));

    /// <summary>
    /// Plumbing: haalt alle microflow-expressie-strings uit een bson-dump
    /// (ExpressionSplitCondition.Expression + ChangeActionItem.Value), ongeacht nesting. Pure
    /// alternatieve bron naast de YAML-extractie; beide voeden <see cref="ExpressionRules"/>.
    /// </summary>
    public static IReadOnlyList<string> ExtractExpressions(string microflowBsonJson)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(microflowBsonJson)) return result;
        JsonNode? root;
        try { root = JsonNode.Parse(microflowBsonJson); }
        catch (JsonException) { return result; }
        VisitNodes(root, node =>
        {
            string? expr = LocalType(node) switch
            {
                "ExpressionSplitCondition" => GetString(node, "Expression"),
                "ChangeActionItem" => GetString(node, "Value"),
                _ => null,
            };
            if (expr is not null) result.Add(expr);
        });
        return result;
    }

    /// <summary>
    /// Bezoekt recursief ELKE node (array van {Key,Value}-paren) in de bson-AST. Onderscheidt een
    /// node (eerste element is een {Key,..}-object) van een collectie (<c>[marker, node, ...]</c>)
    /// en van scalars. Zo vinden we expressie-knopen ongeacht hoe diep ze genest zitten.
    /// </summary>
    private static void VisitNodes(JsonNode? node, Action<JsonArray> onNode)
    {
        if (node is not JsonArray arr) return;
        if (arr.Count > 0 && arr[0] is JsonObject first && first.ContainsKey("Key"))
        {
            onNode(arr); // dit is een node
            foreach (var item in arr)
                if (item is JsonObject pair && pair.TryGetPropertyValue("Value", out var v))
                    VisitNodes(v, onNode);
        }
        else
        {
            foreach (var el in arr) VisitNodes(el, onNode); // collectie/array → recurse (marker-int wordt genegeerd)
        }
    }

    // ---- BSON-node-helpers ------------------------------------------------------------------

    /// <summary>De activiteiten onder node.ObjectCollection.Objects (leeg als die ontbreken).</summary>
    private static IEnumerable<JsonArray> ObjectsOf(JsonArray node)
    {
        if (GetValue(node, "ObjectCollection") is not JsonArray coll) return Array.Empty<JsonArray>();
        if (GetValue(coll, "Objects") is not JsonArray objects) return Array.Empty<JsonArray>();
        return ChildNodes(objects);
    }

    /// <summary>De waarde van een Key in een node (array van {Key,Value}-paren), of null.</summary>
    private static JsonNode? GetValue(JsonArray node, string key)
    {
        foreach (var item in node)
        {
            if (item is JsonObject pair && pair.TryGetPropertyValue("Key", out var k)
                && k is JsonValue kv && kv.TryGetValue<string>(out var ks) && ks == key)
            {
                return pair.TryGetPropertyValue("Value", out var v) ? v : null;
            }
        }
        return null;
    }

    private static string? GetString(JsonArray node, string key)
        => GetValue(node, key) is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    /// <summary>Het type na de laatste '$' van de $Type-pair (bv. "Microflows$LoopedActivity" → "LoopedActivity").</summary>
    private static string LocalType(JsonArray node)
    {
        var t = GetString(node, "$Type") ?? "";
        var i = t.LastIndexOf('$');
        return i >= 0 ? t[(i + 1)..] : t;
    }

    /// <summary>
    /// De node-elementen van een collectie-waarde <c>[ markerInt, [node], [node], ... ]</c>:
    /// alle array-elementen (de marker-int en eventuele scalars worden overgeslagen).
    /// </summary>
    private static IEnumerable<JsonArray> ChildNodes(JsonArray collection)
    {
        foreach (var element in collection)
            if (element is JsonArray childNode)
                yield return childNode;
    }
}
