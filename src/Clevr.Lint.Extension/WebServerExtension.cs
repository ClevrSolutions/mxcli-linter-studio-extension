using System.ComponentModel.Composition;
using System.Net;
using Mendix.StudioPro.ExtensionsAPI.Services;
using HandleWebRequestAsync = Mendix.StudioPro.ExtensionsAPI.UI.WebServer.HandleWebRequestAsync;
using IWebServer = Mendix.StudioPro.ExtensionsAPI.UI.WebServer.IWebServer;
using MxWebServerExtension = Mendix.StudioPro.ExtensionsAPI.UI.WebServer.WebServerExtension;

namespace Clevr.Lint.Extension;

/// <summary>
/// Serves the web assets (index.html + main.js) to the C#-hosted webview.
/// A C#-managed webview requires a Studio Pro-hosted web server
/// to display custom HTML; this is that server.
/// </summary>
[Export(typeof(MxWebServerExtension))]
public class WebServerExtension : MxWebServerExtension
{
    private readonly IExtensionFileService _extensionFileService;

    [ImportingConstructor]
    public WebServerExtension(IExtensionFileService extensionFileService)
    {
        _extensionFileService = extensionFileService;
    }

    public override void InitializeWebServer(IWebServer webServer)
    {
        webServer.AddRoute("index",       ServeFile("index.html",      "text/html"));
        webServer.AddRoute("main.js",     ServeFile("main.js",         "text/javascript"));
        // The CLEVR logo. Without this route <base>/clevr-logo.png is missing → the panel <img>
        // (src="./clevr-logo.png") and the report fetch (loadLogo → data-URI) receive nothing
        // back → broken image. The PNG is deployed to wwwroot alongside the other assets.
        webServer.AddRoute("clevr-logo.png", ServeFile("clevr-logo.png", "image/png"));
    }

    private HandleWebRequestAsync ServeFile(string fileName, string contentType)
    {
        return async (_, response, ct) =>
        {
            var path = _extensionFileService.ResolvePath("wwwroot", fileName);
            await response.SendFileAndClose(contentType, path, ct);
        };
    }
}
