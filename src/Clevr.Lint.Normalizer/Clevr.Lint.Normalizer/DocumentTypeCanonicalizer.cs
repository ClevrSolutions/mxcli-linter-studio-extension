namespace Clevr.Lint.Normalizer;

/// <summary>
/// Canonicalizes the engine documentType to the canonical PascalCase form from
/// spec section 2, so that both engines (.star/.rego) converge to one form and the
/// UI groups/filters consistently. Unknown values are passed through with an
/// uppercase first letter (not silently broken).
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

    /// <summary>Whether the value is a known canonical type (after trim, case-insensitive).</summary>
    public static bool IsKnown(string? engineDocumentType)
        => !string.IsNullOrWhiteSpace(engineDocumentType)
           && Canonical.ContainsKey(engineDocumentType.Trim());

    public static string Canonicalize(string? engineDocumentType)
    {
        var value = engineDocumentType?.Trim() ?? "";
        if (value.Length == 0) return "";
        if (Canonical.TryGetValue(value, out var canonical)) return canonical;
        // Unknown: uppercase first letter, rest unchanged (detectable via IsKnown).
        return char.ToUpperInvariant(value[0]) + value[1..];
    }
}
