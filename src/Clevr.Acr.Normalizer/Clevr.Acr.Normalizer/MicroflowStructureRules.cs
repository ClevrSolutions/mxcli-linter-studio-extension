namespace Clevr.Acr.Normalizer;

/// <summary>
/// Microflow-STRUCTUUR-regels: bouwen Violations uit (microflow-QN, structuur-metriek)-paren —
/// onafhankelijk van de BRON (modelsource-YAML óf bson-AST). Puur, geen IO. Net als
/// <see cref="ExpressionRules"/> levert de extractie (YAML-walker) de paren; deze klasse past de
/// regel toe. Dit is de eerste regel op de flow-AST-route (geïnternaliseerde mxlint Rego-regel).
/// </summary>
public static class MicroflowStructureRules
{
    // 005_0003 NumberOfElementsInMicroflow (mxlint Rego, categorie Maintainability, severity MEDIUM).
    // Geïnternaliseerd als eigen CLEVR-regel op onze YAML-route.
    public const string NumberOfElementsRuleId = "CLEVR-MAINT-007";
    public const string NumberOfElementsAcrCode = "NumberOfElementsInMicroflow"; // originele mxlint-rulename
    public const string NumberOfElementsCategory = "Maintainability";            // letterlijk uit de .rego-metadata (al één van de zes)
    public const string NumberOfElementsSeverity = "Major";                      // mxlint MEDIUM → ACR Major (voorstel, bij te stellen)

    // 005_0004 ComplexMicroflowsWithoutAnnotations (mxlint Rego, Maintainability, MEDIUM).
    public const string ComplexRuleId = "CLEVR-MAINT-008";
    public const string ComplexAcrCode = "ComplexMicroflowsWithoutAnnotations";
    public const string ComplexCategory = "Maintainability"; // letterlijk uit de .rego (al één van de zes)
    public const string ComplexSeverity = "Major";           // mxlint MEDIUM → ACR Major (bij te stellen)

    // 005_0005 NestedIfStatements (mxlint Rego, category Complexity, MEDIUM).
    public const string NestedIfRuleId = "CLEVR-MAINT-009";
    public const string NestedIfAcrCode = "NestedIfStatements";
    public const string NestedIfCategory = "Maintainability"; // .rego-categorie "Complexity" → gemapt naar Maintainability (geen eigen ACR-categorie)
    public const string NestedIfSeverity = "Major";           // mxlint MEDIUM → ACR Major (bij te stellen)

    // 005_0002 AvoidCommitInLoop (mxlint Rego, category Microflows, MEDIUM). BEWUSTE keuze: dit is
    // dezelfde regel als de (niet live-gewirede) bson-PoC → hergebruik HETZELFDE id; de YAML-route
    // vervangt de bson-route (geen 13-min plumbing). Categorie Performance (commit-per-iteratie =
    // SQL-update per iteratie; conform de bson-PoC + de ACR-tegenhanger "Commits outside of a loop").
    public const string CommitInLoopRuleId = BsonMicroflowParser.RuleId; // "CLEVR-PERF-COMMIT-IN-LOOP"
    public const string CommitInLoopAcrCode = "AvoidCommitInLoop";
    public const string CommitInLoopCategory = "Performance";
    public const string CommitInLoopSeverity = "Major";       // mxlint MEDIUM → ACR Major (bij te stellen)

    // 005_0005-regex, VERBATIM uit de .rego (`^[\S\s]*(then|else)[\S\s]*(if)[\S\s]*$`): er staat een
    // then/else gevolgd door een latere if → een geneste if-in-expressie. [\S\s] matcht ook newlines
    // (multi-line block-scalar-condities), dus geen RegexOptions nodig.
    private static readonly System.Text.RegularExpressions.Regex NestedIfRegex =
        new(@"^[\S\s]*(then|else)[\S\s]*(if)[\S\s]*$");

    public const string Engine = "expr"; // zelfde YAML-route-familie; de UI toont dit nooit

    // De .rego telt: count(ObjectCollection.Objects) - 2  > 25.
    // De -2 is een VASTE constante (de Start- + End-event, altijd de eerste twee objecten);
    // de telling is NIET-recursief (objecten in een loop-body zitten in hun eigen geneste
    // ObjectCollection en tellen dus niet mee). Letterlijk overgenomen, niet aangenomen.
    private const int StartEndOffset = 2;
    private const int MaxElements = 25;

    /// <summary>
    /// CLEVR-MAINT-007 over een stroom (microflow-QN, top-level-ObjectCollection.Objects-aantal)-paren.
    /// Eén violation per microflow waar (aantal - 2) > 25. Reproduceert mxlint 005_0003 exact.
    /// </summary>
    public static IReadOnlyList<Violation> NumberOfElements(IEnumerable<(string Microflow, int TopLevelObjectCount)> microflows)
    {
        var result = new List<Violation>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (mf, objectCount) in microflows)
        {
            var elements = objectCount - StartEndOffset;
            if (elements <= MaxElements) continue;
            if (!seen.Add(mf)) continue; // één per microflow (defensieve dedup)
            result.Add(new Violation
            {
                RuleId = NumberOfElementsRuleId,
                Kind = ViolationKind.Acr,
                Source = "clevr-acr",
                AcrCode = NumberOfElementsAcrCode,
                Engine = Engine,
                Category = NumberOfElementsCategory,
                Severity = NumberOfElementsSeverity,
                DocumentType = "Microflow",
                DocumentQualifiedName = mf,
                ElementName = "",
                Reason = $"Microflow has {elements} elements, which is more than {MaxElements}. Large microflows are harder to maintain.",
                Suggestion = "Split microflow into logical, functional elements.",
                Fingerprint = Fingerprint.Compute(NumberOfElementsRuleId, mf, ""),
            });
        }
        return result;
    }

    /// <summary>
    /// CLEVR-MAINT-008 (mxlint 005_0004): complex (>10 ActionActivity OF >2 ExclusiveSplit, top-level)
    /// ÉN nul Annotations. Eén violation per microflow. Reproduceert de .rego AND/OR-logica exact.
    /// </summary>
    public static IReadOnlyList<Violation> ComplexWithoutAnnotations(
        IEnumerable<(string Microflow, int ActionActivityCount, int ExclusiveSplitCount, int AnnotationCount)> microflows)
    {
        var result = new List<Violation>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (mf, aa, es, ann) in microflows)
        {
            var isComplex = aa > 10 || es > 2; // .rego is_complex: OR
            if (!isComplex || ann != 0) continue;
            if (!seen.Add(mf)) continue;
            result.Add(new Violation
            {
                RuleId = ComplexRuleId,
                Kind = ViolationKind.Acr,
                Source = "clevr-acr",
                AcrCode = ComplexAcrCode,
                Engine = Engine,
                Category = ComplexCategory,
                Severity = ComplexSeverity,
                DocumentType = "Microflow",
                DocumentQualifiedName = mf,
                ElementName = "",
                Reason = $"Microflow has more than 10 activities and/or more than 2 exclusive splits ({aa} activities, {es} exclusive splits) but no annotations, which makes it hard to understand.",
                Suggestion = "Add one or more annotations to explain the microflow.",
                Fingerprint = Fingerprint.Compute(ComplexRuleId, mf, ""),
            });
        }
        return result;
    }

    /// <summary>
    /// CLEVR-MAINT-009 (mxlint 005_0005): een ExclusiveSplit waarvan de SplitCondition.Expression het
    /// geneste-if-patroon matcht (verbatim .rego-regex). Eén violation per (microflow, split-caption).
    /// </summary>
    public static IReadOnlyList<Violation> NestedIfStatements(
        IEnumerable<(string Microflow, string Caption, string Expression)> splits)
    {
        var result = new List<Violation>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (mf, caption, expr) in splits)
        {
            if (string.IsNullOrEmpty(expr) || !NestedIfRegex.IsMatch(expr)) continue;
            if (!seen.Add(mf + "|" + caption)) continue;
            result.Add(new Violation
            {
                RuleId = NestedIfRuleId,
                Kind = ViolationKind.Acr,
                Source = "clevr-acr",
                AcrCode = NestedIfAcrCode,
                Engine = Engine,
                Category = NestedIfCategory,
                Severity = NestedIfSeverity,
                DocumentType = "Microflow",
                DocumentQualifiedName = mf,
                ElementName = caption,
                Reason = $"Exclusive split '{caption}' has nested if-statements in its expression, which hides complexity and is harder to maintain.",
                Suggestion = "Simplify the expression or use exclusive splits.",
                Fingerprint = Fingerprint.Compute(NestedIfRuleId, mf, caption),
            });
        }
        return result;
    }

    /// <summary>
    /// CLEVR-PERF-COMMIT-IN-LOOP (mxlint 005_0002): een committende actie BINNEN een loop. De spike
    /// levert per microflow de (Action.$Type, Action.Commit) van alle acties die in een LoopedActivity
    /// zitten; deze regel past de .rego-conditie toe: een CommitAction, OF een ChangeAction met
    /// Commit=="Yes". Eén violation per microflow. (Vervangt de niet-gewirede bson-PoC; zelfde id.)
    /// </summary>
    public static IReadOnlyList<Violation> CommitInLoop(
        IEnumerable<(string Microflow, IReadOnlyList<(string ActionType, string? Commit)> InLoopActions)> microflows)
    {
        var result = new List<Violation>();
        foreach (var (mf, actions) in microflows)
        {
            var commits = actions.Any(a =>
                a.ActionType == "Microflows$CommitAction"
                || (a.ActionType == "Microflows$ChangeAction" && a.Commit == "Yes"));
            if (!commits) continue;
            result.Add(new Violation
            {
                RuleId = CommitInLoopRuleId,
                Kind = ViolationKind.Acr,
                Source = "clevr-acr",
                AcrCode = CommitInLoopAcrCode,
                Engine = Engine,
                Category = CommitInLoopCategory,
                Severity = CommitInLoopSeverity,
                DocumentType = "Microflow",
                DocumentQualifiedName = mf,
                ElementName = "",
                Reason = "Microflow commits an object inside a loop, which fires a SQL update query for each iteration.",
                Suggestion = "Commit outside the loop: within the loop add the objects to a list, and commit the list once after the loop.",
                Fingerprint = Fingerprint.Compute(CommitInLoopRuleId, mf, ""),
            });
        }
        return result;
    }
}
