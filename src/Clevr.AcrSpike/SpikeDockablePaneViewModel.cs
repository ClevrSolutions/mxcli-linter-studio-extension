using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using Clevr.Acr.Normalizer;
using Mendix.StudioPro.ExtensionsAPI.Model;
using Mendix.StudioPro.ExtensionsAPI.Model.DomainModels;
using Mendix.StudioPro.ExtensionsAPI.Model.Projects;
using Mendix.StudioPro.ExtensionsAPI.Services;
using Mendix.StudioPro.ExtensionsAPI.UI.DockablePane;
using Mendix.StudioPro.ExtensionsAPI.UI.Services;
using Mendix.StudioPro.ExtensionsAPI.UI.WebView;

namespace Clevr.AcrSpike;

/// <summary>
/// De C#-gehoste webview-pane. Twee message-handlers:
///   "RunCommand"  → de oorspronkelijke spike (cmd /c echo test) — bewijst de bus.
///   "RunAcrScan"  → Fase 2A: draait de ECHTE mxcli-engine via AcrScanService,
///                   normaliseert naar Violation[] en stuurt die als JSON terug.
///
/// Brug = de WebView message-bus:
///   web → C# : window.chrome.webview.postMessage({ message, data })  →  MessageReceived
///   C# → web : webView.PostMessage(message, data)                    →  window.chrome.webview "message"-event
/// </summary>
public class SpikeDockablePaneViewModel : WebViewDockablePaneViewModel
{
    private readonly Uri _baseUri;
    private readonly ILogService _logService;
    private readonly IExtensionFileService _fileService;
    private readonly Func<string?> _getProjectDir;
    private readonly Func<IModel?> _getModel;
    private readonly IDockingWindowService _dockingWindowService;
    private readonly ExclusionStore _exclusions = new();
    private readonly ManualCheckStore _manualChecks = new();

    public SpikeDockablePaneViewModel(
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
                _logService.Info($"[CLEVR ACR] command klaar, exit={r.ExitCode}, ok={r.Ok}");
                string report =
                    $"exitCode: {r.ExitCode}\nok: {r.Ok}\n\n" +
                    $"--- stdout ---\n{r.StdOut}\n--- stderr ---\n{r.StdErr}" +
                    (r.Error is null ? "" : $"\n\n--- error ---\n{r.Error}");
                webView.PostMessage("CommandOutput", report);
            }
            else if (args.Message == "RunAcrScan")
            {
                // Synchroon (zoals de spike): mxcli op een project kan enkele seconden
                // duren. Voor productie later async + terug-marshallen naar de UI-thread.
                var service = new AcrScanService(_fileService, _logService);
                var json = service.RunScanAsJson(_getProjectDir());
                webView.PostMessage("AcrViolations", json);
            }
            else if (args.Message == "RunFullScan")
            {
                // SNELLE scan (default "Scan"-knop): export + catalog-route + mxcli-eigen + YAML-route-
                // regels + manual checks. Slaat de trage describe-route over (~seconden vs ~minuten).
                RunFullScan(webView, SynchronizationContext.Current, deepScan: false);
            }
            else if (args.Message == "RunDeepScan")
            {
                // DEEPSCAN-knop: alles van de snelle scan PLUS de describe-route (5 microflow-/expressie-/
                // access-regels) — de volledige analyse, ~minuten.
                RunFullScan(webView, SynchronizationContext.Current, deepScan: true);
            }
            else if (args.Message == "ExportHtml")
            {
                // De web-UI bouwt het standalone HTML-rapport; C# schrijft het weg + opent.
                try
                {
                    var html = args.Data?["html"]?.GetValue<string>() ?? "";
                    if (string.IsNullOrEmpty(html))
                    {
                        webView.PostMessage("ReportError", "Received empty report content.");
                        return;
                    }
                    var path = ReportExporter.Write(html, _getProjectDir());
                    _logService.Info($"[CLEVR ACR] rapport opgeslagen: {path}");
                    ReportExporter.TryOpen(path);
                    webView.PostMessage("ReportSaved", path);
                }
                catch (Exception ex)
                {
                    _logService.Error("[CLEVR ACR] rapport-export mislukt", ex);
                    webView.PostMessage("ReportError", ex.Message);
                }
            }
            else if (args.Message == "OpenDocument")
            {
                // Fase 4 (a): navigeer naar het document van een improvement IN Studio Pro.
                // Synchroon: MessageReceived loopt op de UI-thread; TryOpenEditor + het model
                // lezen zijn UI-thread-operaties.
                OpenDocument(webView, args.Data);
            }
            else if (args.Message == "OpenUrl")
            {
                // Fase 4 (b): open de documentatie-URL van een regel in de standaardbrowser.
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
            else if (args.Message == "AnswerManualCheck")
            {
                AnswerManualCheck(webView, args.Data);
            }
            else if (args.Message == "ClearManualCheck")
            {
                ClearManualCheck(webView, args.Data);
            }
        };
    }

    // ---- Exclusions (Fase 6, spec sectie 3): suppress MET verplichte reden. Opslag in
    // $project/.clevr-acr/exclusions.json (mee in version control). De render-laag filtert
    // op fingerprint; C# bewaart het bestand en stuurt de actuele lijst terug.

    private void PostExclusions(IWebView webView)
    {
        try
        {
            webView.PostMessage("Exclusions", _exclusions.LoadJson(ExclusionsProjectDir()));
        }
        catch (Exception ex)
        {
            _logService.Error("[CLEVR ACR] exclusions laden mislukt", ex);
            webView.PostMessage("ExclusionError", ex.Message);
        }
    }

    private void AddExclusion(IWebView webView, JsonObject? data)
    {
        try
        {
            var reason = data?["reason"]?.GetValue<string>()?.Trim() ?? "";
            if (reason.Length == 0)
            {
                // Server-side vangnet: nooit een stille uitsluiting zonder reden.
                webView.PostMessage("ExclusionError", "A reason is required to exclude an improvement.");
                return;
            }
            var fingerprint = data?["fingerprint"]?.GetValue<string>() ?? "";
            if (fingerprint.Length == 0)
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
            _logService.Error("[CLEVR ACR] exclusion toevoegen mislukt", ex);
            webView.PostMessage("ExclusionError", ex.Message);
        }
    }

    /// <summary>
    /// Batch-exclude (Fase 6-uitbreiding, "Exclude rule"): sluit alle aangeleverde punten uit
    /// met DEZELFDE reden, in één bestand-write. De render-laag stuurt al één entry per unieke
    /// fingerprint; AddMany doet bovendien upsert (dedup) als vangnet.
    /// </summary>
    private void AddExclusions(IWebView webView, JsonObject? data)
    {
        try
        {
            var reason = data?["reason"]?.GetValue<string>()?.Trim() ?? "";
            if (reason.Length == 0)
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
                    if (fp.Length == 0) continue;
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
            _logService.Error("[CLEVR ACR] exclusions (batch) toevoegen mislukt", ex);
            webView.PostMessage("ExclusionError", ex.Message);
        }
    }

    private void RemoveExclusion(IWebView webView, JsonObject? data)
    {
        try
        {
            var fingerprint = data?["fingerprint"]?.GetValue<string>() ?? "";
            if (fingerprint.Length == 0)
            {
                webView.PostMessage("ExclusionError", "Missing fingerprint for the exclusion.");
                return;
            }
            _exclusions.Remove(ExclusionsProjectDir(), fingerprint);
            PostExclusions(webView);
        }
        catch (Exception ex)
        {
            _logService.Error("[CLEVR ACR] exclusion verwijderen mislukt", ex);
            webView.PostMessage("ExclusionError", ex.Message);
        }
    }

    /// <summary>
    /// Batch-un-exclude (Fase 6-uitbreiding, "Remove rule exclusion"): zet alle aangeleverde
    /// fingerprints in één keer terug (één bestand-write). Mag stale entries meenemen.
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
                    if (fp.Length > 0) fingerprints.Add(fp);
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
            _logService.Error("[CLEVR ACR] exclusions (batch) verwijderen mislukt", ex);
            webView.PostMessage("ExclusionError", ex.Message);
        }
    }

    // ---- Manual checks (controlevragen): spiegelt de exclusions-flow. C# bewaart alleen
    // het antwoord per check-id; de vragen + 30-dagen-verloop leven in de render-laag.

    private void PostManualChecks(IWebView webView)
    {
        try
        {
            webView.PostMessage("ManualCheckAnswers", _manualChecks.LoadJson(ExclusionsProjectDir()));
        }
        catch (Exception ex)
        {
            _logService.Error("[CLEVR ACR] manual checks laden mislukt", ex);
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
            if (id.Length == 0) { webView.PostMessage("ManualCheckError", "Missing manual-check id."); return; }
            if (answer != "yes" && answer != "no") { webView.PostMessage("ManualCheckError", "Answer must be 'yes' or 'no'."); return; }
            if (note.Length == 0)
            {
                // Server-side vangnet: nooit een antwoord zonder verplichte toelichting/reden.
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
            _logService.Error("[CLEVR ACR] manual check beantwoorden mislukt", ex);
            webView.PostMessage("ManualCheckError", ex.Message);
        }
    }

    private void ClearManualCheck(IWebView webView, JsonObject? data)
    {
        try
        {
            var id = data?["id"]?.GetValue<string>() ?? "";
            if (id.Length == 0) { webView.PostMessage("ManualCheckError", "Missing manual-check id."); return; }
            _manualChecks.Clear(ExclusionsProjectDir(), id);
            PostManualChecks(webView);
        }
        catch (Exception ex)
        {
            _logService.Error("[CLEVR ACR] manual check wissen mislukt", ex);
            webView.PostMessage("ManualCheckError", ex.Message);
        }
    }

    /// <summary>
    /// De projectmap waarin .clevr-acr/exclusions.json staat — DEZELFDE map als die de scan
    /// gebruikt (acr-scan-settings.json → projectPath; .mpr → de map ervan; anders de
    /// geopende app). Zo horen exclusions bij het project dat de violations produceerde.
    /// </summary>
    private string? ExclusionsProjectDir()
    {
        try
        {
            var settingsPath = _fileService.ResolvePath("acr-scan-settings.json");
            var json = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : null;
            var settings = AcrScanSettings.Load(json, _getProjectDir());
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
    /// Opent het document waar een improvement op slaat in Studio Pro. Strategie:
    ///   1. via de stabiele GUID (documentId, gevuld door mxcli/ACR) → TryGetAbstractUnitById;
    ///   2. anders (geen GUID) via naam-walk: module → DomainModel/folders/documenten.
    /// Navigeert op DOCUMENTNIVEAU (elementToFocus = null).
    /// </summary>
    private void OpenDocument(IWebView webView, JsonObject? data)
    {
        var projectDir = _getProjectDir();
        try
        {
            var model = _getModel();
            if (model == null)
            {
                DebugLog.Write(projectDir, "OpenDocument: GEEN app geopend in Studio Pro.");
                webView.PostMessage("DocumentOpenError", "No app open in Studio Pro.");
                return;
            }

            var documentId = data?["documentId"]?.GetValue<string>();
            var qualifiedName = data?["documentQualifiedName"]?.GetValue<string>() ?? "";
            var documentType = data?["documentType"]?.GetValue<string>() ?? "";

            DebugLog.Write(projectDir, $"=== OpenDocument === documentId='{documentId}' qn='{qualifiedName}' type='{documentType}'");

            // Project security is een project-niveau-artefact, GEEN module-document. De
            // Extensibility API 11.10 biedt geen methode om de project-security-editor te
            // openen (geen ISecurity-type, geen open-methode, IProjectDocument heeft geen
            // naam om op te matchen). Eerlijke melding i.p.v. een misleidende "niet gevonden".
            if (IsProjectSecurity(documentType))
            {
                DebugLog.Write(projectDir, "OpenDocument: route=PROJECT-SECURITY → API-grens (geen open-methode in 11.10)");
                webView.PostMessage("DocumentOpenError", "Project security cannot be opened directly via the Extensibility API (11.10).");
                return;
            }

            var (unit, focus) = ResolveUnit(model, documentId, qualifiedName, documentType, projectDir);
            if (unit == null)
            {
                // Snippets worden NIET als unit blootgesteld door de 11.10 ExtensionsAPI
                // (geen ISnippet-type, geen documentId vanuit mxcli, niet in GetDocuments()).
                // Eerlijke melding i.p.v. een misleidende "niet gevonden".
                if (IsSnippet(documentType))
                {
                    DebugLog.Write(projectDir, $"OpenDocument: route=SNIPPET → API-grens (snippets niet als unit in 11.10) (qn='{qualifiedName}')");
                    webView.PostMessage("DocumentOpenError", "Snippets are not exposed as documents by the Studio Pro Extensibility API (11.10) and therefore cannot be opened directly.");
                    return;
                }
                DebugLog.Write(projectDir, $"OpenDocument: RESULTAAT niet gevonden (qn='{qualifiedName}', type='{documentType}')");
                _logService.Info($"[CLEVR ACR] OpenDocument: niet gevonden (id='{documentId}', qn='{qualifiedName}', type='{documentType}')");
                webView.PostMessage("DocumentOpenError", $"Document not found: {qualifiedName}");
                return;
            }

            _dockingWindowService.TryOpenEditor(unit, focus);
            DebugLog.Write(projectDir, $"OpenDocument: GEOPEND unit.Id='{unit.Id}' focus={(focus != null ? "ja" : "nee")} (qn='{qualifiedName}')");

            // Enumeraties zijn wél een unit (TryOpenEditor slaagt technisch), maar Studio Pro
            // toont ze als dialoog — niet altijd zichtbaar vanuit een extensie. Eerlijke melding.
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
            _logService.Error("[CLEVR ACR] OpenDocument faalde", ex);
            webView.PostMessage("DocumentOpenError", ex.Message);
        }
    }

    /// <summary>
    /// Zoekt de te openen unit op + LOGT welke route gekozen wordt (GUID / naam) en waarom.
    ///   1. GUID (TryGetAbstractUnitById) — werkt voor echte units (microflow, page, domeinmodel-
    ///      document). Entiteiten hebben ofwel geen GUID, ofwel een element-GUID die GEEN unit-id
    ///      is → dan faalt deze stap en vallen we terug op de naam-route.
    ///   2. Naam-route: module zoeken, dan:
    ///      - entiteit/attribuut/associatie/domeinmodel → het DOMEINMODEL van de module (een
    ///        entiteit is geen losse unit maar leeft in het domeinmodel — vandaar de fout
    ///        "Document niet gevonden" eerder);
    ///      - anders → het document op naam (recursief door folders).
    /// </summary>
    private (IAbstractUnit? unit, IElement? focus) ResolveUnit(IModel model, string? documentId, string qualifiedName, string documentType, string? projectDir)
    {
        // 1) GUID — stabiel, geen naam-parsing. Werkt voor echte units (microflow, page,
        //    enumeratie, domeinmodel-document). Een entiteit-GUID is GEEN unit-id → faalt hier.
        if (!string.IsNullOrWhiteSpace(documentId))
        {
            if (model.TryGetAbstractUnitById(documentId, out var byId) && byId != null)
            {
                DebugLog.Write(projectDir, $"OpenDocument: route=GUID-OK id='{documentId}'");
                return (byId, null);
            }
            DebugLog.Write(projectDir, $"OpenDocument: route=GUID-MISS id='{documentId}' → naam-fallback");
        }
        else
        {
            DebugLog.Write(projectDir, "OpenDocument: route=GUID-LEEG → naam-fallback");
        }

        // 2) Naam-route. qualifiedName = "Module.Document" (onze normalizer levert de module
        //    als eerste segment). Defensief: strip een dubbele module-prefix mocht die er
        //    onverhoopt nog in zitten ("Module.Module.X" → "Module.X").
        if (string.IsNullOrWhiteSpace(qualifiedName))
        {
            DebugLog.Write(projectDir, "OpenDocument: naam-route met lege qualifiedName → niet gevonden");
            return (null, null);
        }

        var dot = qualifiedName.IndexOf('.');
        if (dot <= 0)
        {
            DebugLog.Write(projectDir, $"OpenDocument: naam-route, qn zonder module-scheiding ('{qualifiedName}') → niet gevonden");
            return (null, null);
        }
        var moduleName = qualifiedName[..dot];
        var localName = qualifiedName[(dot + 1)..];
        if (localName.StartsWith(moduleName + ".", StringComparison.Ordinal))
            localName = localName[(moduleName.Length + 1)..]; // defensieve de-dup

        var module = model.Root.GetModules().FirstOrDefault(m => m.Name == moduleName);
        if (module == null)
        {
            DebugLog.Write(projectDir, $"OpenDocument: naam-route, module '{moduleName}' niet gevonden → niet gevonden");
            return (null, null);
        }

        // Entiteit → open het domeinmodel én FOCUS de entiteit (IEntity is een IElement, dus
        // bruikbaar als elementToFocus). Lukt de match niet, dan alleen het domeinmodel openen.
        if (IsEntity(documentType))
        {
            var dm = module.DomainModel;
            var entity = dm.GetEntities().FirstOrDefault(e => e.Name == localName);
            DebugLog.Write(projectDir, entity != null
                ? $"OpenDocument: route=NAAM entiteit '{localName}' gefocust in domeinmodel '{moduleName}'"
                : $"OpenDocument: route=NAAM entiteit '{localName}' NIET gevonden in domeinmodel '{moduleName}' → alleen domeinmodel openen");
            return (dm, entity);
        }

        // Attribuut/associatie/domeinmodel → het DOMEINMODEL van de module (zonder focus:
        // het exacte sub-element is niet betrouwbaar te resolven uit de mxcli-data).
        if (IsDomainModelElement(documentType))
        {
            DebugLog.Write(projectDir, $"OpenDocument: route=NAAM type='{documentType}' → module '{moduleName}' DomainModel (geen focus)");
            return (module.DomainModel, null);
        }

        var doc = FindDocument(module, localName);
        DebugLog.Write(projectDir, doc != null
            ? $"OpenDocument: route=NAAM document '{localName}' gevonden in module '{moduleName}'"
            : $"OpenDocument: route=NAAM document '{localName}' NIET gevonden in module '{moduleName}' (recursief gezocht)");
        return (doc, null);
    }

    private static bool IsEntity(string documentType)
        => documentType.Equals("Entity", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Documenttypes die GEEN losse openbare unit zijn maar in het domeinmodel leven
    /// (entiteit/attribuut/associatie), plus het domeinmodel zelf. Case-insensitief.
    /// </summary>
    private static bool IsDomainModelElement(string documentType)
        => documentType.Equals("Entity", StringComparison.OrdinalIgnoreCase)
        || documentType.Equals("Attribute", StringComparison.OrdinalIgnoreCase)
        || documentType.Equals("Association", StringComparison.OrdinalIgnoreCase)
        || documentType.Equals("DomainModel", StringComparison.OrdinalIgnoreCase);

    /// <summary>Project-niveau security-artefact (geen module-document, geen open-API in 11.10).</summary>
    private static bool IsProjectSecurity(string documentType)
        => documentType.Equals("ProjectSecurity", StringComparison.OrdinalIgnoreCase)
        || documentType.Equals("Security", StringComparison.OrdinalIgnoreCase);

    private static bool IsEnumeration(string documentType)
        => documentType.Equals("Enumeration", StringComparison.OrdinalIgnoreCase);

    /// <summary>Snippet: niet als unit gemodelleerd in de 11.10 ExtensionsAPI (geen open-handle).</summary>
    private static bool IsSnippet(string documentType)
        => documentType.Equals("Snippet", StringComparison.OrdinalIgnoreCase);

    /// <summary>Zoekt een document op naam binnen een module + (recursief) z'n subfolders.</summary>
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

    /// <summary>Opent een (documentatie-)URL in de standaardbrowser — zelfde patroon als het rapport openen.</summary>
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
            _logService.Error("[CLEVR ACR] OpenUrl faalde", ex);
            webView.PostMessage("UrlError", ex.Message);
        }
    }

    /// <summary>
    /// Scan achter de "Scan"-knop. Draait mxcli lint + de CLEVR-eigen regels (security-export +
    /// expressie-pass) op een achtergrond-thread (UI blijft responsive). Voortgang + resultaten
    /// worden naar de UI-thread gemarshald via de gecaptureerde <paramref name="uiContext"/>.
    /// Elke uitkomst wordt gepost (nooit stil op "bezig"): AcrViolations, ScanProgress, ScanFinished.
    /// </summary>
    private void RunFullScan(IWebView webView, SynchronizationContext? uiContext, bool deepScan)
    {
        // Eén canonieke projectmap voor BEIDE stappen (zelfde resolutie als de exclusions/scan:
        // settings.ProjectPath → z'n map, anders de geopende app). Zo ververst de export exact de
        // modelsource die de mxcli-/expressie-regels daarna lezen.
        var projectDir = ExclusionsProjectDir();
        DebugLog.Write(projectDir, $"=== Volledige scan (één knop) gestart === projectDir='{projectDir}', UI-context aanwezig={uiContext != null}");
        _logService.Info($"[CLEVR ACR] volledige scan gestart (projectDir='{projectDir}')");

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

                // GESTREAMD: de FAST-batch komt eerst (catalog/lint/security, ~seconden), daarna — alleen bij
                // deepscan — de describe-findings PER CHUNK (~20-30s elk). Elke batch gaat als "AcrViolations"
                // naar de UI; die onderscheidt op 'phase' (fast = vervang/clean-slate, describe = append) en
                // op 'final' (laatste batch → tellingen definitief). De som = exact de niet-gestreamde scan.
                try
                {
                    new AcrScanService(_fileService, _logService)
                        .RunScanStreaming(projectDir, deepScan, batchJson => Post("AcrViolations", batchJson));
                }
                catch (Exception ex)
                {
                    DebugLog.Write(projectDir, $"volledige scan: mxcli-stap FOUT: {ex}");
                    Post("AcrViolations", AcrScanService.ErrorJson($"Onverwachte fout tijdens mxcli-scan: {ex.Message}"));
                }

                DebugLog.Write(projectDir, "=== Volledige scan KLAAR ===");
            }
            catch (Exception ex)
            {
                DebugLog.Write(projectDir, $"Volledige scan FOUT (orkestratie): {ex}");
                _logService.Error("[CLEVR ACR] volledige scan mislukt", ex);
                Post("ScanError", ex.Message);
            }
            finally
            {
                // ALTIJD de knop her-enablen + spinner verbergen, ongeacht de uitkomst.
                Post("ScanFinished", "");
            }
        });
    }

    /// <summary>PostMessage met vangnet: faalt het (bv. verkeerde thread), dan loggen i.p.v. stil slikken.</summary>
    private void SafePost(IWebView webView, string? projectDir, string message, string data)
    {
        try
        {
            webView.PostMessage(message, data);
        }
        catch (Exception ex)
        {
            DebugLog.Write(projectDir, $"PostMessage({message}) FAALDE: {ex}");
            _logService.Error($"[CLEVR ACR] PostMessage({message}) faalde", ex);
        }
    }

}
