using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clevr.AcrSpike;

/// <summary>
/// Configureerbare scan-instellingen (NIET hardcoded). Geladen uit
/// acr-scan-settings.json in de extensiemap; ontbrekende velden krijgen een default.
/// Als projectPath leeg is, valt de scan terug op de map van de geopende app.
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
            settings.MxcliPath = "mxcli"; // veronderstelt mxcli op PATH

        if (string.IsNullOrWhiteSpace(settings.ProjectPath) && !string.IsNullOrWhiteSpace(fallbackProjectDir))
            settings.ProjectPath = fallbackProjectDir!;

        return settings;
    }
}
