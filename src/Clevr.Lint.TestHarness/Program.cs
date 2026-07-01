// Standalone test harness for the CLEVR Lint scan pipeline.
// Tests the full cycle (settings → mxcli → parse → normalise → JSON) without Studio Pro.
//
// Usage:
//   dotnet run --project src/Clevr.Lint.TestHarness -- [projectDir] [extensionDir]
//
//   projectDir   : path to a Mendix project directory or .mpr file
//                  (omit to use the mxcli default / lint-scan-settings.json)
//   extensionDir : where lint-scan-settings.json lives
//                  (defaults to the extension's Debug build output)
//
// Scan results (JSON) go to stdout; progress/log lines go to stderr.
// Exit code: 0 = harness ran (violations or not), 1 = scan returned ok:false.
//
// -- serve mode ----------------------------------------------------------------
// Hosts the React UI in a browser so Claude (or a developer) can test the full
// scan + UI cycle without Studio Pro.
//
// Usage:
//   dotnet run --project src/Clevr.Lint.TestHarness -- --serve [projectDir] [extensionDir]
//
// Opens http://localhost:5174/index in the default browser automatically.
// The chrome.webview bridge is mocked via a JS shim: POST /api/message (browser
// → C#) and GET /api/events (C# → browser via SSE).

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Clevr.Lint.Extension;
using Clevr.Lint.Normalizer;
using Mendix.StudioPro.ExtensionsAPI.Services;

// ── arg parsing ───────────────────────────────────────────────────────────────
bool serveMode = args.Length > 0 && args[0] == "--serve";
var rest = serveMode ? args[1..] : args;

var projectDir   = rest.Length > 0 ? rest[0] : null;
var extensionDir = rest.Length > 1
    ? rest[1]
    : Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "../../../../Clevr.Lint.Extension/bin/Debug/net10.0"));

if (!serveMode)
{
    // ── scan-only mode (original behaviour) ───────────────────────────────────
    Err($"extension dir : {extensionDir}");
    Err($"project dir   : {projectDir ?? "(none — falls back to mxcli auto-detect)"}");
    Err("");

    var fileService = new HarnessFileService(extensionDir);
    var logService  = new HarnessLogService();
    var service     = new LintScanService(fileService, logService);

    Err("── scan ──");
    var json = service.RunScanAsJson(projectDir);
    Console.WriteLine(json);
    Err("");

    Err("── summary ──");
    try
    {
        using var doc  = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("ok", out var ok) && !ok.GetBoolean())
        {
            var error = root.TryGetProperty("error", out var e) ? e.GetString() : "unknown";
            Err($"FAILED : {error}");
            return 1;
        }

        var violationCount = root.TryGetProperty("violations", out var v) ? v.GetArrayLength() : 0;
        var rawCount       = root.TryGetProperty("rawCount",   out var r) ? r.GetInt32()       : -1;
        var exitCode       = root.TryGetProperty("exitCode",   out var x) ? x.GetInt32()       : -1;
        var ruleNames      = root.TryGetProperty("ruleNames",  out var rn) ? rn : (JsonElement?)null;

        Err($"mxcli exit  : {exitCode}");
        Err($"raw count   : {rawCount}");
        Err($"normalised  : {violationCount} violation(s)");
        Err($"rule names  : {(ruleNames?.EnumerateObject().Count() ?? 0)} loaded");
        Err("");

        if (violationCount > 0)
        {
            foreach (var viol in v.EnumerateArray())
            {
                var ruleId = Str(viol, "ruleId");
                var kind   = Str(viol, "kind");
                var cat    = Str(viol, "category");
                var sev    = Str(viol, "severity");
                var qname  = Str(viol, "documentQualifiedName");
                var reason = Str(viol, "reason");
                Err($"  [{ruleId}] ({kind}/{cat}/{sev}) {qname}");
                Err($"    {reason}");
            }
        }
        else
        {
            Err("  (no violations — project is clean, or mxcli found nothing)");
        }
    }
    catch (Exception ex)
    {
        Err($"(summary parse failed: {ex.Message})");
    }

    return 0;
}

// ── serve mode ────────────────────────────────────────────────────────────────
await RunServeModeAsync(projectDir, extensionDir);
return 0;

static void Err(string msg) => Console.Error.WriteLine(msg);
static string Str(JsonElement el, string prop)
    => el.TryGetProperty(prop, out var v) ? (v.GetString() ?? "") : "";

// ── serve-mode implementation ─────────────────────────────────────────────────

static async Task RunServeModeAsync(string? projectDir, string extensionDir)
{
    const int port = 5174;
    var baseUrl = $"http://localhost:{port}/";

    var fileService  = new HarnessFileService(extensionDir);
    var logService   = new HarnessLogService();
    var projectDirResolver = new ProjectDirResolver(fileService, () => projectDir);
    var exclusionCoordinator = new ExclusionCoordinator(new ExclusionStore(), projectDirResolver);
    var linterConfigCoordinator = new LinterConfigCoordinator(new LinterConfigStore(), projectDirResolver);
    var wwwroot      = Path.Combine(AppContext.BaseDirectory, "wwwroot");

    // Each connected SSE client gets its own channel so push() fans out to all of them.
    var clients = new ConcurrentDictionary<Guid, Channel<SseEvent>>();

    void Push(string message, string data)
    {
        var ev = new SseEvent(message, data);
        foreach (var (_, ch) in clients)
            ch.Writer.TryWrite(ev);
    }

    var listener = new HttpListener();
    listener.Prefixes.Add(baseUrl);
    listener.Start();

    Console.Error.WriteLine($"[serve] extension dir : {extensionDir}");
    Console.Error.WriteLine($"[serve] project dir   : {projectDir ?? "(none)"}");
    Console.Error.WriteLine($"[serve] wwwroot       : {wwwroot}");
    Console.Error.WriteLine($"[serve] listening on  : {baseUrl}");
    Console.Error.WriteLine($"[serve] open          : {baseUrl}index");
    Console.Error.WriteLine($"[serve] Ctrl+C to stop");
    Console.Error.WriteLine("");
    Console.Error.WriteLine($"[serve] hot reload    : cd src/Clevr.Lint.Extension/ui && npm run dev");
    Console.Error.WriteLine($"[serve]                 then open http://localhost:5173 (Vite proxies /api/* here)");
    Console.Error.WriteLine("");

    try { Process.Start(new ProcessStartInfo { FileName = $"{baseUrl}index", UseShellExecute = true }); }
    catch { Console.Error.WriteLine("[serve] Could not auto-open browser — navigate to {baseUrl}index manually."); }

    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

    while (!cts.Token.IsCancellationRequested)
    {
        HttpListenerContext ctx;
        try { ctx = await listener.GetContextAsync(); }
        catch (HttpListenerException) { break; }
        catch (ObjectDisposedException) { break; }

        _ = Task.Run(async () =>
        {
            try
            {
                await HandleRequestAsync(ctx, projectDir, fileService, logService,
                    exclusionCoordinator, linterConfigCoordinator, wwwroot, Push, clients);
            }
            catch (Exception ex) { Console.Error.WriteLine($"[serve] request error: {ex.Message}"); }
        }, cts.Token);
    }

    listener.Stop();
    Console.Error.WriteLine("[serve] stopped.");
}

static async Task HandleRequestAsync(
    HttpListenerContext ctx,
    string? projectDir,
    HarnessFileService fileService,
    HarnessLogService logService,
    ExclusionCoordinator exclusionCoordinator,
    LinterConfigCoordinator linterConfigCoordinator,
    string wwwroot,
    Action<string, string> push,
    ConcurrentDictionary<Guid, Channel<SseEvent>> clients)
{
    var req  = ctx.Request;
    var resp = ctx.Response;
    var path = req.Url?.AbsolutePath.TrimStart('/') ?? "";

    resp.Headers.Add("Access-Control-Allow-Origin", "*");

    if (req.HttpMethod == "OPTIONS") { resp.StatusCode = 204; resp.Close(); return; }

    // GET /chrome-webview-shim.js
    if (path == "chrome-webview-shim.js" && req.HttpMethod == "GET")
    {
        await ServeTextAsync(resp, ChromeWebViewShimJs(), "application/javascript; charset=utf-8");
        return;
    }

    // GET /api/events — SSE stream (C# → browser)
    if (path == "api/events" && req.HttpMethod == "GET")
    {
        var id = Guid.NewGuid();
        var ch = Channel.CreateUnbounded<SseEvent>();
        clients[id] = ch;
        Console.Error.WriteLine($"[serve] SSE client connected ({id.ToString("N")[..8]})");
        try
        {
            resp.ContentType = "text/event-stream; charset=utf-8";
            resp.Headers.Add("Cache-Control", "no-cache");
            resp.Headers.Add("X-Accel-Buffering", "no");
            resp.SendChunked = true;

            using var writer = new StreamWriter(resp.OutputStream, new UTF8Encoding(false), leaveOpen: true);
            writer.AutoFlush = true;

            await writer.WriteAsync(": connected\n\n");

            while (true)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                bool hasData;
                try { hasData = await ch.Reader.WaitToReadAsync(timeout.Token); }
                catch (OperationCanceledException)
                {
                    await writer.WriteAsync(": keepalive\n\n");
                    continue;
                }
                if (!hasData) break; // channel completed

                while (ch.Reader.TryRead(out var ev))
                {
                    var json = JsonSerializer.Serialize(new { ev.Message, ev.Data });
                    await writer.WriteAsync($"data: {json}\n\n");
                }
            }
        }
        catch { /* client disconnected */ }
        finally
        {
            clients.TryRemove(id, out _);
            Console.Error.WriteLine($"[serve] SSE client disconnected ({id.ToString("N")[..8]})");
        }
        return;
    }

    // POST /api/message — browser → C#
    if (path == "api/message" && req.HttpMethod == "POST")
    {
        using var reader = new StreamReader(req.InputStream, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();

        _ = Task.Run(() => DispatchMessage(body, projectDir, fileService, logService,
            exclusionCoordinator, linterConfigCoordinator, push));

        resp.StatusCode = 204;
        resp.Close();
        return;
    }

    // Static files from wwwroot
    if (req.HttpMethod == "GET")
    {
        if (path == "" || path == "index" || path == "index.html")
        {
            var html = await File.ReadAllTextAsync(Path.Combine(wwwroot, "index.html"));
            // Inject shim before main.js so window.chrome.webview exists when React loads.
            html = html.Replace(
                "<script type=\"module\"",
                "<script src=\"/chrome-webview-shim.js\"></script>\n    <script type=\"module\"");
            await ServeTextAsync(resp, html, "text/html; charset=utf-8");
            return;
        }

        var filePath = Path.Combine(wwwroot, path.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(filePath))
        {
            var mime = path.EndsWith(".js")  ? "application/javascript; charset=utf-8" :
                       path.EndsWith(".png") ? "image/png" :
                       path.EndsWith(".css") ? "text/css; charset=utf-8" :
                                               "application/octet-stream";
            var bytes = await File.ReadAllBytesAsync(filePath);
            resp.ContentType = mime;
            resp.ContentLength64 = bytes.Length;
            await resp.OutputStream.WriteAsync(bytes);
            resp.Close();
            return;
        }
    }

    resp.StatusCode = 404;
    resp.Close();
}

static void DispatchMessage(
    string body,
    string? projectDir,
    HarnessFileService fileService,
    HarnessLogService logService,
    ExclusionCoordinator exclusionCoordinator,
    LinterConfigCoordinator linterConfigCoordinator,
    Action<string, string> push)
{
    JsonObject? data;
    string message;
    try
    {
        var doc = JsonNode.Parse(body);
        message = doc?["message"]?.GetValue<string>() ?? "";
        data    = doc?["data"] as JsonObject;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[serve] bad message body: {ex.Message}");
        return;
    }

    Console.Error.WriteLine($"[serve] ← {message}");

    switch (message)
    {
        case "MessageListenerRegistered":
            Console.Error.WriteLine("[serve] UI connected and ready.");
            break;

        case "RunFullScan":
        {
            push("ScanProgress", "Analyzing with mxcli…");
            var settingsPath = fileService.ResolvePath("lint-scan-settings.json");
            var settings = LintScanSettings.Load(
                File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : null, projectDir);
            var (resolvedDir, mprFileName, resolveErr) = ResolveProject(settings.ProjectPath);
            var changedTask = (resolveErr == null && !string.IsNullOrEmpty(mprFileName) && !string.IsNullOrEmpty(resolvedDir))
                ? Task.Run(() => new ChangedElementsResolver(settings.MxcliPath, resolvedDir, mprFileName, logService).Resolve())
                : Task.FromResult(new ChangedScanResult { Status = ChangedScanStatus.Error, Message = "project not resolved" });
            try
            {
                new LintScanService(fileService, logService)
                    .RunScanStreaming(projectDir, batchJson => push("LintViolations", batchJson));
            }
            catch (Exception ex)
            {
                push("LintViolations", LintScanService.ErrorJson($"Unexpected error: {ex.Message}"));
            }
            var changedResult = changedTask.GetAwaiter().GetResult();
            push("UncommittedDocuments", JsonSerializer.Serialize(new
            {
                status         = changedResult.Status.ToString(),
                available      = changedResult.Status == ChangedScanStatus.Ok,
                qualifiedNames = changedResult.Microflows.Concat(changedResult.Entities).ToArray(),
                documentIds    = Array.Empty<string>(),
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            push("ScanFinished", "");
            break;
        }

        case "RequestExclusions":
        case "AddExclusion":
        case "AddExclusions":
        case "RemoveExclusion":
        case "RemoveExclusions":
            try
            {
                var updated = message switch
                {
                    "RequestExclusions" => exclusionCoordinator.List(),
                    "AddExclusion" => exclusionCoordinator.Add(
                        ParseExclusionRequest(data), data?["reason"]?.GetValue<string>() ?? ""),
                    "AddExclusions" => exclusionCoordinator.AddMany(
                        (data?["items"] as JsonArray)?.OfType<JsonObject>().Select(ParseExclusionRequest)
                            ?? Enumerable.Empty<ExclusionRequest>(),
                        data?["reason"]?.GetValue<string>() ?? ""),
                    "RemoveExclusion" => exclusionCoordinator.Remove(
                        data?["fingerprint"]?.GetValue<string>() ?? ""),
                    "RemoveExclusions" => exclusionCoordinator.RemoveMany(
                        (data?["fingerprints"] as JsonArray)?.Select(n => n?.GetValue<string>() ?? "")
                            ?? Enumerable.Empty<string>()),
                    _ => throw new InvalidOperationException($"Unhandled exclusion message: {message}"),
                };
                push("Exclusions", ExclusionsJson.Serialize(updated));
            }
            catch (Exception ex) { push("ExclusionError", ex.Message); }
            break;

        case "RequestRulesCatalog":
            _ = Task.Run(() =>
            {
                try
                {
                    var svc    = new LintScanService(fileService, logService);
                    var result = svc.TryLoadRulesCatalog(projectDir);
                    if (result == null) return;
                    var payload = JsonSerializer.Serialize(
                        new { ruleNames = result.Value.ruleNames, ruleCategories = result.Value.ruleCategories },
                        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                    push("RulesCatalog", payload);
                }
                catch (Exception ex) { Console.Error.WriteLine($"[serve] RulesCatalog error: {ex.Message}"); }
            });
            break;

        case "ExportHtml":
        {
            var html = data?["html"]?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(html)) { push("ReportError", "Received empty report content."); break; }
            try
            {
                var reportPath = ReportExporter.Write(html, projectDir);
                Console.Error.WriteLine($"[serve] report saved: {reportPath}");
                ReportExporter.TryOpen(reportPath);
                push("ReportSaved", reportPath);
            }
            catch (Exception ex) { push("ReportError", ex.Message); }
            break;
        }

        case "OpenDocument":
            push("DocumentOpenError", "Opening documents in Studio Pro is not available in the test harness.");
            break;

        case "OpenUrl":
        {
            var url = data?["url"]?.GetValue<string>() ?? "";
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
             && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                push("UrlError", $"Invalid or disallowed URL: {url}");
                break;
            }
            try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); push("UrlOpened", url); }
            catch (Exception ex) { push("UrlError", ex.Message); }
            break;
        }

        case "RequestModules":
        {
            // Simulate a Studio Pro model with these modules.
            var modules = new[] { "Administration", "System" };
            push("Modules", JsonSerializer.Serialize(new { modules }));
            break;
        }

        case "RequestLinterConfig":
        {
            try
            {
                var config = linterConfigCoordinator.Load();
                var payload = JsonSerializer.Serialize(new
                {
                    rules = config.Rules.ToDictionary(
                        kv => kv.Key,
                        kv => new { enabled = kv.Value.Enabled, severity = kv.Value.Severity }),
                    excludedModules = config.ExcludedModules,
                }, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                });
                push("LinterConfig", payload);
            }
            catch (Exception ex) { push("LinterConfigError", ex.Message); }
            break;
        }

        case "SaveLinterConfig":
        {
            try
            {
                var rulesNode = data?["rules"]?.AsObject();
                var rules     = new Dictionary<string, LinterConfigRule>();
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
                if (data?["excludedModules"]?.AsArray() is { } modsArr)
                    foreach (var item in modsArr)
                    {
                        var name = item?.GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(name)) excludedModules.Add(name);
                    }
                linterConfigCoordinator.Save(new LinterConfig { Rules = rules, ExcludedModules = excludedModules });
                push("LinterConfigSaved", "{}");
            }
            catch (Exception ex) { push("LinterConfigError", ex.Message); }
            break;
        }

        default:
            Console.Error.WriteLine($"[serve] unhandled message: {message}");
            break;
    }
}

static ExclusionRequest ParseExclusionRequest(JsonObject? data) => new(
    Fingerprint: data?["fingerprint"]?.GetValue<string>() ?? "",
    RuleId: data?["ruleId"]?.GetValue<string>() ?? "",
    DocumentQualifiedName: data?["documentQualifiedName"]?.GetValue<string>() ?? "",
    ElementName: data?["elementName"]?.GetValue<string>() ?? "");

static async Task ServeTextAsync(HttpListenerResponse resp, string content, string contentType)
{
    var bytes = Encoding.UTF8.GetBytes(content);
    resp.ContentType = contentType;
    resp.ContentLength64 = bytes.Length;
    await resp.OutputStream.WriteAsync(bytes);
    resp.Close();
}

static string ChromeWebViewShimJs() => """
    // chrome-webview-shim.js — mocks window.chrome.webview for the CLEVR Lint TestHarness.
    // Replaces Studio Pro's WebView bridge with:
    //   browser → C#  :  POST /api/message  { message, data }
    //   C# → browser  :  GET  /api/events   (Server-Sent Events)
    (function () {
      'use strict';

      var handlers = [];

      // Connect the SSE stream (C# → browser).
      var evtSource = new EventSource('/api/events');
      evtSource.onmessage = function (e) {
        var msg;
        try { msg = JSON.parse(e.data); } catch { return; }
        // Normalise casing: C# serialises with PascalCase, the app expects { message, data }.
        var evt = new MessageEvent('message', {
          data: { message: msg.Message || msg.message, data: msg.Data || msg.data }
        });
        handlers.forEach(function (h) { try { h(evt); } catch (err) { console.error('[shim]', err); } });
      };
      evtSource.onerror = function () {
        console.warn('[shim] SSE connection lost — reload to reconnect.');
      };

      window.chrome = {
        webview: {
          postMessage: function (msg) {
            fetch('/api/message', {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify(msg),
            }).catch(function (err) { console.error('[shim] postMessage failed:', err); });
          },
          addEventListener: function (type, handler) {
            if (type === 'message' && handlers.indexOf(handler) === -1) handlers.push(handler);
          },
          removeEventListener: function (type, handler) {
            var idx = handlers.indexOf(handler);
            if (idx >= 0) handlers.splice(idx, 1);
          },
        },
      };

      console.log('[shim] chrome.webview mock installed — TestHarness bridge active.');
    })();
    """;

static (string projectDir, string mprFileName, string? error) ResolveProject(string projectPath)
{
    if (string.IsNullOrWhiteSpace(projectPath))
        return ("", "", "No project path.");
    if (File.Exists(projectPath) && projectPath.EndsWith(".mpr", StringComparison.OrdinalIgnoreCase))
        return (Path.GetDirectoryName(projectPath) ?? "", Path.GetFileName(projectPath), null);
    if (Directory.Exists(projectPath))
    {
        var mprs = Directory.GetFiles(projectPath, "*.mpr", SearchOption.TopDirectoryOnly);
        if (mprs.Length == 1) return (projectPath, Path.GetFileName(mprs[0]), null);
        return ("", "", mprs.Length == 0 ? "No .mpr found." : "Multiple .mpr files found.");
    }
    return ("", "", $"Path does not exist: {projectPath}");
}

record SseEvent(string Message, string Data);

// ── stubs ─────────────────────────────────────────────────────────────────────

sealed class HarnessFileService(string dir) : IExtensionFileService
{
    public string ResolvePath(string name)             => Path.Combine(dir, name);
    public string ResolvePath(string sub, string name) => Path.Combine(dir, sub, name);
    public string ResolvePath(params string[] parts)   => Path.Combine([dir, .. parts]);
}

sealed class HarnessLogService : ILogService
{
    public void Info(string msg)                                          => Console.Error.WriteLine($"[I] {msg}");
    public void Info(string msg, string cat, string comp)                 => Console.Error.WriteLine($"[I/{cat}] {msg}");
    public void Warn(string msg)                                          => Console.Error.WriteLine($"[W] {msg}");
    public void Warn(string msg, string cat, string comp)                 => Console.Error.WriteLine($"[W/{cat}] {msg}");
    public void Debug(string msg)                                         => Console.Error.WriteLine($"[D] {msg}");
    public void Debug(string msg, string cat, string comp)                => Console.Error.WriteLine($"[D/{cat}] {msg}");
    public void Error(string msg, Exception? ex = null)                   => Console.Error.WriteLine($"[E] {msg}{(ex is null ? "" : $": {ex.Message}")}");
    public void Error(string msg, Exception? ex, string cat, string comp) => Console.Error.WriteLine($"[E/{cat}] {msg}{(ex is null ? "" : $": {ex.Message}")}");
}
