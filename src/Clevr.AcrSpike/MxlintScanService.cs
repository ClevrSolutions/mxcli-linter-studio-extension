using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Clevr.Acr.Normalizer;
using Mendix.StudioPro.ExtensionsAPI.Services;

namespace Clevr.AcrSpike;

/// <summary>
/// Draait de mxlint EXPORT (model → <c>modelsource\</c> YAML; lokaal, geen auth) zodat de CLEVR-
/// regels (mxcli + de YAML-routes) verse modelsource lezen. Eén stap via Process.Start (zoals de open
/// mxlint-extensie, zie _reference/mxlint-extension/Core/MxLint.cs):
///   mxlint --config "&lt;project&gt;\mxlint.yaml" export
///
/// DE REGO-LINT-ENGINE IS UITGESCHAKELD ALS FINDINGS-BRON. Alle 17 mxlint.com-regels zijn
/// geïnternaliseerd als eigen CLEVR-regels; de mxlint <c>lint</c>-stap voegde niets meer toe (findings
/// werden onderdrukt of verschenen als generic). We roepen <c>lint</c> daarom niet meer aan en nemen
/// geen mxlint-Rego-findings meer op in de aggregatie. De EXPORT blijft (de binary is gedeeld; we
/// raken 'm niet aan, we laten alleen de lint-aanroep weg). De claim-tabel-onderdrukking voor de
/// mxlint-twins blijft staan (schaadt niet; de tripwire bewaakt 'm).
///
/// Bevat alleen IO/bedrading. Geeft een payload met <c>violations: []</c> terug (geen findings-bron meer).
/// </summary>
public sealed class MxlintScanService
{
    private const string DefaultCliExe = "mxlint-v3.14.2-windows-amd64.exe";

    // Vangnet: export ~60s, lint enkele seconden (na de deadlock-fix). 5 min ceiling zodat
    // een echt vastgelopen proces netjes afbreekt i.p.v. eeuwig hangt.
    private const int TimeoutMs = 300_000;

    private readonly IExtensionFileService _files;
    private readonly ILogService _log;

    private static readonly JsonSerializerOptions JsonOut = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public MxlintScanService(IExtensionFileService files, ILogService log)
    {
        _files = files;
        _log = log;
    }

    public string RunScanAsJson(string? projectDir)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(projectDir) || !Directory.Exists(projectDir))
                return Error("Geen geldige projectmap. Open een app in Studio Pro of zet 'projectPath' in acr-scan-settings.json.");

            var settings = LoadSettings(projectDir);
            var exe = !string.IsNullOrWhiteSpace(settings.MxlintPath)
                ? settings.MxlintPath
                : Path.Combine(projectDir!, ".mendix-cache", DefaultCliExe);
            var config = Path.Combine(projectDir!, "mxlint.yaml");

            // Diagnostiek: log expliciet wat we aanroepen, zodat bij een hang/fout
            // zichtbaar is welke binary/cwd/config gebruikt is en welke stap liep.
            // (Vergelijk met de werkende CLI-test: cwd = projectmap, binary in .mendix-cache.)
            _log.Info($"[CLEVR ACR] mxlint binary : {exe}");
            _log.Info($"[CLEVR ACR] mxlint cwd    : {projectDir}");
            _log.Info($"[CLEVR ACR] mxlint config : {config}");
            DebugLog.Write(projectDir, $"binary={exe}");
            DebugLog.Write(projectDir, $"cwd={projectDir}");
            DebugLog.Write(projectDir, $"config={config}");

            if (!File.Exists(exe))
                return Error($"mxlint-binary niet gevonden: {exe}\nDraai eenmalig de mxlint Studio Pro-extensie (die downloadt de CLI + rules), of zet 'mxlintPath' in acr-scan-settings.json.");
            if (!File.Exists(config))
                return Error($"mxlint.yaml niet gevonden: {config}\nDraai eenmalig de mxlint-extensie zodat config + rules aangemaakt worden.");

            var args = $"--config \"{config}\"";

            // EXPORT (lokaal; exit 0 verwacht). ~60s op een groot model. Ververst modelsource\ zodat
            // de mxcli-/YAML-route-regels daarna verse modellen lezen. De Rego-LINT-stap is bewust
            // weggelaten (engine uitgeschakeld als findings-bron — alle 17 regels zijn geïnternaliseerd).
            _log.Info("[CLEVR ACR] mxlint-fase: export GESTART (Rego-lint uitgeschakeld)");
            DebugLog.Write(projectDir, "fase: export GESTART (Rego-lint uitgeschakeld)");
            var sw = Stopwatch.StartNew();
            var export = ProcessRunner.Run(exe, $"{args} export", projectDir, TimeoutMs);
            _log.Info($"[CLEVR ACR] mxlint-fase: export KLAAR (exit {export.ExitCode}) in {sw.Elapsed.TotalSeconds:F0}s");
            DebugLog.Write(projectDir, $"fase: export KLAAR (exit {export.ExitCode}) in {sw.Elapsed.TotalSeconds:F0}s");
            if (export.Error is not null)
                return Diagnostic($"mxlint export kon niet starten: {export.Error}", $"\"{exe}\" {args} export", projectDir!, export);
            if (export.ExitCode != 0)
                return Diagnostic($"mxlint export eindigde met exitcode {export.ExitCode}", $"\"{exe}\" {args} export", projectDir!, export);

            // Geen lint → geen mxlint-Rego-findings. Lege violations-set (de UI wist daarmee elke
            // eerdere mxlint-herkomst; de CLEVR-regels leveren alle findings).
            var payload = new
            {
                ok = true,
                source = "mxlint-export",
                regoEngineDisabled = true,
                command = $"\"{exe}\" {args} export",
                workingDirectory = projectDir,
                exportExit = export.ExitCode,
                violationCount = 0,
                ruleNames = new Dictionary<string, string>(),
                violations = System.Array.Empty<Violation>(),
            };
            _log.Info($"[CLEVR ACR] mxlint: alleen export (exit {export.ExitCode}); Rego-lint uitgeschakeld → 0 findings");
            return JsonSerializer.Serialize(payload, JsonOut);
        }
        catch (Exception ex)
        {
            _log.Error("[CLEVR ACR] mxlint-scan mislukt", ex);
            return Error(ex.Message);
        }
    }

    private AcrScanSettings LoadSettings(string? fallbackProjectDir)
    {
        var path = _files.ResolvePath("acr-scan-settings.json");
        var json = File.Exists(path) ? File.ReadAllText(path) : null;
        return AcrScanSettings.Load(json, fallbackProjectDir);
    }

    private static string Diagnostic(string message, string commandLine, string workingDirectory, ProcessRunner.Result proc)
    {
        var detail =
            $"{message}\n\n" +
            $"command : {commandLine}\n" +
            $"cwd     : {workingDirectory}\n" +
            $"exitCode: {proc.ExitCode}\n\n" +
            $"--- stdout (eerste 1000) ---\n{Truncate(proc.StdOut, 1000)}\n\n" +
            $"--- stderr (eerste 1000) ---\n{Truncate(proc.StdErr, 1000)}";
        return Error(detail);
    }

    private static string Error(string message) => ErrorJson(message);

    /// <summary>Publieke fout-payload (gebruikt door de async-aanroeper bij een uitzondering).</summary>
    public static string ErrorJson(string message)
        => JsonSerializer.Serialize(new { ok = false, source = "mxlint", error = message }, JsonOut);

    private static string Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) ? "(leeg)" : (s.Length <= max ? s : s[..max] + "…");
}
