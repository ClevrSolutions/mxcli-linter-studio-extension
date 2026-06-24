using System.Text.RegularExpressions;

namespace Clevr.Acr.Normalizer;

/// <summary>
/// Eerste eigen CLEVR-regel op de PROJECT-SECURITY-EXPORT (niet uit mxcli/mxlint).
/// ACR #12: "Project role should have at most one module role per module."
///
/// Bron: de project-security-YAML die de mxlint-export-stap al produceert
/// (<c>modelsource/Security$ProjectSecurity.yaml</c>), met:
///   UserRoles:
///     - $Type: Security$UserRole
///       Name: Administrator
///       ModuleRoles:
///         - ModuleA.Admin
///         - ModuleA.User      # ← twee rollen uit dezelfde module = overtreding
///         - ModuleB.User
/// Per user-role groeperen we de ModuleRoles op het module-deel (vóór de eerste '.'); een
/// module met >1 module-role onder dezelfde user-role is één overtreding.
///
/// Puur: YAML-tekst in, Violation[] uit. Geen IO. Spiegelt <see cref="BsonMicroflowParser"/>.
///
/// CATEGORIE-KEUZE (bevestigen met Michel): ACR plaatst deze regel onder Performance, maar de
/// inhoud — te veel rollen per module — is feitelijk een structuur/maintainability-zaak. We
/// labelen 'm daarom als <see cref="Category"/> = "Maintainability". Eén constante om bij te stellen.
/// </summary>
public static class ProjectSecurityParser
{
    public const string RuleId = "CLEVR-MAINT-005";
    public const string AcrCode = "ProjectRoleMaxOneModuleRolePerModule"; // ACR-rulenaam (beschrijvend)
    public const string EngineRuleKey = "CLEVR_SEC_ONE_MODULEROLE_PER_MODULE"; // identiteit (zelf-geproduceerd, niet mxcli-geclaimd)
    public const string Engine = "security"; // alleen debug; de UI toont dit nooit
    public const string Category = "Maintainability"; // ← knop voor Michel (ACR: Performance)
    public const string Severity = "Critical";

    private static readonly Regex NameField = new(@"^Name:\s*(.+?)\s*$", RegexOptions.Compiled);

    public static IReadOnlyList<Violation> Detect(string projectSecurityYaml)
    {
        var result = new List<Violation>();
        if (string.IsNullOrWhiteSpace(projectSecurityYaml)) return result;

        var lines = projectSecurityYaml.Replace("\r", "").Split('\n');

        var inUserRoles = false;
        string? curName = null;
        List<string>? curModuleRoles = null;
        var inModuleRoles = false;

        void Flush()
        {
            if (curName is { Length: > 0 } && curModuleRoles is not null)
                EmitForRole(curName, curModuleRoles, result);
            curName = null;
            curModuleRoles = null;
            inModuleRoles = false;
        }

        foreach (var line in lines)
        {
            if (!inUserRoles)
            {
                if (line.TrimEnd() == "UserRoles:") inUserRoles = true;
                continue;
            }

            // Een niet-ingesprongen, niet-lege regel = volgende top-level sleutel → einde UserRoles.
            if (line.Length > 0 && !char.IsWhiteSpace(line[0])) { Flush(); break; }

            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue;

            // Start van een nieuwe user-role.
            if (trimmed.StartsWith("- ") && trimmed.Contains("$Type:") && trimmed.Contains("Security$UserRole"))
            {
                Flush();
                curName = null;
                curModuleRoles = new List<string>();
                inModuleRoles = false;
                continue;
            }
            if (curModuleRoles is null) continue; // nog niet in een user-role-item

            // Binnen de ModuleRoles-lijst: verzamel de "Module.Role"-entries.
            if (inModuleRoles && trimmed.StartsWith("- "))
            {
                var entry = trimmed[2..].Trim();
                if (entry.Length > 0) curModuleRoles.Add(entry);
                continue;
            }

            // Geen lijst-item meer → de ModuleRoles-lijst is afgelopen.
            inModuleRoles = false;
            if (trimmed.StartsWith("ModuleRoles:")) { inModuleRoles = true; continue; }
            var nm = NameField.Match(trimmed);
            if (nm.Success) curName = nm.Groups[1].Value.Trim();
        }
        Flush(); // laatste user-role (als UserRoles de laatste sectie is)

        return result;
    }

    private static void EmitForRole(string roleName, List<string> moduleRoles, List<Violation> result)
    {
        // Groepeer op het module-deel (vóór de eerste '.'); behoud invoegvolgorde.
        var byModule = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var mr in moduleRoles)
        {
            var dot = mr.IndexOf('.');
            if (dot <= 0) continue; // geen geldige "Module.Role"
            var module = mr[..dot];
            if (!byModule.TryGetValue(module, out var list))
            {
                list = new List<string>();
                byModule[module] = list;
                order.Add(module);
            }
            list.Add(mr);
        }

        foreach (var module in order)
        {
            var roles = byModule[module];
            if (roles.Count <= 1) continue; // ≤1 module-role uit deze module = correct
            result.Add(new Violation
            {
                RuleId = RuleId,
                Kind = ViolationKind.Acr,
                Source = "clevr-acr",
                AcrCode = AcrCode,
                Engine = Engine,
                Category = Category,
                Severity = Severity,
                DocumentType = "ProjectSecurity",
                DocumentQualifiedName = roleName,
                ElementName = module,
                Reason = $"User role '{roleName}' has {roles.Count} module roles from module '{module}' ({string.Join(", ", roles)}). A project role should have at most one module role per module.",
                Suggestion = "Consolidate to a single module role for this module, or split the user role so each module contributes one role.",
                Fingerprint = Fingerprint.Compute(RuleId, roleName, module),
            });
        }
    }

    // ============================================================================
    // ACR #7 — "Anonymous users should only be allowed to create non-persistent entities."
    // ============================================================================
    // Anonieme rol-set = de ModuleRoles van de GuestUserRole, MITS EnableGuestAccess true is
    // (anders geen anonieme rol → 0 violations). Een PERSISTENTE entiteit met een AccessRule die
    // AllowCreate:true heeft én een AllowedModuleRole in die set = violation. Non-persistente
    // entiteiten met create-recht zijn toegestaan.
    //
    // BRONNEN (twee, bewust gescheiden): access-rules + create-recht uit de domain-model-YAML;
    // de persistable-vlag uit CATALOG.entities.EntityType (betrouwbaarste bron — de YAML zet
    // Persistable genest onder MaybeGeneralization en ÉRFT 'm via generalization, dus niet plat
    // leesbaar). De caller levert de set persistente QualifiedNames + de YAML's aan (pure parser).
    public const string Rule7Id = "CLEVR-SEC-005";
    public const string Rule7AcrCode = "AnonymousCreatePersistentEntity";
    public const string Rule7EngineRuleKey = "CLEVR_SEC_ANON_CREATE_PERSISTENT";
    public const string Rule7Category = "Security"; // zoals ACR
    public const string Rule7Severity = "Blocker";  // zoals ACR

    public static IReadOnlyList<Violation> DetectAnonymousCreateOnPersistent(
        string projectSecurityYaml,
        IEnumerable<(string Module, string Yaml)> domainModels,
        IReadOnlySet<string> persistentEntityQualifiedNames)
    {
        var result = new List<Violation>();
        var anon = AnonymousRoleSet(projectSecurityYaml);
        if (anon.Count == 0) return result; // guest uit of geen guest-rol → geen anonieme rol

        foreach (var (module, yaml) in domainModels)
        {
            foreach (var ent in ParseEntities(yaml))
            {
                if (ent.Name.Length == 0) continue;
                var qn = $"{module}.{ent.Name}";
                if (!persistentEntityQualifiedNames.Contains(qn)) continue; // alleen persistente
                foreach (var rule in ent.Rules)
                {
                    if (!rule.AllowCreate) continue;
                    var via = rule.Roles.Where(anon.Contains).ToList();
                    if (via.Count == 0) continue;
                    result.Add(new Violation
                    {
                        RuleId = Rule7Id,
                        Kind = ViolationKind.Acr,
                        Source = "clevr-acr",
                        AcrCode = Rule7AcrCode,
                        Engine = Engine,
                        Category = Rule7Category,
                        Severity = Rule7Severity,
                        DocumentType = "Entity",
                        DocumentQualifiedName = qn,
                        ElementName = "",
                        Reason = $"Persistent entity '{qn}' grants Create to the anonymous role (via {string.Join(", ", via)}). Anonymous users should only be allowed to create non-persistent entities.",
                        Suggestion = "Remove Create for the anonymous role on this persistent entity, or make the entity non-persistent if it is meant for anonymous input.",
                        Fingerprint = Fingerprint.Compute(Rule7Id, qn, ""),
                    });
                    break; // één violation per entiteit
                }
            }
        }
        return result;
    }

    /// <summary>
    /// De anonieme rol-set: de ModuleRoles van de GuestUserRole, of leeg als
    /// EnableGuestAccess false is / de guest-rol niet bestaat. Public zodat ook regel #10 'm hergebruikt.
    /// </summary>
    public static IReadOnlySet<string> AnonymousRoleSet(string projectSecurityYaml)
    {
        var empty = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(projectSecurityYaml)) return empty;
        var lines = projectSecurityYaml.Replace("\r", "").Split('\n');

        var enableGuest = false;
        string? guestRole = null;
        var roleModuleRoles = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        var inUserRoles = false;
        string? curName = null;
        List<string>? curMrs = null;
        var inMr = false;
        void FlushRole() { if (curName is { Length: > 0 } && curMrs is not null) roleModuleRoles[curName] = curMrs; curName = null; curMrs = null; inMr = false; }

        foreach (var line in lines)
        {
            if (!inUserRoles)
            {
                // Top-level velden (kolom 0).
                if (line.StartsWith("EnableGuestAccess:", StringComparison.Ordinal))
                    enableGuest = line.Contains("true");
                else if (line.StartsWith("GuestUserRole:", StringComparison.Ordinal))
                    guestRole = line["GuestUserRole:".Length..].Trim();
                else if (line.TrimEnd() == "UserRoles:")
                    inUserRoles = true;
                continue;
            }
            if (line.Length > 0 && !char.IsWhiteSpace(line[0])) { FlushRole(); break; } // einde UserRoles

            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue;
            if (trimmed.StartsWith("- ") && trimmed.Contains("$Type:") && trimmed.Contains("Security$UserRole"))
            { FlushRole(); curName = null; curMrs = new List<string>(); inMr = false; continue; }
            if (curMrs is null) continue;
            if (inMr && trimmed.StartsWith("- ")) { var e = trimmed[2..].Trim(); if (e.Length > 0) curMrs.Add(e); continue; }
            inMr = false;
            if (trimmed.StartsWith("ModuleRoles:")) { inMr = true; continue; }
            var nm = NameField.Match(trimmed);
            if (nm.Success) curName = nm.Groups[1].Value.Trim();
        }
        FlushRole();

        if (!enableGuest || string.IsNullOrEmpty(guestRole)) return empty;
        return roleModuleRoles.TryGetValue(guestRole!, out var mrs)
            ? new HashSet<string>(mrs, StringComparer.Ordinal)
            : empty;
    }

    // ============================================================================
    // ACR #10 — "Unlimited string attributes should not be editable by anonymous users."
    // ============================================================================
    // Een string-attribuut met Length 0 (= unlimited) dat via een AccessRule met een anonieme
    // AllowedModuleRole ReadWrite (MemberAccess) is = violation. Length uit de YAML (NewType →
    // StringAttributeType → Length); CATALOG.attributes.Length is onbetrouwbaar (0 voor alles).
    // Geen persistable-filter: bewerkbaarheid door anoniem is het risico, ongeacht persistentie.
    public const string Rule10Id = "CLEVR-SEC-006";
    public const string Rule10AcrCode = "AnonymousEditableUnlimitedString";
    public const string Rule10EngineRuleKey = "CLEVR_SEC_ANON_EDIT_UNLIMITED_STRING";
    public const string Rule10Category = "Security"; // zoals ACR
    public const string Rule10Severity = "Blocker";  // zoals ACR

    public static IReadOnlyList<Violation> DetectAnonymousEditableUnlimitedStrings(
        string projectSecurityYaml,
        IEnumerable<(string Module, string Yaml)> domainModels)
    {
        var result = new List<Violation>();
        var anon = AnonymousRoleSet(projectSecurityYaml);
        if (anon.Count == 0) return result;

        // Parse alle entiteiten (met attributen + member-accesses) per module.
        var all = new List<(string Module, EntityFull Ent)>();
        foreach (var (module, yaml) in domainModels)
            foreach (var e in ParseEntitiesWithAttributes(yaml))
                all.Add((module, e));

        // 1) Globale set van unlimited-string-attribuut-QN's (Module.Entity.Attr).
        var unlimited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (module, e) in all)
            if (e.Name.Length > 0)
                foreach (var a in e.Attributes)
                    if (a.IsString && a.Length == 0 && a.Name.Length > 0)
                        unlimited.Add($"{module}.{e.Name}.{a.Name}");

        // 2) Attributen die ReadWrite zijn voor de anonieme rol én unlimited string → violation.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (module, e) in all)
        {
            if (e.Name.Length == 0) continue;
            foreach (var rule in e.Rules)
            {
                var via = rule.Roles.Where(anon.Contains).ToList();
                if (via.Count == 0) continue;
                foreach (var ma in rule.MemberAccesses)
                {
                    if (ma.Rights != "ReadWrite" || ma.Attribute.Length == 0) continue;
                    if (!unlimited.Contains(ma.Attribute) || !seen.Add(ma.Attribute)) continue;
                    var entityQn = $"{module}.{e.Name}";
                    var attrName = ma.Attribute.Length > entityQn.Length + 1 && ma.Attribute.StartsWith(entityQn + ".", StringComparison.Ordinal)
                        ? ma.Attribute[(entityQn.Length + 1)..]
                        : ma.Attribute;
                    result.Add(new Violation
                    {
                        RuleId = Rule10Id,
                        Kind = ViolationKind.Acr,
                        Source = "clevr-acr",
                        AcrCode = Rule10AcrCode,
                        Engine = Engine,
                        Category = Rule10Category,
                        Severity = Rule10Severity,
                        DocumentType = "Entity",
                        DocumentQualifiedName = entityQn,
                        ElementName = attrName,
                        Reason = $"Unlimited (length 0) string attribute '{ma.Attribute}' is editable (ReadWrite) by the anonymous role (via {string.Join(", ", via)}). Unlimited string attributes should not be editable by anonymous users.",
                        Suggestion = "Set a maximum length on the attribute, or make it read-only (or remove it) for the anonymous role.",
                        Fingerprint = Fingerprint.Compute(Rule10Id, entityQn, attrName),
                    });
                }
            }
        }
        return result;
    }

    // ============================================================================
    // mxlint 002_0009 NoDefaultValue (Maintainability, LOW). Geïnternaliseerd op de domein-model-
    // YAML-route: een attribuut met een gezette, niet-lege DefaultValue. Hergebruikt
    // ParseEntitiesWithAttributes (nu ook DefaultValue, correct ontquote). De .rego flagt ELKE
    // niet-lege default (incl. boolean "false"/integer "0"); we reproduceren dat getrouw.
    // ============================================================================
    public const string NoDefaultRuleId = "CLEVR-MAINT-010";
    public const string NoDefaultAcrCode = "NoDefaultValue";
    public const string NoDefaultEngineRuleKey = "CLEVR_MAINT_NO_DEFAULT_VALUE";
    public const string NoDefaultCategory = "Maintainability"; // letterlijk uit de .rego (één van de zes)
    public const string NoDefaultSeverity = "Minor";           // mxlint LOW → ACR Minor (voorstel)

    public static IReadOnlyList<Violation> DetectNoDefaultValue(IEnumerable<(string Module, string Yaml)> domainModels)
    {
        var result = new List<Violation>();
        foreach (var (module, yaml) in domainModels)
            foreach (var e in ParseEntitiesWithAttributes(yaml))
            {
                if (e.Name.Length == 0) continue;
                foreach (var a in e.Attributes)
                {
                    if (a.Name.Length == 0 || string.IsNullOrEmpty(a.DefaultValue)) continue; // != null && != ""
                    var entityQn = $"{module}.{e.Name}";
                    result.Add(new Violation
                    {
                        RuleId = NoDefaultRuleId,
                        Kind = ViolationKind.Acr,
                        Source = "clevr-acr",
                        AcrCode = NoDefaultAcrCode,
                        Engine = Engine,
                        Category = NoDefaultCategory,
                        Severity = NoDefaultSeverity,
                        DocumentType = "Entity",
                        DocumentQualifiedName = entityQn,
                        ElementName = a.Name,
                        Reason = $"Attribute '{a.Name}' has a default value set ('{a.DefaultValue}'). Avoid default values: they introduce hidden logic that is hard to detect via 'find changes'.",
                        Suggestion = "Remove the attribute default value and set it explicitly in logic where needed.",
                        Fingerprint = Fingerprint.Compute(NoDefaultRuleId, entityQn, a.Name),
                    });
                }
            }
        return result;
    }

    /// <summary>Ontquote een YAML-scalar-tekst: "" / '' → leeg; "x"/'x' → x; bare → bare.</summary>
    private static string Unquote(string s)
        => s.Length >= 2 && ((s[0] == '"' && s[^1] == '"') || (s[0] == '\'' && s[^1] == '\''))
            ? s[1..^1]
            : s;

    // ============================================================================
    // Domein-model-batch (mxlint 002_*). N.B.: de mxlint-tegenhangers zijn op Windows DORMANT
    // (hun input-patroon ".*/DomainModels…" matcht geen backslash-paden → 0 files); de YamlDotNet-
    // grondwaarheid is de geldige toets. 002_0004 NIET geïnternaliseerd (buggy over-fire).
    // ============================================================================

    // 002_0001 NumberOfPersistentEntities (Maintainability, MEDIUM→Major). Per domeinmodel.
    public const string PersistEntitiesRuleId = "CLEVR-MAINT-011";
    public const string PersistEntitiesAcrCode = "NumberOfPersistantEntities";
    private const int MaxPersistentEntities = 15;

    public static IReadOnlyList<Violation> DetectNumberOfPersistentEntities(IEnumerable<(string Module, string Yaml)> domainModels)
    {
        var result = new List<Violation>();
        foreach (var (module, yaml) in domainModels)
        {
            var ents = ParseEntitiesWithAttributes(yaml).ToList();
            if (ents.Count == 0) continue;
            // non-persistent = MaybeGeneralization.Persistable == false; al het andere (true/null) = persistent.
            int persistent = ents.Count(e => e.Persistable != false);
            if (persistent <= MaxPersistentEntities) continue;
            result.Add(new Violation
            {
                RuleId = PersistEntitiesRuleId, Kind = ViolationKind.Acr, Source = "clevr-acr", AcrCode = PersistEntitiesAcrCode,
                Engine = Engine, Category = "Maintainability", Severity = "Major", DocumentType = "DomainModel",
                DocumentQualifiedName = module, ElementName = "",
                Reason = $"Domain model has {persistent} persistent entities, which is more than {MaxPersistentEntities}. Large domain models are harder to maintain.",
                Suggestion = "Split the domain model into multiple modules.",
                Fingerprint = Fingerprint.Compute(PersistEntitiesRuleId, module, ""),
            });
        }
        return result;
    }

    // 002_0003 InheritFromAdministrationAccount (Performance, MEDIUM→Major). Per entiteit.
    public const string InheritAdminRuleId = "CLEVR-PERF-001";
    public const string InheritAdminAcrCode = "AvoidInheritanceFromAdministrationAccount";

    public static IReadOnlyList<Violation> DetectInheritFromAdministrationAccount(IEnumerable<(string Module, string Yaml)> domainModels)
    {
        var result = new List<Violation>();
        foreach (var (module, yaml) in domainModels)
            foreach (var e in ParseEntitiesWithAttributes(yaml))
                if (e.Name.Length > 0 && e.Generalization == "Administration.Account")
                    result.Add(new Violation
                    {
                        RuleId = InheritAdminRuleId, Kind = ViolationKind.Acr, Source = "clevr-acr", AcrCode = InheritAdminAcrCode,
                        Engine = Engine, Category = "Performance", Severity = "Major", DocumentType = "Entity",
                        DocumentQualifiedName = $"{module}.{e.Name}", ElementName = "",
                        Reason = "Entity inherits from 'Administration.Account'. There is no need to inherit from it; unnecessary inheritance hurts performance.",
                        Suggestion = "Inherit from System.User instead, or adapt Administration.Account to fit your needs.",
                        Fingerprint = Fingerprint.Compute(InheritAdminRuleId, $"{module}.{e.Name}", ""),
                    });
        return result;
    }

    // 002_0005 AvoidSystemEntityAssociation (Security, HIGH→Critical). Top-level CrossAssociations.
    public const string SysAssocRuleId = "CLEVR-SEC-007";
    public const string SysAssocAcrCode = "AvoidSystemEntityAssociation";

    public static IReadOnlyList<Violation> DetectSystemEntityAssociation(IEnumerable<(string Module, string Yaml)> domainModels)
    {
        var result = new List<Violation>();
        foreach (var (module, yaml) in domainModels)
            foreach (var (name, child) in ParseCrossAssociations(yaml))
                if (child.StartsWith("System.", StringComparison.Ordinal))
                    result.Add(new Violation
                    {
                        RuleId = SysAssocRuleId, Kind = ViolationKind.Acr, Source = "clevr-acr", AcrCode = SysAssocAcrCode,
                        Engine = Engine, Category = "Security", Severity = "Critical", DocumentType = "DomainModel",
                        DocumentQualifiedName = module, ElementName = name,
                        Reason = $"Cross-association '{name}' refers to System entity '{child}', which has limited security configuration.",
                        Suggestion = "Remove direct associations to the System domain model; use inheritance (Generalization) instead.",
                        Fingerprint = Fingerprint.Compute(SysAssocRuleId, module, name),
                    });
        return result;
    }

    // 002_0006 AvoidTooManyVirtualAttributes (Performance, MEDIUM→Major). >10 CalculatedValue per entiteit.
    public const string VirtualAttrsRuleId = "CLEVR-PERF-002";
    public const string VirtualAttrsAcrCode = "AvoidTooManyVirtualAttributes";
    private const int MaxVirtualAttributes = 10;

    public static IReadOnlyList<Violation> DetectTooManyVirtualAttributes(IEnumerable<(string Module, string Yaml)> domainModels)
    {
        var result = new List<Violation>();
        foreach (var (module, yaml) in domainModels)
            foreach (var e in ParseEntitiesWithAttributes(yaml))
            {
                if (e.Name.Length == 0) continue;
                int calc = e.Attributes.Count(a => a.ValueType == "DomainModels$CalculatedValue");
                if (calc <= MaxVirtualAttributes) continue;
                result.Add(new Violation
                {
                    RuleId = VirtualAttrsRuleId, Kind = ViolationKind.Acr, Source = "clevr-acr", AcrCode = VirtualAttrsAcrCode,
                    Engine = Engine, Category = "Performance", Severity = "Major", DocumentType = "Entity",
                    DocumentQualifiedName = $"{module}.{e.Name}", ElementName = "",
                    Reason = $"Entity has {calc} virtual (calculated) attributes, which is more than {MaxVirtualAttributes}. Too many calculated attributes cause performance issues.",
                    Suggestion = "Reduce the number of virtual attributes to 10 or fewer.",
                    Fingerprint = Fingerprint.Compute(VirtualAttrsRuleId, $"{module}.{e.Name}", ""),
                });
            }
        return result;
    }

    // 002_0007 AvoidUsingValidationRules (Maintainability, MEDIUM→Major). >0 ValidationRules per entiteit.
    public const string ValidationRulesRuleId = "CLEVR-MAINT-012";
    public const string ValidationRulesAcrCode = "AvoidUsingValidationRules";

    public static IReadOnlyList<Violation> DetectUsingValidationRules(IEnumerable<(string Module, string Yaml)> domainModels)
    {
        var result = new List<Violation>();
        foreach (var (module, yaml) in domainModels)
            foreach (var e in ParseEntitiesWithAttributes(yaml))
                if (e.Name.Length > 0 && e.ValidationRulesCount > 0)
                    result.Add(new Violation
                    {
                        RuleId = ValidationRulesRuleId, Kind = ViolationKind.Acr, Source = "clevr-acr", AcrCode = ValidationRulesAcrCode,
                        Engine = Engine, Category = "Maintainability", Severity = "Major", DocumentType = "Entity",
                        DocumentQualifiedName = $"{module}.{e.Name}", ElementName = "",
                        Reason = $"Entity has {e.ValidationRulesCount} domain-model validation rule(s), which give users unexpected errors.",
                        Suggestion = "Remove domain-model validation rules; validate in microflows instead.",
                        Fingerprint = Fingerprint.Compute(ValidationRulesRuleId, $"{module}.{e.Name}", ""),
                    });
        return result;
    }

    // 002_0008 AvoidDefaultReadWriteAccess (Maintainability, MEDIUM→Major). AccessRule met ReadWrite default.
    public const string DefaultRwRuleId = "CLEVR-MAINT-013";
    public const string DefaultRwAcrCode = "AvoidDefaultReadWriteAccess";

    public static IReadOnlyList<Violation> DetectDefaultReadWriteAccess(IEnumerable<(string Module, string Yaml)> domainModels)
    {
        var result = new List<Violation>();
        foreach (var (module, yaml) in domainModels)
            foreach (var e in ParseEntitiesWithAttributes(yaml))
            {
                if (e.Name.Length == 0) continue;
                foreach (var r in e.Rules)
                {
                    if (r.DefaultRights != "ReadWrite") continue;
                    var roles = string.Join(", ", r.Roles.Select(role => { var i = role.IndexOf('.'); return i >= 0 ? role[(i + 1)..] : role; }));
                    result.Add(new Violation
                    {
                        RuleId = DefaultRwRuleId, Kind = ViolationKind.Acr, Source = "clevr-acr", AcrCode = DefaultRwAcrCode,
                        Engine = Engine, Category = "Maintainability", Severity = "Major", DocumentType = "Entity",
                        DocumentQualifiedName = $"{module}.{e.Name}", ElementName = roles,
                        Reason = $"Entity has an access rule with default ReadWrite member access (roles: {roles}). This can lead to wrongly-set access rights.",
                        Suggestion = "Set the rule's default member access rights to Read only or None.",
                        Fingerprint = Fingerprint.Compute(DefaultRwRuleId, $"{module}.{e.Name}", roles),
                    });
                }
            }
        return result;
    }

    // ============================================================================
    // Security-/Settings-/Modules-batch (mxlint 001_0005, 001_0007, 001_0008, 003_0001).
    // Alle vier MXLINT-ONLY (geen mxcli/ACR-regel dekt het onderwerp — zie STAP 0). De mxlint-
    // tegenhangers vuren 0/1 op deze export (001_0007/0008/003_0001 om structurele redenen — zie
    // per regel); de YamlDotNet-grondwaarheid is de toets. Severity per metadata (HIGH→Critical,
    // MEDIUM→Major).
    // ============================================================================

    // 001_0005 MxAdminNotUsed (Security, HIGH→Critical). Top-level AdminUserName == "MxAdmin".
    public const string MxAdminRuleId = "CLEVR-SEC-008";
    public const string MxAdminAcrCode = "MxAdminNotUsed";
    public const string MxAdminEngineRuleKey = "CLEVR_SEC_MXADMIN_NOT_USED";

    public static IReadOnlyList<Violation> DetectMxAdminUserId(string projectSecurityYaml)
    {
        var result = new List<Violation>();
        if (string.IsNullOrWhiteSpace(projectSecurityYaml)) return result;
        foreach (var raw in projectSecurityYaml.Replace("\r", "").Split('\n'))
        {
            // AdminUserName staat op kolom 0 (top-level). Eén match volstaat.
            if (!raw.StartsWith("AdminUserName:", StringComparison.Ordinal)) continue;
            var name = Unquote(raw["AdminUserName:".Length..].Trim());
            if (name == "MxAdmin")
                result.Add(new Violation
                {
                    RuleId = MxAdminRuleId, Kind = ViolationKind.Acr, Source = "clevr-acr", AcrCode = MxAdminAcrCode,
                    Engine = Engine, Category = "Security", Severity = "Critical", DocumentType = "ProjectSecurity",
                    DocumentQualifiedName = "Administrator account", ElementName = "MxAdmin",
                    Reason = "The Mendix administrator user id is still the default 'MxAdmin'. The default account name is widely known and aids brute-force/credential attacks.",
                    Suggestion = "Rename the administrator user id to a non-default value.",
                    Fingerprint = Fingerprint.Compute(MxAdminRuleId, "Administrator account", "MxAdmin"),
                });
            break;
        }
        return result;
    }

    // 001_0008 CheckSecurityOnUserRoles (Security, HIGH→Critical). Per user-role: CheckSecurity moet
    // expliciet true zijn (afwezig/false = overtreding, zoals de Rego: `not user_role.CheckSecurity`).
    public const string CheckSecRuleId = "CLEVR-SEC-010";
    public const string CheckSecAcrCode = "CheckSecurityOnUserRoles";
    public const string CheckSecEngineRuleKey = "CLEVR_SEC_CHECK_SECURITY_ON_USER_ROLES";

    public static IReadOnlyList<Violation> DetectCheckSecurityOnUserRoles(string projectSecurityYaml)
    {
        var result = new List<Violation>();
        if (string.IsNullOrWhiteSpace(projectSecurityYaml)) return result;
        var lines = projectSecurityYaml.Replace("\r", "").Split('\n');

        var inUserRoles = false;
        var inRole = false;
        var fieldIndent = -1;
        string? curName = null;
        var curChecked = false;
        void Flush()
        {
            if (inRole && curName is { Length: > 0 } && !curChecked)
                result.Add(new Violation
                {
                    RuleId = CheckSecRuleId, Kind = ViolationKind.Acr, Source = "clevr-acr", AcrCode = CheckSecAcrCode,
                    Engine = Engine, Category = "Security", Severity = "Critical", DocumentType = "ProjectSecurity",
                    DocumentQualifiedName = curName!, ElementName = "",
                    Reason = $"User role '{curName}' has 'Check security' disabled. Security should be checked for each user role so users can only access the minimum amount of data.",
                    Suggestion = "Enable 'Check security' for this user role.",
                    Fingerprint = Fingerprint.Compute(CheckSecRuleId, curName!, ""),
                });
            inRole = false; curName = null; curChecked = false; fieldIndent = -1;
        }

        foreach (var line in lines)
        {
            if (!inUserRoles)
            {
                if (line.TrimEnd() == "UserRoles:") inUserRoles = true;
                continue;
            }
            if (line.Length > 0 && !char.IsWhiteSpace(line[0])) { Flush(); break; } // einde UserRoles

            var t = line.TrimStart();
            var ind = line.Length - t.Length;
            t = t.TrimEnd();
            if (t.Length == 0) continue;

            // Start van een nieuwe user-role.
            if (t.StartsWith("- ", StringComparison.Ordinal) && t.Contains("$Type:") && t.Contains("Security$UserRole"))
            { Flush(); inRole = true; fieldIndent = ind + 2; continue; }
            if (!inRole) continue;

            // Alleen velden op het role-veld-niveau lezen (geen geneste ModuleRoles-items).
            if (ind != fieldIndent) continue;
            if (t.StartsWith("Name:", StringComparison.Ordinal)) curName = t["Name:".Length..].Trim();
            else if (t.StartsWith("CheckSecurity:", StringComparison.Ordinal)) curChecked = t["CheckSecurity:".Length..].Trim() == "true";
        }
        Flush();
        return result;
    }

    // 001_0007 HashAlgorithm (Security, HIGH→Critical). Settings$ProjectSettings.yaml: HashAlgorithm
    // moet BCrypt of SSHA256 zijn. (De Rego leest input.Settings.HashAlgorithm; in de export zit
    // HashAlgorithm genest in een Settings-lijst-item → we zoeken het veld waar het ook staat.)
    public const string HashAlgoRuleId = "CLEVR-SEC-009";
    public const string HashAlgoAcrCode = "HashAlgorithm";
    public const string HashAlgoEngineRuleKey = "CLEVR_SEC_HASH_ALGORITHM";

    public static IReadOnlyList<Violation> DetectHashAlgorithm(string projectSettingsYaml)
    {
        var result = new List<Violation>();
        if (string.IsNullOrWhiteSpace(projectSettingsYaml)) return result;
        foreach (var raw in projectSettingsYaml.Replace("\r", "").Split('\n'))
        {
            var t = raw.Trim();
            if (!t.StartsWith("HashAlgorithm:", StringComparison.Ordinal)) continue;
            var alg = Unquote(t["HashAlgorithm:".Length..].Trim());
            // Afwezige waarde = niets te melden (de Rego's sprintf wordt dan undefined → geen error).
            if (alg.Length > 0 && alg != "BCrypt" && alg != "SSHA256")
                result.Add(new Violation
                {
                    RuleId = HashAlgoRuleId, Kind = ViolationKind.Acr, Source = "clevr-acr", AcrCode = HashAlgoAcrCode,
                    Engine = Engine, Category = "Security", Severity = "Critical", DocumentType = "ProjectSettings",
                    DocumentQualifiedName = "App settings", ElementName = alg,
                    Reason = $"The application uses the '{alg}' hash algorithm, which is not recommended. BCrypt and SSHA256 are considered the safest for data encryption.",
                    Suggestion = "Set the app's hash algorithm (App Settings > Runtime) to BCrypt or SSHA256.",
                    Fingerprint = Fingerprint.Compute(HashAlgoRuleId, "App settings", alg),
                });
            break;
        }
        return result;
    }

    // 003_0001 NumberOfModules (Maintainability, MEDIUM→Major). Metadata.yaml: >20 USER-modules
    // (FromAppStore != true). De export zet FromAppStore vlak onder elk module-item (niet genest
    // onder Attributes zoals de Rego aanneemt), dus user-module = item zónder 'FromAppStore: true'.
    public const string ModuleCountRuleId = "CLEVR-MAINT-014";
    public const string ModuleCountAcrCode = "NumberOfModules";
    public const string ModuleCountEngineRuleKey = "CLEVR_MAINT_NUMBER_OF_MODULES";
    private const int MaxUserModules = 20;

    public static IReadOnlyList<Violation> DetectNumberOfModules(string metadataYaml)
    {
        var result = new List<Violation>();
        if (string.IsNullOrWhiteSpace(metadataYaml)) return result;
        var lines = metadataYaml.Replace("\r", "").Split('\n');

        var inModules = false;
        var userModules = 0;
        var haveItem = false;
        var curFromAppStore = false;
        void Flush() { if (haveItem && !curFromAppStore) userModules++; haveItem = false; curFromAppStore = false; }

        foreach (var line in lines)
        {
            if (!inModules)
            {
                if (line.TrimEnd() == "Modules:") inModules = true;
                continue;
            }
            if (line.Length > 0 && !char.IsWhiteSpace(line[0])) { Flush(); break; } // volgende top-level sleutel

            var t = line.TrimStart().TrimEnd();
            if (t.Length == 0) continue;
            if (t.StartsWith("- ", StringComparison.Ordinal)) { Flush(); haveItem = true; continue; }
            if (!haveItem) continue;
            if (t.StartsWith("FromAppStore:", StringComparison.Ordinal) && t["FromAppStore:".Length..].Trim() == "true")
                curFromAppStore = true;
        }
        Flush();

        if (userModules > MaxUserModules)
            result.Add(new Violation
            {
                RuleId = ModuleCountRuleId, Kind = ViolationKind.Acr, Source = "clevr-acr", AcrCode = ModuleCountAcrCode,
                Engine = Engine, Category = "Maintainability", Severity = "Major", DocumentType = "Project",
                DocumentQualifiedName = "Project modules", ElementName = "",
                Reason = $"The project has {userModules} user modules, which is more than {MaxUserModules}. The bigger the application, the harder it is to maintain.",
                Suggestion = "Consider a multi-app strategy to avoid one big, unmaintainable application.",
                Fingerprint = Fingerprint.Compute(ModuleCountRuleId, "Project modules", ""),
            });
        return result;
    }

    /// <summary>Parseert top-level CrossAssociations → (Name, Child). Block-vorm (niet de lege []).</summary>
    private static IEnumerable<(string Name, string Child)> ParseCrossAssociations(string yaml)
    {
        var list = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(yaml)) return list;
        var lines = yaml.Replace("\r", "").Split('\n');
        var inBlock = false;
        string? name = null, child = null;
        void Flush() { if (child != null) list.Add((name ?? "", child)); name = null; child = null; }
        foreach (var line in lines)
        {
            var t = line.TrimEnd();
            var tt = t.TrimStart();
            if (!inBlock) { if (t == "CrossAssociations:") inBlock = true; continue; }
            if (t.Length > 0 && !char.IsWhiteSpace(t[0])) { Flush(); break; } // volgende top-level sleutel
            if (tt.StartsWith("- ", StringComparison.Ordinal) && tt.Contains("$Type:") && tt.Contains("DomainModels$CrossAssociation"))
            { Flush(); continue; }
            if (tt.StartsWith("Child:", StringComparison.Ordinal) && child == null) child = tt["Child:".Length..].Trim();
            else if (tt.StartsWith("Name:", StringComparison.Ordinal) && name == null) name = tt["Name:".Length..].Trim();
        }
        Flush();
        return list;
    }

    private sealed class EntityFull
    {
        public string Name = "";
        public List<AttrAcc> Attributes = new();
        public List<RuleFull> Rules = new();
        public string? Generalization;        // MaybeGeneralization.Generalization (null = NoGeneralization)
        public bool? Persistable;             // MaybeGeneralization.Persistable (null = absent, bv. bij Generalization)
        public int ValidationRulesCount;      // aantal directe items onder ValidationRules:
    }
    private sealed class AttrAcc { public string Name = ""; public bool IsString; public int? Length; public string? DefaultValue; public string? ValueType; }
    private sealed class RuleFull { public List<string> Roles = new(); public List<MaAcc> MemberAccesses = new(); public string? DefaultRights; }
    private sealed class MaAcc { public string Attribute = ""; public string Rights = ""; }

    /// <summary>
    /// Rijkere entity-parser voor #10: per entiteit de attributen (Name + StringAttributeType +
    /// Length) én per AccessRule de AllowedModuleRoles + MemberAccesses (Attribute-QN + AccessRights).
    /// LET OP: de entity-Name komt in de YAML ná de Attributes/AccessRules (alfabetisch), dus we
    /// verzamelen per entiteit en bouwen QN's pas na afronding (zoals #7).
    /// </summary>
    private static IEnumerable<EntityFull> ParseEntitiesWithAttributes(string yaml)
    {
        var list = new List<EntityFull>();
        if (string.IsNullOrWhiteSpace(yaml)) return list;
        var lines = yaml.Replace("\r", "").Split('\n');

        EntityFull? ent = null;
        var entIndent = 0;
        string mode = ""; // "attr" | "rule"
        AttrAcc? curAttr = null;
        var inNewType = false;
        var inValue = false; // binnen het Value:-blok van een attribuut (waar DefaultValue staat)
        RuleFull? rule = null;
        var inAmr = false;
        MaAcc? ma = null;
        void Fin() { if (ent is not null) list.Add(ent); }

        foreach (var line in lines)
        {
            var t = line.TrimStart();
            var ind = line.Length - t.Length;
            t = t.TrimEnd();
            if (t.Length == 0) continue;

            if (t.StartsWith("- ") && t.Contains("$Type:") && t.Contains("DomainModels$EntityImpl"))
            { Fin(); ent = new EntityFull(); entIndent = ind + 2; mode = ""; curAttr = null; inNewType = false; inValue = false; rule = null; inAmr = false; ma = null; continue; }
            if (ent is null) continue;

            if (ind == entIndent)
            {
                mode = ""; curAttr = null; inNewType = false; inValue = false; rule = null; inAmr = false; ma = null;
                if (t.StartsWith("Name:", StringComparison.Ordinal)) ent.Name = t["Name:".Length..].Trim();
                else if (t.StartsWith("MaybeGeneralization:", StringComparison.Ordinal)) mode = "maybegen";
                else if (t.StartsWith("ValidationRules:", StringComparison.Ordinal)) mode = "valrules";
                continue;
            }

            // MaybeGeneralization-blok: Generalization (inherit-target) + Persistable (voor 002_0001/0003).
            if (mode == "maybegen")
            {
                if (t.StartsWith("Generalization:", StringComparison.Ordinal)) ent.Generalization = t["Generalization:".Length..].Trim();
                else if (t.StartsWith("Persistable:", StringComparison.Ordinal)) ent.Persistable = t["Persistable:".Length..].Trim() == "true";
                continue;
            }
            // ValidationRules-sequence: tel de DIRECTE items (entIndent+2) — voor 002_0007.
            if (mode == "valrules")
            {
                if (t.StartsWith("- ") && ind == entIndent + 2) ent.ValidationRulesCount++;
                continue;
            }

            if (t.StartsWith("- ") && t.Contains("$Type:") && t.EndsWith("DomainModels$Attribute"))
            { curAttr = new AttrAcc(); ent.Attributes.Add(curAttr); mode = "attr"; inNewType = false; inValue = false; continue; }
            if (t.StartsWith("- ") && t.Contains("$Type:") && t.Contains("DomainModels$AccessRule"))
            { rule = new RuleFull(); ent.Rules.Add(rule); mode = "rule"; inAmr = false; ma = null; continue; }
            if (t.StartsWith("- ") && t.Contains("$Type:") && t.Contains("DomainModels$MemberAccess"))
            { ma = new MaAcc(); rule?.MemberAccesses.Add(ma); inAmr = false; continue; }

            if (mode == "attr" && curAttr is not null)
            {
                if (t.StartsWith("Name:", StringComparison.Ordinal) && curAttr.Name.Length == 0) curAttr.Name = t["Name:".Length..].Trim();
                else if (t.StartsWith("NewType:", StringComparison.Ordinal)) { inNewType = true; inValue = false; }
                else if (t.StartsWith("Value:", StringComparison.Ordinal)) { inValue = true; inNewType = false; }
                else if (inNewType && t.Contains("DomainModels$StringAttributeType")) curAttr.IsString = true;
                else if (inNewType && t.StartsWith("Length:", StringComparison.Ordinal) && int.TryParse(t["Length:".Length..].Trim(), out var len)) curAttr.Length = len;
                // DefaultValue staat onder Value: — ontquote ('' / "" → leeg; "false" → false) zodat de
                // empty-check identiek is aan de YAML-geparste waarde (mxlint leest de geparste scalar).
                else if (inValue && t.StartsWith("DefaultValue:", StringComparison.Ordinal)) curAttr.DefaultValue = Unquote(t["DefaultValue:".Length..].Trim());
                else if (inValue && t.StartsWith("$Type:", StringComparison.Ordinal)) curAttr.ValueType = t["$Type:".Length..].Trim(); // bv. DomainModels$CalculatedValue (002_0006)
                continue;
            }
            if (mode == "rule" && rule is not null)
            {
                if (inAmr) { if (t.StartsWith("- ")) { rule.Roles.Add(t[2..].Trim()); continue; } inAmr = false; }
                if (t.StartsWith("AllowedModuleRoles:", StringComparison.Ordinal)) { inAmr = true; continue; }
                if (ma is null && t.StartsWith("DefaultMemberAccessRights:", StringComparison.Ordinal)) { rule.DefaultRights = t["DefaultMemberAccessRights:".Length..].Trim(); continue; } // 002_0008
                if (ma is not null)
                {
                    if (t.StartsWith("AccessRights:", StringComparison.Ordinal)) ma.Rights = t["AccessRights:".Length..].Trim();
                    else if (t.StartsWith("Attribute:", StringComparison.Ordinal)) ma.Attribute = t["Attribute:".Length..].Trim();
                }
            }
        }
        Fin();
        return list;
    }

    private sealed class EntityAcc { public string Name = ""; public List<RuleAcc> Rules = new(); }
    private sealed class RuleAcc { public bool AllowCreate; public List<string> Roles = new(); }

    /// <summary>
    /// Parseert per domain-model-YAML de entiteiten met hun AccessRules (Name + per rule
    /// AllowCreate + AllowedModuleRoles). Persistable wordt hier NIET gelezen (komt uit CATALOG).
    /// Indentatie-gebaseerd, tolerant; spiegelt de bewezen line-parser-aanpak.
    /// </summary>
    private static IEnumerable<EntityAcc> ParseEntities(string yaml)
    {
        var list = new List<EntityAcc>();
        if (string.IsNullOrWhiteSpace(yaml)) return list;
        var lines = yaml.Replace("\r", "").Split('\n');

        EntityAcc? ent = null;
        var entIndent = 0;
        RuleAcc? rule = null;
        var inAmr = false;
        void Fin() { if (ent is not null) list.Add(ent); }

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            var ind = line.Length - trimmed.Length;
            trimmed = trimmed.TrimEnd();
            if (trimmed.Length == 0) continue;

            if (trimmed.StartsWith("- ") && trimmed.Contains("$Type:") && trimmed.Contains("DomainModels$EntityImpl"))
            { Fin(); ent = new EntityAcc(); entIndent = ind + 2; rule = null; inAmr = false; continue; }
            if (ent is null) continue;

            if (trimmed.StartsWith("- ") && trimmed.Contains("$Type:") && trimmed.Contains("DomainModels$AccessRule"))
            { rule = new RuleAcc(); ent.Rules.Add(rule); inAmr = false; continue; }

            if (ind == entIndent)
            {
                if (trimmed.StartsWith("Name:", StringComparison.Ordinal)) ent.Name = trimmed["Name:".Length..].Trim();
                inAmr = false;
                continue;
            }
            if (rule is not null)
            {
                if (inAmr)
                {
                    if (trimmed.StartsWith("- ")) { rule.Roles.Add(trimmed[2..].Trim()); continue; }
                    inAmr = false;
                }
                if (trimmed.StartsWith("AllowCreate:", StringComparison.Ordinal)) { rule.AllowCreate = trimmed["AllowCreate:".Length..].Trim() == "true"; continue; }
                if (trimmed.StartsWith("AllowedModuleRoles:", StringComparison.Ordinal)) { inAmr = true; continue; }
            }
        }
        Fin();
        return list;
    }
}
