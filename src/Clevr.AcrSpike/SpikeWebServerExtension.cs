using System.ComponentModel.Composition;
using System.Net;
using Mendix.StudioPro.ExtensionsAPI.Services;
using Mendix.StudioPro.ExtensionsAPI.UI.WebServer;

namespace Clevr.AcrSpike;

/// <summary>
/// Serveert de web-assets (index.html + main.js) aan de door C# gehoste webview.
/// Een door C# beheerde webview heeft een door Studio Pro gehoste webserver nodig
/// om eigen HTML te tonen; dit is die server.
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
        // Het CLEVR-logo. Zonder deze route mist <base>/clevr-logo.png → het paneel-<img>
        // (src="./clevr-logo.png") én de rapport-fetch (loadLogo → data-URI) krijgen niets
        // terug → broken-image. Het PNG wordt wél naar wwwroot mee-gedeployed.
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
