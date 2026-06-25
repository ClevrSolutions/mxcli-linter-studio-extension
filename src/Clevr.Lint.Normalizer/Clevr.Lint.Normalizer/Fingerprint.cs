using System.Security.Cryptography;
using System.Text;

namespace Clevr.Lint.Normalizer;

/// <summary>
/// Fingerprint strategy from spec section 3:
///     fingerprint = sha1( ruleId + "|" + documentQualifiedName + "|" + elementName )
/// Deliberately only the STABLE identity; no reason/thresholds/counts.
/// </summary>
public static class Fingerprint
{
    public static string Compute(string ruleId, string documentQualifiedName, string elementName)
    {
        var input = $"{ruleId}|{documentQualifiedName}|{elementName}";
        // SHA1 is intentional: used for stable identity fingerprinting only, not for security.
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(input));
        return "sha1:" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
