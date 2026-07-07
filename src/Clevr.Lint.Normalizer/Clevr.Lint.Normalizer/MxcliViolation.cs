using System.Text.Json.Serialization;

namespace Clevr.Lint.Normalizer;

/// <summary>
/// Raw mxcli violation as delivered by `mxcli lint --format json`
/// DTO for deserialization — no logic.
///
/// RuleId/Severity/Message use explicit setters that coalesce null to "" instead of the usual
/// `= ""` auto-property initializer: System.Text.Json assigns an explicit JSON `null` straight
/// to the property, bypassing the initializer, and a null RuleId later NREs in
/// MxcliNormalizer.DeriveCategory. `required`/[JsonRequired] is deliberately NOT used — a
/// missing field must still deserialize (tolerant contract), only an explicit null needs coercing.
/// </summary>
public sealed class MxcliViolation
{
    private string _ruleId = "";
    private string _severity = "";
    private string _message = "";

    /// <summary>Engine rule id, e.g. "MPR001". Matched against registry.engineRuleKey.</summary>
    [JsonPropertyName("ruleId")]
    public string RuleId { get => _ruleId; set => _ruleId = value ?? ""; }

    /// <summary>"warning" | "error" | "hint" | "info" — engine severity (for generic rules only).</summary>
    [JsonPropertyName("severity")]
    public string Severity { get => _severity; set => _severity = value ?? ""; }

    [JsonPropertyName("message")]
    public string Message { get => _message; set => _message = value ?? ""; }

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
