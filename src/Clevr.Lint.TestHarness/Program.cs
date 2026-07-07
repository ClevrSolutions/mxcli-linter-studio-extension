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
//   dotnet run --project src/Clevr.Lint.TestHarness -- --serve [--mock] [projectDir] [extensionDir]
//
// Opens http://localhost:5174/index in the default browser automatically.
// The chrome.webview bridge is mocked via a JS shim: POST /api/message (browser
// → C#) and GET /api/events (C# → browser via SSE).
//
// -- --mock ---------------------------------------------------------------------
// Serves canned violation/rule data (see MockFixtures.cs) instead of invoking mxcli,
// so the UI dev loop needs neither mxcli installed nor a real Mendix project on disk.
// projectDir/extensionDir are unused for scanning in this mode but still accepted.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Clevr.Lint.Extension;
using Mendix.StudioPro.ExtensionsAPI.Services;

// ── arg parsing ───────────────────────────────────────────────────────────────
bool serveMode = args.Contains("--serve");
bool mockMode  = args.Contains("--mock");
var rest = args.Where(a => a != "--serve" && a != "--mock").ToArray();

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
await RunServeModeAsync(projectDir, extensionDir, mockMode);
return 0;

static void Err(string msg) => Console.Error.WriteLine(msg);
static string Str(JsonElement el, string prop)
    => el.TryGetProperty(prop, out var v) ? (v.GetString() ?? "") : "";

// ── serve-mode implementation ─────────────────────────────────────────────────

static async Task RunServeModeAsync(string? projectDir, string extensionDir, bool mockMode)
{
    const int port = 5174;
    var baseUrl = $"http://localhost:{port}/";

    var fileService  = new HarnessFileService(extensionDir);
    var logService   = new HarnessLogService();
    var projectDirResolver = new ProjectDirResolver(fileService, () => projectDir);
    var exclusionCoordinator = new ExclusionCoordinator(new ExclusionStore(), projectDirResolver);
    var linterConfigCoordinator = new LinterConfigCoordinator(new LinterConfigStore(), projectDirResolver);
    var scanCoordinator = new ScanCoordinator(fileService, logService, projectDirResolver);
    var settingsCoordinator = new SettingsCoordinator(fileService, () => projectDir, projectDirResolver, new RuleSourcesService());
    var baselineStore = new BaselineStore();
    var scanLifecycle = new ScanLifecycle();
    var wwwroot      = Path.Combine(AppContext.BaseDirectory, "wwwroot");

    // Each connected SSE client gets its own channel so push() fans out to all of them.
    var clients = new ConcurrentDictionary<Guid, Channel<SseEvent>>();

    void Push(string message, string data)
    {
        var ev = new SseEvent(message, data);
        foreach (var (_, ch) in clients)
            ch.Writer.TryWrite(ev);
    }

    var router = new LintMessageRouter(
        fileService,
        logService,
        exclusionCoordinator,
        linterConfigCoordinator,
        settingsCoordinator,
        baselineStore,
        projectDirResolver,
        () => projectDir,
        Push);

    var listener = new HttpListener();
    listener.Prefixes.Add(baseUrl);
    try
    {
        listener.Start();
    }
    catch (HttpListenerException ex)
    {
        Console.Error.WriteLine($"[serve] Could not start listener on {baseUrl}: {ex.Message}");
        Console.Error.WriteLine($"[serve] Port {port} is likely already in use by another harness instance —");
        Console.Error.WriteLine($"[serve] stop it (check for an orphaned 'dotnet run' process) and try again.");
        Environment.Exit(1);
        return;
    }

    Console.Error.WriteLine($"[serve] mock mode     : {(mockMode ? "ON — canned data, no mxcli/project required" : "off")}");
    Console.Error.WriteLine($"[serve] extension dir : {extensionDir}");
    Console.Error.WriteLine($"[serve] project dir   : {(mockMode ? "(unused in mock mode)" : projectDir ?? "(none)")}");
    Console.Error.WriteLine($"[serve] wwwroot       : {wwwroot}");
    Console.Error.WriteLine($"[serve] listening on  : {baseUrl}");
    Console.Error.WriteLine($"[serve] open          : {baseUrl}index");
    Console.Error.WriteLine($"[serve] Ctrl+C to stop");
    Console.Error.WriteLine("");
    Console.Error.WriteLine($"[serve] hot reload    : cd src/Clevr.Lint.Extension/ui && npm run dev");
    Console.Error.WriteLine($"[serve]                 then open http://localhost:5173 (Vite proxies /api/* here)");
    Console.Error.WriteLine("");

    try { Process.Start(new ProcessStartInfo { FileName = $"{baseUrl}index", UseShellExecute = true }); }
    catch { Console.Error.WriteLine($"[serve] Could not auto-open browser — navigate to {baseUrl}index manually."); }

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
                await HandleRequestAsync(ctx, projectDir, scanCoordinator,
                    scanLifecycle, router, wwwroot, Push, clients, mockMode);
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
    ScanCoordinator scanCoordinator,
    ScanLifecycle scanLifecycle,
    LintMessageRouter router,
    string wwwroot,
    Action<string, string> push,
    ConcurrentDictionary<Guid, Channel<SseEvent>> clients,
    bool mockMode)
{
    var req  = ctx.Request;
    var resp = ctx.Response;
    var path = req.Url?.AbsolutePath.TrimStart('/') ?? "";

    // Only echo CORS back to localhost origins (the Vite dev server proxies /api/* itself and
    // sends no Origin header at all; a direct browser hit on 5174 is same-origin either way).
    // This keeps arbitrary third-party pages from reading responses via CORS.
    var origin = req.Headers["Origin"];
    if (origin != null && IsLocalhostOrigin(origin))
        resp.Headers.Add("Access-Control-Allow-Origin", origin);

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
        // A non-localhost Origin means some other website's page is making this request
        // (e.g. via a stray CORS-less fetch or form post) — reject before dispatching, since
        // these messages can launch a browser (OpenUrl) or write/open a file (ExportHtml).
        if (origin != null && !IsLocalhostOrigin(origin))
        {
            resp.StatusCode = 403;
            resp.Close();
            return;
        }

        using var reader = new StreamReader(req.InputStream, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();

        _ = Task.Run(() => DispatchMessageAsync(body, projectDir, scanCoordinator, scanLifecycle, router, push, mockMode));

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

        // Resolve against wwwroot and require the result to stay inside it — Path.Combine
        // happily discards its first argument for a rooted second one (e.g. "C:/Users/..."),
        // which without this check would let a request read any file on disk.
        var wwwrootFull = Path.GetFullPath(wwwroot) + Path.DirectorySeparatorChar;
        var filePath = Path.GetFullPath(Path.Combine(wwwroot, path.Replace('/', Path.DirectorySeparatorChar)));
        if (filePath.StartsWith(wwwrootFull, StringComparison.OrdinalIgnoreCase) && File.Exists(filePath))
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

static async Task DispatchMessageAsync(
    string body,
    string? projectDir,
    ScanCoordinator scanCoordinator,
    ScanLifecycle scanLifecycle,
    LintMessageRouter router,
    Action<string, string> push,
    bool mockMode)
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
            if (mockMode)
            {
                push("ScanProgress", "Analyzing with mxcli… (mock)");
                push("LintViolations", MockFixtures.BuildFullScanBatchJson());
                push("UncommittedDocuments", MockFixtures.BuildUncommittedDocumentsJson());
                push("ScanFinished", "");
            }
            else
            {
                var token = scanLifecycle.StartNew(out var generation);
                scanCoordinator.RunFullScan(
                    new SyncProgress<ScanEvent>(ev =>
                    {
                        if (scanLifecycle.IsCurrent(generation)) push(ScanMessageName(ev.Kind), ev.Data);
                    }),
                    token);
            }
            break;

        case "CancelScan":
            scanLifecycle.Cancel();
            Console.Error.WriteLine("[serve] scan cancellation requested by user");
            break;

        case "RequestRulesCatalog" when mockMode:
            push("RulesCatalog", MockFixtures.BuildRulesCatalogJson());
            break;

        case "OpenDocument":
            push("DocumentOpenError", "Opening documents in Studio Pro is not available in the test harness.");
            break;

        case "RequestModules":
        {
            // Simulate a Studio Pro model with these modules.
            var modules = new[] { "Administration", "System" };
            push("Modules", JsonSerializer.Serialize(new { modules }));
            break;
        }

        default:
            if (!await router.TryDispatchAsync(message, data))
                Console.Error.WriteLine($"[serve] unhandled message: {message}");
            break;
    }
}

static string ScanMessageName(ScanEventKind kind) => kind switch
{
    ScanEventKind.Progress => "ScanProgress",
    ScanEventKind.Violations => "LintViolations",
    ScanEventKind.UncommittedDocuments => "UncommittedDocuments",
    ScanEventKind.Error => "ScanError",
    ScanEventKind.Finished => "ScanFinished",
    _ => throw new InvalidOperationException($"Unhandled scan event kind: {kind}"),
};

// True for http(s)://localhost[:port] and http(s)://127.0.0.1[:port] — the only origins the
// dev harness should trust (direct browser access on 5174, or the Vite dev server on 5173).
static bool IsLocalhostOrigin(string origin)
    => Uri.TryCreate(origin, UriKind.Absolute, out var uri)
    && (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || uri.Host == "127.0.0.1");

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

record SseEvent(string Message, string Data);

// Progress<T> marshals via SynchronizationContext, which this console app doesn't have —
// report events synchronously and in order instead.
sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}

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
