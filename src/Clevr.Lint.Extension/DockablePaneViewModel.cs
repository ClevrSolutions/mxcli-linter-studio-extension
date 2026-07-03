using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using Clevr.Lint.Normalizer;
using Mendix.StudioPro.ExtensionsAPI.Model;
using Mendix.StudioPro.ExtensionsAPI.Model.DomainModels;
using Mendix.StudioPro.ExtensionsAPI.Model.Projects;
using Mendix.StudioPro.ExtensionsAPI.Services;
using Mendix.StudioPro.ExtensionsAPI.UI.DockablePane;
using Mendix.StudioPro.ExtensionsAPI.UI.Services;
using Mendix.StudioPro.ExtensionsAPI.UI.WebView;

namespace Clevr.Lint.Extension;

/// <summary>
/// The C#-hosted webview pane. Two message handlers:
///   "RunCommand"  → the original spike (cmd /c echo test) — proves the bus.
///   "RunLintScan"  → Phase 2A: runs the REAL mxcli engine via LintScanService,
///                   normalizes to Violation[] and sends them back as JSON.
///
/// Bridge = the WebView message bus:
///   web → C# : window.chrome.webview.postMessage({ message, data })  →  MessageReceived
///   C# → web : webView.PostMessage(message, data)                    →  window.chrome.webview "message" event
/// </summary>
public class DockablePaneViewModel : WebViewDockablePaneViewModel
{
    private readonly Uri _baseUri;
    private readonly ILogService _logService;
    private readonly IExtensionFileService _fileService;
    private readonly Func<string?> _getProjectDir;
    private readonly Func<IModel?> _getModel;
    private readonly IDockingWindowService _dockingWindowService;
    private readonly string? _mendixVersion;
    private readonly ExclusionStore _exclusions = new();
    private readonly BaselineStore _baselines = new();
    private readonly RuleSourcesService _ruleSourcesService = new();
    private readonly ProjectDirResolver _projectDirResolver;
    private readonly ExclusionCoordinator _exclusionCoordinator;
    private readonly NavigationCoordinator _navigationCoordinator;
    private readonly LinterConfigCoordinator _linterConfigCoordinator;
    private readonly SettingsCoordinator _settingsCoordinator;
    private readonly ScanCoordinator _scanCoordinator;
    private CancellationTokenSource? _scanCts;
    private SynchronizationContext? _uiContext;

    public DockablePaneViewModel(
        Uri baseUri,
        ILogService logService,
        IExtensionFileService fileService,
        Func<string?> getProjectDir,
        Func<IModel?> getModel,
        IDockingWindowService dockingWindowService,
        string? mendixVersion = null)
    {
        _baseUri = baseUri;
        _logService = logService;
        _fileService = fileService;
        _getProjectDir = getProjectDir;
        _getModel = getModel;
        _dockingWindowService = dockingWindowService;
        _mendixVersion = mendixVersion;
        _projectDirResolver = new ProjectDirResolver(fileService, getProjectDir);
        _exclusionCoordinator = new ExclusionCoordinator(_exclusions, _projectDirResolver);
        _navigationCoordinator = new NavigationCoordinator(getModel);
        _linterConfigCoordinator = new LinterConfigCoordinator(new LinterConfigStore(), _projectDirResolver);
        _settingsCoordinator = new SettingsCoordinator(fileService, getProjectDir, _projectDirResolver, _ruleSourcesService);
        _scanCoordinator = new ScanCoordinator(fileService, logService, _projectDirResolver, mendixVersion);
    }

    public override void InitWebView(IWebView webView)
    {
        _uiContext = SynchronizationContext.Current;
        webView.Address = new Uri(_baseUri, "index");

        webView.MessageReceived += (_, args) =>
        {
            if (args.Message == "RunCommand")
            {
                var r = ProcessRunner.RunSpikeCommand();
                _logService.Info($"[CLEVR Lint] command done, exit={r.ExitCode}, ok={r.Ok}");
                string report =
                    $"exitCode: {r.ExitCode}\nok: {r.Ok}\n\n" +
                    $"--- stdout ---\n{r.StdOut}\n--- stderr ---\n{r.StdErr}" +
                    (r.Error is null ? "" : $"\n\n--- error ---\n{r.Error}");
                webView.PostMessage("CommandOutput", report);
            }
            else if (args.Message == "RunLintScan")
            {
                // Synchronous (like the spike): mxcli on a project can take a few seconds.
                // For production later: async + marshal back to the UI thread.
                var json = _scanCoordinator.RunLintScan();
                webView.PostMessage("LintViolations", json);
            }
            else if (args.Message == "RunFullScan")
            {
                var ctx = SynchronizationContext.Current;
                _uiContext ??= ctx; // Capture the known-good UI context for background→UI marshaling
                if (ctx == null)
                    DebugLog.Write(_getProjectDir(), "[CLEVR Lint] WARNING: SynchronizationContext.Current is null at scan start — UI posts will run on background thread");
                _scanCts?.Dispose();
                _scanCts = new CancellationTokenSource();
                var token = _scanCts.Token;
                // Progress<T> captures SynchronizationContext.Current at construction — here, on the
                // UI thread — so each Report() below auto-marshals back to the UI without manual Post().
                var progress = new Progress<ScanEvent>(ev => PostScanEvent(webView, ev));
                _ = Task.Run(() => _scanCoordinator.RunFullScan(progress, token));
            }
            else if (args.Message == "CancelScan")
            {
                _scanCts?.Cancel();
                DebugLog.Write(_getProjectDir(), "[CLEVR Lint] scan cancellation requested by user");
            }
            else if (args.Message == "ExportHtml")
            {
                // The web UI builds the standalone HTML report; C# writes it out + opens it.
                try
                {
                    var html = args.Data?["html"]?.GetValue<string>() ?? "";
                    if (string.IsNullOrEmpty(html))
                    {
                        webView.PostMessage("ReportError", "Received empty report content.");
                        return;
                    }
                    var path = ReportExporter.Write(html, _getProjectDir());
                    _logService.Info($"[CLEVR Lint] report saved: {path}");
                    ReportExporter.TryOpen(path);
                    webView.PostMessage("ReportSaved", path);
                }
                catch (Exception ex)
                {
                    _logService.Error("[CLEVR Lint] report export failed", ex);
                    webView.PostMessage("ReportError", ex.Message);
                }
            }
            else if (args.Message == "OpenDocument")
            {
                // Phase 4 (a): navigate to the document of an improvement IN Studio Pro.
                // Synchronous: MessageReceived runs on the UI thread; TryOpenEditor + reading the model
                // are UI-thread operations.
                OpenDocument(webView, args.Data);
            }
            else if (args.Message == "OpenUrl")
            {
                // Phase 4 (b): open the documentation URL of a rule in the default browser.
                OpenUrl(webView, args.Data);
            }
            else if (args.Message is "RequestExclusions" or "AddExclusion" or "AddExclusions"
                     or "RemoveExclusion" or "RemoveExclusions")
            {
                DispatchExclusionMessage(webView, args.Message, args.Data);
            }
            else if (args.Message == "RequestRulesCatalog")
            {
                _ = Task.Run(() => PostRulesCatalog(webView, _uiContext));
            }
            else if (args.Message == "RequestLinterConfig")
            {
                PostLinterConfig(webView);
            }
            else if (args.Message == "SaveLinterConfig")
            {
                SaveLinterConfig(webView, args.Data);
            }
            else if (args.Message is "RequestMxcliInfo" or "BrowseMxcliPath" or "SetMxcliPath" or "DownloadMxcli")
            {
                _ = DispatchMxcliMessageAsync(webView, args.Message, args.Data);
            }
            else if (args.Message == "RequestModules")
            {
                PostModules(webView);
            }
            else if (args.Message == "RequestBaselines")
            {
                PostBaselines(webView);
            }
            else if (args.Message == "SaveBaseline")
            {
                SaveBaseline(webView, args.Data);
            }
            else if (args.Message == "DeleteBaseline")
            {
                DeleteBaseline(webView, args.Data);
            }
            else if (args.Message is "RequestRuleSources" or "SaveRuleSources"
                     or "FetchRuleSource" or "DeleteRuleSourceFiles")
            {
                _ = DispatchRuleSourcesMessageAsync(webView, args.Message, args.Data);
            }
        };
    }

    // ---- Exclusions (Phase 6, spec section 3): suppress WITH mandatory reason. Stored in
    // $project/.clevr-lint/exclusions.json (included in version control). All validation,
    // stamping (who/when), and persistence live behind ExclusionCoordinator's seam; this
    // dispatcher only translates WebView JSON into ExclusionRequest values and posts results.

    private void DispatchExclusionMessage(IWebView webView, string message, JsonObject? data)
    {
        try
        {
            var updated = message switch
            {
                "RequestExclusions" => _exclusionCoordinator.List(),
                "AddExclusion" => _exclusionCoordinator.Add(
                    ParseExclusionRequest(data), data?["reason"]?.GetValue<string>() ?? ""),
                "AddExclusions" => _exclusionCoordinator.AddMany(
                    (data?["items"] as JsonArray)?.OfType<JsonObject>().Select(ParseExclusionRequest)
                        ?? Enumerable.Empty<ExclusionRequest>(),
                    data?["reason"]?.GetValue<string>() ?? ""),
                "RemoveExclusion" => _exclusionCoordinator.Remove(
                    data?["fingerprint"]?.GetValue<string>() ?? ""),
                "RemoveExclusions" => _exclusionCoordinator.RemoveMany(
                    (data?["fingerprints"] as JsonArray)?.Select(n => n?.GetValue<string>() ?? "")
                        ?? Enumerable.Empty<string>()),
                _ => throw new InvalidOperationException($"Unhandled exclusion message: {message}"),
            };
            webView.PostMessage("Exclusions", ExclusionsJson.Serialize(updated));
        }
        catch (Exception ex)
        {
            _logService.Error($"[CLEVR Lint] {message} failed", ex);
            webView.PostMessage("ExclusionError", ex.Message);
        }
    }

    private static ExclusionRequest ParseExclusionRequest(JsonObject? data) => new(
        Fingerprint: data?["fingerprint"]?.GetValue<string>() ?? "",
        RuleId: data?["ruleId"]?.GetValue<string>() ?? "",
        DocumentQualifiedName: data?["documentQualifiedName"]?.GetValue<string>() ?? "",
        ElementName: data?["elementName"]?.GetValue<string>() ?? "");

    /// <summary>
    /// Opens the document an improvement refers to in Studio Pro. Resolution (GUID lookup, name
    /// walk, the project-security/snippet/enumeration API boundaries) all live behind
    /// <see cref="NavigationCoordinator"/>; this dispatcher only logs the trace once and
    /// translates the <see cref="Resolution"/> into a WebView message.
    /// </summary>
    private void OpenDocument(IWebView webView, JsonObject? data)
    {
        var projectDir = _getProjectDir();
        var documentId = data?["documentId"]?.GetValue<string>();
        var qualifiedName = data?["documentQualifiedName"]?.GetValue<string>() ?? "";
        var documentType = data?["documentType"]?.GetValue<string>() ?? "";
        DebugLog.Write(projectDir, $"=== OpenDocument === documentId='{documentId}' qn='{qualifiedName}' type='{documentType}'");

        try
        {
            var resolution = _navigationCoordinator.Resolve(documentId, qualifiedName, documentType);
            DebugLog.Write(projectDir, $"OpenDocument: {resolution.Reason}");

            switch (resolution.Route)
            {
                case NavigationRoute.NoModel:
                    webView.PostMessage("DocumentOpenError", resolution.Reason);
                    return;
                case NavigationRoute.ProjectSecurity:
                    // Honest message instead of a misleading "not found": the Extensibility API
                    // 11.10 offers no method to open the project security editor.
                    webView.PostMessage("DocumentOpenError", "Project security cannot be opened directly via the Extensibility API (11.10).");
                    return;
                case NavigationRoute.Snippet:
                    // Honest message: snippets are not exposed as documents by the 11.10 API.
                    webView.PostMessage("DocumentOpenError", "Snippets are not exposed as documents by the Studio Pro Extensibility API (11.10) and therefore cannot be opened directly.");
                    return;
                case NavigationRoute.NotFound:
                    _logService.Info($"[CLEVR Lint] OpenDocument: not found (id='{documentId}', qn='{qualifiedName}', type='{documentType}')");
                    webView.PostMessage("DocumentOpenError", $"Document not found: {qualifiedName}");
                    return;
            }

            _dockingWindowService.TryOpenEditor(resolution.Unit!, resolution.Focus);
            DebugLog.Write(projectDir, $"OpenDocument: OPENED unit.Id='{resolution.Unit!.Id}' focus={(resolution.Focus != null ? "yes" : "no")} (qn='{qualifiedName}')");

            // Enumerations are a unit (TryOpenEditor succeeds technically), but Studio Pro
            // shows them as a dialog — not always visible from an extension. Honest message.
            if (resolution.IsEnumeration)
                webView.PostMessage("DocumentOpened", $"{qualifiedName} (enumeration — opens as a dialog in Studio Pro; may not appear as a tab)");
            else if (resolution.Focus != null)
                webView.PostMessage("DocumentOpened", $"{qualifiedName} (entity focused in the domain model)");
            else
                webView.PostMessage("DocumentOpened", qualifiedName);
        }
        catch (Exception ex)
        {
            DebugLog.Write(projectDir, $"OpenDocument: EXCEPTION {ex}");
            _logService.Error("[CLEVR Lint] OpenDocument failed", ex);
            webView.PostMessage("DocumentOpenError", ex.Message);
        }
    }

    /// <summary>Opens a (documentation) URL in the default browser — same pattern as opening a report.</summary>
    private void OpenUrl(IWebView webView, JsonObject? data)
    {
        var url = data?["url"]?.GetValue<string>() ?? "";
        if (string.IsNullOrWhiteSpace(url)
            || !(url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                 || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            webView.PostMessage("UrlError", $"Invalid or disallowed URL: {url}");
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            webView.PostMessage("UrlOpened", url);
        }
        catch (Exception ex)
        {
            _logService.Error("[CLEVR Lint] OpenUrl failed", ex);
            webView.PostMessage("UrlError", ex.Message);
        }
    }

    /// <summary>Translates a <see cref="ScanEvent"/> from <see cref="ScanCoordinator.RunFullScan"/>
    /// into the WebView message it corresponds to. The only place scan events touch the WebView.</summary>
    private void PostScanEvent(IWebView webView, ScanEvent ev)
    {
        var message = ev.Kind switch
        {
            ScanEventKind.Progress => "ScanProgress",
            ScanEventKind.Violations => "LintViolations",
            ScanEventKind.UncommittedDocuments => "UncommittedDocuments",
            ScanEventKind.Error => "ScanError",
            ScanEventKind.Finished => "ScanFinished",
            _ => throw new InvalidOperationException($"Unhandled scan event kind: {ev.Kind}"),
        };
        SafePost(webView, _getProjectDir(), message, ev.Data);
    }

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

    private void PostRulesCatalog(IWebView webView, SynchronizationContext? uiContext = null)
    {
        var projectDirForLog = _getProjectDir();
        DebugLog.Write(projectDirForLog, $"[PostRulesCatalog] starting, uiContext={(uiContext != null ? uiContext.GetType().Name : "null")}");
        try
        {
            var service = new LintScanService(_fileService, _logService);
            var result = service.TryLoadRulesCatalog(_getProjectDir());
            if (result == null) { DebugLog.Write(projectDirForLog, "[PostRulesCatalog] TryLoadRulesCatalog returned null — mxcli or project not configured"); return; }
            DebugLog.Write(projectDirForLog, $"[PostRulesCatalog] catalog loaded: {result.Value.ruleNames.Count} rules");

            var payload = JsonSerializer.Serialize(new
            {
                ruleNames = result.Value.ruleNames,
                ruleCategories = result.Value.ruleCategories,
                ruleDescriptions = result.Value.ruleDescriptions,
                ruleStarContent = LoadStarContent(_projectDirResolver.Resolve()),
            }, LintScanService.JsonOut);

            var projectDir = _getProjectDir();
            if (uiContext != null)
                uiContext.Post(_ => SafePost(webView, projectDir, "RulesCatalog", payload), null);
            else
                SafePost(webView, projectDir, "RulesCatalog", payload);
        }
        catch (Exception ex)
        {
            _logService.Warn($"[CLEVR Lint] could not load rules catalog on open: {ex.Message}");
        }
    }

    // ---- Baselines: snapshot scan results for new/fixed comparison.
    // Stored in $project/.clevr-lint/baselines.json (version-controlled, shared with team).

    private void PostBaselines(IWebView webView)
    {
        try
        {
            var list = _baselines.Load(_projectDirResolver.Resolve());
            webView.PostMessage("BaselinesLoaded", JsonSerializer.Serialize(list, LintScanService.JsonOut));
        }
        catch (Exception ex)
        {
            _logService.Error("[CLEVR Lint] loading baselines failed", ex);
            webView.PostMessage("BaselineError", ex.Message);
        }
    }

    private void SaveBaseline(IWebView webView, JsonObject? data)
    {
        try
        {
            var projectDir = _projectDirResolver.Resolve();
            if (string.IsNullOrWhiteSpace(projectDir))
            {
                webView.PostMessage("BaselineError", "No project folder available to save the baseline.");
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
            var linterConfig = _linterConfigCoordinator.Load();
            var disabledRuleIds = linterConfig.Rules
                .Where(kv => kv.Value.Enabled == false)
                .Select(kv => kv.Key)
                .ToArray();
            var entry = new BaselineEntry
            {
                Id = id,
                SavedAt = savedAt,
                GitRevision = gitRevision,
                Violations = violations,
                ExcludedModules = linterConfig.ExcludedModules.ToArray(),
                DisabledRuleIds = disabledRuleIds,
            };
            _baselines.Save(projectDir, entry);
            PostBaselines(webView);
        }
        catch (Exception ex)
        {
            _logService.Error("[CLEVR Lint] saving baseline failed", ex);
            webView.PostMessage("BaselineError", ex.Message);
        }
    }

    private void DeleteBaseline(IWebView webView, JsonObject? data)
    {
        try
        {
            var projectDir = _projectDirResolver.Resolve();
            if (string.IsNullOrWhiteSpace(projectDir))
            {
                webView.PostMessage("BaselineError", "No project folder available.");
                return;
            }
            var id = data?["id"]?.GetValue<string>() ?? "";
            if (!string.IsNullOrWhiteSpace(id))
                _baselines.Delete(projectDir, id);
            PostBaselines(webView);
        }
        catch (Exception ex)
        {
            _logService.Error("[CLEVR Lint] deleting baseline failed", ex);
            webView.PostMessage("BaselineError", ex.Message);
        }
    }

    private void PostLinterConfig(IWebView webView)
    {
        try
        {
            var config = _linterConfigCoordinator.Load();
            var payload = JsonSerializer.Serialize(new
            {
                rules = config.Rules.ToDictionary(
                    kv => kv.Key,
                    kv => new { enabled = kv.Value.Enabled, severity = kv.Value.Severity }),
                excludedModules = config.ExcludedModules,
            }, LintScanService.JsonOut);
            webView.PostMessage("LinterConfig", payload);
        }
        catch (Exception ex)
        {
            _logService.Error("[CLEVR Lint] loading linter config failed", ex);
            webView.PostMessage("LinterConfigError", ex.Message);
        }
    }

    private void SaveLinterConfig(IWebView webView, JsonObject? data)
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
            _linterConfigCoordinator.Save(new LinterConfig { Rules = rules, ExcludedModules = excludedModules });
            webView.PostMessage("LinterConfigSaved", "{}");
        }
        catch (Exception ex)
        {
            _logService.Error("[CLEVR Lint] saving linter config failed", ex);
            webView.PostMessage("LinterConfigError", ex.Message);
        }
    }

    private void PostModules(IWebView webView)
    {
        try
        {
            var model = _getModel();
            var apiModules = model is null
                ? []
                : model.Root.GetModules()
                    .OrderBy(m => m.Name)
                    .Select(m => (Name: m.Name, FromMarketplace: m.FromAppStore, AppStoreVersion: (string?)m.AppStoreVersion))
                    .ToList();
            apiModules.Insert(0, ("Project", false, null));
            if (!apiModules.Any(m => m.Name == "System"))
                apiModules.Insert(1, ("System", false, null));
            var modules = apiModules
                .Select(m => new { name = m.Name, fromMarketplace = m.FromMarketplace, appStoreVersion = m.AppStoreVersion })
                .ToList<object>();
            var payload = JsonSerializer.Serialize(new { modules }, LintScanService.JsonOut);
            webView.PostMessage("Modules", payload);
        }
        catch (Exception ex)
        {
            _logService.Error("[CLEVR Lint] loading modules failed", ex);
            webView.PostMessage("ModulesError", ex.Message);
        }
    }

    // ---- mxcli location: resolve/browse/set/download, all behind SettingsCoordinator ----------

    private async Task DispatchMxcliMessageAsync(IWebView webView, string message, JsonObject? data)
    {
        var ctx = _uiContext;
        switch (message)
        {
            case "RequestMxcliInfo":
                // GetMxcliInfo() shells out (where.exe / mxcli --version) — keep it off the UI thread.
                await Task.Run(() =>
                {
                    try
                    {
                        var info = _settingsCoordinator.GetMxcliInfo();
                        PostBackground(ctx, webView, "MxcliInfo", JsonSerializer.Serialize(info, LintScanService.JsonOut));
                    }
                    catch (Exception ex)
                    {
                        _logService.Warn($"[CLEVR Lint] RequestMxcliInfo failed: {ex.Message}");
                    }
                });
                break;

            case "BrowseMxcliPath":
            {
                // File picker must run on the UI (STA) thread — MessageReceived is already on it.
                var current = _settingsCoordinator.CurrentMxcliPath();
                var picked = NativeFileDialog.ShowExePicker("Select mxcli.exe", current);
                if (picked != null) await ApplyMxcliPathAsync(webView, ctx, picked);
                break;
            }

            case "SetMxcliPath":
            {
                var path = data?["path"]?.GetValue<string>()?.Trim() ?? "";
                if (!string.IsNullOrEmpty(path)) await ApplyMxcliPathAsync(webView, ctx, path);
                break;
            }

            case "DownloadMxcli":
                try
                {
                    var info = await _settingsCoordinator.DownloadMxcliAsync(
                        pct => PostBackground(ctx, webView, "MxcliDownloadProgress", pct.ToString()), default);
                    PostBackground(ctx, webView, "MxcliInfo", JsonSerializer.Serialize(info, LintScanService.JsonOut));
                }
                catch (Exception ex)
                {
                    _logService.Error("[CLEVR Lint] mxcli download failed", ex);
                    PostBackground(ctx, webView, "MxcliDownloadError", ex.Message);
                }
                break;
        }
    }

    /// <summary>Saves a user-selected mxcli path to settings and posts back the updated MxcliInfo.</summary>
    private async Task ApplyMxcliPathAsync(IWebView webView, SynchronizationContext? ctx, string path)
    {
        try
        {
            // ApplyMxcliPath re-resolves (shells out) after saving — keep it off the UI thread.
            var info = await Task.Run(() => _settingsCoordinator.ApplyMxcliPath(path));
            PostBackground(ctx, webView, "MxcliInfo", JsonSerializer.Serialize(info, LintScanService.JsonOut));
        }
        catch (Exception ex)
        {
            _logService.Error("[CLEVR Lint] ApplyMxcliPath failed", ex);
            SafePost(webView, _getProjectDir(), "MxcliPathError", ex.Message);
        }
    }

    // ---- rule sources: list/save/fetch-from-GitHub/delete-fetched-files, all behind SettingsCoordinator

    private async Task DispatchRuleSourcesMessageAsync(IWebView webView, string message, JsonObject? data)
    {
        var ctx = _uiContext;
        switch (message)
        {
            case "RequestRuleSources":
                try
                {
                    var sources = _settingsCoordinator.GetRuleSources();
                    webView.PostMessage("RuleSources", JsonSerializer.Serialize(sources, LintScanService.JsonOut));
                }
                catch (Exception ex)
                {
                    _logService.Error("[CLEVR Lint] loading rule sources failed", ex);
                    webView.PostMessage("RuleSourcesError", ex.Message);
                }
                break;

            case "SaveRuleSources":
                try
                {
                    var sourcesNode = data?["sources"];
                    var sources = sourcesNode is null
                        ? []
                        : JsonSerializer.Deserialize<List<RuleSource>>(sourcesNode.ToJsonString(), LintScanService.JsonOut) ?? [];
                    _settingsCoordinator.SaveRuleSources(sources);
                    webView.PostMessage("RuleSourcesSaved", "{}");
                }
                catch (Exception ex)
                {
                    _logService.Error("[CLEVR Lint] saving rule sources failed", ex);
                    webView.PostMessage("RuleSourcesError", ex.Message);
                }
                break;

            case "FetchRuleSource":
            {
                var id = data?["id"]?.GetValue<string>() ?? "";
                var url = data?["url"]?.GetValue<string>() ?? "";
                var replace = data?["replaceExisting"]?.GetValue<bool>() ?? false;
                PostBackground(ctx, webView, "RuleSourceFetchStarted", JsonSerializer.Serialize(new { id }, LintScanService.JsonOut));
                try
                {
                    var result = await _settingsCoordinator.FetchRuleSourceAsync(url, replace,
                        msg => PostBackground(ctx, webView, "RuleSourceFetchProgress", JsonSerializer.Serialize(new { id, message = msg }, LintScanService.JsonOut)),
                        default);
                    PostBackground(ctx, webView, "RuleSourceFetched", JsonSerializer.Serialize(
                        new { id, copied = result.Copied, skipped = result.Skipped, failed = result.Failed, errors = result.Errors },
                        LintScanService.JsonOut));
                }
                catch (Exception ex)
                {
                    _logService.Error("[CLEVR Lint] FetchRuleSource failed", ex);
                    PostBackground(ctx, webView, "RuleSourceFetchError", JsonSerializer.Serialize(new { id, error = ex.Message }, LintScanService.JsonOut));
                }
                break;
            }

            case "DeleteRuleSourceFiles":
            {
                var id = data?["id"]?.GetValue<string>() ?? "";
                var url = data?["url"]?.GetValue<string>() ?? "";
                PostBackground(ctx, webView, "RuleSourceFetchStarted", JsonSerializer.Serialize(new { id }, LintScanService.JsonOut));
                try
                {
                    var result = await _settingsCoordinator.DeleteRuleSourceFilesAsync(url,
                        msg => PostBackground(ctx, webView, "RuleSourceFetchProgress", JsonSerializer.Serialize(new { id, message = msg }, LintScanService.JsonOut)),
                        default);
                    PostBackground(ctx, webView, "RuleSourceFilesDeleted", JsonSerializer.Serialize(
                        new { id, deleted = result.Deleted, notFound = result.NotFound, failed = result.Failed, errors = result.Errors },
                        LintScanService.JsonOut));
                }
                catch (Exception ex)
                {
                    _logService.Error("[CLEVR Lint] DeleteRuleSourceFiles failed", ex);
                    PostBackground(ctx, webView, "RuleSourceFetchError", JsonSerializer.Serialize(new { id, error = ex.Message }, LintScanService.JsonOut));
                }
                break;
            }
        }
    }

    private void PostBackground(SynchronizationContext? ctx, IWebView webView, string message, string data)
    {
        if (ctx != null) ctx.Post(_ => SafePost(webView, _getProjectDir(), message, data), null);
        else SafePost(webView, _getProjectDir(), message, data);
    }

    /// <summary>PostMessage with safeguard: if it fails (e.g. wrong thread), log instead of silently swallowing.</summary>
    private void SafePost(IWebView webView, string? projectDir, string message, string data)
    {
        try
        {
            webView.PostMessage(message, data);
        }
        catch (Exception ex)
        {
            DebugLog.Write(projectDir, $"PostMessage({message}) FAILED: {ex}");
            _logService.Error($"[CLEVR Lint] PostMessage({message}) failed", ex);
        }
    }

}
