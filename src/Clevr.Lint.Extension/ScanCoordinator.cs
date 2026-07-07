using System.Text.Json;
using Mendix.StudioPro.ExtensionsAPI.Services;

namespace Clevr.Lint.Extension;

/// <summary>How <see cref="ScanCoordinator.RunFullScan"/> reports progress — a sequence of
/// events over time, not a single request/result, since a full scan streams mxcli batches and
/// changed-files results as they become available.</summary>
public enum ScanEventKind { Progress, Violations, UncommittedDocuments, Error, Finished }

/// <summary>One step of a full scan. <see cref="Data"/> is the raw JSON (or plain text for
/// <see cref="ScanEventKind.Progress"/>/<see cref="ScanEventKind.Error"/>) the dispatcher forwards
/// to the WebView verbatim — the coordinator does no WebView-shaped formatting.</summary>
public sealed record ScanEvent(ScanEventKind Kind, string Data)
{
    public static ScanEvent Progress(string message) => new(ScanEventKind.Progress, message);
    public static ScanEvent Violations(string json) => new(ScanEventKind.Violations, json);
    public static ScanEvent Uncommitted(string json) => new(ScanEventKind.UncommittedDocuments, json);
    public static ScanEvent Error(string message) => new(ScanEventKind.Error, message);
    public static ScanEvent Finished() => new(ScanEventKind.Finished, "");
}

/// <summary>
/// Owns the scan workflow: "RunFullScan" streams mxcli findings and a parallel changed-files
/// git diff, reported together as they complete.
/// No IWebView and no thread marshaling live here — <see cref="RunFullScan"/> reports via a
/// plain <see cref="IProgress{ScanEvent}"/>, so a unit test can assert on the emitted event
/// sequence with a fake IProgress&lt;T&gt; and no WebView2, no UI thread. The dispatcher is
/// responsible for running this on a background thread and marshaling delivery to the UI
/// thread (in practice, <see cref="Progress{ScanEvent}"/> captures the UI SynchronizationContext
/// automatically when constructed on the UI thread).
/// </summary>
public sealed class ScanCoordinator
{
    private readonly IExtensionFileService _fileService;
    private readonly ILogService _logService;
    private readonly ProjectDirResolver _projectDir;
    private readonly string? _mendixVersion;

    public ScanCoordinator(
        IExtensionFileService fileService,
        ILogService logService,
        ProjectDirResolver projectDir,
        string? mendixVersion = null)
    {
        _fileService = fileService;
        _logService = logService;
        _projectDir = projectDir;
        _mendixVersion = mendixVersion;
    }

    /// <summary>
    /// The "one button" full scan: mxcli lint (streamed in batches) plus a parallel changed-files
    /// git diff, reported to <paramref name="progress"/> as: Progress, Violations* (one per batch),
    /// UncommittedDocuments, then Error (on orchestration failure) and always Finished last.
    /// </summary>
    public void RunFullScan(IProgress<ScanEvent> progress, CancellationToken ct = default)
    {
        var projectDir = _projectDir.Resolve();
        DebugLog.Write(projectDir, $"=== Full scan (one button) started === projectDir='{projectDir}'");
        _logService.Info($"[CLEVR Lint] full scan started (projectDir='{projectDir}')");

        try
        {
            progress.Report(ScanEvent.Progress("Analyzing with mxcli…"));

            // Resolve project settings so the changed-files resolver uses the same .mpr as the scan.
            var settingsPath = _fileService.ResolvePath("lint-scan-settings.json");
            var settings = LintScanSettings.Load(
                File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : null, projectDir);
            var (resolvedDir, mprFileName, resolveErr) = LintScanService.ResolveProject(settings.ProjectPath);

            // Start changed-files detection in parallel — finishes well before mxcli does on any real project.
            var changedTask = (resolveErr == null && !string.IsNullOrEmpty(mprFileName) && !string.IsNullOrEmpty(resolvedDir))
                ? Task.Run(() => new ChangedElementsResolver(
                      settings.MxcliPath, resolvedDir, mprFileName, _logService, _mendixVersion).Resolve(ct))
                : Task.FromResult(new ChangedScanResult
                      { Status = ChangedScanStatus.Error, Message = "project not resolved" });

            try
            {
                new LintScanService(_fileService, _logService)
                    .RunScanStreaming(projectDir, batchJson => progress.Report(ScanEvent.Violations(batchJson)), ct);
            }
            catch (Exception ex)
            {
                DebugLog.Write(projectDir, $"full scan: mxcli step ERROR: {ex}");
                progress.Report(ScanEvent.Violations(LintScanService.ErrorJson($"Unexpected error during mxcli scan: {ex.Message}")));
            }

            var changedResult = changedTask.GetAwaiter().GetResult();
            DebugLog.Write(projectDir,
                $"[changed-files] status={changedResult.Status} message=\"{changedResult.Message}\" " +
                $"microflows={changedResult.Microflows.Count} entities={changedResult.Entities.Count}");
            if (changedResult.Microflows.Count > 0)
                DebugLog.Write(projectDir, $"[changed-files] microflows: {string.Join(", ", changedResult.Microflows)}");
            if (changedResult.Entities.Count > 0)
                DebugLog.Write(projectDir, $"[changed-files] entities: {string.Join(", ", changedResult.Entities)}");

            var gitPayload = JsonSerializer.Serialize(new
            {
                status         = changedResult.Status.ToString(),
                available      = changedResult.Status == ChangedScanStatus.Ok,
                qualifiedNames = changedResult.Microflows.Concat(changedResult.Entities).ToArray(),
                documentIds    = Array.Empty<string>(),
            }, LintScanService.JsonOut);
            progress.Report(ScanEvent.Uncommitted(gitPayload));

            DebugLog.Write(projectDir, "=== Full scan DONE ===");
        }
        catch (Exception ex)
        {
            DebugLog.Write(projectDir, $"Full scan ERROR (orchestration): {ex}");
            _logService.Error("[CLEVR Lint] full scan failed", ex);
            progress.Report(ScanEvent.Error(ex.Message));
        }
        finally
        {
            // ALWAYS re-enable the button + hide the spinner, regardless of outcome.
            progress.Report(ScanEvent.Finished());
        }
    }
}
