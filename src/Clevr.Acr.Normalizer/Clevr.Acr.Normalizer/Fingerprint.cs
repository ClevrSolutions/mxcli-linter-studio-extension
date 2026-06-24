using System.Security.Cryptography;
using System.Text;

namespace Clevr.Acr.Normalizer;

/// <summary>
/// Fingerprint-strategie uit spec sectie 3:
///     fingerprint = sha1( ruleId + "|" + documentQualifiedName + "|" + elementName )
/// Bewust alleen de STABIELE identiteit; geen reason/drempels/tellingen.
/// </summary>
public static class Fingerprint
{
    public static string Compute(string ruleId, string documentQualifiedName, string elementName)
    {
        var input = $"{ruleId}|{documentQualifiedName}|{elementName}";
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(input));
        return "sha1:" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
