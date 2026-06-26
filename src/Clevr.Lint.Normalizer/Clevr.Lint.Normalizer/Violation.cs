using System.Text.Json.Serialization;

namespace Clevr.Lint.Normalizer;

/// <summary>
/// Violation from a native mxcli rule. Serialises as "mxcli" to match the TypeScript
/// ViolationKind union so the UI origin filter works.
/// </summary>
public enum ViolationKind
{
    [JsonStringEnumMemberName("mxcli")]
    Mxcli,
}

/// <summary>
/// The normalised Violation object. This is the ONLY form that the UI, exclusions, and
/// the report know.
/// </summary>
public sealed record Violation
{
    /// <summary>mxcli native rule id (e.g. "MPR001", "ACR_ENT_ATTRS").</summary>
    public required string RuleId { get; init; }

    public required ViolationKind Kind { get; init; }

    /// <summary>mxcli engine category derived from the rule id prefix.</summary>
    public required string Category { get; init; }

    /// <summary>mxcli severity (error / warning / info / hint).</summary>
    public required string Severity { get; init; }

    public required string DocumentType { get; init; }

    public required string DocumentQualifiedName { get; init; }

    /// <summary>Optional sub-element (widget/attribute); "" if not applicable.</summary>
    public string ElementName { get; init; } = "";

    public required string Reason { get; init; }

    public string? Suggestion { get; init; }

    /// <summary>sha1(ruleId|documentQualifiedName|elementName).</summary>
    public required string Fingerprint { get; init; }

    public string? DocumentationUrl { get; init; }

    /// <summary>Mendix document GUID from mxcli — stable navigation handle.</summary>
    public string? DocumentId { get; init; }
}
