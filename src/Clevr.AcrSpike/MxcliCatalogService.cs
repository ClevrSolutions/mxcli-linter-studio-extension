using Clevr.Acr.Normalizer;
using Mendix.StudioPro.ExtensionsAPI.Services;

namespace Clevr.AcrSpike;

/// <summary>
/// mxcli-CATALOG-provider (Apache-2.0). Bevraagt de lokale SQLite-catalog via mxcli's MDL-SQL
/// (<c>-p &lt;mpr&gt; -c "SELECT … FROM CATALOG.*"</c>) en draait er de pure <see cref="CatalogRules"/>
/// op. Dit is de NIEUWE databron achter de provider-abstractie; vervangt de mxlint-export-route voor de
/// 7 bewezen "robuuste" onderwerpen (MAINT-007/010/014, SEC-007/009/011, PERF-001).
///
/// Alleen IO/bedrading + markdown-tabel-parsing (zoals <c>LoadPersistentEntityQns</c>); de regel-logica
/// en ACR-mapping zitten puur in de normalizer. Best-effort: faalt een query (tabel ontbreekt in een
/// mxcli-versie, of de catalog is niet gebouwd), dan levert dat onderwerp 0 — de scan crasht niet.
///
/// Getoetst tegen mxcli v0.11.0; let op de beperkte MDL-SQL (AS verplicht op aliassen, geen
/// ORDER BY+LIMIT-combinatie, geen NOT IN-subquery) — daarom doen we filtering/aggregatie in C#.
/// </summary>
public sealed class MxcliCatalogService
{
    private readonly string _mxcliPath;
    private readonly string _mprFileName;
    private readonly string _projectDir;
    private readonly ILogService _log;

    public MxcliCatalogService(string mxcliPath, string mprFileName, string projectDir, ILogService log)
    {
        _mxcliPath = mxcliPath;
        _mprFileName = mprFileName;
        _projectDir = projectDir;
        _log = log;
    }

    /// <summary>Draait de 7 catalog-regels en levert alle violations (best-effort per regel).</summary>
    public IReadOnlyList<Violation> GetViolations()
    {
        var result = new List<Violation>();

        // user-modules (Source leeg) — nodig voor SEC-007-scoping én MAINT-014.
        var modules = QueryModules();
        var userModules = new HashSet<string>(
            modules.Where(m => string.IsNullOrEmpty(m.Source)).Select(m => m.Name), System.StringComparer.Ordinal);

        Safe("MAINT-007", () => CatalogRules.MicroflowSize(QueryMicroflows()), result);
        Safe("MAINT-010", () => CatalogRules.AttributeDefaultValues(QueryAttributes()), result);
        Safe("MAINT-014", () => CatalogRules.ModuleCount(modules), result);
        Safe("SEC-011", () => CatalogRules.ExposedConstants(QueryConstants()), result);
        Safe("PERF-001", () => CatalogRules.InheritAdmin(QueryEntities()), result);
        Safe("SEC-007", () => CatalogRules.SystemAssociations(QueryAssociations(), userModules), result);
        Safe("SEC-009", () => CatalogRules.HashAlgorithm(QueryHashAlgorithm()), result);

        _log.Info($"[CLEVR ACR] mxcli-catalog-provider: {result.Count} violation(s) over 7 regels");
        return result;
    }

    /// <summary>
    /// App-store/marketplace-modulenamen (CATALOG.MODULES.Source niet leeg) — voor het UI-marktplaats-
    /// filter (DEEL 3). Zelfde Source-mechanisme als FASE 1 (user-module = Source leeg). Best-effort.
    /// </summary>
    public IReadOnlyList<string> AppStoreModuleNames()
    {
        try { return QueryModules().Where(m => !string.IsNullOrEmpty(m.Source)).Select(m => m.Name).ToList(); }
        catch (System.Exception ex) { _log.Warn($"[CLEVR ACR] app-store-modulelijst overgeslagen: {ex.Message}"); return System.Array.Empty<string>(); }
    }

    private void Safe(string ruleTag, System.Func<IReadOnlyList<Violation>> run, List<Violation> sink)
    {
        try { sink.AddRange(run()); }
        catch (System.Exception ex) { _log.Warn($"[CLEVR ACR] catalog-regel {ruleTag} overgeslagen: {ex.Message}"); }
    }

    // ── queries → getypeerde rijen ─────────────────────────────────────────────────────────────────
    private List<CatalogRules.Microflow> QueryMicroflows() =>
        Rows("SELECT QualifiedName, ModuleName, ActivityCount FROM CATALOG.MICROFLOWS")
            .Select(c => new CatalogRules.Microflow(c[0], c[1], ParseInt(c[2]))).ToList();

    private List<CatalogRules.Attribute> QueryAttributes() =>
        Rows("SELECT EntityQualifiedName, ModuleName, Name, DefaultValue, IsCalculated FROM CATALOG.ATTRIBUTES")
            .Select(c => new CatalogRules.Attribute(c[0], c[1], c[2], c[3], ParseBool(c[4]))).ToList();

    private List<CatalogRules.Module> QueryModules() =>
        Rows("SELECT Name, Source FROM CATALOG.MODULES")
            .Select(c => new CatalogRules.Module(c[0], c[1])).ToList();

    private List<CatalogRules.Constant> QueryConstants() =>
        Rows("SELECT QualifiedName, ModuleName, Name, ExposedToClient FROM CATALOG.CONSTANTS")
            .Select(c => new CatalogRules.Constant(c[0], c[1], c[2], ParseBool(c[3]))).ToList();

    private List<CatalogRules.Entity> QueryEntities() =>
        Rows("SELECT QualifiedName, ModuleName, Generalization FROM CATALOG.ENTITIES")
            .Select(c => new CatalogRules.Entity(c[0], c[1], string.IsNullOrEmpty(c[2]) ? null : c[2])).ToList();

    private List<CatalogRules.Association> QueryAssociations() =>
        Rows("SELECT QualifiedName, ModuleName, Name, ToEntity FROM CATALOG.ASSOCIATIONS")
            .Select(c => new CatalogRules.Association(c[0], c[1], c[2], c[3])).ToList();

    /// <summary>Hash-algoritme uit SHOW SETTINGS (Model Settings-rij: "… Hash: BCrypt, …").</summary>
    private string? QueryHashAlgorithm()
    {
        var proc = ProcessRunner.Run(_mxcliPath, $"-p \"{_mprFileName}\" -c \"SHOW SETTINGS\"", _projectDir);
        foreach (var line in (proc.StdOut ?? "").Split('\n'))
        {
            var idx = line.IndexOf("Hash:", System.StringComparison.Ordinal);
            if (idx < 0) continue;
            var rest = line[(idx + "Hash:".Length)..].Trim();
            // tot de eerstvolgende komma of pipe.
            var end = rest.IndexOfAny(new[] { ',', '|' });
            return (end >= 0 ? rest[..end] : rest).Trim();
        }
        return null;
    }

    // ── generieke markdown-tabel-parser (zelfde vorm als LoadPersistentEntityQns) ────────────────────
    private List<string[]> Rows(string sql)
    {
        var proc = ProcessRunner.Run(_mxcliPath, $"-p \"{_mprFileName}\" -c \"{sql}\"", _projectDir);
        var rows = new List<string[]>();
        var headerSeen = false;
        foreach (var line in (proc.StdOut ?? "").Split('\n'))
        {
            var t = line.Trim();
            if (!t.StartsWith("|")) continue;
            // separator-rij (|----|----|) overslaan.
            if (t.Trim('|', '-', ' ').Length == 0) continue;
            var cells = t.Trim('|').Split('|').Select(s => s.Trim()).ToArray();
            if (!headerSeen) { headerSeen = true; continue; } // eerste pipe-rij = kolomkoppen
            rows.Add(cells);
        }
        return rows;
    }

    private static int ParseInt(string s) => int.TryParse(s.Trim(), out var n) ? n : 0;

    private static bool ParseBool(string s)
    {
        var t = s.Trim();
        return t == "1" || string.Equals(t, "true", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(t, "yes", System.StringComparison.OrdinalIgnoreCase);
    }
}
