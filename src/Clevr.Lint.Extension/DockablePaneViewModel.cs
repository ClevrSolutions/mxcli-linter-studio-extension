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
    private readonly ExclusionStore _exclusions = new();
    private readonly ManualCheckStore _manualChecks = new();

    public DockablePaneViewModel(
        Uri baseUri,
        ILogService logService,
        IExtensionFileService fileService,
        Func<string?> getProjectDir,
        Func<IModel?> getModel,
        IDockingWindowService dockingWindowService)
    {
        _baseUri = baseUri;
        _logService = logService;
        _fileService = fileService;
        _getProjectDir = getProjectDir;
        _getModel = getModel;
        _dockingWindowService = dockingWindowService;
    }

    public override void InitWebView(IWebView webView)
    {
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
                var service = new LintScanService(_fileService, _logService);
                var json = service.RunScanAsJson(_getProjectDir());
                webView.PostMessage("LintViolations", json);
            }
            else if (args.Message == "RunFullScan")
            {
                // FAST scan (default "Scan" button): export + catalog route + mxcli native + YAML route
                // rules + manual checks. Skips the slow describe route (~seconds vs ~minutes).
                RunFullScan(webView, SynchronizationContext.Current, deepScan: false);
            }
            else if (args.Message == "RunDeepScan")
            {
                // DEEPSCAN button: everything from the fast scan PLUS the describe route (5 microflow/expression/
                // access rules) — the full analysis, ~minutes.
                RunFullScan(webView, SynchronizationContext.Current, deepScan: true);
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
            else if (args.Message == "RequestExclusions")
            {
                PostExclusions(webView);
            }
            else if (args.Message == "AddExclusion")
            {
                AddExclusion(webView, args.Data);
            }
            else if (args.Message == "AddExclusions")
            {
                AddExclusions(webView, args.Data);
            }
            else if (args.Message == "RemoveExclusion")
            {
                RemoveExclusion(webView, args.Data);
            }
            else if (args.Message == "RemoveExclusions")
            {
                RemoveExclusions(webView, args.Data);
            }
            else if (args.Message == "RequestManualChecks")
            {
                PostManualChecks(webView);
            }
            else if (args.Message == "RequestRulesCatalog")
            {
                _ = Task.Run(() => PostRulesCatalog(webView));
            }
            else if (args.Message == "AnswerManualCheck")
            {
                AnswerManualCheck(webView, args.Data);
            }
            else if (args.Message == "ClearManualCheck")
            {
                ClearManualCheck(webView, args.Data);
            }
            else if (args.Message == "RequestLinterConfig")
            {
                PostLinterConfig(webView);
            }
            else if (args.Message == "SaveLinterConfig")
            {
                SaveLinterConfig(webView, args.Data);
            }
            else if (args.Message == "RequestModules")
            {
                PostModules(webView);
            }
        };
    }

    // ---- Exclusions (Phase 6, spec section 3): suppress WITH mandatory reason. Stored in
    // $project/.clevr-lint/exclusions.json (included in version control). The render layer filters
    // on fingerprint; C# saves the file and sends back the current list.

    private void PostExclusions(IWebView webView)
    {
        try
        {
            webView.PostMessage("Exclusions", _exclusions.LoadJson(ExclusionsProjectDir()));
        }
        catch (Exception ex)
        {
            _logService.Error("[CLEVR Lint] loading exclusions failed", ex);
            webView.PostMessage("ExclusionError", ex.Message);
        }
    }

    private void AddExclusion(IWebView webView, JsonObject? data)
    {
        try
        {
            var reason = data?["reason"]?.GetValue<string>()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(reason))
            {
                // Server-side safeguard: never a silent exclusion without a reason.
                webView.PostMessage("ExclusionError", "A reason is required to exclude an improvement.");
                return;
            }
            var fingerprint = data?["fingerprint"]?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(fingerprint))
            {
                webView.PostMessage("ExclusionError", "Missing fingerprint for the exclusion.");
                return;
            }
            var exclusion = new Exclusion
            {
                Fingerprint = fingerprint,
                RuleId = data?["ruleId"]?.GetValue<string>() ?? "",
                DocumentQualifiedName = data?["documentQualifiedName"]?.GetValue<string>() ?? "",
                ElementName = data?["elementName"]?.GetValue<string>() ?? "",
                Reason = reason,
                ExcludedBy = Environment.UserName,
                Date = DateTime.Now.ToString("yyyy-MM-dd"),
            };
            _exclusions.Add(ExclusionsProjectDir(), exclusion);
            PostExclusions(webView);
        }
        catch (Exception ex)
        {
            _logService.Error("[CLEVR Lint] adding exclusion failed", ex);
            webView.PostMessage("ExclusionError", ex.Message);
        }
    }

    /// <summary>
    /// Batch-exclude (Phase 6 extension, "Exclude rule"): excludes all supplied items with
    /// THE SAME reason, in a single file write. The render layer already sends one entry per unique
    /// fingerprint; AddMany additionally performs an upsert (dedup) as a safeguard.
    /// </summary>
    private void AddExclusions(IWebView webView, JsonObject? data)
    {
        try
        {
            var reason = data?["reason"]?.GetValue<string>()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(reason))
            {
                webView.PostMessage("ExclusionError", "A reason is required to exclude a rule.");
                return;
            }
            var date = DateTime.Now.ToString("yyyy-MM-dd");
            var by = Environment.UserName;
            var toAdd = new List<Exclusion>();
            if (data?["items"] is JsonArray items)
            {
                foreach (var node in items)
                {
                    if (node is not JsonObject o) continue;
                    var fp = o["fingerprint"]?.GetValue<string>() ?? "";
                    if (string.IsNullOrWhiteSpace(fp)) continue;
                    toAdd.Add(new Exclusion
                    {
                        Fingerprint = fp,
                        RuleId = o["ruleId"]?.GetValue<string>() ?? "",
                        DocumentQualifiedName = o["documentQualifiedName"]?.GetValue<string>() ?? "",
                        ElementName = o["elementName"]?.GetValue<string>() ?? "",
                        Reason = reason,
                        ExcludedBy = by,
                        Date = date,
                    });
                }
            }
            if (toAdd.Count == 0)
            {
                webView.PostMessage("ExclusionError", "No findings to exclude for this rule.");
                return;
            }
            _exclusions.AddMany(ExclusionsProjectDir(), toAdd);
            PostExclusions(webView);
        }
        catch (Exception ex)
        {
            _logService.Error("[CLEVR Lint] adding exclusions (batch) failed", ex);
            webView.PostMessage("ExclusionError", ex.Message);
        }
    }

    private void RemoveExclusion(IWebView webView, JsonObject? data)
    {
        try
        {
            var fingerprint = data?["fingerprint"]?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(fingerprint))
            {
                webView.PostMessage("ExclusionError", "Missing fingerprint for the exclusion.");
                return;
            }
            _exclusions.Remove(ExclusionsProjectDir(), fingerprint);
            PostExclusions(webView);
        }
        catch (Exception ex)
        {
            _logService.Error("[CLEVR Lint] removing exclusion failed", ex);
            webView.PostMessage("ExclusionError", ex.Message);
        }
    }

    /// <summary>
    /// Batch-un-exclude (Phase 6 extension, "Remove rule exclusion"): restores all supplied
    /// fingerprints in one go (single file write). May include stale entries.
    /// </summary>
    private void RemoveExclusions(IWebView webView, JsonObject? data)
    {
        try
        {
            var fingerprints = new List<string>();
            if (data?["fingerprints"] is JsonArray arr)
            {
                foreach (var node in arr)
                {
                    var fp = node?.GetValue<string>() ?? "";
                    if (!string.IsNullOrWhiteSpace(fp)) fingerprints.Add(fp);
                }
            }
            if (fingerprints.Count == 0)
            {
                webView.PostMessage("ExclusionError", "No exclusions to remove for this rule.");
                return;
            }
            _exclusions.RemoveMany(ExclusionsProjectDir(), fingerprints);
            PostExclusions(webView);
        }
        catch (Exception ex)
        {
            _logService.Error("[CLEVR Lint] removing exclusions (batch) failed", ex);
            webView.PostMessage("ExclusionError", ex.Message);
        }
    }

    // ---- Manual checks (control questions): mirrors the exclusions flow. C# only stores
    // the answer per check-id; the questions + 30-day expiry live in the render layer.

    private void PostManualChecks(IWebView webView)
    {
        try
        {
            webView.PostMessage("ManualCheckAnswers", _manualChecks.LoadJson(ExclusionsProjectDir()));
        }
        catch (Exception ex)
        {
            _logService.Error("[CLEVR Lint] loading manual checks failed", ex);
            webView.PostMessage("ManualCheckError", ex.Message);
        }
    }

    private void AnswerManualCheck(IWebView webView, JsonObject? data)
    {
        try
        {
            var id = data?["id"]?.GetValue<string>() ?? "";
            var answer = (data?["answer"]?.GetValue<string>() ?? "").Trim().ToLowerInvariant();
            var note = data?["note"]?.GetValue<string>()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(id)) { webView.PostMessage("ManualCheckError", "Missing manual-check id."); return; }
            if (answer != "yes" && answer != "no") { webView.PostMessage("ManualCheckError", "Answer must be 'yes' or 'no'."); return; }
            if (string.IsNullOrWhiteSpace(note))
            {
                // Server-side safeguard: never an answer without a mandatory explanation/reason.
                webView.PostMessage("ManualCheckError", "An explanation (Yes) or reason (No) is required.");
                return;
            }
            _manualChecks.Answer(ExclusionsProjectDir(), new ManualCheckAnswer
            {
                Id = id,
                Answer = answer,
                Note = note,
                AnsweredBy = Environment.UserName,
                Date = DateTime.Now.ToString("yyyy-MM-dd"),
            });
            PostManualChecks(webView);
        }
        catch (Exception ex)
        {
            _logService.Error("[CLEVR Lint] answering manual check failed", ex);
            webView.PostMessage("ManualCheckError", ex.Message);
        }
    }

    private void ClearManualCheck(IWebView webView, JsonObject? data)
    {
        try
        {
            var id = data?["id"]?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(id)) { webView.PostMessage("ManualCheckError", "Missing manual-check id."); return; }
            _manualChecks.Clear(ExclusionsProjectDir(), id);
            PostManualChecks(webView);
        }
        catch (Exception ex)
        {
            _logService.Error("[CLEVR Lint] clearing manual check failed", ex);
            webView.PostMessage("ManualCheckError", ex.Message);
        }
    }

    /// <summary>
    /// The project directory in which .clevr-lint/exclusions.json resides — THE SAME directory
    /// the scan uses (lint-scan-settings.json → projectPath; .mpr → its containing directory;
    /// otherwise the open app). This ensures exclusions belong to the project that produced the violations.
    /// </summary>
    private string? ExclusionsProjectDir()
    {
        try
        {
            var settingsPath = _fileService.ResolvePath("lint-scan-settings.json");
            var json = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : null;
            var settings = LintScanSettings.Load(json, _getProjectDir());
            var p = settings.ProjectPath;
            if (string.IsNullOrWhiteSpace(p)) return _getProjectDir();
            if (File.Exists(p) && p.EndsWith(".mpr", StringComparison.OrdinalIgnoreCase))
                return Path.GetDirectoryName(p);
            if (Directory.Exists(p)) return p;
            return _getProjectDir();
        }
        catch
        {
            return _getProjectDir();
        }
    }

    /// <summary>
    /// Opens the document an improvement refers to in Studio Pro. Strategy:
    ///   1. via the stable GUID (documentId, populated by mxcli/Lint) → TryGetAbstractUnitById;
    ///   2. otherwise (no GUID) via name walk: module → DomainModel/folders/documents.
    /// Navigates at DOCUMENT LEVEL (elementToFocus = null).
    /// </summary>
    private void OpenDocument(IWebView webView, JsonObject? data)
    {
        var projectDir = _getProjectDir();
        try
        {
            var model = _getModel();
            if (model == null)
            {
                DebugLog.Write(projectDir, "OpenDocument: NO app open in Studio Pro.");
                webView.PostMessage("DocumentOpenError", "No app open in Studio Pro.");
                return;
            }

            var documentId = data?["documentId"]?.GetValue<string>();
            var qualifiedName = data?["documentQualifiedName"]?.GetValue<string>() ?? "";
            var documentType = data?["documentType"]?.GetValue<string>() ?? "";

            DebugLog.Write(projectDir, $"=== OpenDocument === documentId='{documentId}' qn='{qualifiedName}' type='{documentType}'");

            // Project security is a project-level artifact, NOT a module document. The
            // Extensibility API 11.10 offers no method to open the project security editor
            // (no ISecurity type, no open method, IProjectDocument has no name to match on).
            // Honest message instead of a misleading "not found".
            if (IsProjectSecurity(documentType))
            {
                DebugLog.Write(projectDir, "OpenDocument: route=PROJECT-SECURITY → API boundary (no open method in 11.10)");
                webView.PostMessage("DocumentOpenError", "Project security cannot be opened directly via the Extensibility API (11.10).");
                return;
            }

            var (unit, focus) = ResolveUnit(model, documentId, qualifiedName, documentType, projectDir);
            if (unit == null)
            {
                // Snippets are NOT exposed as units by the 11.10 ExtensionsAPI
                // (no ISnippet type, no documentId from mxcli, not in GetDocuments()).
                // Honest message instead of a misleading "not found".
                if (IsSnippet(documentType))
                {
                    DebugLog.Write(projectDir, $"OpenDocument: route=SNIPPET → API boundary (snippets not exposed as units in 11.10) (qn='{qualifiedName}')");
                    webView.PostMessage("DocumentOpenError", "Snippets are not exposed as documents by the Studio Pro Extensibility API (11.10) and therefore cannot be opened directly.");
                    return;
                }
                DebugLog.Write(projectDir, $"OpenDocument: RESULT not found (qn='{qualifiedName}', type='{documentType}')");
                _logService.Info($"[CLEVR Lint] OpenDocument: not found (id='{documentId}', qn='{qualifiedName}', type='{documentType}')");
                webView.PostMessage("DocumentOpenError", $"Document not found: {qualifiedName}");
                return;
            }

            _dockingWindowService.TryOpenEditor(unit, focus);
            DebugLog.Write(projectDir, $"OpenDocument: OPENED unit.Id='{unit.Id}' focus={(focus != null ? "yes" : "no")} (qn='{qualifiedName}')");

            // Enumerations are a unit (TryOpenEditor succeeds technically), but Studio Pro
            // shows them as a dialog — not always visible from an extension. Honest message.
            if (IsEnumeration(documentType))
                webView.PostMessage("DocumentOpened", $"{qualifiedName} (enumeration — opens as a dialog in Studio Pro; may not appear as a tab)");
            else if (focus != null)
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

    /// <summary>
    /// Looks up the unit to open + LOGS which route is chosen (GUID / name) and why.
    ///   1. GUID (TryGetAbstractUnitById) — works for real units (microflow, page, domain model
    ///      document). Entities either have no GUID, or an element GUID that is NOT a unit id
    ///      → this step fails and we fall back to the name route.
    ///   2. Name route: find module, then:
    ///      - entity/attribute/association/domain model → the DOMAIN MODEL of the module (an
    ///        entity is not a standalone unit but lives in the domain model — hence the earlier
    ///        "Document not found" error);
    ///      - otherwise → the document by name (recursively through folders).
    /// </summary>
    private (IAbstractUnit? unit, IElement? focus) ResolveUnit(IModel model, string? documentId, string qualifiedName, string documentType, string? projectDir)
    {
        // 1) GUID — stable, no name parsing. Works for real units (microflow, page,
        //    enumeration, domain model document). An entity GUID is NOT a unit id → fails here.
        if (!string.IsNullOrWhiteSpace(documentId))
        {
            if (model.TryGetAbstractUnitById(documentId, out var byId) && byId != null)
            {
                DebugLog.Write(projectDir, $"OpenDocument: route=GUID-OK id='{documentId}'");
                return (byId, null);
            }
            DebugLog.Write(projectDir, $"OpenDocument: route=GUID-MISS id='{documentId}' → name fallback");
        }
        else
        {
            DebugLog.Write(projectDir, "OpenDocument: route=GUID-EMPTY → name fallback");
        }

        // 2) Name route. qualifiedName = "Module.Document" (our normalizer delivers the module
        //    as the first segment). Defensive: strip a double module prefix should one
        //    accidentally still be present ("Module.Module.X" → "Module.X").
        if (string.IsNullOrWhiteSpace(qualifiedName))
        {
            DebugLog.Write(projectDir, "OpenDocument: name route with empty qualifiedName → not found");
            return (null, null);
        }

        var dot = qualifiedName.IndexOf('.');
        if (dot <= 0)
        {
            DebugLog.Write(projectDir, $"OpenDocument: name route, qn without module separator ('{qualifiedName}') → not found");
            return (null, null);
        }
        var moduleName = qualifiedName[..dot];
        var localName = qualifiedName[(dot + 1)..];
        if (localName.StartsWith(moduleName + ".", StringComparison.Ordinal))
            localName = localName[(moduleName.Length + 1)..]; // defensive de-dup

        var module = model.Root.GetModules().FirstOrDefault(m => m.Name == moduleName);
        if (module == null)
        {
            DebugLog.Write(projectDir, $"OpenDocument: name route, module '{moduleName}' not found → not found");
            return (null, null);
        }

        // Entity → open the domain model AND FOCUS the entity (IEntity is an IElement, so
        // usable as elementToFocus). If the match fails, open only the domain model.
        if (IsEntity(documentType))
        {
            var dm = module.DomainModel;
            var entity = dm.GetEntities().FirstOrDefault(e => e.Name == localName);
            DebugLog.Write(projectDir, entity != null
                ? $"OpenDocument: route=NAME entity '{localName}' focused in domain model '{moduleName}'"
                : $"OpenDocument: route=NAME entity '{localName}' NOT found in domain model '{moduleName}' → open domain model only");
            return (dm, entity);
        }

        // Attribute/association/domain model → the DOMAIN MODEL of the module (without focus:
        // the exact sub-element cannot be reliably resolved from the mxcli data).
        if (IsDomainModelElement(documentType))
        {
            DebugLog.Write(projectDir, $"OpenDocument: route=NAME type='{documentType}' → module '{moduleName}' DomainModel (no focus)");
            return (module.DomainModel, null);
        }

        var doc = FindDocument(module, localName);
        DebugLog.Write(projectDir, doc != null
            ? $"OpenDocument: route=NAME document '{localName}' found in module '{moduleName}'"
            : $"OpenDocument: route=NAME document '{localName}' NOT found in module '{moduleName}' (searched recursively)");
        return (doc, null);
    }

    private static bool IsEntity(string documentType)
        => documentType.Equals("Entity", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Document types that are NOT standalone public units but live in the domain model
    /// (entity/attribute/association), plus the domain model itself. Case-insensitive.
    /// </summary>
    private static bool IsDomainModelElement(string documentType)
        => documentType.Equals("Entity", StringComparison.OrdinalIgnoreCase)
        || documentType.Equals("Attribute", StringComparison.OrdinalIgnoreCase)
        || documentType.Equals("Association", StringComparison.OrdinalIgnoreCase)
        || documentType.Equals("DomainModel", StringComparison.OrdinalIgnoreCase);

    /// <summary>Project-level security artifact (not a module document, no open API in 11.10).</summary>
    private static bool IsProjectSecurity(string documentType)
        => documentType.Equals("ProjectSecurity", StringComparison.OrdinalIgnoreCase)
        || documentType.Equals("Security", StringComparison.OrdinalIgnoreCase);

    private static bool IsEnumeration(string documentType)
        => documentType.Equals("Enumeration", StringComparison.OrdinalIgnoreCase);

    /// <summary>Snippet: not modeled as a unit in the 11.10 ExtensionsAPI (no open handle).</summary>
    private static bool IsSnippet(string documentType)
        => documentType.Equals("Snippet", StringComparison.OrdinalIgnoreCase);

    /// <summary>Finds a document by name within a module + (recursively) its subfolders.</summary>
    private static IAbstractUnit? FindDocument(IFolderBase container, string documentName)
    {
        var direct = container.GetDocuments().FirstOrDefault(d => d.Name == documentName);
        if (direct != null) return direct;

        foreach (var folder in container.GetFolders())
        {
            var found = FindDocument(folder, documentName);
            if (found != null) return found;
        }
        return null;
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

    /// <summary>
    /// Scan behind the "Scan" button. Runs mxcli lint + the CLEVR native rules (security export +
    /// expression pass) on a background thread (UI stays responsive). Progress + results
    /// are marshaled to the UI thread via the captured <paramref name="uiContext"/>.
    /// Every outcome is posted (never silently stuck on "busy"): AcrViolations, ScanProgress, ScanFinished.
    /// </summary>
    private void RunFullScan(IWebView webView, SynchronizationContext? uiContext, bool deepScan)
    {
        // One canonical project directory for BOTH steps (same resolution as exclusions/scan:
        // settings.ProjectPath → its directory, otherwise the open app). This ensures the export
        // refreshes exactly the model source that the mxcli/expression rules read afterwards.
        var projectDir = ExclusionsProjectDir();
        DebugLog.Write(projectDir, $"=== Full scan (one button) started === projectDir='{projectDir}', UI context present={uiContext != null}");
        _logService.Info($"[CLEVR Lint] full scan started (projectDir='{projectDir}')");

        void Post(string message, string data)
        {
            if (uiContext != null) uiContext.Post(_ => SafePost(webView, projectDir, message, data), null);
            else SafePost(webView, projectDir, message, data);
        }

        _ = Task.Run(() =>
        {
            try
            {
                Post("ScanProgress", "Analyzing with mxcli…");

                // Start git in parallel — finishes well before mxcli does on any real project.
                var gitTask = Task.Run(() => GitChangedDocumentsService.GetChangedDocumentIds(projectDir));

                // STREAMED: the FAST batch comes first (catalog/lint/security, ~seconds), then — only on
                // deepscan — the describe findings PER CHUNK (~20-30s each). Each batch goes as "LintViolations"
                // to the UI; it distinguishes on 'phase' (fast = replace/clean-slate, describe = append) and
                // on 'final' (last batch → counts definitive). The sum = exactly the non-streamed scan.
                try
                {
                    new LintScanService(_fileService, _logService)
                        .RunScanStreaming(projectDir, deepScan, batchJson => Post("LintViolations", batchJson));
                }
                catch (Exception ex)
                {
                    DebugLog.Write(projectDir, $"full scan: mxcli step ERROR: {ex}");
                    Post("LintViolations", LintScanService.ErrorJson($"Unexpected error during mxcli scan: {ex.Message}"));
                }

                var changedIds = gitTask.GetAwaiter().GetResult();
                var gitPayload = JsonSerializer.Serialize(
                    new { documentIds = changedIds.ToArray(), available = changedIds.Count > 0 },
                    LintScanService.JsonOut);
                Post("UncommittedDocuments", gitPayload);

                DebugLog.Write(projectDir, "=== Full scan DONE ===");
            }
            catch (Exception ex)
            {
                DebugLog.Write(projectDir, $"Full scan ERROR (orchestration): {ex}");
                _logService.Error("[CLEVR Lint] full scan failed", ex);
                Post("ScanError", ex.Message);
            }
            finally
            {
                // ALWAYS re-enable the button + hide the spinner, regardless of outcome.
                Post("ScanFinished", "");
            }
        });
    }

    private void PostRulesCatalog(IWebView webView)
    {
        try
        {
            var service = new LintScanService(_fileService, _logService);
            var result = service.TryLoadRulesCatalog(_getProjectDir());
            if (result == null) return;

            var payload = JsonSerializer.Serialize(new
            {
                ruleNames = result.Value.ruleNames,
                ruleCategories = result.Value.ruleCategories,
            }, LintScanService.JsonOut);

            SafePost(webView, _getProjectDir(), "RulesCatalog", payload);
        }
        catch (Exception ex)
        {
            _logService.Warn($"[CLEVR Lint] could not load rules catalog on open: {ex.Message}");
        }
    }

    private readonly LinterConfigStore _linterConfig = new();

    private void PostLinterConfig(IWebView webView)
    {
        try
        {
            var projectDir = ExclusionsProjectDir();
            var config = _linterConfig.Load(projectDir ?? "");
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
            var projectDir = ExclusionsProjectDir() ?? "";
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
            var config = new LinterConfig { Rules = rules, ExcludedModules = excludedModules };
            _linterConfig.Save(projectDir, config);
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
            var modules = model is null
                ? new List<string>()
                : model.Root.GetModules()
                    .Select(m => m.Name)
                    .Where(n => n != "System")
                    .OrderBy(n => n)
                    .ToList();
            var payload = JsonSerializer.Serialize(new { modules }, LintScanService.JsonOut);
            webView.PostMessage("Modules", payload);
        }
        catch (Exception ex)
        {
            _logService.Error("[CLEVR Lint] loading modules failed", ex);
            webView.PostMessage("ModulesError", ex.Message);
        }
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
