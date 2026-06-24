namespace Clevr.Acr.Normalizer;

/// <summary>
/// Eén onderwerp in de cross-engine claim-tabel: welke bron WINT, en welke mxcli-tegenhangers
/// daarom onderdrukt worden. <see cref="Impact"/> benoemt expliciet welke findings verloren gaan
/// wanneer een onderdrukte tegenhanger méér dekt dan de winnaar (bewuste keuze, niet weggepoetst).
/// </summary>
public sealed record EngineClaim(
    string Topic,
    string Winner,
    IReadOnlyList<string> SuppressMxcli,
    string Impact);

/// <summary>
/// Cross-engine precedentie/ontdubbeling op ONDERWERP-niveau. Per onderwerp claimt één bron de
/// check; de mxcli-tegenhangers worden onderdrukt in <see cref="MxcliNormalizer"/> (SuppressMxcli).
/// De regel-LOGICA zelf blijft ongemoeid — dit is puur de aggregatie-keuze welke bron getoond wordt.
/// </summary>
public static class ClaimTable
{
    public static readonly IReadOnlyList<EngineClaim> Claims = new EngineClaim[]
    {
        // ── PROEF-onderwerp 1: microflow-grootte — CLEVR-MAINT-007 wint ──────────────────────────
        new("Microflow size", "CLEVR-MAINT-007",
            SuppressMxcli: new[] { "QUAL003", "CONV009" },
            Impact: "QUAL003 (>25 activiteiten) is een SUBSET van de winnaar → geen verlies. " +
                    "CONV009 (>15 activiteiten) is LOSSER → onderdrukken laat de microflows vallen die " +
                    "alleen CONV009 meldt (16–25 activiteiten)."),

        // ── PROEF-onderwerp 2: attribuut-telling — CLEVR-MAINT-001 (ACR_ENT_ATTRS) wint ─────────
        new("Entity attribute count", "CLEVR-MAINT-001 (ACR_ENT_ATTRS)",
            SuppressMxcli: new[] { "DESIGN001" },
            Impact: "DESIGN001 (>10 attrs) is STRENGER/breder → onderdrukken laat de entities vallen die " +
                    "alleen DESIGN001 meldt (11–25 attrs)."),

        // ── GEÏNTERNALISEERD: attribuut-default-waarde — CLEVR-MAINT-010 wint ───────────────────
        new("Attribute default value", "CLEVR-MAINT-010",
            SuppressMxcli: new[] { "CONV002" },
            Impact: "mxcli CONV002 flagt alleen integer-'0'-defaults (106) — STRIKTE SUBSET van onze 283 " +
                    "(die ook 'false'/strings dekt) → onderdrukken = geen verlies."),
    };

    /// <summary>mxcli rule-ids (rulenames) die onderdrukt worden — voor <see cref="MxcliNormalizer"/>.</summary>
    public static readonly IReadOnlySet<string> SuppressedMxcli =
        Claims.SelectMany(c => c.SuppressMxcli).ToHashSet(StringComparer.Ordinal);
}
