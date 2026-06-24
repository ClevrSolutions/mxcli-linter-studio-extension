namespace Clevr.AcrSpike;

/// <summary>
/// Robuuste, ALTIJD-vindbare bestand-logger voor diagnose. Schrijft naar
/// &lt;project&gt;\.clevr-acr\clevr-acr-debug.log (of de temp-map als er geen projectmap is).
///
/// Bestaansreden: ILogService routeert naar Studio Pro's interne log, dat lastig te
/// vinden is; deze logger schrijft naar een vast, bekend pad dat we kunnen openen om
/// de async/marshalling-keten te volgen. Gooit NOOIT (diagnose mag de extensie niet breken).
/// </summary>
public static class DebugLog
{
    public static string ResolvePath(string? projectDir)
    {
        var dir = !string.IsNullOrWhiteSpace(projectDir) && Directory.Exists(projectDir)
            ? Path.Combine(projectDir!, ".clevr-acr")
            : Path.Combine(Path.GetTempPath(), "clevr-acr");
        return Path.Combine(dir, "clevr-acr-debug.log");
    }

    public static void Write(string? projectDir, string message)
    {
        try
        {
            var path = ResolvePath(projectDir);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Diagnose mag nooit de extensie-flow breken.
        }
    }
}
