using System.Net;

namespace Clevr.AcrSpike;

/// <summary>
/// Minimale helper om een bestand vanuit wwwroot via de ingebouwde webserver
/// aan de webview te serveren. (Afgeleid van het officiële To-do-voorbeeld,
/// gestript tot alleen wat de spike nodig heeft.)
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
