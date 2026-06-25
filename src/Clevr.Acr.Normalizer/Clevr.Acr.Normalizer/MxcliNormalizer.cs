namespace Clevr.Acr.Normalizer;

/// <summary>
/// Pure normalizer (spec section 9, step 3): converts raw mxcli-violations to the
/// normalized Violation format (section 2), driven by the ACR registry.
///
/// No process invocation, no UI, no file IO — only the mapping. The raw input
/// and the registry are provided from outside, so this remains independently testable.
///
/// Mapping (section 4):
///  (a) raw.ruleId matches an ACR-registry entry (on engineRuleKey)
///        → kind=Acr: acrCode + ACR category/severity FROM the registry,
///          source="clevr-acr", ruleId = the CLEVR id.
///          The ACR severity therefore comes from the registry, NOT from the mxcli severity.
///  (b) no match (rule from an enabled generic pack)
///        → kind=Generic: own engine category (prefix of the mxcli ruleId) and the
///          mxcli severity, source="mxcli", ruleId = the engine rule id, no acrCode.
///
/// Precedence: if a ruleId is in the registry, it is ALWAYS mapped as ACR
/// and NEVER also as generic — each raw violation yields exactly one Violation.
/// </summary>
public sealed class MxcliNormalizer
{
    /// <summary>mxcli is the .star engine; origin label for generic mxcli rules.</summary>
    public const string GenericSource = "mxcli";
    public const string MxcliEngine = "star";
    public const string AcrSource = "clevr-acr";

    /// <summary>
    /// Static claim table: generic mxcli rule IDs that ACR "owns" and should therefore
    /// always be suppressed. Claimed generics are filtered out whether or not the ACR
    /// claimant fires — ACR is the authoritative source for these areas, so the generic
    /// counterpart should never reach the UI.
    /// </summary>
    private static readonly HashSet<string> ClaimedGenerics = new(StringComparer.OrdinalIgnoreCase)
    {
        // Microflow complexity — owned by CLEVR-MAINT-007
        "QUAL003",
        "CONV009",
        // Entity attribute count — owned by ACR_ENT_ATTRS (CLEVR-MAINT-001)
        "DESIGN001",
    };

    public IReadOnlyList<Violation> Normalize(
        IEnumerable<MxcliViolation> rawViolations, RuleRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(rawViolations);
        ArgumentNullException.ThrowIfNull(registry);

        var result = new List<Violation>();

        foreach (var raw in rawViolations)
        {
            var documentQualifiedName = BuildDocumentQualifiedName(raw);
            var documentType = DocumentTypeCanonicalizer.Canonicalize(raw.DocumentType); // → canonical PascalCase (section 2)
            var documentId = NullIfBlank(raw.DocumentId);
            var suggestion = NullIfBlank(raw.Suggestion);

            var acrEntry = registry.FindByEngineRuleKey(raw.RuleId);

            // Skip generic rules that ACR owns (claim table) — ACR is authoritative.
            if (acrEntry is null && ClaimedGenerics.Contains(raw.RuleId))
                continue;

            result.Add(acrEntry is not null
                ? BuildAcrViolation(raw, acrEntry, documentType, documentQualifiedName, documentId, suggestion)
                : BuildGenericViolation(raw, documentType, documentQualifiedName, documentId, suggestion));
        }

        return result;
    }

    /// <summary>(a) ACR match. Metadata FROM the registry; severity NOT from mxcli.</summary>
    private static Violation BuildAcrViolation(
        MxcliViolation raw, AcrRuleEntry acrEntry,
        string documentType, string documentQualifiedName, string? documentId, string? suggestion)
    {
        var ruleId = acrEntry.RuleId;
        return new Violation
        {
            RuleId = ruleId,
            Kind = ViolationKind.Acr,
            Source = AcrSource,
            AcrCode = acrEntry.AcrCode,
            Engine = acrEntry.Engine,
            Category = acrEntry.Category,
            Severity = acrEntry.Severity,
            DocumentType = documentType,
            DocumentQualifiedName = documentQualifiedName,
            Reason = raw.Message,
            Suggestion = suggestion,
            DocumentId = documentId,
            Fingerprint = Fingerprint.Compute(ruleId, documentQualifiedName, ""),
        };
    }

    /// <summary>(b) No registry match → generic. Own category/severity retained.</summary>
    private static Violation BuildGenericViolation(
        MxcliViolation raw,
        string documentType, string documentQualifiedName, string? documentId, string? suggestion)
    {
        var ruleId = raw.RuleId;
        return new Violation
        {
            RuleId = ruleId,
            Kind = ViolationKind.Generic,
            Source = GenericSource,
            AcrCode = null,
            Engine = MxcliEngine,
            Category = DeriveGenericCategory(raw.RuleId),
            Severity = raw.Severity,
            DocumentType = documentType,
            DocumentQualifiedName = documentQualifiedName,
            Reason = raw.Message,
            Suggestion = suggestion,
            DocumentId = documentId,
            Fingerprint = Fingerprint.Compute(ruleId, documentQualifiedName, ""),
        };
    }

    /// <summary>
    /// module + document → "Module.Document"; falls back to whatever is available.
    /// NOTE: some mxcli/ACR rules (e.g. ACR_UNIQ_ENT, CONV001) deliver 'document'
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
            if (document.StartsWith(module + ".", StringComparison.Ordinal)) return document;
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
    private static string DeriveGenericCategory(string ruleId)
    {
        var i = 0;
        while (i < ruleId.Length && char.IsLetter(ruleId[i])) i++;
        return i > 0 ? ruleId[..i] : ruleId;
    }

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
