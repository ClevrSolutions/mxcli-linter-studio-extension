using System.Linq;
using Mendix.StudioPro.ExtensionsAPI.Model;
using Mendix.StudioPro.ExtensionsAPI.Model.DomainModels;
using Mendix.StudioPro.ExtensionsAPI.Model.Projects;

namespace Clevr.Lint.Extension;

/// <summary>How <see cref="NavigationCoordinator.Resolve"/> wants the dispatcher to respond.
/// The three non-<see cref="Opened"/> cases are honest "can't do that" outcomes, not exceptions —
/// they're expected results of the API's shape, not control flow a caller branches on by kind.</summary>
public enum NavigationRoute
{
    Opened,
    NoModel,
    ProjectSecurity,
    Snippet,
    NotFound,
}

/// <summary>
/// Result of resolving an improvement's document to something Studio Pro can open.
/// <see cref="Reason"/> is a one-line trace of which route was taken and why — the dispatcher
/// logs it once via DebugLog; the coordinator itself does no file IO, which is what makes it
/// unit-testable without a project directory.
/// </summary>
public sealed record Resolution(
    IAbstractUnit? Unit,
    IElement? Focus,
    NavigationRoute Route,
    string Reason,
    bool IsEnumeration = false);

/// <summary>
/// Resolves an improvement's (documentId, qualifiedName, documentType) triple to the unit
/// Studio Pro should open, or to a data outcome the dispatcher already has a message for.
/// Strategy: 1) stable GUID lookup via TryGetAbstractUnitById; 2) name walk (module →
/// DomainModel/folders/documents) when there's no GUID or it doesn't resolve to a unit.
/// No IWebView, no DebugLog — both would make this seam hypothetical instead of unit-testable.
/// </summary>
public sealed class NavigationCoordinator
{
    private readonly Func<IModel?> _getModel;

    public NavigationCoordinator(Func<IModel?> getModel)
    {
        _getModel = getModel;
    }

    public Resolution Resolve(string? documentId, string qualifiedName, string documentType)
    {
        var model = _getModel();
        if (model == null)
            return new Resolution(null, null, NavigationRoute.NoModel, "No app open in Studio Pro.");

        // Project security is a project-level artifact, NOT a module document. The
        // Extensibility API 11.10 offers no method to open the project security editor
        // (no ISecurity type, no open method, IProjectDocument has no name to match on).
        if (IsProjectSecurity(documentType))
            return new Resolution(null, null, NavigationRoute.ProjectSecurity,
                "route=PROJECT-SECURITY → API boundary (no open method in 11.10)");

        var (unit, focus, reason) = ResolveUnit(model, documentId, qualifiedName, documentType);
        if (unit == null)
        {
            // Snippets are NOT exposed as units by the 11.10 ExtensionsAPI (no ISnippet type,
            // no documentId from mxcli, not in GetDocuments()).
            if (IsSnippet(documentType))
                return new Resolution(null, null, NavigationRoute.Snippet,
                    $"route=SNIPPET → API boundary (snippets not exposed as units in 11.10) (qn='{qualifiedName}') | {reason}");

            return new Resolution(null, null, NavigationRoute.NotFound,
                $"RESULT not found (qn='{qualifiedName}', type='{documentType}') | {reason}");
        }

        return new Resolution(unit, focus, NavigationRoute.Opened, reason, IsEnumeration(documentType));
    }

    /// <summary>
    /// Looks up the unit to open + builds a trace of which route was chosen (GUID / name) and why.
    ///   1. GUID (TryGetAbstractUnitById) — works for real units (microflow, page, domain model
    ///      document). Entities either have no GUID, or an element GUID that is NOT a unit id
    ///      → this step fails and we fall back to the name route.
    ///   2. Name route: find module, then:
    ///      - entity/attribute/association/domain model → the DOMAIN MODEL of the module (an
    ///        entity is not a standalone unit but lives in the domain model — hence the earlier
    ///        "Document not found" error);
    ///      - otherwise → the document by name (recursively through folders).
    /// </summary>
    private static (IAbstractUnit? unit, IElement? focus, string reason) ResolveUnit(
        IModel model, string? documentId, string qualifiedName, string documentType)
    {
        var reasons = new List<string>();

        // 1) GUID — stable, no name parsing. Works for real units (microflow, page,
        //    enumeration, domain model document). An entity GUID is NOT a unit id → fails here.
        if (!string.IsNullOrWhiteSpace(documentId))
        {
            if (model.TryGetAbstractUnitById(documentId, out var byId) && byId != null)
            {
                reasons.Add($"route=GUID-OK id='{documentId}'");
                return (byId, null, string.Join("; ", reasons));
            }
            reasons.Add($"route=GUID-MISS id='{documentId}' → name fallback");
        }
        else
        {
            reasons.Add("route=GUID-EMPTY → name fallback");
        }

        // 2) Name route. qualifiedName = "Module.Document" (our normalizer delivers the module
        //    as the first segment). Defensive: strip a double module prefix should one
        //    accidentally still be present ("Module.Module.X" → "Module.X").
        if (string.IsNullOrWhiteSpace(qualifiedName))
        {
            reasons.Add("name route with empty qualifiedName → not found");
            return (null, null, string.Join("; ", reasons));
        }

        var dot = qualifiedName.IndexOf('.');
        if (dot <= 0)
        {
            reasons.Add($"name route, qn without module separator ('{qualifiedName}') → not found");
            return (null, null, string.Join("; ", reasons));
        }
        var moduleName = qualifiedName[..dot];
        var localName = qualifiedName[(dot + 1)..];
        if (localName.StartsWith(moduleName + ".", StringComparison.Ordinal))
            localName = localName[(moduleName.Length + 1)..]; // defensive de-dup

        var module = model.Root.GetModules().FirstOrDefault(m => m.Name == moduleName);
        if (module == null)
        {
            reasons.Add($"name route, module '{moduleName}' not found → not found");
            return (null, null, string.Join("; ", reasons));
        }

        // Entity → open the domain model AND FOCUS the entity (IEntity is an IElement, so
        // usable as elementToFocus). If the match fails, open only the domain model.
        if (IsEntity(documentType))
        {
            var dm = module.DomainModel;
            var entity = dm.GetEntities().FirstOrDefault(e => e.Name == localName);
            reasons.Add(entity != null
                ? $"route=NAME entity '{localName}' focused in domain model '{moduleName}'"
                : $"route=NAME entity '{localName}' NOT found in domain model '{moduleName}' → open domain model only");
            return (dm, entity, string.Join("; ", reasons));
        }

        // Attribute/association/domain model → the DOMAIN MODEL of the module (without focus:
        // the exact sub-element cannot be reliably resolved from the mxcli data).
        if (IsDomainModelElement(documentType))
        {
            reasons.Add($"route=NAME type='{documentType}' → module '{moduleName}' DomainModel (no focus)");
            return (module.DomainModel, null, string.Join("; ", reasons));
        }

        var doc = FindDocument(module, localName);
        reasons.Add(doc != null
            ? $"route=NAME document '{localName}' found in module '{moduleName}'"
            : $"route=NAME document '{localName}' NOT found in module '{moduleName}' (searched recursively)");
        return (doc, null, string.Join("; ", reasons));
    }

    private static bool IsEntity(string documentType)
        => documentType.Equals("Entity", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Document types that are NOT standalone public units but live in the domain model
    /// (entity/attribute/association), plus the domain model itself. Case-insensitive.
    /// </summary>
    private static bool IsDomainModelElement(string documentType)
        => documentType.Equals("Entity", StringComparison.OrdinalIgnoreCase)
        || documentType.Equals("Attribute", StringComparison.OrdinalIgnoreCase)
        || documentType.Equals("Association", StringComparison.OrdinalIgnoreCase)
        || documentType.Equals("DomainModel", StringComparison.OrdinalIgnoreCase);

    /// <summary>Project-level security artifact (not a module document, no open API in 11.10).</summary>
    private static bool IsProjectSecurity(string documentType)
        => documentType.Equals("ProjectSecurity", StringComparison.OrdinalIgnoreCase)
        || documentType.Equals("Security", StringComparison.OrdinalIgnoreCase);

    private static bool IsEnumeration(string documentType)
        => documentType.Equals("Enumeration", StringComparison.OrdinalIgnoreCase);

    /// <summary>Snippet: not modeled as a unit in the 11.10 ExtensionsAPI (no open handle).</summary>
    private static bool IsSnippet(string documentType)
        => documentType.Equals("Snippet", StringComparison.OrdinalIgnoreCase);

    /// <summary>Finds a document by name within a module + (recursively) its subfolders.</summary>
    private static IAbstractUnit? FindDocument(IFolderBase container, string documentName)
    {
        var direct = container.GetDocuments().FirstOrDefault(d => d.Name == documentName);
        if (direct != null) return direct;

        foreach (var folder in container.GetFolders())
        {
            var found = FindDocument(folder, documentName);
            if (found != null) return found;
        }
        return null;
    }
}
