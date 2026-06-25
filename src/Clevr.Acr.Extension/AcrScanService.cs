using System.Text.Json;
using System.Text.Json.Serialization;
using Clevr.Acr.Normalizer;
using Mendix.StudioPro.ExtensionsAPI.Services;

namespace Clevr.Acr.Extension;

/// <summary>
/// Connects the mxcli engine to the normalizer.
/// Chain: load settings + rules.json → run mxcli → parse JSON (MxcliOutputParser)
/// → normalize (MxcliNormalizer + RuleRegistry) → JSON for the webview.
///
/// Contains ONLY IO and wiring; no normalization logic.
/// </summary>
public sealed class AcrScanService
{
    private readonly IExtensionFileService _files;
    private readonly ILogService _log;

    private static readonly JsonSerializerOptions JsonOut = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public AcrScanService(IExtensionFileService files, ILogService log)
    {
        _files = files;
        _log = log;
    }

    /// <summary>
    /// Runs the mxcli scan and returns one merged JSON end result.
    /// </summary>
    public string RunScanAsJson(string? fallbackProjectDir, bool deepScan = false)
    {
        try
        {
            var (fast, error) = RunFastPhase(fallbackProjectDir);
            if (error is not null) return error;
            return SerializeScan(fast!, fast!.Violations, deepScan);
        }
        catch (Exception ex)
        {
            _log.Error("[CLEVR ACR] scan failed", ex);
            return Error(ex.Message);
        }
    }

    /// <summary>
    /// Streamed scan: emit findings as one JSON batch via <paramref name="emit"/>.
    /// </summary>
    public void RunScanStreaming(string? fallbackProjectDir, bool deepScan, Action<string> emit)
    {
        try
        {
            var (fast, error) = RunFastPhase(fallbackProjectDir);
            if (error is not null) { emit(error); return; }

            emit(SerializeBatch(fast!, fast!.Violations, deepScan, phase: "fast",
                final: true, processed: 0, total: 0, label: null, requested: 0, returned: 0));
        }
        catch (Exception ex)
        {
            _log.Error("[CLEVR ACR] streamed scan failed", ex);
            emit(Error(ex.Message));
        }
    }

    /// <summary>Result of the scan (lint + normalize + rule catalog + metadata).</summary>
    private sealed class FastPhase
    {
        public required List<Violation> Violations;
        public required IReadOnlyDictionary<string, string> RuleNames;
        public required IReadOnlyDictionary<string, string> RuleCategories;
        public required IReadOnlyList<string> AppStoreModules;
        public required string Command;
        public required string ProjectDir;
        public required string MprFileName;
        public required string MxcliPath;
        public required int ExitCode;
        public required int RawCount;
        public required string? StdErr;
    }

    private (FastPhase?, string?) RunFastPhase(string? fallbackProjectDir)
    {
        var settings = LoadSettings(fallbackProjectDir);

        var (projectDir, mprFileName, resolveError) = ResolveProject(settings.ProjectPath);
        if (resolveError is not null)
            return (null, Error(resolveError));

        DebugLog.Write(projectDir, $"=== Scan for improvements === projectDir='{projectDir}' | settings.ProjectPath='{settings.ProjectPath}' | fallback='{fallbackProjectDir}'");

        var registry = LoadRegistry();

        var arguments = $"lint -p \"{mprFileName}\" --format json";
        var commandLine = $"\"{settings.MxcliPath}\" {arguments}";
        _log.Info($"[CLEVR ACR] {commandLine}  (cwd: {projectDir})");

        var proc = ProcessRunner.Run(settings.MxcliPath, arguments, projectDir);

        if (proc.Error is not null)
            return (null, Diagnostic($"mxcli could not start: {proc.Error}", commandLine, projectDir, proc));

        if (!MxcliOutputParser.ContainsJson(proc.StdOut))
            return (null, Diagnostic($"mxcli produced no JSON result (exitcode {proc.ExitCode}) — likely a real error, not findings", commandLine, projectDir, proc));

        IReadOnlyList<MxcliViolation> raw;
        try { raw = MxcliOutputParser.Parse(proc.StdOut); }
        catch (Exception parseEx) { return (null, Diagnostic($"Could not parse mxcli JSON: {parseEx.Message}", commandLine, projectDir, proc)); }

        var violations = new MxcliNormalizer().Normalize(raw, registry).ToList();

        var catalog = LoadRuleCatalog(settings.MxcliPath, mprFileName, projectDir);
        var ruleNames = catalog.ToDictionary(kv => kv.Key, kv => kv.Value.Name, StringComparer.Ordinal);
        var ruleCategories = catalog.ToDictionary(kv => kv.Key, kv => kv.Value.Category, StringComparer.Ordinal);

        return (new FastPhase
        {
            Violations = violations,
            RuleNames = ruleNames,
            RuleCategories = ruleCategories,
            AppStoreModules = Array.Empty<string>(),
            Command = commandLine,
            ProjectDir = projectDir,
            MprFileName = mprFileName,
            MxcliPath = settings.MxcliPath,
            ExitCode = proc.ExitCode,
            RawCount = raw.Count,
            StdErr = proc.StdErr,
        }, null);
    }

    private string SerializeScan(FastPhase fast, List<Violation> violations, bool deepScan)
    {
        var payload = new
        {
            ok = true,
            command = fast.Command,
            workingDirectory = fast.ProjectDir,
            exitCode = fast.ExitCode,
            rawCount = fast.RawCount,
            violationCount = violations.Count,
            acrCount = violations.Count(v => v.Kind == ViolationKind.Acr),
            genericCount = violations.Count(v => v.Kind == ViolationKind.Generic),
            stderr = fast.StdErr,
            deepScan,
            ruleNames = fast.RuleNames,
            ruleCategories = fast.RuleCategories,
            appStoreModules = fast.AppStoreModules,
            violations,
        };
        _log.Info($"[CLEVR ACR] {fast.RawCount} raw → {violations.Count} normalized " +
                  $"({payload.acrCount} acr / {payload.genericCount} generic), exit={fast.ExitCode}");
        return JsonSerializer.Serialize(payload, JsonOut);
    }

    private string SerializeBatch(FastPhase fast, List<Violation> violations, bool deepScan, string phase,
        bool final, int processed, int total, string? label, int requested, int returned)
    {
        var isFast = phase == "fast";
        var payload = new
        {
            ok = true,
            streaming = true,
            phase,
            final,
            progress = isFast ? null : new { processed, total, label, requested, returned },
            command = isFast ? fast.Command : null,
            workingDirectory = isFast ? fast.ProjectDir : null,
            exitCode = isFast ? fast.ExitCode : (int?)null,
            rawCount = isFast ? fast.RawCount : (int?)null,
            stderr = isFast ? fast.StdErr : null,
            deepScan = isFast ? deepScan : (bool?)null,
            ruleNames = isFast ? fast.RuleNames : null,
            ruleCategories = isFast ? fast.RuleCategories : null,
            appStoreModules = isFast ? fast.AppStoreModules : null,
            violationCount = violations.Count,
            violations,
        };
        return JsonSerializer.Serialize(payload, JsonOut);
    }

    /// <summary>
    /// Resolves projectPath to (project directory, .mpr file name).
    /// </summary>
    private static (string projectDir, string mprFileName, string? error) ResolveProject(string projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            return ("", "", "No project path. Set 'projectPath' in acr-scan-settings.json or open an app in Studio Pro.");

        if (File.Exists(projectPath) && projectPath.EndsWith(".mpr", StringComparison.OrdinalIgnoreCase))
        {
            var dir = Path.GetDirectoryName(projectPath) ?? "";
            return (dir, Path.GetFileName(projectPath), null);
        }

        if (Directory.Exists(projectPath))
        {
            var mprs = Directory.GetFiles(projectPath, "*.mpr", SearchOption.TopDirectoryOnly);
            if (mprs.Length == 0)
                return ("", "", $"No .mpr file found in project directory: {projectPath}");
            if (mprs.Length > 1)
                return ("", "", $"Multiple .mpr files in {projectPath}: " +
                                $"{string.Join(", ", mprs.Select(Path.GetFileName))}. " +
                                "Set the full .mpr path in 'projectPath'.");
            return (projectPath, Path.GetFileName(mprs[0]), null);
        }

        return ("", "", $"projectPath does not exist: {projectPath}");
    }

    private AcrScanSettings LoadSettings(string? fallbackProjectDir)
    {
        var path = _files.ResolvePath("acr-scan-settings.json");
        var json = File.Exists(path) ? File.ReadAllText(path) : null;
        return AcrScanSettings.Load(json, fallbackProjectDir);
    }

    private RuleRegistry LoadRegistry()
    {
        var path = _files.ResolvePath("rules.json");
        return RuleRegistryJson.Parse(File.ReadAllText(path));
    }

    /// <summary>
    /// Retrieves ruleId → (name, mxcli category) via `mxcli lint --list-rules`. Best-effort.
    /// </summary>
    private IReadOnlyDictionary<string, MxcliRuleInfo> LoadRuleCatalog(string mxcliPath, string mprFileName, string projectDir)
    {
        try
        {
            var proc = ProcessRunner.Run(mxcliPath, $"lint -p \"{mprFileName}\" --list-rules", projectDir);
            var catalog = MxcliRulesCatalogParser.Parse(proc.StdOut);
            _log.Info($"[CLEVR ACR] {catalog.Count} rules (name+category) from --list-rules");
            return catalog;
        }
        catch (Exception ex)
        {
            _log.Warn($"[CLEVR ACR] could not load rule catalog: {ex.Message}");
            return new Dictionary<string, MxcliRuleInfo>();
        }
    }

    private const int DiagnosticOutputMaxChars = 1000;

    private static string Diagnostic(string message, string commandLine, string workingDirectory, ProcessRunner.Result proc)
    {
        var detail =
            $"{message}\n\n" +
            $"command : {commandLine}\n" +
            $"cwd     : {workingDirectory}\n" +
            $"exitCode: {proc.ExitCode}\n\n" +
            $"--- stdout (first {DiagnosticOutputMaxChars}) ---\n{Truncate(proc.StdOut, DiagnosticOutputMaxChars)}\n\n" +
            $"--- stderr (first {DiagnosticOutputMaxChars}) ---\n{Truncate(proc.StdErr, DiagnosticOutputMaxChars)}";
        return Error(detail);
    }

    private static string Error(string message) => ErrorJson(message);

    public static string ErrorJson(string message)
        => JsonSerializer.Serialize(new { ok = false, error = message }, JsonOut);

    private static string Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "(empty)";
        return s.Length <= max ? s : s[..max] + "…";
    }
}
