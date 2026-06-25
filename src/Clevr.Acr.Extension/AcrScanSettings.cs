using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clevr.Acr.Extension;

/// <summary>
/// Configurable scan settings (NOT hardcoded). Loaded from
/// acr-scan-settings.json in the extension directory; missing fields receive a default.
/// If projectPath is empty, the scan falls back to the directory of the opened app.
/// </summary>
public sealed class AcrScanSettings
{
    [JsonPropertyName("mxcliPath")] public string MxcliPath { get; set; } = "mxcli";
    [JsonPropertyName("projectPath")] public string ProjectPath { get; set; } = "";

    public static AcrScanSettings Load(string? settingsJson, string? fallbackProjectDir)
    {
        var settings = string.IsNullOrWhiteSpace(settingsJson)
            ? new AcrScanSettings()
            : JsonSerializer.Deserialize<AcrScanSettings>(settingsJson) ?? new AcrScanSettings();

        if (string.IsNullOrWhiteSpace(settings.MxcliPath))
            settings.MxcliPath = "mxcli"; // assumes mxcli on PATH

        if (string.IsNullOrWhiteSpace(settings.ProjectPath) && !string.IsNullOrWhiteSpace(fallbackProjectDir))
            settings.ProjectPath = fallbackProjectDir ?? "";

        return settings;
    }
}
