namespace Clevr.Acr.Normalizer;

/// <summary>
/// De expressie-route-regels: bouwen Violations uit (microflow-QN, expressie-string)-paren —
/// onafhankelijk van de BRON van die strings (bson-dump óf modelsource-YAML). Puur, geen IO.
/// De extractie (bson- of YAML-walker) levert de paren; deze klasse past de regels toe + dedupt.
/// </summary>
public static class ExpressionRules
{
    // REL-001 — redundante empty-string-check (Reliability; leunt tegen correctness).
    public const string RedundantEmptyStringRuleId = "CLEVR-REL-001";
    public const string RedundantEmptyStringAcrCode = "RedundantEmptyStringCheck";
    public const string RedundantEmptyStringEngineRuleKey = "CLEVR_REL_REDUNDANT_EMPTY_STRING";
    public const string RedundantEmptyStringCategory = "Reliability"; // ← knop voor Michel (of Maintainability)
    public const string RedundantEmptyStringSeverity = "Major";        // ← voorstel, bij te stellen

    // REL-002 — incomplete empty-string-check (mxlint 005_0001; complement van REL-001).
    // .rego-categorie "Error" → gemapt naar Reliability; severity MEDIUM → ACR Major (voorstel).
    public const string IncompleteEmptyStringRuleId = "CLEVR-REL-002";
    public const string IncompleteEmptyStringAcrCode = "EmptyStringCheckNotComplete";
    public const string IncompleteEmptyStringCategory = "Reliability";
    public const string IncompleteEmptyStringSeverity = "Major";

    // Regel D — redundante boolean-vergelijking ($x = true/false). Maintainability.
    public const string RedundantBooleanRuleId = "CLEVR-MAINT-006";
    public const string RedundantBooleanAcrCode = "RedundantBooleanComparison";
    public const string RedundantBooleanEngineRuleKey = "CLEVR_MAINT_REDUNDANT_BOOLEAN_COMPARE";
    public const string RedundantBooleanCategory = "Maintainability"; // ← bij te stellen
    public const string RedundantBooleanSeverity = "Major";            // ← voorstel, bij te stellen

    public const string Engine = "expr"; // alleen debug; de UI toont dit nooit

    /// <summary>REL-001 over een stroom (microflow, expressie)-paren. Dedup per (microflow, pad).</summary>
    public static IReadOnlyList<Violation> RedundantEmptyString(IEnumerable<(string Microflow, string Expression)> expressions)
    {
        var result = new List<Violation>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (mf, expr) in expressions)
            foreach (var path in ExpressionAnalysis.RedundantEmptyStringPaths(expr))
            {
                if (!seen.Add(mf + "|" + path)) continue;
                result.Add(new Violation
                {
                    RuleId = RedundantEmptyStringRuleId,
                    Kind = ViolationKind.Acr,
                    Source = "clevr-acr",
                    AcrCode = RedundantEmptyStringAcrCode,
                    Engine = Engine,
                    Category = RedundantEmptyStringCategory,
                    Severity = RedundantEmptyStringSeverity,
                    DocumentType = "Microflow",
                    DocumentQualifiedName = mf,
                    ElementName = path,
                    Reason = $"Redundant empty-string check on '{path}': it is compared to both empty and an empty string (''/\"\"). In Mendix an empty string already equals empty, so the empty-string comparison is redundant.",
                    Suggestion = "Keep only the '= empty' / '!= empty' check and drop the redundant '= ''' / '!= ''' comparison.",
                    Fingerprint = Fingerprint.Compute(RedundantEmptyStringRuleId, mf, path),
                });
            }
        return result;
    }

    /// <summary>Regel D over een stroom (microflow, expressie)-paren. Dedup per (microflow, operand).</summary>
    public static IReadOnlyList<Violation> RedundantBoolean(IEnumerable<(string Microflow, string Expression)> expressions)
    {
        var result = new List<Violation>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (mf, expr) in expressions)
            foreach (var operand in ExpressionAnalysis.RedundantBooleanOperands(expr))
            {
                if (!seen.Add(mf + "|" + operand)) continue;
                result.Add(new Violation
                {
                    RuleId = RedundantBooleanRuleId,
                    Kind = ViolationKind.Acr,
                    Source = "clevr-acr",
                    AcrCode = RedundantBooleanAcrCode,
                    Engine = Engine,
                    Category = RedundantBooleanCategory,
                    Severity = RedundantBooleanSeverity,
                    DocumentType = "Microflow",
                    DocumentQualifiedName = mf,
                    ElementName = operand,
                    Reason = $"Redundant boolean comparison on '{operand}': compared to the literal true/false. A boolean operand needs no '= true' / '= false' — use '{operand}' (or 'not {operand}') directly.",
                    Suggestion = "Drop the '= true' / '= false' comparison; use the boolean operand directly (negate with 'not' where needed).",
                    Fingerprint = Fingerprint.Compute(RedundantBooleanRuleId, mf, operand),
                });
            }
        return result;
    }

    /// <summary>
    /// REL-002 (mxlint 005_0001) over (microflow, "Expression"-keyed waarde)-paren. Eén violation per
    /// (microflow, expressie) met een incomplete empty-string-check. De whitespace-genormaliseerde
    /// expressie is de ElementName, zodat twee verschillende incomplete expressies in één microflow
    /// apart tellen (zoals de Rego, die per (mf, expressie) een error geeft).
    /// </summary>
    public static IReadOnlyList<Violation> IncompleteEmptyStringCheck(IEnumerable<(string Microflow, string Expression)> expressions)
    {
        var result = new List<Violation>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (mf, expr) in expressions)
        {
            if (!ExpressionAnalysis.IsIncompleteEmptyStringCheck(expr)) continue;
            var shown = System.Text.RegularExpressions.Regex.Replace(expr, @"\s+", " ").Trim();
            if (!seen.Add(mf + "|" + shown)) continue;
            result.Add(new Violation
            {
                RuleId = IncompleteEmptyStringRuleId,
                Kind = ViolationKind.Acr,
                Source = "clevr-acr",
                AcrCode = IncompleteEmptyStringAcrCode,
                Engine = Engine,
                Category = IncompleteEmptyStringCategory,
                Severity = IncompleteEmptyStringSeverity,
                DocumentType = "Microflow",
                DocumentQualifiedName = mf,
                ElementName = shown,
                Reason = $"Expression has an incomplete empty-string check: '{shown}'. It compares to '' (a truncated/zero-length string) but not to empty (database NULL); in Mendix you should check both.",
                Suggestion = "Check the string against both '!= empty' and \"!= ''\": empty covers the database NULL, '' covers a truncated/zero-length string.",
                Fingerprint = Fingerprint.Compute(IncompleteEmptyStringRuleId, mf, shown),
            });
        }
        return result;
    }
}
