using YamlDotNet.RepresentationModel;

namespace Clevr.AcrSpike;

/// <summary>
/// Leest de microflow-structuur uit een modelsource-microflow-YAML, in ÉÉN YamlDotNet-pass, voor de
/// flow-AST-route. Gekozen boven 471× bson-dump (~13 min): de YAML bevat de volledige getypeerde
/// flow-AST (ObjectCollection.Objects met geneste LoopedActivity/ExclusiveSplit/Action), en YamlDotNet
/// ontquote single/double/block-scalars correct.
///
/// Levert de primitieven die de pure regels (ExpressionRules + MicroflowStructureRules) nodig hebben:
///   - Expressions      : split-condities + change-values (REL-001, MAINT-006)
///   - TopLevelObjectCount : count(root.ObjectCollection.Objects) (MAINT-007 = mxlint 005_0003)
///   - top-level type-tellingen ActionActivity/ExclusiveSplit/Annotation (MAINT-008 = 005_0004)
///   - ExclusiveSplits  : (Caption, SplitCondition.Expression) per top-level split (MAINT-009 = 005_0005)
///   - InLoopActions    : (Action.$Type, Action.Commit) van acties BINNEN een LoopedActivity (PERF, 005_0002)
/// Tolerant: corrupte YAML → lege/0-info.
/// </summary>
public static class MicroflowYamlExpressions
{
    private static readonly YamlScalarNode TypeKey = new("$Type");
    private static readonly YamlScalarNode ExpressionKey = new("Expression");
    private static readonly YamlScalarNode ValueKey = new("Value");
    private static readonly YamlScalarNode ObjectCollectionKey = new("ObjectCollection");
    private static readonly YamlScalarNode ObjectsKey = new("Objects");
    private static readonly YamlScalarNode CaptionKey = new("Caption");
    private static readonly YamlScalarNode SplitConditionKey = new("SplitCondition");
    private static readonly YamlScalarNode ActionKey = new("Action");
    private static readonly YamlScalarNode CommitKey = new("Commit");

    public sealed record MicroflowYamlInfo(
        IReadOnlyList<string> Expressions,
        int TopLevelObjectCount,
        int ActionActivityCount,
        int ExclusiveSplitCount,
        int AnnotationCount,
        IReadOnlyList<(string Caption, string Expression)> ExclusiveSplits,
        IReadOnlyList<(string ActionType, string? Commit)> InLoopActions,
        // Elke scalar-waarde met SLEUTEL "Expression", ongeacht $Type (split-condities + filter/find/
        // aggregate-expressies). Faithful aan de mxlint 005_0001-walk (last(path)=="Expression"); NIET
        // hetzelfde als Expressions (dat is split-condities + change-Values).
        IReadOnlyList<string> ExpressionKeyedValues);

    private static readonly MicroflowYamlInfo Empty =
        new(Array.Empty<string>(), 0, 0, 0, 0, Array.Empty<(string, string)>(), Array.Empty<(string, string?)>(), Array.Empty<string>());

    /// <summary>Parseert de microflow-YAML één keer en levert alle structuur-primitieven.</summary>
    public static MicroflowYamlInfo Parse(string yamlText)
    {
        if (string.IsNullOrWhiteSpace(yamlText)) return Empty;
        YamlStream stream = new();
        try { stream.Load(new StringReader(yamlText)); }
        catch { return Empty; } // corrupte YAML → niets

        var expressions = new List<string>();
        var expressionKeyed = new List<string>();
        foreach (var doc in stream.Documents)
        {
            WalkExpressions(doc.RootNode, expressions);
            WalkExpressionKeyed(doc.RootNode, expressionKeyed);
        }

        var splits = new List<(string Caption, string Expression)>();
        var inLoop = new List<(string ActionType, string? Commit)>();
        int objectCount = 0, actionActivity = 0, exclusiveSplit = 0, annotation = 0;

        if (stream.Documents.Count > 0 && stream.Documents[0].RootNode is YamlMappingNode root)
        {
            // Top-level objecten (NIET-recursief — exact zoals de mxlint .rego's count(input.ObjectCollection.Objects)).
            if (TopLevelObjects(root) is YamlSequenceNode top)
            {
                objectCount = top.Children.Count;
                foreach (var item in top.Children)
                {
                    if (item is not YamlMappingNode im) continue;
                    switch (Scalar(im, TypeKey))
                    {
                        case "Microflows$ActionActivity": actionActivity++; break;
                        case "Microflows$Annotation": annotation++; break;
                        case "Microflows$ExclusiveSplit":
                            exclusiveSplit++;
                            if (im.Children.TryGetValue(SplitConditionKey, out var sc) && sc is YamlMappingNode scm)
                            {
                                var expr = Scalar(scm, ExpressionKey);
                                if (expr != null) splits.Add((Scalar(im, CaptionKey) ?? "", expr));
                            }
                            break;
                    }
                }
            }
            // Acties BINNEN een loop (recursief): zodra we onder een LoopedActivity zitten, verzamel
            // van elke ActionActivity de Action.$Type + Action.Commit (de pure regel beslist de commit-conditie).
            WalkInLoop(root, insideLoop: false, inLoop);
        }

        return new MicroflowYamlInfo(expressions, objectCount, actionActivity, exclusiveSplit, annotation, splits, inLoop, expressionKeyed);
    }

    // ---- wrappers (bestaande call-sites) -------------------------------------------------------
    public static IReadOnlyList<string> Extract(string yamlText) => Parse(yamlText).Expressions;
    public static int CountTopLevelObjects(string yamlText) => Parse(yamlText).TopLevelObjectCount;

    // ---- walks ---------------------------------------------------------------------------------

    /// <summary>
    /// Recursief: ELKE scalar-waarde met sleutel "Expression" (ongeacht $Type). Faithful aan de
    /// mxlint 005_0001-walk (last(path)=="Expression"). Voor REL-002 (incomplete empty-string-check).
    /// </summary>
    private static void WalkExpressionKeyed(YamlNode node, List<string> result)
    {
        switch (node)
        {
            case YamlMappingNode map:
                foreach (var kv in map.Children)
                {
                    if (kv.Key is YamlScalarNode k && k.Value == "Expression"
                        && kv.Value is YamlScalarNode s && !string.IsNullOrEmpty(s.Value))
                        result.Add(s.Value!);
                    WalkExpressionKeyed(kv.Value, result);
                }
                break;
            case YamlSequenceNode seq:
                foreach (var item in seq.Children) WalkExpressionKeyed(item, result);
                break;
        }
    }

    /// <summary>Recursief: split-condities (Expression) + change-values (Value). Ongewijzigd gedrag.</summary>
    private static void WalkExpressions(YamlNode node, List<string> result)
    {
        switch (node)
        {
            case YamlMappingNode map:
                var type = Scalar(map, TypeKey);
                if (type == "Microflows$ExpressionSplitCondition")
                {
                    var e = Scalar(map, ExpressionKey);
                    if (!string.IsNullOrWhiteSpace(e)) result.Add(e!);
                }
                else if (type == "Microflows$ChangeActionItem")
                {
                    var v = Scalar(map, ValueKey);
                    if (!string.IsNullOrWhiteSpace(v)) result.Add(v!);
                }
                foreach (var child in map.Children.Values) WalkExpressions(child, result);
                break;

            case YamlSequenceNode seq:
                foreach (var item in seq.Children) WalkExpressions(item, result);
                break;
        }
    }

    /// <summary>
    /// Recursief: verzamelt van elke ActionActivity BINNEN een LoopedActivity de (Action.$Type, Action.Commit).
    /// insideLoop wordt true zodra we een LoopedActivity binnengaan (en blijft true voor geneste loops).
    /// </summary>
    private static void WalkInLoop(YamlNode node, bool insideLoop, List<(string ActionType, string? Commit)> result)
    {
        switch (node)
        {
            case YamlMappingNode map:
                if (insideLoop && map.Children.TryGetValue(ActionKey, out var act) && act is YamlMappingNode am)
                {
                    var at = Scalar(am, TypeKey);
                    if (at != null) result.Add((at, Scalar(am, CommitKey)));
                }
                var nowInLoop = insideLoop || Scalar(map, TypeKey) == "Microflows$LoopedActivity";
                foreach (var child in map.Children.Values) WalkInLoop(child, nowInLoop, result);
                break;

            case YamlSequenceNode seq:
                foreach (var item in seq.Children) WalkInLoop(item, insideLoop, result);
                break;
        }
    }

    private static YamlSequenceNode? TopLevelObjects(YamlMappingNode root)
        => root.Children.TryGetValue(ObjectCollectionKey, out var oc) && oc is YamlMappingNode ocm
        && ocm.Children.TryGetValue(ObjectsKey, out var objs) && objs is YamlSequenceNode seq ? seq : null;

    private static string? Scalar(YamlMappingNode map, YamlScalarNode key)
        => map.Children.TryGetValue(key, out var v) && v is YamlScalarNode s ? s.Value : null;
}
