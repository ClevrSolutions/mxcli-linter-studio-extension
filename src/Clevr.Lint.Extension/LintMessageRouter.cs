using System.Text.Json;
using System.Text.Json.Nodes;
using Clevr.Lint.Normalizer;
using Mendix.StudioPro.ExtensionsAPI.Services;

namespace Clevr.Lint.Extension;

/// <summary>
/// The message→coordinator wiring shared by <see cref="DockablePaneViewModel"/> (Studio Pro)
/// and the TestHarness (dev server). Everything here is coordinator calls plus JSON translation
/// — no WebView, no HTTP, no Mendix model access — so both hosts can own one copy instead of
/// hand-duplicating the switch (see docs/review_07_07.md §2/§6, "the harness re-implements the
/// message router by hand"). The two things each host still owns are <c>RunFullScan</c>/
/// <c>CancelScan</c> (progress-reporting shape differs) and <c>OpenDocument</c>/
/// <c>RequestModules</c> (need Studio Pro's IModel/IDockingWindowService, which the harness has
/// no equivalent for).
///
/// <paramref name="post"/> is the one transport seam: the caller supplies a delegate that
/// posts a (message, data) pair back to its UI — <see cref="DockablePaneViewModel"/> wraps its
/// WebView + SynchronizationContext marshaling, the TestHarness wraps its SSE fan-out. Neither
/// host's transport differs beyond this.
/// </summary>
public sealed class LintMessageRouter(
    IExtensionFileService fileService,
    ILogService logService,
    ExclusionCoordinator exclusions,
    LinterConfigCoordinator linterConfig,
    SettingsCoordinator settings,
    BaselineStore baselines,
    ProjectDirResolver projectDirResolver,
    Func<string?> getProjectDir,
    Action<string, string> post)
{
    /// <summary>
    /// Attempts to handle <paramref name="message"/>. Returns false for the messages each host
    /// still owns itself (RunFullScan, CancelScan, OpenDocument, RequestModules,
    /// MessageListenerRegistered) so the caller's own switch can fall through to those.
    /// </summary>
    public async Task<bool> TryDispatchAsync(string message, JsonObject? data)
    {
        switch (message)
        {
            case "RequestExclusions" or "AddExclusion" or "AddExclusions" or "RemoveExclusion" or "RemoveExclusions":
                DispatchExclusion(message, data);
                return true;

            case "RequestRulesCatalog":
                await Task.Run(RequestRulesCatalog);
                return true;

            case "RequestLinterConfig":
                RequestLinterConfig();
                return true;

            case "SaveLinterConfig":
                SaveLinterConfig(data);
                return true;

            case "RequestBaselines":
                RequestBaselines();
                return true;

            case "SaveBaseline":
                SaveBaseline(data);
                return true;

            case "DeleteBaseline":
                DeleteBaseline(data);
                return true;

            case "RequestMxcliInfo" or "BrowseMxcliPath" or "SetMxcliPath" or "DownloadMxcli":
                await DispatchMxcliAsync(message, data);
                return true;

            case "RequestRuleSources" or "SaveRuleSources" or "FetchRuleSource" or "DeleteRuleSourceFiles":
                await DispatchRuleSourcesAsync(message, data);
                return true;

            case "ExportHtml":
                ExportHtml(data);
                return true;

            case "OpenUrl":
                OpenUrl(data);
                return true;

            case "RequestLogLevel":
                post("LogLevel", settings.GetLogLevel());
                return true;

            case "SetLogLevel":
            {
                var level = data?["level"]?.GetValue<string>() ?? "error";
                post("LogLevel", settings.SetLogLevel(level));
                return true;
            }

            default:
                return false;
        }
    }

    // ---- Exclusions (Phase 6, spec section 3): suppress WITH mandatory reason. Stored in
    // $project/.clevr-lint/exclusions.json (included in version control).

    private void DispatchExclusion(string message, JsonObject? data)
    {
        try
        {
            var updated = message switch
            {
                "RequestExclusions" => exclusions.List(),
                "AddExclusion" => exclusions.Add(
                    ParseExclusionRequest(data), data?["reason"]?.GetValue<string>() ?? ""),
                "AddExclusions" => exclusions.AddMany(
                    (data?["items"] as JsonArray)?.OfType<JsonObject>().Select(ParseExclusionRequest)
                        ?? Enumerable.Empty<ExclusionRequest>(),
                    data?["reason"]?.GetValue<string>() ?? ""),
                "RemoveExclusion" => exclusions.Remove(
                    data?["fingerprint"]?.GetValue<string>() ?? ""),
                "RemoveExclusions" => exclusions.RemoveMany(
                    (data?["fingerprints"] as JsonArray)?.Select(n => n?.GetValue<string>() ?? "")
                        ?? Enumerable.Empty<string>()),
                _ => throw new InvalidOperationException($"Unhandled exclusion message: {message}"),
            };
            post("Exclusions", ExclusionsJson.Serialize(updated));
        }
        catch (Exception ex)
        {
            logService.Error($"[CLEVR Lint] {message} failed", ex);
            post("ExclusionError", ex.Message);
        }
    }

    private static ExclusionRequest ParseExclusionRequest(JsonObject? data) => new(
        Fingerprint: data?["fingerprint"]?.GetValue<string>() ?? "",
        RuleId: data?["ruleId"]?.GetValue<string>() ?? "",
        DocumentQualifiedName: data?["documentQualifiedName"]?.GetValue<string>() ?? "",
        ElementName: data?["elementName"]?.GetValue<string>() ?? "");

    // ---- Rules catalog: rule names/categories/descriptions from mxcli, plus any locally
    // authored .star rule content (for the RuleInfo dialog's "view source" tab).

    private static Dictionary<string, string> LoadStarContent(string? projectDir)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(projectDir)) return result;
        var rulesDir = Path.Combine(projectDir, ".claude", "lint-rules");
        if (!Directory.Exists(rulesDir)) return result;
        foreach (var file in Directory.GetFiles(rulesDir, "*.star"))
        {
            try
            {
                var content = File.ReadAllText(file);
                var match = System.Text.RegularExpressions.Regex.Match(
                    content, @"^RULE_ID\s*=\s*""([^""]+)""", System.Text.RegularExpressions.RegexOptions.Multiline);
                if (match.Success)
                    result[match.Groups[1].Value.Trim().ToUpperInvariant()] = content;
            }
            catch { /* skip unreadable files */ }
        }
        return result;
    }

    private void RequestRulesCatalog()
    {
        var projectDirForLog = getProjectDir();
        DebugLog.Write(projectDirForLog, "[RequestRulesCatalog] starting", LogLevel.Trace);
        try
        {
            var service = new LintScanService(fileService, logService);
            var result = service.TryLoadRulesCatalog(getProjectDir());
            if (result == null)
            {
                DebugLog.Write(projectDirForLog, "[RequestRulesCatalog] TryLoadRulesCatalog returned null — mxcli or project not configured", LogLevel.Info);
                return;
            }
            DebugLog.Write(projectDirForLog, $"[RequestRulesCatalog] catalog loaded: {result.Value.ruleNames.Count} rules", LogLevel.Trace);

            var payload = JsonSerializer.Serialize(new
            {
                ruleNames = result.Value.ruleNames,
                ruleCategories = result.Value.ruleCategories,
                ruleDescriptions = result.Value.ruleDescriptions,
                ruleStarContent = LoadStarContent(projectDirResolver.Resolve()),
            }, LintScanService.JsonOut);

            post("RulesCatalog", payload);
        }
        catch (Exception ex)
        {
            logService.Warn($"[CLEVR Lint] could not load rules catalog on open: {ex.Message}");
        }
    }

    // ---- Linter config: rule enable/severity overrides and excluded modules.

    private void RequestLinterConfig()
    {
        try
        {
            var config = linterConfig.Load();
            var payload = JsonSerializer.Serialize(new
            {
                rules = config.Rules.ToDictionary(
                    kv => kv.Key,
                    kv => new { enabled = kv.Value.Enabled, severity = kv.Value.Severity }),
                excludedModules = config.ExcludedModules,
            }, LintScanService.JsonOut);
            post("LinterConfig", payload);
        }
        catch (Exception ex)
        {
            logService.Error("[CLEVR Lint] loading linter config failed", ex);
            post("LinterConfigError", ex.Message);
        }
    }

    private void SaveLinterConfig(JsonObject? data)
    {
        try
        {
            var rulesNode = data?["rules"]?.AsObject();
            var rules = new Dictionary<string, LinterConfigRule>();
            if (rulesNode is not null)
            {
                foreach (var kv in rulesNode)
                {
                    var obj = kv.Value?.AsObject();
                    bool? enabled = null;
                    string? severity = null;
                    if (obj?["enabled"] is { } en) enabled = en.GetValue<bool?>();
                    if (obj?["severity"] is { } sv) severity = sv.GetValue<string?>();
                    rules[kv.Key] = new LinterConfigRule { Enabled = enabled, Severity = severity };
                }
            }
            var excludedModules = new List<string>();
            if (data?["excludedModules"]?.AsArray() is { } modsArray)
            {
                foreach (var item in modsArray)
                {
                    var name = item?.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(name)) excludedModules.Add(name);
                }
            }
            linterConfig.Save(new LinterConfig { Rules = rules, ExcludedModules = excludedModules });
            post("LinterConfigSaved", "{}");
        }
        catch (Exception ex)
        {
            logService.Error("[CLEVR Lint] saving linter config failed", ex);
            post("LinterConfigError", ex.Message);
        }
    }

    // ---- Baselines: snapshot scan results for new/fixed comparison.
    // Stored in $project/.clevr-lint/baselines.json (version-controlled, shared with team).

    private void RequestBaselines()
    {
        try
        {
            var list = baselines.Load(projectDirResolver.Resolve());
            post("BaselinesLoaded", JsonSerializer.Serialize(list, LintScanService.JsonOut));
        }
        catch (Exception ex)
        {
            logService.Error("[CLEVR Lint] loading baselines failed", ex);
            post("BaselineError", ex.Message);
        }
    }

    private void SaveBaseline(JsonObject? data)
    {
        try
        {
            var projectDir = projectDirResolver.Resolve();
            if (string.IsNullOrWhiteSpace(projectDir))
            {
                post("BaselineError", "No project folder available to save the baseline.");
                return;
            }
            var savedAtMs = data?["savedAt"]?.GetValue<long>() ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var savedAt = DateTimeOffset.FromUnixTimeMilliseconds(savedAtMs);
            var id = savedAt.ToString("yyyyMMdd-HHmmss");
            var violationsNode = data?["violations"];
            var violations = violationsNode != null
                ? JsonSerializer.Deserialize<Violation[]>(violationsNode.ToJsonString(), LintScanService.JsonOut) ?? []
                : Array.Empty<Violation>();
            var gitRevision = BaselineStore.GetGitRevision(projectDir);
            var linterConfigValue = linterConfig.Load();
            var disabledRuleIds = linterConfigValue.Rules
                .Where(kv => kv.Value.Enabled == false)
                .Select(kv => kv.Key)
                .ToArray();
            var entry = new BaselineEntry
            {
                Id = id,
                SavedAt = savedAt,
                GitRevision = gitRevision,
                Violations = violations,
                ExcludedModules = linterConfigValue.ExcludedModules.ToArray(),
                DisabledRuleIds = disabledRuleIds,
            };
            baselines.Save(projectDir, entry);
            RequestBaselines();
        }
        catch (Exception ex)
        {
            logService.Error("[CLEVR Lint] saving baseline failed", ex);
            post("BaselineError", ex.Message);
        }
    }

    private void DeleteBaseline(JsonObject? data)
    {
        try
        {
            var projectDir = projectDirResolver.Resolve();
            if (string.IsNullOrWhiteSpace(projectDir))
            {
                post("BaselineError", "No project folder available.");
                return;
            }
            var id = data?["id"]?.GetValue<string>() ?? "";
            if (!string.IsNullOrWhiteSpace(id))
                baselines.Delete(projectDir, id);
            RequestBaselines();
        }
        catch (Exception ex)
        {
            logService.Error("[CLEVR Lint] deleting baseline failed", ex);
            post("BaselineError", ex.Message);
        }
    }

    // ---- mxcli location: resolve/browse/set/download, all behind SettingsCoordinator.

    private async Task DispatchMxcliAsync(string message, JsonObject? data)
    {
        switch (message)
        {
            case "RequestMxcliInfo":
                // GetMxcliInfo() shells out (where.exe / mxcli --version) — keep it off the caller's thread.
                await Task.Run(() =>
                {
                    try
                    {
                        var info = settings.GetMxcliInfo();
                        post("MxcliInfo", JsonSerializer.Serialize(info, LintScanService.JsonOut));
                    }
                    catch (Exception ex)
                    {
                        logService.Warn($"[CLEVR Lint] RequestMxcliInfo failed: {ex.Message}");
                    }
                });
                break;

            case "BrowseMxcliPath":
            {
                // File picker must run on the caller's thread (STA on Studio Pro's UI thread) —
                // this runs synchronously before the first await below.
                var current = settings.CurrentMxcliPath();
                var picked = NativeFileDialog.ShowExePicker("Select mxcli.exe", current);
                if (picked != null) await ApplyMxcliPathAsync(picked);
                break;
            }

            case "SetMxcliPath":
            {
                var path = data?["path"]?.GetValue<string>()?.Trim() ?? "";
                if (!string.IsNullOrEmpty(path)) await ApplyMxcliPathAsync(path);
                break;
            }

            case "DownloadMxcli":
                try
                {
                    var info = await settings.DownloadMxcliAsync(
                        pct => post("MxcliDownloadProgress", pct.ToString()), default);
                    post("MxcliInfo", JsonSerializer.Serialize(info, LintScanService.JsonOut));
                }
                catch (Exception ex)
                {
                    logService.Error("[CLEVR Lint] mxcli download failed", ex);
                    post("MxcliDownloadError", ex.Message);
                }
                break;
        }
    }

    /// <summary>Saves a user-selected mxcli path to settings and posts back the updated MxcliInfo.</summary>
    private async Task ApplyMxcliPathAsync(string path)
    {
        try
        {
            // ApplyMxcliPath re-resolves (shells out) after saving — keep it off the caller's thread.
            var info = await Task.Run(() => settings.ApplyMxcliPath(path));
            post("MxcliInfo", JsonSerializer.Serialize(info, LintScanService.JsonOut));
        }
        catch (Exception ex)
        {
            logService.Error("[CLEVR Lint] ApplyMxcliPath failed", ex);
            post("MxcliPathError", ex.Message);
        }
    }

    // ---- rule sources: list/save/fetch-from-GitHub/delete-fetched-files, all behind SettingsCoordinator.

    private async Task DispatchRuleSourcesAsync(string message, JsonObject? data)
    {
        switch (message)
        {
            case "RequestRuleSources":
                try
                {
                    var sources = settings.GetRuleSources();
                    post("RuleSources", JsonSerializer.Serialize(sources, LintScanService.JsonOut));
                }
                catch (Exception ex)
                {
                    logService.Error("[CLEVR Lint] loading rule sources failed", ex);
                    post("RuleSourcesError", ex.Message);
                }
                break;

            case "SaveRuleSources":
                try
                {
                    var sourcesNode = data?["sources"];
                    var sources = sourcesNode is null
                        ? []
                        : JsonSerializer.Deserialize<List<RuleSource>>(sourcesNode.ToJsonString(), LintScanService.JsonOut) ?? [];
                    settings.SaveRuleSources(sources);
                    post("RuleSourcesSaved", "{}");
                }
                catch (Exception ex)
                {
                    logService.Error("[CLEVR Lint] saving rule sources failed", ex);
                    post("RuleSourcesError", ex.Message);
                }
                break;

            case "FetchRuleSource":
            {
                var id = data?["id"]?.GetValue<string>() ?? "";
                var url = data?["url"]?.GetValue<string>() ?? "";
                var replace = data?["replaceExisting"]?.GetValue<bool>() ?? false;
                post("RuleSourceFetchStarted", JsonSerializer.Serialize(new { id }, LintScanService.JsonOut));
                try
                {
                    var result = await settings.FetchRuleSourceAsync(url, replace,
                        msg => post("RuleSourceFetchProgress", JsonSerializer.Serialize(new { id, message = msg }, LintScanService.JsonOut)),
                        default);
                    post("RuleSourceFetched", JsonSerializer.Serialize(
                        new { id, copied = result.Copied, skipped = result.Skipped, failed = result.Failed, errors = result.Errors },
                        LintScanService.JsonOut));
                    await Task.Run(RequestRulesCatalog);
                }
                catch (Exception ex)
                {
                    logService.Error("[CLEVR Lint] FetchRuleSource failed", ex);
                    post("RuleSourceFetchError", JsonSerializer.Serialize(new { id, error = ex.Message }, LintScanService.JsonOut));
                }
                break;
            }

            case "DeleteRuleSourceFiles":
            {
                var id = data?["id"]?.GetValue<string>() ?? "";
                var url = data?["url"]?.GetValue<string>() ?? "";
                post("RuleSourceFetchStarted", JsonSerializer.Serialize(new { id }, LintScanService.JsonOut));
                try
                {
                    var result = await settings.DeleteRuleSourceFilesAsync(url,
                        msg => post("RuleSourceFetchProgress", JsonSerializer.Serialize(new { id, message = msg }, LintScanService.JsonOut)),
                        default);
                    post("RuleSourceFilesDeleted", JsonSerializer.Serialize(
                        new { id, deleted = result.Deleted, notFound = result.NotFound, failed = result.Failed, errors = result.Errors },
                        LintScanService.JsonOut));
                    await Task.Run(RequestRulesCatalog);
                }
                catch (Exception ex)
                {
                    logService.Error("[CLEVR Lint] DeleteRuleSourceFiles failed", ex);
                    post("RuleSourceFetchError", JsonSerializer.Serialize(new { id, error = ex.Message }, LintScanService.JsonOut));
                }
                break;
            }
        }
    }

    // ---- HTML report export + doc-URL opening: same IO, both driven by the web UI.

    private void ExportHtml(JsonObject? data)
    {
        try
        {
            var html = data?["html"]?.GetValue<string>() ?? "";
            if (string.IsNullOrEmpty(html))
            {
                post("ReportError", "Received empty report content.");
                return;
            }
            var path = ReportExporter.Write(html, getProjectDir());
            logService.Info($"[CLEVR Lint] report saved: {path}");
            ReportExporter.TryOpen(path);
            post("ReportSaved", path);
        }
        catch (Exception ex)
        {
            logService.Error("[CLEVR Lint] report export failed", ex);
            post("ReportError", ex.Message);
        }
    }

    private void OpenUrl(JsonObject? data)
    {
        var url = data?["url"]?.GetValue<string>() ?? "";
        if (string.IsNullOrWhiteSpace(url)
            || !(url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                 || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            post("UrlError", $"Invalid or disallowed URL: {url}");
            return;
        }
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });
            post("UrlOpened", url);
        }
        catch (Exception ex)
        {
            logService.Error("[CLEVR Lint] OpenUrl failed", ex);
            post("UrlError", ex.Message);
        }
    }
}
