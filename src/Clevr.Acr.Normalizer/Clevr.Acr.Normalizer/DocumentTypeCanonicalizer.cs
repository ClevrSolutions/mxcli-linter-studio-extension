namespace Clevr.Acr.Normalizer;

/// <summary>
/// Canonicaliseert de engine-documentType naar de canonieke PascalCase-vorm uit
/// spec sectie 2, zodat beide engines (.star/.rego) naar één vorm convergeren en de
/// UI consistent groepeert/filtert. Onbekende waarden worden met hoofdletter-begin
/// doorgegeven (niet stil gebroken).
/// </summary>
public static class DocumentTypeCanonicalizer
{
    private static readonly Dictionary<string, string> Canonical = new(StringComparer.OrdinalIgnoreCase)
    {
        ["entity"] = "Entity",
        ["association"] = "Association",
        ["microflow"] = "Microflow",
        ["nanoflow"] = "Nanoflow",
        ["page"] = "Page",
        ["snippet"] = "Snippet",
        ["layout"] = "Layout",
        ["module"] = "Module",
        ["enumeration"] = "Enumeration",
        ["projectsecurity"] = "ProjectSecurity",
        ["modulesecurity"] = "ModuleSecurity",
        ["rule"] = "Rule",
        ["constant"] = "Constant",
        ["scheduledevent"] = "ScheduledEvent",
        ["publishedrestservice"] = "PublishedRestService",
        ["consumedrestservice"] = "ConsumedRestService",
        ["messagedefinition"] = "MessageDefinition",
        ["image"] = "Image",
        ["document"] = "Document",
        ["javaaction"] = "JavaAction",
        ["javascriptaction"] = "JavaScriptAction",
    };

    /// <summary>Of de waarde een bekend canoniek type is (na trim, case-insensitief).</summary>
    public static bool IsKnown(string? engineDocumentType)
        => !string.IsNullOrWhiteSpace(engineDocumentType)
           && Canonical.ContainsKey(engineDocumentType.Trim());

    public static string Canonicalize(string? engineDocumentType)
    {
        var value = engineDocumentType?.Trim() ?? "";
        if (value.Length == 0) return "";
        if (Canonical.TryGetValue(value, out var canonical)) return canonical;
        // Onbekend: hoofdletter-begin, rest ongewijzigd (signaleerbaar via IsKnown).
        return char.ToUpperInvariant(value[0]) + value[1..];
    }
}
