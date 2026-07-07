using System.Text.Json;
using System.Text.Json.Serialization;
using Clevr.Lint.Normalizer;
using Mendix.StudioPro.ExtensionsAPI.Services;

namespace Clevr.Lint.Extension;

/// <summary>
/// Connects the mxcli engine to the normalizer.
/// Chain: load settings → run mxcli → parse JSON (MxcliOutputParser)
/// → normalize (MxcliNormalizer) → filter excluded modules → JSON for the webview.
///
/// Contains ONLY IO and wiring; no normalization logic. Rule filtering is mxcli's
/// own concern (it reads lint-config.yaml from the project directory directly).
/// Module exclusion, however, needs a backstop here: project-scoped rules (e.g.
/// ARCH004, which analyzes cross-module dependencies for the whole project) attach
/// a module to their violations without being tied to scanning a document owned by
/// that module, and mxcli does not scrub those from its output even when the module
/// is excluded.
/// </summary>
public sealed class LintScanService
{
    private readonly IExtensionFileService _files;
    private readonly ILogService _log;

    internal static readonly JsonSerializerOptions JsonOut = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public LintScanService(IExtensionFileService files, ILogService log)
    {
        _files = files;
        _log = log;
    }

    /// <summary>
    /// Runs the mxcli scan and returns one merged JSON end result.
    /// </summary>
    public string RunScanAsJson(string? fallbackProjectDir)
    {
        try
        {
            var (fast, error) = RunFastPhase(fallbackProjectDir);
            if (error is not null) return error;
            return SerializeScan(fast!, fast!.Violations);
        }
        catch (Exception ex)
        {
            _log.Error("[CLEVR Lint] scan failed", ex);
            return Error(ex.Message);
        }
    }

    /// <summary>
    /// Loads the mxcli rules catalog (name + category per ruleId) without running a full scan.
    /// Returns null if the project cannot be resolved or mxcli fails.
    /// </summary>
    public (IReadOnlyDictionary<string, string> ruleNames, IReadOnlyDictionary<string, string> ruleCategories, IReadOnlyDictionary<string, string> ruleDescriptions)?
        TryLoadRulesCatalog(string? fallbackProjectDir)
    {
        try
        {
            var settings = LoadSettings(fallbackProjectDir);
            var (projectDir, mprFileName, error) = ResolveProject(settings.ProjectPath);

            var catalog = error != null
                ? LoadRuleCatalogGlobal(settings.MxcliPath)
                : LoadRuleCatalog(settings.MxcliPath, mprFileName, projectDir);

            if (catalog.Count == 0) return null;

            return (
                catalog.ToDictionary(kv => kv.Key, kv => kv.Value.Name, StringComparer.Ordinal),
                catalog.ToDictionary(kv => kv.Key, kv => kv.Value.Category, StringComparer.Ordinal),
                catalog.ToDictionary(kv => kv.Key, kv => kv.Value.Description, StringComparer.Ordinal)
            );
        }
        catch (Exception ex) { _log.Warn($"[CLEVR Lint] TryLoadRulesCatalog failed: {ex.Message}"); return null; }
    }

    /// <summary>
    /// Streamed scan: emit findings as one JSON batch via <paramref name="emit"/>.
    /// </summary>
    public void RunScanStreaming(string? fallbackProjectDir, Action<string> emit, CancellationToken ct = default)
    {
        try
        {
            var (fast, error) = RunFastPhase(fallbackProjectDir, ct);
            if (fast == null && error == null) return; // cancelled
            if (error is not null) { emit(error); return; }
            if (ct.IsCancellationRequested) return;

            emit(SerializeBatch(fast!, fast!.Violations));
        }
        catch (Exception ex)
        {
            _log.Error("[CLEVR Lint] streamed scan failed", ex);
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

    private (FastPhase?, string?) RunFastPhase(string? fallbackProjectDir, CancellationToken ct = default)
    {
        var settings = LoadSettings(fallbackProjectDir);

        var (projectDir, mprFileName, resolveError) = ResolveProject(settings.ProjectPath);
        if (resolveError is not null)
            return (null, Error(resolveError));

        DebugLog.Write(projectDir, $"=== Scan for improvements === projectDir='{projectDir}' | settings.ProjectPath='{settings.ProjectPath}' | fallback='{fallbackProjectDir}'", LogLevel.Trace);

        // mxcli reads lint-config.yaml directly from projectDir; ensure it exists
        // before the first scan so mxcli's own filtering (rules/modules) applies
        // even if the user has never opened Settings.
        new LinterConfigStore().Load(projectDir);

        var arguments = $"lint -p \"{mprFileName}\" --format json";
        var commandLine = $"\"{settings.MxcliPath}\" {arguments}";
        _log.Info($"[CLEVR Lint] {commandLine}  (cwd: {projectDir})");

        var proc = ProcessRunner.Run(settings.MxcliPath, arguments, projectDir, timeoutMs: 300_000, ct);

        if (ct.IsCancellationRequested)
            return (null, null);

        if (proc.Error is not null)
            return (null, Diagnostic($"mxcli could not start: {proc.Error}", commandLine, projectDir, proc));

        // TryParse returns false only when stdout contains no JSON at all (a real mxcli error,
        // see MxcliOutputParser.ContainsJson); malformed JSON is tolerated and yields whatever
        // violations could be salvaged.
        if (!MxcliOutputParser.TryParse(proc.StdOut, out var raw))
            return (null, Diagnostic($"mxcli produced no JSON result (exitcode {proc.ExitCode}) — likely a real error, not findings", commandLine, projectDir, proc));

        var violations = MxcliNormalizer.Normalize(raw).ToList();

        var linterConfig = new LinterConfigStore().Load(projectDir);
        if (linterConfig.ExcludedModules.Count > 0)
        {
            var excluded = new HashSet<string>(linterConfig.ExcludedModules, StringComparer.Ordinal);
            violations = violations.Where(v =>
            {
                var qn = v.DocumentQualifiedName;
                var dot = qn.IndexOf('.');
                var moduleName = dot > 0 ? qn[..dot] : qn;
                return !excluded.Contains(moduleName);
            }).ToList();
        }

        var catalog = LoadRuleCatalog(settings.MxcliPath, mprFileName, projectDir, ct);
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

    private string SerializeScan(FastPhase fast, List<Violation> violations)
    {
        var payload = new
        {
            ok = true,
            command = fast.Command,
            workingDirectory = fast.ProjectDir,
            exitCode = fast.ExitCode,
            rawCount = fast.RawCount,
            violationCount = violations.Count,
            stderr = fast.StdErr,
            ruleNames = fast.RuleNames,
            ruleCategories = fast.RuleCategories,
            appStoreModules = fast.AppStoreModules,
            violations,
        };
        _log.Info($"[CLEVR Lint] {fast.RawCount} raw → {violations.Count} normalized, exit={fast.ExitCode}");
        return JsonSerializer.Serialize(payload, JsonOut);
    }

    private string SerializeBatch(FastPhase fast, List<Violation> violations)
    {
        var payload = new
        {
            ok = true,
            streaming = true,
            phase = "fast",
            final = true,
            command = fast.Command,
            workingDirectory = fast.ProjectDir,
            exitCode = fast.ExitCode,
            rawCount = fast.RawCount,
            stderr = fast.StdErr,
            ruleNames = fast.RuleNames,
            ruleCategories = fast.RuleCategories,
            appStoreModules = fast.AppStoreModules,
            violationCount = violations.Count,
            violations,
        };
        return JsonSerializer.Serialize(payload, JsonOut);
    }

    /// <summary>
    /// Resolves projectPath to (project directory, .mpr file name).
    /// </summary>
    internal static (string projectDir, string mprFileName, string? error) ResolveProject(string projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            return ("", "", "No project path. Set 'projectPath' in lint-scan-settings.json or open an app in Studio Pro.");

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

    private LintScanSettings LoadSettings(string? fallbackProjectDir)
    {
        var path = _files.ResolvePath("lint-scan-settings.json");
        var json = File.Exists(path) ? File.ReadAllText(path) : null;
        return LintScanSettings.Load(json, fallbackProjectDir);
    }

    /// <summary>
    /// Retrieves ruleId → (name, mxcli category) via `mxcli lint --list-rules`. Best-effort.
    /// </summary>
    private IReadOnlyDictionary<string, MxcliRuleInfo> LoadRuleCatalog(string mxcliPath, string mprFileName, string projectDir, CancellationToken ct = default)
    {
        try
        {
            var proc = ProcessRunner.Run(mxcliPath, $"lint -p \"{mprFileName}\" --list-rules", projectDir, timeoutMs: 30_000, ct);
            var catalog = MxcliRulesCatalogParser.Parse(proc.StdOut);
            _log.Info($"[CLEVR Lint] {catalog.Count} rules (name+category) from --list-rules");
            DebugLog.Write(projectDir, $"[catalog] {catalog.Count} rules loaded; PH001 present={catalog.ContainsKey("PH001")}; stdout-len={proc.StdOut?.Length}", LogLevel.Trace);
            return catalog;
        }
        catch (Exception ex)
        {
            _log.Warn($"[CLEVR Lint] could not load rule catalog: {ex.Message}");
            return new Dictionary<string, MxcliRuleInfo>();
        }
    }

    private IReadOnlyDictionary<string, MxcliRuleInfo> LoadRuleCatalogGlobal(string mxcliPath, CancellationToken ct = default)
    {
        try
        {
            var proc = ProcessRunner.Run(mxcliPath, "lint --list-rules", timeoutMs: 30_000, ct: ct);
            var catalog = MxcliRulesCatalogParser.Parse(proc.StdOut);
            _log.Info($"[CLEVR Lint] {catalog.Count} rules (name+category) from --list-rules (no project)");
            return catalog;
        }
        catch (Exception ex)
        {
            _log.Warn($"[CLEVR Lint] could not load global rule catalog: {ex.Message}");
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
