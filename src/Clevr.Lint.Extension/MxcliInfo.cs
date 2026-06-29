using System.Text.Json.Serialization;

namespace Clevr.Lint.Extension;

/// <summary>Describes the resolved mxcli binary state sent to the React UI.</summary>
public sealed record MxcliInfo
{
    /// <summary>"path" | "clevrLint" | "custom" | "notFound"</summary>
    [JsonPropertyName("source")]       public string  Source       { get; init; } = "notFound";
    [JsonPropertyName("resolvedPath")] public string? ResolvedPath { get; init; }
    [JsonPropertyName("version")]      public string? Version      { get; init; }
    [JsonPropertyName("found")]        public bool    Found        { get; init; }
    /// <summary>File last-write date (yyyy-MM-dd) for clevrLint/custom sources.</summary>
    [JsonPropertyName("downloadedAt")] public string? DownloadedAt { get; init; }
}
