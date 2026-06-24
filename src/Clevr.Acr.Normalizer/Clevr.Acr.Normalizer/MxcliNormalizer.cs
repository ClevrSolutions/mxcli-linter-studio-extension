namespace Clevr.Acr.Normalizer;

/// <summary>
/// Pure normalizer (spec sectie 9, stap 3): zet ruwe mxcli-violations om naar het
/// genormaliseerde Violation-formaat (sectie 2), gestuurd door het ACR-registry.
///
/// Geen proces-aanroep, geen UI, geen bestand-IO — alleen de mapping. De ruwe input
/// en het registry worden van buiten aangereikt, zodat dit los testbaar blijft.
///
/// Mapping (sectie 4):
///  (a) raw.ruleId matcht een ACR-registry-entry (op engineRuleKey)
///        → kind=Acr: acrCode + ACR-categorie/severity UIT het registry,
///          source="clevr-acr", ruleId = de CLEVR-id.
///          De ACR-severity komt dus uit het registry, NIET uit de mxcli-severity.
///  (b) geen match (regel uit een ingeschakeld generiek pack)
///        → kind=Generic: eigen engine-categorie (prefix van de mxcli-ruleId) en de
///          mxcli-severity, source="mxcli", ruleId = de engine-regel-id, geen acrCode.
///
/// Precedentie: staat een ruleId in het registry, dan wordt 'ie ALTIJD als ACR
/// gemapt en NOOIT óók als generiek — elke ruwe violation levert precies één Violation.
/// </summary>
public sealed class MxcliNormalizer
{
    /// <summary>mxcli is de .star-engine; herkomst-label voor generieke mxcli-regels.</summary>
    public const string GenericSource = "mxcli";
    public const string MxcliEngine = "star";
    public const string AcrSource = "clevr-acr";

    public IReadOnlyList<Violation> Normalize(
        IEnumerable<MxcliViolation> rawViolations, RuleRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(rawViolations);
        ArgumentNullException.ThrowIfNull(registry);

        var result = new List<Violation>();

        foreach (var raw in rawViolations)
        {
            var documentQualifiedName = BuildDocumentQualifiedName(raw);
            const string elementName = ""; // mxcli-schema kent geen subelement
            var documentType = DocumentTypeCanonicalizer.Canonicalize(raw.DocumentType); // → canonieke PascalCase (sectie 2)
            var documentId = NullIfBlank(raw.DocumentId);
            var suggestion = NullIfBlank(raw.Suggestion);

            var acrEntry = registry.FindByEngineRuleKey(raw.RuleId);

            if (acrEntry is not null)
            {
                // (a) ACR-match. Metadata UIT het registry; severity NIET uit mxcli.
                var ruleId = acrEntry.RuleId;
                result.Add(new Violation
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
                    ElementName = elementName,
                    Reason = raw.Message,
                    Suggestion = suggestion,
                    DocumentId = documentId,
                    Fingerprint = Fingerprint.Compute(ruleId, documentQualifiedName, elementName),
                });
            }
            else
            {
                // (b) Geen match → generiek. Eigen categorie/severity behouden.
                var ruleId = raw.RuleId;
                result.Add(new Violation
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
                    ElementName = elementName,
                    Reason = raw.Message,
                    Suggestion = suggestion,
                    DocumentId = documentId,
                    Fingerprint = Fingerprint.Compute(ruleId, documentQualifiedName, elementName),
                });
            }
        }

        return result;
    }

    /// <summary>
    /// module + document → "Module.Document"; valt terug op wat beschikbaar is.
    /// LET OP: sommige mxcli/ACR-regels (bv. ACR_UNIQ_ENT, CONV001) leveren 'document'
    /// AL gekwalificeerd ("Module.Entity"), terwijl andere (MPR001, SEC001) 'document'
    /// KAAL leveren ("Entity"). In het eerste geval mag de module NIET nóg eens geprefixt
    /// worden, anders ontstaat "Module.Module.Entity" (en faalt o.a. de navigatie).
    /// </summary>
    private static string BuildDocumentQualifiedName(MxcliViolation raw)
    {
        var module = raw.Module?.Trim() ?? "";
        var document = raw.Document?.Trim() ?? "";

        if (module.Length > 0 && document.Length > 0)
        {
            // 'document' al gekwalificeerd met deze module → niet dubbel prefixen.
            if (document.StartsWith(module + ".", StringComparison.Ordinal)) return document;
            return $"{module}.{document}";
        }
        if (document.Length > 0) return document;
        if (module.Length > 0) return module;
        return raw.DocumentId ?? ""; // laatste redmiddel: documentId waar nuttig
    }

    /// <summary>
    /// De mxcli-ruleId codeert de categorie in z'n letter-prefix: "MPR001" → "MPR",
    /// "CONV012" → "CONV". Geen letter-prefix → de hele ruleId.
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
