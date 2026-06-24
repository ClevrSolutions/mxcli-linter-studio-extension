using System.ComponentModel.Composition;
using System.Net;
using Mendix.StudioPro.ExtensionsAPI.Services;
using Mendix.StudioPro.ExtensionsAPI.UI.WebServer;

namespace Clevr.AcrSpike;

/// <summary>
/// Serves the web assets (index.html + main.js) to the C#-hosted webview.
/// A C#-managed webview requires a Studio Pro-hosted web server
/// to display custom HTML; this is that server.
/// </summary>
[Export(typeof(WebServerExtension))]
public class SpikeWebServerExtension : WebServerExtension
{
    private readonly IExtensionFileService _extensionFileService;

    [ImportingConstructor]
    public SpikeWebServerExtension(IExtensionFileService extensionFileService)
    {
        _extensionFileService = extensionFileService;
    }

    public override void InitializeWebServer(IWebServer webServer)
    {
        webServer.AddRoute("index", ServeIndex);
        webServer.AddRoute("main.js", ServeMainJs);
        // The CLEVR logo. Without this route <base>/clevr-logo.png is missing → the panel <img>
        // (src="./clevr-logo.png") and the report fetch (loadLogo → data-URI) receive nothing
        // back → broken image. The PNG is deployed to wwwroot alongside the other assets.
        webServer.AddRoute("clevr-logo.png", ServeLogo);
    }

    private async Task ServeIndex(HttpListenerRequest request, HttpListenerResponse response, CancellationToken ct)
    {
        var path = _extensionFileService.ResolvePath("wwwroot", "index.html");
        await response.SendFileAndClose("text/html", path, ct);
    }

    private async Task ServeMainJs(HttpListenerRequest request, HttpListenerResponse response, CancellationToken ct)
    {
        var path = _extensionFileService.ResolvePath("wwwroot", "main.js");
        await response.SendFileAndClose("text/javascript", path, ct);
    }

    private async Task ServeLogo(HttpListenerRequest request, HttpListenerResponse response, CancellationToken ct)
    {
        var path = _extensionFileService.ResolvePath("wwwroot", "clevr-logo.png");
        await response.SendFileAndClose("image/png", path, ct);
    }
}
