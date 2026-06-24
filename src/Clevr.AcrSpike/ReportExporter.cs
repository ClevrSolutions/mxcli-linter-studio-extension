using System.Diagnostics;
using System.Text;

namespace Clevr.AcrSpike;

/// <summary>
/// Schrijft het door de web-UI gegenereerde standalone HTML-rapport naar schijf.
/// De UI (render-laag) bouwt de HTML — dit is alleen het wegschrijven/openen (IO),
/// zodat de data/UI-scheiding intact blijft.
///
/// Locatie: <projectmap>\.clevr-acr\ (consistent met spec sectie 3), of de temp-map
/// als er geen projectmap is. Bestandsnaam met timestamp zodat eerdere rapporten
/// blijven staan.
/// </summary>
public static class ReportExporter
{
    public static string Write(string html, string? projectDir)
    {
        var baseDir = !string.IsNullOrWhiteSpace(projectDir) && Directory.Exists(projectDir)
            ? Path.Combine(projectDir!, ".clevr-acr")
            : Path.Combine(Path.GetTempPath(), "clevr-acr");

        Directory.CreateDirectory(baseDir);
        var fileName = $"CLEVR-ACR-report-{DateTime.Now:yyyyMMdd-HHmmss}.html";
        var path = Path.Combine(baseDir, fileName);

        File.WriteAllText(path, html, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    /// <summary>Opent het rapport in de standaardbrowser. Best-effort: faalt stil.</summary>
    public static void TryOpen(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch
        {
            // Openen is een bonus; het pad is al gemeld aan de gebruiker.
        }
    }
}
