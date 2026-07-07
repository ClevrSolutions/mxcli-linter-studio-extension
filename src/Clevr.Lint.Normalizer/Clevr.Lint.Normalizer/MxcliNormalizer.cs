namespace Clevr.Lint.Normalizer;

/// <summary>
/// Pure normalizer: converts raw mxcli violations to the normalized Violation format.
/// No process invocation, no UI, no file IO — only the mapping.
/// Which rules/modules are reported is mxcli's own concern (it reads lint-config.yaml
/// from the project directory directly); this class never filters.
/// </summary>
public static class MxcliNormalizer
{
    public static IReadOnlyList<Violation> Normalize(IEnumerable<MxcliViolation> rawViolations)
    {
        ArgumentNullException.ThrowIfNull(rawViolations);

        var result = new List<Violation>();

        foreach (var raw in rawViolations)
        {
            // A violation with no rule id can't be matched to the rules catalog, can't be
            // categorized (DeriveCategory would return ""), and produces a fingerprint that no
            // exclusion could ever meaningfully target. mxcli should never emit this, but the
            // envelope is untrusted (explicit JSON `null` coalesces to "" in MxcliViolation) —
            // skip rather than surface a garbage "Other"/"" category entry.
            if (raw.RuleId.Length == 0) continue;

            var documentQualifiedName = BuildDocumentQualifiedName(raw);
            var documentType = DocumentTypeCanonicalizer.Canonicalize(raw.DocumentType);
            var documentId = NullIfBlank(raw.DocumentId);
            var suggestion = NullIfBlank(raw.Suggestion);

            var elementName = NullIfBlank(raw.Element) ?? "";

            result.Add(new Violation
            {
                RuleId = raw.RuleId,
                Kind = ViolationKind.Mxcli,
                Category = DeriveCategory(raw.RuleId),
                Severity = raw.Severity,
                DocumentType = documentType,
                DocumentQualifiedName = documentQualifiedName,
                ElementName = elementName,
                Reason = raw.Message,
                Suggestion = suggestion,
                DocumentId = documentId,
                Fingerprint = Fingerprint.Compute(raw.RuleId, documentQualifiedName, elementName),
            });
        }

        return result;
    }

    /// <summary>
    /// module + document → "Module.Document"; falls back to whatever is available.
    /// NOTE: some mxcli/Lint rules (e.g. ACR_UNIQ_ENT, CONV001) deliver 'document'
    /// ALREADY qualified ("Module.Entity"), while others (MPR001, SEC001) deliver 'document'
    /// BARE ("Entity"). In the first case the module must NOT be prefixed again,
    /// otherwise "Module.Module.Entity" results (and navigation, among other things, breaks).
    /// </summary>
    private static string BuildDocumentQualifiedName(MxcliViolation raw)
    {
        var module = raw.Module?.Trim() ?? "";
        var document = raw.Document?.Trim() ?? "";

        if (module.Length > 0 && document.Length > 0)
        {
            // 'document' already qualified with this module → do not double-prefix.
            // Case-INsensitive on purpose: Mendix module names are case-insensitively unique,
            // and mxcli's 'module' casing does not always match the casing inside 'document'
            // (e.g. module "sales", document "Sales.Customer"). An Ordinal compare would
            // produce the corrupted QN "sales.Sales.Customer", which changes the fingerprint
            // and silently invalidates existing exclusions. On a match, 'document' is returned
            // UNCHANGED — its casing is authoritative and must never be rewritten.
            if (document.StartsWith(module + ".", StringComparison.OrdinalIgnoreCase)) return document;
            return $"{module}.{document}";
        }
        if (document.Length > 0) return document;
        if (module.Length > 0) return module;
        return raw.DocumentId ?? ""; // last resort: documentId where useful
    }

    /// <summary>
    /// The mxcli ruleId encodes the category in its letter prefix: "MPR001" → "MPR",
    /// "CONV012" → "CONV". No letter prefix → the entire ruleId.
    /// </summary>
    private static string DeriveCategory(string ruleId)
    {
        var i = 0;
        while (i < ruleId.Length && char.IsLetter(ruleId[i])) i++;
        return i > 0 ? ruleId[..i] : ruleId;
    }

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
