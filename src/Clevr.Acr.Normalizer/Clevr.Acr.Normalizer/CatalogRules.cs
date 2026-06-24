namespace Clevr.Acr.Normalizer;

/// <summary>
/// PURE regel-logica voor de mxcli-CATALOG-route (Apache-2.0; vervangt de mxlint-export-route voor de
/// 7 bewezen "robuuste" onderwerpen). De spike-provider <c>MxcliCatalogService</c> bevraagt de SQLite-
/// catalog via <c>SELECT … FROM CATALOG.*</c> en levert de getypeerde rijen hieronder aan; deze klasse
/// zet ze om naar <see cref="Violation"/> met EXACT dezelfde rule-id/categorie/severity als de oude
/// route, zodat de ACR-mapping, claim-tabel en exclusions ongemoeid blijven.
///
/// BEWUSTE KALIBRATIE (besloten): MAINT-007 telt nu <c>ActivityCount</c> (mxcli-metriek) en MAINT-010
/// telt elke niet-lege <c>DefaultValue</c> (incl. impliciete type-defaults). Dat zijn ANDERE getallen
/// dan de oude mxlint-grondwaarheid (30 vs 44; 592 vs 283) — de mxcli-meting IS de nieuwe grondwaarheid.
///
/// NIET hier (bewust): MAINT-011 (↔ mxcli MPR003), PERF-002 (↔ CONV017), MAINT-012 (↔ ACR_ENT_VALRULES/
/// CONV015), commit-in-loop (↔ CONV011) — mxcli's EIGEN regels dekken die onderwerpen al, dus we bouwen
/// ze niet zelf (consistent met "zelfde onderwerp = mxcli dekt het").
/// </summary>
public static class CatalogRules
{
    public const string Engine = "mxcli-catalog"; // alleen debug; de UI toont dit nooit

    // Getypeerde catalog-rijen (één-op-één met de SELECT-kolommen die de provider ophaalt).
    public sealed record Microflow(string QualifiedName, string ModuleName, int ActivityCount);
    public sealed record Attribute(string EntityQualifiedName, string ModuleName, string Name, string? DefaultValue, bool IsCalculated);
    public sealed record Module(string Name, string? Source);
    public sealed record Constant(string QualifiedName, string ModuleName, string Name, bool ExposedToClient);
    public sealed record Entity(string QualifiedName, string ModuleName, string? Generalization);
    public sealed record Association(string QualifiedName, string ModuleName, string Name, string ToEntity);
    // CATALOG.PERMISSIONS-rij: per module-role/entiteit/member de toegangssoort (CREATE, MEMBER_WRITE, …).
    public sealed record Permission(string ModuleRoleName, string AccessType, string ElementName, string MemberName);

    // ── SEC-005 (anon create op persistente entiteit) — catalog-route i.p.v. domein-YAML. ───────────
    // Anon module-rollen (de ModuleRoles van de GuestUserRole, mits guest aan) met CREATE op een
    // PERSISTENTE entiteit. Eén violation per entiteit. Hergebruikt de rule-id/categorie/severity van
    // de oude regel (ProjectSecurityParser.Rule7*); alleen de databron is PERMISSIONS + ENTITIES.
    public static IReadOnlyList<Violation> AnonymousCreateOnPersistent(
        IReadOnlySet<string> anonModuleRoles, IEnumerable<Permission> permissions, IReadOnlySet<string> persistentEntities)
    {
        var result = new List<Violation>();
        if (anonModuleRoles.Count == 0) return result; // guest uit → geen anonieme rol
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in permissions)
        {
            if (p.AccessType != "CREATE" || !anonModuleRoles.Contains(p.ModuleRoleName)) continue;
            if (!persistentEntities.Contains(p.ElementName) || !seen.Add(p.ElementName)) continue;
            result.Add(new Violation
            {
                RuleId = ProjectSecurityParser.Rule7Id, Kind = ViolationKind.Acr, Source = "clevr-acr",
                AcrCode = ProjectSecurityParser.Rule7AcrCode, Engine = Engine,
                Category = ProjectSecurityParser.Rule7Category, Severity = ProjectSecurityParser.Rule7Severity,
                DocumentType = "Entity", DocumentQualifiedName = p.ElementName, ElementName = "",
                Reason = $"Persistent entity '{p.ElementName}' grants Create to the anonymous role (via {p.ModuleRoleName}). Anonymous users should only be allowed to create non-persistent entities.",
                Suggestion = "Remove Create for the anonymous role on this persistent entity, or make the entity non-persistent if it is meant for anonymous input.",
                Fingerprint = Fingerprint.Compute(ProjectSecurityParser.Rule7Id, p.ElementName, ""),
            });
        }
        return result;
    }

    // ── SEC-006 (anon-editable unlimited string) — catalog-route i.p.v. domein-YAML. ────────────────
    // Anon module-rol met MEMBER_WRITE op een unlimited-string-attribuut (DataType=String, Length=0).
    // Eén violation per attribuut. <paramref name="unlimitedStringAttrs"/> = gekwalificeerde namen
    // (Module.Entity.Attr) die overeenkomen met PERMISSIONS.MemberName.
    public static IReadOnlyList<Violation> AnonymousEditableUnlimitedString(
        IReadOnlySet<string> anonModuleRoles, IEnumerable<Permission> permissions, IReadOnlySet<string> unlimitedStringAttrs)
    {
        var result = new List<Violation>();
        if (anonModuleRoles.Count == 0) return result;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in permissions)
        {
            if (p.AccessType != "MEMBER_WRITE" || !anonModuleRoles.Contains(p.ModuleRoleName)) continue;
            if (!unlimitedStringAttrs.Contains(p.MemberName) || !seen.Add(p.MemberName)) continue;
            var entityQn = p.ElementName;
            var attrName = p.MemberName.Length > entityQn.Length + 1 && p.MemberName.StartsWith(entityQn + ".", StringComparison.Ordinal)
                ? p.MemberName[(entityQn.Length + 1)..]
                : p.MemberName;
            result.Add(new Violation
            {
                RuleId = ProjectSecurityParser.Rule10Id, Kind = ViolationKind.Acr, Source = "clevr-acr",
                AcrCode = ProjectSecurityParser.Rule10AcrCode, Engine = Engine,
                Category = ProjectSecurityParser.Rule10Category, Severity = ProjectSecurityParser.Rule10Severity,
                DocumentType = "Entity", DocumentQualifiedName = entityQn, ElementName = attrName,
                Reason = $"Unlimited (length 0) string attribute '{p.MemberName}' is editable (ReadWrite) by the anonymous role (via {p.ModuleRoleName}). Unlimited string attributes should not be editable by anonymous users.",
                Suggestion = "Set a maximum length on the attribute, or make it read-only (or remove it) for the anonymous role.",
                Fingerprint = Fingerprint.Compute(ProjectSecurityParser.Rule10Id, entityQn, attrName),
            });
        }
        return result;
    }

    // ── MAINT-007 microflow-grootte (mxcli ActivityCount > 25). Nieuwe GT = mxcli-telling. ──────────
    public const int MaxActivities = 25;
    public static IReadOnlyList<Violation> MicroflowSize(IEnumerable<Microflow> microflows)
    {
        var result = new List<Violation>();
        foreach (var mf in microflows)
        {
            if (mf.ActivityCount <= MaxActivities) continue;
            result.Add(new Violation
            {
                RuleId = MicroflowStructureRules.NumberOfElementsRuleId, Kind = ViolationKind.Acr, Source = "clevr-acr",
                AcrCode = MicroflowStructureRules.NumberOfElementsAcrCode, Engine = Engine,
                Category = MicroflowStructureRules.NumberOfElementsCategory, Severity = MicroflowStructureRules.NumberOfElementsSeverity,
                DocumentType = "Microflow", DocumentQualifiedName = mf.QualifiedName, ElementName = "",
                Reason = $"Microflow has {mf.ActivityCount} activities, which is more than {MaxActivities}. Large microflows are hard to read, test and maintain.",
                Suggestion = "Split logical sections into sub-microflows (SUB_).",
                Fingerprint = Fingerprint.Compute(MicroflowStructureRules.NumberOfElementsRuleId, mf.QualifiedName, ""),
            });
        }
        return result;
    }

    // ── MAINT-010 attribuut-default-waarde (elke niet-lege DefaultValue; incl. impliciete defaults). ─
    public static IReadOnlyList<Violation> AttributeDefaultValues(IEnumerable<Attribute> attributes)
    {
        var result = new List<Violation>();
        foreach (var a in attributes)
        {
            if (string.IsNullOrEmpty(a.DefaultValue) || a.Name.Length == 0) continue;
            result.Add(new Violation
            {
                RuleId = ProjectSecurityParser.NoDefaultRuleId, Kind = ViolationKind.Acr, Source = "clevr-acr",
                AcrCode = ProjectSecurityParser.NoDefaultAcrCode, Engine = Engine,
                Category = ProjectSecurityParser.NoDefaultCategory, Severity = ProjectSecurityParser.NoDefaultSeverity,
                DocumentType = "Entity", DocumentQualifiedName = a.EntityQualifiedName, ElementName = a.Name,
                Reason = $"Attribute '{a.Name}' has a default value set ('{a.DefaultValue}'). Avoid default values: they introduce hidden logic that is hard to detect via 'find changes'.",
                Suggestion = "Remove the attribute default value and set it explicitly in logic where needed.",
                Fingerprint = Fingerprint.Compute(ProjectSecurityParser.NoDefaultRuleId, a.EntityQualifiedName, a.Name),
            });
        }
        return result;
    }

    // ── MAINT-014 aantal user-modules (Source leeg = user; > 20). ───────────────────────────────────
    public const int MaxUserModules = 20;
    public static IReadOnlyList<Violation> ModuleCount(IEnumerable<Module> modules)
    {
        var userModules = modules.Count(m => string.IsNullOrEmpty(m.Source));
        if (userModules <= MaxUserModules) return System.Array.Empty<Violation>();
        return new[]
        {
            new Violation
            {
                RuleId = ProjectSecurityParser.ModuleCountRuleId, Kind = ViolationKind.Acr, Source = "clevr-acr",
                AcrCode = ProjectSecurityParser.ModuleCountAcrCode, Engine = Engine,
                Category = "Maintainability", Severity = "Major", DocumentType = "Project",
                DocumentQualifiedName = "Project modules", ElementName = "",
                Reason = $"The project has {userModules} user modules, which is more than {MaxUserModules}. The bigger the application, the harder it is to maintain.",
                Suggestion = "Consider a multi-app strategy to avoid one big, unmaintainable application.",
                Fingerprint = Fingerprint.Compute(ProjectSecurityParser.ModuleCountRuleId, "Project modules", ""),
            }
        };
    }

    // ── SEC-011 exposed constants met gevoelige naam — hergebruikt de bewezen ConstantRules-logica. ──
    public static IReadOnlyList<Violation> ExposedConstants(IEnumerable<Constant> constants)
        => ConstantRules.ExposedSensitiveConstants(constants.Select(c => (c.ModuleName, c.Name, c.ExposedToClient)));

    // ── PERF-001 inherit van Administration.Account. ────────────────────────────────────────────────
    public static IReadOnlyList<Violation> InheritAdmin(IEnumerable<Entity> entities)
    {
        var result = new List<Violation>();
        foreach (var e in entities)
        {
            if (e.Generalization != "Administration.Account") continue;
            result.Add(new Violation
            {
                RuleId = ProjectSecurityParser.InheritAdminRuleId, Kind = ViolationKind.Acr, Source = "clevr-acr",
                AcrCode = ProjectSecurityParser.InheritAdminAcrCode, Engine = Engine,
                Category = "Performance", Severity = "Major", DocumentType = "Entity",
                DocumentQualifiedName = e.QualifiedName, ElementName = "",
                Reason = "Entity inherits from 'Administration.Account'. There is no need to inherit from it; unnecessary inheritance hurts performance.",
                Suggestion = "Inherit from System.User instead, or adapt Administration.Account to fit your needs.",
                Fingerprint = Fingerprint.Compute(ProjectSecurityParser.InheritAdminRuleId, e.QualifiedName, ""),
            });
        }
        return result;
    }

    // ── SEC-007 associatie naar een System-entiteit, gescoped op USER-modules (excl. System/app-store). ─
    public static IReadOnlyList<Violation> SystemAssociations(IEnumerable<Association> associations, IReadOnlySet<string> userModules)
    {
        var result = new List<Violation>();
        foreach (var a in associations)
        {
            if (!a.ToEntity.StartsWith("System.", System.StringComparison.Ordinal)) continue;
            if (!userModules.Contains(a.ModuleName)) continue; // alleen eigen modules (excl. System + marketplace)
            result.Add(new Violation
            {
                RuleId = ProjectSecurityParser.SysAssocRuleId, Kind = ViolationKind.Acr, Source = "clevr-acr",
                AcrCode = ProjectSecurityParser.SysAssocAcrCode, Engine = Engine,
                Category = "Security", Severity = "Critical", DocumentType = "DomainModel",
                DocumentQualifiedName = a.ModuleName, ElementName = a.Name,
                Reason = $"Cross-association '{a.Name}' refers to System entity '{a.ToEntity}', which has limited security configuration.",
                Suggestion = "Remove direct associations to the System domain model; use inheritance (Generalization) instead.",
                Fingerprint = Fingerprint.Compute(ProjectSecurityParser.SysAssocRuleId, a.ModuleName, a.Name),
            });
        }
        return result;
    }

    // ── SEC-009 hash-algoritme (moet BCrypt of SSHA256 zijn). ───────────────────────────────────────
    public static IReadOnlyList<Violation> HashAlgorithm(string? hashAlgorithm)
    {
        var alg = (hashAlgorithm ?? "").Trim();
        if (alg.Length == 0 || alg == "BCrypt" || alg == "SSHA256") return System.Array.Empty<Violation>();
        return new[]
        {
            new Violation
            {
                RuleId = ProjectSecurityParser.HashAlgoRuleId, Kind = ViolationKind.Acr, Source = "clevr-acr",
                AcrCode = ProjectSecurityParser.HashAlgoAcrCode, Engine = Engine,
                Category = "Security", Severity = "Critical", DocumentType = "ProjectSettings",
                DocumentQualifiedName = "App settings", ElementName = alg,
                Reason = $"The application uses the '{alg}' hash algorithm, which is not recommended. BCrypt and SSHA256 are considered the safest for data encryption.",
                Suggestion = "Set the app's hash algorithm (App Settings > Runtime) to BCrypt or SSHA256.",
                Fingerprint = Fingerprint.Compute(ProjectSecurityParser.HashAlgoRuleId, "App settings", alg),
            }
        };
    }
}
