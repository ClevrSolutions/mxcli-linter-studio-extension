using System.Text.Json.Serialization;

namespace Clevr.Acr.Normalizer;

/// <summary>
/// Ruwe mxcli-violation zoals `mxcli lint --format json` ze per stuk levert
/// (schema vastgesteld op TRB). DTO voor deserialisatie — geen logica.
/// </summary>
public sealed class MxcliViolation
{
    /// <summary>Engine-regel-id, bv. "MPR001". Wordt tegen registry.engineRuleKey gematcht.</summary>
    [JsonPropertyName("ruleId")] public string RuleId { get; set; } = "";

    /// <summary>"warning" | "error" | "hint" | "info" — engine-severity (alleen voor generiek).</summary>
    [JsonPropertyName("severity")] public string Severity { get; set; } = "";

    [JsonPropertyName("message")] public string Message { get; set; } = "";

    [JsonPropertyName("module")] public string? Module { get; set; }

    [JsonPropertyName("document")] public string? Document { get; set; }

    /// <summary>bv. "entity" / "microflow" / ... (mxcli-casing).</summary>
    [JsonPropertyName("documentType")] public string? DocumentType { get; set; }

    /// <summary>Mendix document-GUID.</summary>
    [JsonPropertyName("documentId")] public string? DocumentId { get; set; }

    [JsonPropertyName("suggestion")] public string? Suggestion { get; set; }
}
