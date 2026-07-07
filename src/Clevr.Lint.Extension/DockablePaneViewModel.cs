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
/// The C#-hosted webview pane, dispatching WebView messages (Scan, Settings, Exclusions,
/// Baselines, ...) to the coordinators that implement them.
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
    private readonly ScanLifecycle _scanLifecycle = new();
    private LintMessageRouter? _router;
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

        DebugLog.TryParseLevel(_settingsCoordinator.GetLogLevel(), out var initialLogLevel);
        DebugLog.SetLevel(initialLogLevel);
    }

    public override void InitWebView(IWebView webView)
    {
        _uiContext = SynchronizationContext.Current;
        webView.Address = new Uri(_baseUri, "index");
        _router = new LintMessageRouter(
            _fileService,
            _logService,
            _exclusionCoordinator,
            _linterConfigCoordinator,
            _settingsCoordinator,
            _baselines,
            _projectDirResolver,
            _getProjectDir,
            (message, data) => PostBackground(_uiContext, webView, message, data));

        webView.MessageReceived += (_, args) =>
        {
            if (args.Message == "RunFullScan")
            {
                var ctx = SynchronizationContext.Current;
                _uiContext ??= ctx; // Capture the known-good UI context for background→UI marshaling
                if (ctx == null)
                    DebugLog.Write(_getProjectDir(), "[CLEVR Lint] WARNING: SynchronizationContext.Current is null at scan start — UI posts will run on background thread", LogLevel.Error);
                var token = _scanLifecycle.StartNew(out var generation);
                // Progress<T> captures SynchronizationContext.Current at construction — here, on the
                // UI thread — so each Report() below auto-marshals back to the UI without manual Post().
                var progress = new Progress<ScanEvent>(ev => PostScanEvent(webView, ev, generation));
                _ = Task.Run(() => _scanCoordinator.RunFullScan(progress, token));
            }
            else if (args.Message == "CancelScan")
            {
                _scanLifecycle.Cancel();
                DebugLog.Write(_getProjectDir(), "[CLEVR Lint] scan cancellation requested by user", LogLevel.Info);
            }
            else if (args.Message == "OpenDocument")
            {
                // Phase 4 (a): navigate to the document of an improvement IN Studio Pro.
                // Synchronous: MessageReceived runs on the UI thread; TryOpenEditor + reading the model
                // are UI-thread operations.
                OpenDocument(webView, args.Data);
            }
            else if (args.Message == "RequestModules")
            {
                PostModules(webView);
            }
            else
            {
                // Everything else is host-agnostic coordinator wiring shared with the TestHarness —
                // see LintMessageRouter. Fire-and-forget so BrowseMxcliPath's file picker (which must
                // run on this UI/STA thread) still executes synchronously up to its first await.
                _ = _router!.TryDispatchAsync(args.Message, args.Data);
            }
        };
    }

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
        DebugLog.Write(projectDir, $"=== OpenDocument === documentId='{documentId}' qn='{qualifiedName}' type='{documentType}'", LogLevel.Trace);

        try
        {
            var resolution = _navigationCoordinator.Resolve(documentId, qualifiedName, documentType);
            DebugLog.Write(projectDir, $"OpenDocument: {resolution.Reason}", LogLevel.Trace);

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
            DebugLog.Write(projectDir, $"OpenDocument: OPENED unit.Id='{resolution.Unit!.Id}' focus={(resolution.Focus != null ? "yes" : "no")} (qn='{qualifiedName}')", LogLevel.Trace);

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
            DebugLog.Write(projectDir, $"OpenDocument: EXCEPTION {ex}", LogLevel.Error);
            _logService.Error("[CLEVR Lint] OpenDocument failed", ex);
            webView.PostMessage("DocumentOpenError", ex.Message);
        }
    }

    /// <summary>Translates a <see cref="ScanEvent"/> from <see cref="ScanCoordinator.RunFullScan"/>
    /// into the WebView message it corresponds to. The only place scan events touch the WebView.
    /// Drops events from a superseded scan (<paramref name="generation"/> not current per
    /// <see cref="_scanLifecycle"/>) — e.g. a stale Finished that would otherwise re-enable the
    /// Scan button while a newer scan, started before the old one noticed cancellation, is still running.</summary>
    private void PostScanEvent(IWebView webView, ScanEvent ev, int generation)
    {
        if (!_scanLifecycle.IsCurrent(generation)) return;
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
            DebugLog.Write(projectDir, $"PostMessage({message}) FAILED: {ex}", LogLevel.Error);
            _logService.Error($"[CLEVR Lint] PostMessage({message}) failed", ex);
        }
    }

}
