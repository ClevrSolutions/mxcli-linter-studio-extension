namespace Clevr.Acr.Normalizer;

/// <summary>Soort violation (spec sectie 2). ACR-regel vs generieke pack-regel.</summary>
public enum ViolationKind
{
    /// <summary>CLEVR ACR-regel: heeft acrCode + ACR-categorie/severity uit het registry.</summary>
    Acr,

    /// <summary>Generieke best-practice-regel (mxcli): eigen categorie/severity.</summary>
    Generic,
}

/// <summary>
/// Het genormaliseerde Violation-object — het datacontract uit spec sectie 2.
/// Dit is de ENIGE vorm die de UI, exclusions en het rapport kennen.
/// </summary>
public sealed record Violation
{
    /// <summary>Voor ACR: de CLEVR-id (CLEVR-...). Voor generiek: de engine-regel-id.</summary>
    public required string RuleId { get; init; }

    public required ViolationKind Kind { get; init; }

    /// <summary>Herkomst-label: "clevr-acr" | "mxcli". Wordt in de UI getoond.</summary>
    public required string Source { get; init; }

    /// <summary>Originele ACR-rulenaam. Alleen gevuld bij Kind == Acr.</summary>
    public string? AcrCode { get; init; }

    /// <summary>"star" | "rego" — ALLEEN voor debug; de UI toont dit nooit.</summary>
    public required string Engine { get; init; }

    /// <summary>ACR: exact één uit sectie 1 (uit registry). Generiek: eigen engine-categorie.</summary>
    public required string Category { get; init; }

    /// <summary>ACR: exact één uit sectie 1 (uit registry). Generiek: eigen engine-severity.</summary>
    public required string Severity { get; init; }

    public required string DocumentType { get; init; }

    public required string DocumentQualifiedName { get; init; }

    /// <summary>Optioneel subelement (widget/attribuut); "" indien n.v.t.</summary>
    public string ElementName { get; init; } = "";

    public required string Reason { get; init; }

    public string? Suggestion { get; init; }

    /// <summary>sha1(ruleId|documentQualifiedName|elementName) — spec sectie 3.</summary>
    public required string Fingerprint { get; init; }

    public string? DocumentationUrl { get; init; }

    /// <summary>
    /// Optioneel: de Mendix document-GUID uit mxcli (documentId). Buiten de verplichte
    /// sectie-2-velden, bewaard als stabiele navigatie-handle ("gebruik waar nuttig").
    /// </summary>
    public string? DocumentId { get; init; }
}
