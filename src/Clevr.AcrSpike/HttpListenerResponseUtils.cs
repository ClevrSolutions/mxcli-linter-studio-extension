using System.Net;

namespace Clevr.AcrSpike;

/// <summary>
/// Minimal helper to serve a file from wwwroot to the webview via the built-in web server.
/// (Derived from the official To-do example, stripped down to only what the spike needs.)
/// </summary>
public static class HttpListenerResponseUtils
{
    public static async Task SendFileAndClose(
        this HttpListenerResponse response, string contentType, string filePath, CancellationToken ct)
    {
        response.StatusCode = 200;
        response.AddHeader("Access-Control-Allow-Origin", "*");

        var bytes = await File.ReadAllBytesAsync(filePath, ct);
        response.ContentType = contentType;
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, ct);
        response.Close();
    }
}
