namespace Clevr.Lint.Extension;

/// <summary>Severity gate for <see cref="DebugLog"/>. Ordered least → most verbose; a call writes
/// when its level is at or below the currently configured level (e.g. Info writes when the
/// configured level is Info or Trace, but not when it's Error).</summary>
public enum LogLevel
{
    Error = 0,
    Info = 1,
    Trace = 2,
}

/// <summary>
/// Robust, ALWAYS-findable file logger for diagnostics. Writes to
/// &lt;project&gt;\.clevr-lint\clevr-lint-debug.log (or the temp directory if there is no project directory).
///
/// Reason for existence: ILogService routes to Studio Pro's internal log, which is hard to
/// find; this logger writes to a fixed, known path that we can open to
/// follow the async/marshalling chain. NEVER throws (diagnostics must not break the extension).
///
/// Default level is Error, so the log stays small until a user opts into Info/Trace from
/// Settings — see B8 in docs/review_07_07.md (unbounded growth from unconditional verbose writes).
/// </summary>
public static class DebugLog
{
    private static LogLevel _currentLevel = LogLevel.Error;

    public static void SetLevel(LogLevel level) => _currentLevel = level;

    public static LogLevel CurrentLevel => _currentLevel;

    public static bool TryParseLevel(string? value, out LogLevel level)
    {
        if (!string.IsNullOrWhiteSpace(value) && Enum.TryParse(value, ignoreCase: true, out level))
            return true;
        level = LogLevel.Error;
        return false;
    }

    public static string ResolvePath(string? projectDir)
    {
        var dir = !string.IsNullOrWhiteSpace(projectDir) && Directory.Exists(projectDir)
            ? Path.Combine(projectDir!, ".clevr-lint")
            : Path.Combine(Path.GetTempPath(), "clevr-lint");
        return Path.Combine(dir, "clevr-lint-debug.log");
    }

    public static void Write(string? projectDir, string message, LogLevel level)
    {
        if (level > _currentLevel)
            return;

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
