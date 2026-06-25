using System.Text.Json.Serialization;

namespace Clevr.Lint.Normalizer;

/// <summary>Type of violation (spec section 2). Lint rule vs generic pack rule.</summary>
public enum ViolationKind
{
    /// <summary>CLEVR Lint rule: has lintCode + Lint category/severity from the registry.</summary>
    Lint,

    /// <summary>
    /// Generic best-practice rule from a native mxcli pack: own category/severity, no CLEVR registry entry.
    /// Serialises as "mxcli" (matching the TypeScript ViolationKind union) so the UI source filter works.
    /// </summary>
    [JsonStringEnumMemberName("mxcli")]
    Generic,
}

/// <summary>
/// The normalised Violation object — the data contract from spec section 2.
/// This is the ONLY form that the UI, exclusions, and the report know.
/// </summary>
public sealed record Violation
{
    /// <summary>For Lint: the CLEVR id (CLEVR-...). For generic: the engine rule id.</summary>
    public required string RuleId { get; init; }

    public required ViolationKind Kind { get; init; }

    /// <summary>Origin label: "clevr-lint" | "mxcli". Displayed in the UI.</summary>
    public required string Source { get; init; }

    /// <summary>Original Lint rule name. Only populated when Kind == Lint.</summary>
    public string? LintCode { get; init; }

    /// <summary>"star" | "rego" — for debug purposes ONLY; the UI never displays this.</summary>
    public required string Engine { get; init; }

    /// <summary>Lint: exactly one from section 1 (from registry). Generic: own engine category.</summary>
    public required string Category { get; init; }

    /// <summary>Lint: exactly one from section 1 (from registry). Generic: own engine severity.</summary>
    public required string Severity { get; init; }

    public required string DocumentType { get; init; }

    public required string DocumentQualifiedName { get; init; }

    /// <summary>Optional sub-element (widget/attribute); "" if not applicable.</summary>
    public string ElementName { get; init; } = "";

    public required string Reason { get; init; }

    public string? Suggestion { get; init; }

    /// <summary>sha1(ruleId|documentQualifiedName|elementName) — spec section 3.</summary>
    public required string Fingerprint { get; init; }

    public string? DocumentationUrl { get; init; }

    /// <summary>
    /// Optional: the Mendix document GUID from mxcli (documentId). Outside the mandatory
    /// section-2 fields, kept as a stable navigation handle ("use where useful").
    /// </summary>
    public string? DocumentId { get; init; }
}
