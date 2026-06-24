using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Clevr.Acr.Normalizer;
using Mendix.StudioPro.ExtensionsAPI.Services;

namespace Clevr.AcrSpike;

/// <summary>
/// mxcli-SECURITY-provider (Apache-2.0). Levert de GOEDKOPE security-migranten zonder mxlint-export:
///   SEC-008 (admin=MxAdmin), SEC-010 (per-userrole check-security), MAINT-005 (module-rollen/module),
///   SEC-005 (anon create op persistente entiteit), SEC-006 (anon-write unlimited string — GEDEPRECATEERD).
///
/// AANPAK (regel-logica ONGEWIJZIGD): uit `describe projectsecurity --json` (admin/guest/guest-access) +
/// `project-tree` (user-role-namen) + `describe userrole &lt;r&gt;` (module-rollen + check-security) bouwen
/// we een EQUIVALENTE project-security-YAML en voeden die aan de BESTAANDE pure predicaten
/// (ProjectSecurityParser.Detect / DetectMxAdminUserId / DetectCheckSecurityOnUserRoles / AnonymousRoleSet).
/// Alleen de databron wisselt (describe i.p.v. de export-YAML).
///
/// FAST/DEEP-SPLITSING (de `describe userrole`-calls zijn NIET batchbaar — MDL kent geen DESCRIBE
/// USERROLE; elke rol = één los proces ~2s → 9 rollen ~20s). Daarom:
///   • SNELLE scan: alleen SEC-008 (top-level admin, 0 userrole-calls) + SEC-005 (anon-create, alleen de
///     GUEST-rol nodig → ≤1 describe userrole). Goedkoop.
///   • DEEPSCAN: dezelfde twee PLUS SEC-010 + MAINT-005, die ALLE user-rollen nodig hebben (9 calls).
///
/// SEC-005/006 draaien op de CATALOG (PERMISSIONS CREATE/MEMBER_WRITE + ENTITIES PERSISTENT + ATTRIBUTES
/// String/Length=0) via de pure CatalogRules-methodes, met de anonieme rol-set uit de synth-YAML.
/// </summary>
public sealed class MxcliSecurityService
{
    private readonly string _mxcliPath;
    private readonly string _mprFileName;
    private readonly string _projectDir;
    private readonly ILogService _log;

    public MxcliSecurityService(string mxcliPath, string mprFileName, string projectDir, ILogService log)
    {
        _mxcliPath = mxcliPath; _mprFileName = mprFileName; _projectDir = projectDir; _log = log;
    }

    /// <summary>
    /// <paramref name="deepScan"/>=false (snelle scan): alleen SEC-008 + SEC-005 (≤1 describe userrole).
    /// =true (deepscan): die twee PLUS SEC-010 + MAINT-005 (alle user-rollen → 9 describe userrole-calls).
    /// SEC-008/SEC-005 emitteren dus in BEIDE modi; SEC-010/MAINT-005 alleen in deepscan.
    /// </summary>
    public IReadOnlyList<Violation> GetViolations(bool deepScan)
    {
        var result = new List<Violation>();
        try
        {
            var ps = ProjectSecurityProps();                 // Property→Value uit describe projectsecurity --json
            var perms = Permissions();                       // CATALOG.PERMISSIONS (SEC-005)
            var persistent = PersistentEntities();           // CATALOG.ENTITIES persistent (SEC-005)

            if (!deepScan)
            {
                // ── SNELLE SCAN: SEC-008 (0 userrole-calls) + SEC-005 (alleen de GUEST-rol, ≤1 call) ──
                var guestNames = GuestRoleNames(ps);                  // [guest] mits guest-access aan, anders leeg
                var fastSynth = SynthSecurityYaml(ps, guestNames);    // admin + (alleen) de guest-rol z'n module-rollen
                result.AddRange(ProjectSecurityParser.DetectMxAdminUserId(fastSynth));                   // SEC-008
                var anonFast = ProjectSecurityParser.AnonymousRoleSet(fastSynth);
                result.AddRange(CatalogRules.AnonymousCreateOnPersistent(anonFast, perms, persistent));  // SEC-005
                _log.Info($"[CLEVR ACR] mxcli-security (snel): SEC-008 + SEC-005, {guestNames.Count} guest-rol-call(s)");
            }
            else
            {
                // ── DEEPSCAN: alle user-rollen → volledige synth; alle vier de security-regels ──
                var roleNames = UserRoleNames();                 // uit project-tree
                var synth = SynthSecurityYaml(ps, roleNames);    // equivalente project-security-YAML (9 userrole-calls)

                result.AddRange(ProjectSecurityParser.DetectMxAdminUserId(synth));            // SEC-008
                result.AddRange(ProjectSecurityParser.DetectCheckSecurityOnUserRoles(synth)); // SEC-010
                result.AddRange(ProjectSecurityParser.Detect(synth));                         // MAINT-005

                var anon = ProjectSecurityParser.AnonymousRoleSet(synth);                     // anonieme module-rollen
                result.AddRange(CatalogRules.AnonymousCreateOnPersistent(anon, perms, persistent));      // SEC-005
                _log.Info($"[CLEVR ACR] mxcli-security (deep): SEC-005/008/010 + MAINT-005, {roleNames.Count} user-roles");
            }

            // SEC-006 GEBLOKKEERD/GEDEPRECATEERD: mxcli v0.11.0 ontsluit de string-MAX-LENGTE NIET (catalog
            // ATTRIBUTES.Length=0 voor ALLE strings; describe rendert ALLES als String(unlimited) — ook
            // Length:200-strings, bevestigd ook na een verse catalog-rebuild). De "unlimited vs limited"-
            // discriminator zit alleen in de modelsource-YAML, en die export verdwijnt. NIET bedraad.
            // CatalogRules.AnonymousEditableUnlimitedString + UnlimitedStringAttrs() blijven als (ongebruikte)
            // backup-code — reactiveer zodra mxcli String(N) vs String(unlimited) betrouwbaar terugleest.
            // var unlimited = UnlimitedStringAttrs();
            // result.AddRange(CatalogRules.AnonymousEditableUnlimitedString(anon, perms, unlimited));  // SEC-006 — geblokkeerd
        }
        catch (System.Exception ex)
        {
            _log.Warn($"[CLEVR ACR] mxcli-security-provider overgeslagen: {ex.Message}");
        }
        return result;
    }

    /// <summary>De guest-rol als 1-element-lijst (voor SEC-005 in de snelle scan), of leeg als guest-access
    /// uit staat of er geen guest-rol is. Zo doet de snelle scan ≤1 `describe userrole`-call.</summary>
    private static List<string> GuestRoleNames(Dictionary<string, string> ps)
    {
        var list = new List<string>();
        var on = ps.TryGetValue("GuestAccess", out var ga) && ga.Equals("true", System.StringComparison.OrdinalIgnoreCase);
        if (on && ps.TryGetValue("GuestUserRole", out var g) && !string.IsNullOrWhiteSpace(g))
            list.Add(g);
        return list;
    }

    // ── describe projectsecurity --json → Property→Value ─────────────────────────────────────────
    private Dictionary<string, string> ProjectSecurityProps()
    {
        var props = new Dictionary<string, string>(System.StringComparer.Ordinal);
        var raw = ProcessRunner.Run(_mxcliPath, $"describe projectsecurity Security --format json -p \"{_mprFileName}\"", _projectDir).StdOut ?? "";
        var start = raw.IndexOf('[');
        if (start < 0) return props;
        using var doc = JsonDocument.Parse(raw[start..]);
        foreach (var e in doc.RootElement.EnumerateArray())
            if (e.TryGetProperty("Property", out var p) && e.TryGetProperty("Value", out var v))
                props[p.GetString() ?? ""] = v.GetString() ?? "";
        return props;
    }

    // ── project-tree → user-role-namen (labels van de userrole-nodes onder projectsecurity) ─────────
    private List<string> UserRoleNames()
    {
        var names = new List<string>();
        var raw = ProcessRunner.Run(_mxcliPath, $"project-tree -p \"{_mprFileName}\"", _projectDir).StdOut ?? "";
        string? lastLabel = null;
        foreach (var line in raw.Split('\n'))
        {
            var m = Regex.Match(line, "\"label\":\\s*\"([^\"]+)\"");
            if (m.Success) { lastLabel = m.Groups[1].Value; continue; }
            if (line.Contains("\"type\": \"userrole\"") && lastLabel is not null && !names.Contains(lastLabel))
                names.Add(lastLabel);
        }
        return names;
    }

    // ── describe userrole <r> → (module-rollen, check-security) ──────────────────────────────────
    private (List<string> ModuleRoles, bool CheckSecurity) UserRole(string name)
    {
        var raw = ProcessRunner.Run(_mxcliPath, $"describe userrole \"{name}\" -p \"{_mprFileName}\"", _projectDir).StdOut ?? "";
        var roles = new List<string>();
        var open = raw.IndexOf('(');
        var close = open >= 0 ? raw.IndexOf(')', open) : -1;
        if (open >= 0 && close > open)
            foreach (var r in raw[(open + 1)..close].Split(','))
            { var t = r.Trim(); if (t.Length > 0) roles.Add(t); }
        var check = !raw.Contains("Check security: disabled", System.StringComparison.OrdinalIgnoreCase); // default enabled
        return (roles, check);
    }

    /// <summary>Bouwt de equivalente project-security-YAML zoals de export 'm zou leveren.</summary>
    private string SynthSecurityYaml(Dictionary<string, string> ps, List<string> roleNames)
    {
        var sb = new StringBuilder();
        sb.Append("$Type: Security$ProjectSecurity\n");
        if (ps.TryGetValue("AdminUser", out var admin)) sb.Append($"AdminUserName: {admin}\n");
        var guestOn = ps.TryGetValue("GuestAccess", out var ga) && ga.Equals("true", System.StringComparison.OrdinalIgnoreCase);
        sb.Append($"EnableGuestAccess: {(guestOn ? "true" : "false")}\n");
        if (ps.TryGetValue("GuestUserRole", out var guest)) sb.Append($"GuestUserRole: {guest}\n");
        sb.Append("UserRoles:\n");
        foreach (var name in roleNames)
        {
            var (mods, check) = UserRole(name);
            sb.Append("    - $Type: Security$UserRole\n");
            sb.Append($"      CheckSecurity: {(check ? "true" : "false")}\n");
            sb.Append($"      Name: {name}\n");
            sb.Append("      ModuleRoles:\n");
            foreach (var mr in mods) sb.Append($"        - {mr}\n");
        }
        return sb.ToString();
    }

    // ── catalog-bronnen voor SEC-005/006 ─────────────────────────────────────────────────────────
    private List<CatalogRules.Permission> Permissions()
        => Rows("SELECT ModuleRoleName, AccessType, ElementName, MemberName FROM CATALOG.PERMISSIONS")
            .Where(c => c.Length >= 4).Select(c => new CatalogRules.Permission(c[0], c[1], c[2], c[3])).ToList();

    private IReadOnlySet<string> PersistentEntities()
        => Rows("SELECT QualifiedName FROM CATALOG.ENTITIES WHERE EntityType='PERSISTENT'")
            .Where(c => c.Length >= 1).Select(c => c[0]).ToHashSet(System.StringComparer.Ordinal);

    private IReadOnlySet<string> UnlimitedStringAttrs()
        => Rows("SELECT EntityQualifiedName, Name FROM CATALOG.ATTRIBUTES WHERE DataType='String' AND Length=0")
            .Where(c => c.Length >= 2).Select(c => $"{c[0]}.{c[1]}").ToHashSet(System.StringComparer.Ordinal);

    private List<string[]> Rows(string sql)
    {
        var proc = ProcessRunner.Run(_mxcliPath, $"-p \"{_mprFileName}\" -c \"{sql}\"", _projectDir);
        var rows = new List<string[]>();
        var headerSeen = false;
        foreach (var line in (proc.StdOut ?? "").Split('\n'))
        {
            var t = line.Trim();
            if (!t.StartsWith("|")) continue;
            if (t.Trim('|', '-', ' ').Length == 0) continue;
            var cells = t.Trim('|').Split('|').Select(s => s.Trim()).ToArray();
            if (!headerSeen) { headerSeen = true; continue; }
            rows.Add(cells);
        }
        return rows;
    }
}
