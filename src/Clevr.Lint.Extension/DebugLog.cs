namespace Clevr.Lint.Extension;

/// <summary>
/// Robust, ALWAYS-findable file logger for diagnostics. Writes to
/// &lt;project&gt;\.clevr-lint\clevr-lint-debug.log (or the temp directory if there is no project directory).
///
/// Reason for existence: ILogService routes to Studio Pro's internal log, which is hard to
/// find; this logger writes to a fixed, known path that we can open to
/// follow the async/marshalling chain. NEVER throws (diagnostics must not break the extension).
/// </summary>
public static class DebugLog
{
    public static string ResolvePath(string? projectDir)
    {
        var dir = !string.IsNullOrWhiteSpace(projectDir) && Directory.Exists(projectDir)
            ? Path.Combine(projectDir!, ".clevr-lint")
            : Path.Combine(Path.GetTempPath(), "clevr-lint");
        return Path.Combine(dir, "clevr-lint-debug.log");
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
            // Diagnostics must never break the extension flow.
        }
    }
}
