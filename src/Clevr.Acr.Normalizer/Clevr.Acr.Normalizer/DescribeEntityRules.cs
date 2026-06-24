using System.Text.RegularExpressions;

namespace Clevr.Acr.Normalizer;

/// <summary>
/// PURE regel op de mxcli-DESCRIBE-ENTITY-route. <c>mxcli describe entity</c> rendert access-rules als
/// MDL-grants:
///   <c>grant &lt;Role&gt; on &lt;Entity&gt; (create, delete, read *, write *);</c>
/// Een <c>write *</c> = "alle members beschrijfbaar" = DefaultMemberAccessRights == ReadWrite (de
/// MAINT-013-conditie). Member-gelimiteerde grants gebruiken <c>write (m1, m2, …)</c> en tellen niet.
///
/// Hergebruikt de rule-id/categorie/severity van <see cref="ProjectSecurityParser"/> (CLEVR-MAINT-013),
/// zodat ACR-mapping/claim-tabel identiek blijven. Eén violation per entiteit, met de default-RW-rollen
/// samengevoegd (zoals de oude YAML-route).
/// </summary>
public static class DescribeEntityRules
{
    public const string Engine = "mxcli-describe";

    // grant <Role> on <Entity> ( ... write * ... ) ;  — 'write *' (default-write-all) ergens in de body.
    private static readonly Regex GrantWriteAll =
        new(@"grant\s+(?<role>[^\s]+)\s+on\s+(?<entity>[^\s(]+)\s*\((?<body>[^;]*)\)\s*;", RegexOptions.Compiled);
    private static readonly Regex WriteAll = new(@"\bwrite\s+\*", RegexOptions.Compiled);

    /// <summary>
    /// Detecteert default-ReadWrite-access in de describe-MDL van één entiteit. <paramref name="entityQn"/>
    /// is de gekwalificeerde naam (Module.Entity). Levert ten hoogste één violation (rollen samengevoegd).
    /// </summary>
    public static IReadOnlyList<Violation> DefaultReadWriteAccess(string entityQn, string? mdl)
    {
        if (string.IsNullOrEmpty(mdl)) return System.Array.Empty<Violation>();

        // describe kan grants over meerdere regels wrappen → newlines normaliseren tot spaties.
        var flat = Regex.Replace(mdl.Replace("\r", "\n"), @"\s+", " ");

        var roles = new List<string>();
        foreach (Match m in GrantWriteAll.Matches(flat))
        {
            if (!WriteAll.IsMatch(m.Groups["body"].Value)) continue;
            var role = m.Groups["role"].Value;
            var dot = role.IndexOf('.');
            roles.Add(dot >= 0 ? role[(dot + 1)..] : role); // korte rolnaam (na de '.')
        }
        if (roles.Count == 0) return System.Array.Empty<Violation>();

        var joined = string.Join(", ", roles);
        return new[]
        {
            new Violation
            {
                RuleId = ProjectSecurityParser.DefaultRwRuleId, Kind = ViolationKind.Acr, Source = "clevr-acr",
                AcrCode = ProjectSecurityParser.DefaultRwAcrCode, Engine = Engine,
                Category = "Maintainability", Severity = "Major", DocumentType = "Entity",
                DocumentQualifiedName = entityQn, ElementName = joined,
                Reason = $"Entity has an access rule with default ReadWrite member access (roles: {joined}). This can lead to wrongly-set access rights.",
                Suggestion = "Set the rule's default member access rights to Read only or None.",
                Fingerprint = Fingerprint.Compute(ProjectSecurityParser.DefaultRwRuleId, entityQn, joined),
            }
        };
    }
}
