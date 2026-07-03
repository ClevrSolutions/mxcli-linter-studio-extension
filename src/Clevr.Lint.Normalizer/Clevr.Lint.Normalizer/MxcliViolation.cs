using System.Text.Json.Serialization;

namespace Clevr.Lint.Normalizer;

/// <summary>
/// Raw mxcli violation as delivered by `mxcli lint --format json`
/// DTO for deserialization — no logic.
/// </summary>
public sealed class MxcliViolation
{
    /// <summary>Engine rule id, e.g. "MPR001". Matched against registry.engineRuleKey.</summary>
    [JsonPropertyName("ruleId")] public string RuleId { get; set; } = "";

    /// <summary>"warning" | "error" | "hint" | "info" — engine severity (for generic rules only).</summary>
    [JsonPropertyName("severity")] public string Severity { get; set; } = "";

    [JsonPropertyName("message")] public string Message { get; set; } = "";

    [JsonPropertyName("module")] public string? Module { get; set; }

    [JsonPropertyName("document")] public string? Document { get; set; }

    /// <summary>e.g. "entity" / "microflow" / ... (mxcli casing).</summary>
    [JsonPropertyName("documentType")] public string? DocumentType { get; set; }

    /// <summary>Mendix document-GUID.</summary>
    [JsonPropertyName("documentId")] public string? DocumentId { get; set; }

    [JsonPropertyName("suggestion")] public string? Suggestion { get; set; }

    /// <summary>Optional sub-element name (e.g. widget or attribute); null when not applicable.</summary>
    [JsonPropertyName("element")] public string? Element { get; set; }
}
