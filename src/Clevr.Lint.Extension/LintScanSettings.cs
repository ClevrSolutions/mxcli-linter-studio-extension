using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clevr.Lint.Extension;

public sealed record RuleSource
{
    [JsonPropertyName("id")]    public string Id    { get; init; } = "";
    [JsonPropertyName("url")]   public string Url   { get; init; } = "";
    [JsonPropertyName("label")] public string? Label { get; init; }
}

internal static class SettingsJson
{
    internal static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };
}

/// <summary>
/// Configurable scan settings (NOT hardcoded). Loaded from
/// lint-scan-settings.json in the extension directory; missing fields receive a default.
/// If projectPath is empty, the scan falls back to the directory of the opened app.
/// </summary>
public sealed class LintScanSettings
{
    [JsonPropertyName("mxcliPath")]    public string MxcliPath    { get; set; } = "mxcli";
    [JsonPropertyName("projectPath")]  public string ProjectPath  { get; set; } = "";
    [JsonPropertyName("ruleSources")]  public List<RuleSource> RuleSources { get; set; } = [];

    public static LintScanSettings Load(string? settingsJson, string? fallbackProjectDir)
    {
        var settings = string.IsNullOrWhiteSpace(settingsJson)
            ? new LintScanSettings()
            : JsonSerializer.Deserialize<LintScanSettings>(settingsJson) ?? new LintScanSettings();

        if (string.IsNullOrWhiteSpace(settings.MxcliPath))
            settings.MxcliPath = "mxcli"; // assumes mxcli on PATH

        if (string.IsNullOrWhiteSpace(settings.ProjectPath) && !string.IsNullOrWhiteSpace(fallbackProjectDir))
            settings.ProjectPath = fallbackProjectDir ?? "";

        settings.RuleSources ??= [];

        return settings;
    }
}
