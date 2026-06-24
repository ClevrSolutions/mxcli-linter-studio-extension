using Clevr.Acr.Normalizer;
using Xunit;

namespace Clevr.Acr.Normalizer.Tests;

public class ClaimTableTests
{
    [Theory]
    [InlineData("QUAL003")]   // microflow size (looser/stricter mxcli twins) → suppressed
    [InlineData("CONV009")]
    [InlineData("DESIGN001")] // attribute count
    public void SuppressedMxcli_ContainsTheTwoProofTopicsCounterparts(string ruleId)
        => Assert.Contains(ruleId, ClaimTable.SuppressedMxcli);

    [Theory]
    [InlineData("005_0003")]  // microflow size mxlint twin
    [InlineData("002_0002")]  // attribute count (migrated-correct)
    [InlineData("001_0001")]  // guest (migrated)
    [InlineData("001_0002")]  // demo (migrated)
    [InlineData("001_0003")]  // security checks (migrated)
    public void SuppressedMxlint_ContainsMigratedAndProofRulenumbers(string num)
        => Assert.Contains(num, ClaimTable.SuppressedMxlint);

    // 001_0004 StrongPasswordPolicy is still NOT internalised (no claimed counterpart) → must NOT be
    // suppressed. (002_0007 was such a case too, but is now internalised as CLEVR-MAINT-012 → suppressed.)
    [Fact]
    public void SuppressedMxlint_DoesNotDrop001_0004()
        => Assert.DoesNotContain("001_0004", ClaimTable.SuppressedMxlint);

    // ── BORGING tegen claim-tabel-drift ────────────────────────────────────────────────────────
    // Het gat dat 005_0004 liet ontsnappen: de detect-regel werd gebouwd, maar de claim-tabel-entry
    // (een LOSSE handeling) werd vergeten. Deze test is de tripwire: de canonieke lijst van ÀLLE
    // geïnternaliseerde/gemigreerde mxlint-twins moet EXACT gelijk zijn aan SuppressedMxlint. Wie een
    // mxlint-regel internaliseert moet 'm hier toevoegen — vergeet je de ClaimTable-entry, dan faalt
    // deze test luid (missende twin), en een stray entry valt ook op (extra twin). Eén plek, één lijst.
    // NB na de mxcli-cutover: 4 onderwerpen zijn GEDEFERD naar mxcli's eigen regel (onze CLEVR-emissie uit).
    // Hun mxlint-twin BLIJFT onderdrukt (mxlint = backup; bij terugkeer defert die ook naar mxcli) — dus ze
    // blijven in deze lijst, maar gemarkeerd als 'gedeferd' i.p.v. 'geïnternaliseerd'.
    private static readonly string[] InternalisedMxlintTwins =
    {
        // microflow-structuur (005_*)
        "005_0001", // CLEVR-REL-002  (incomplete empty-string) — describe-route
        "005_0002", // GEDEFERD naar mxcli CONV011 (commit-in-loop); twin nog onderdrukt
        "005_0003", // CLEVR-MAINT-007 (microflow size) — catalog-route
        "005_0004", // CLEVR-MAINT-008 (complex without annotations) — describe-route
        "005_0005", // CLEVR-MAINT-009 (nested if) — describe-route
        // domein-model (002_*)
        "002_0001", // GEDEFERD naar mxcli MPR003 (persistent count); twin nog onderdrukt
        "002_0002", // CLEVR-MAINT-001 (attr count, gemigreerd)
        "002_0003", // CLEVR-PERF-001 — catalog-route
        "002_0005", // CLEVR-SEC-007 — catalog-route
        "002_0006", // GEDEFERD naar mxcli CONV017 (virtuele attrs); twin nog onderdrukt
        "002_0007", // GEDEFERD naar mxcli ACR_ENT_VALRULES/CONV015 (validatieregels); twin nog onderdrukt
        "002_0008", // CLEVR-MAINT-013 (default-RW) — describe-route
        "002_0009", // CLEVR-MAINT-010
        // project-settings / security (001_*)
        "001_0001", // CLEVR-SEC-004 (guest, gemigreerd)
        "001_0002", // CLEVR-SEC-003 (demo, gemigreerd)
        "001_0003", // CLEVR-SEC-001/002 (security checks, gemigreerd)
        "001_0005", // CLEVR-SEC-008
        "001_0007", // CLEVR-SEC-009
        "001_0008", // CLEVR-SEC-010
        // modules / constants / pages (003/006/004)
        "003_0001", // CLEVR-MAINT-014
        "006_0001", // CLEVR-SEC-011
        "004_0001", // CLEVR-MAINT-015
        "004_0002", // CLEVR-REL-003
    };

    [Fact]
    public void SuppressedMxlint_ExactlyMatchesInternalisedTwins()
    {
        var expected = InternalisedMxlintTwins.ToHashSet(System.StringComparer.Ordinal);
        var actual = ClaimTable.SuppressedMxlint;
        var missing = expected.Except(actual).ToList();   // geïnternaliseerd maar entry vergeten (de 005_0004-bug)
        var stray = actual.Except(expected).ToList();      // onderdrukt zonder dat we 'm hier registreerden
        Assert.True(missing.Count == 0, $"mxlint-twins zonder claim-tabel-entry: {string.Join(", ", missing)}");
        Assert.True(stray.Count == 0, $"onderdrukte twins die niet in de canonieke lijst staan: {string.Join(", ", stray)}");
    }

    // ── BORGING, mxcli-kant ─────────────────────────────────────────────────────────────────────
    // Zelfde tripwire, doorgetrokken naar SuppressedMxcli: de canonieke lijst van ÀLLE bewust-
    // onderdrukte mxcli-tegenhangers moet EXACT gelijk zijn aan SuppressedMxcli. Een vergeten mxcli-
    // suppressie (zoals CONV011 voor commit-in-loop was) faalt nu net zo luid als een vergeten mxlint-
    // entry. Per id staat het onderwerp/de winnaar erbij.
    // Na de cutover zijn MPR003 en CONV011 UIT deze lijst: die onderwerpen zijn gedeferd naar mxcli's
    // EIGEN regel, dus die regel moet júist tonen (niet onderdrukt worden). De overige onderdrukken nog
    // de mxcli-twin van een ACTIEVE CLEVR/ACR-regel (MAINT-007/010 + attr-count).
    private static readonly string[] SuppressedMxcliCounterparts =
    {
        "QUAL003",   // microflow-grootte → CLEVR-MAINT-007 (catalog) wint
        "CONV009",   // microflow-grootte (losser) → CLEVR-MAINT-007 wint
        "DESIGN001", // attribuut-telling → CLEVR-MAINT-001 (ACR_ENT_ATTRS) wint
        "CONV002",   // attribuut-default-waarde → CLEVR-MAINT-010 (catalog) wint
    };

    [Fact]
    public void SuppressedMxcli_ExactlyMatchesCounterparts()
    {
        var expected = SuppressedMxcliCounterparts.ToHashSet(System.StringComparer.Ordinal);
        var actual = ClaimTable.SuppressedMxcli;
        var missing = expected.Except(actual).ToList();   // onderwerp geclaimd maar mxcli-twin niet onderdrukt (de CONV011-bug)
        var stray = actual.Except(expected).ToList();      // onderdrukt zonder registratie hier
        Assert.True(missing.Count == 0, $"mxcli-tegenhangers zonder suppressie: {string.Join(", ", missing)}");
        Assert.True(stray.Count == 0, $"onderdrukte mxcli-ids niet in de canonieke lijst: {string.Join(", ", stray)}");
    }

    [Fact]
    public void WinnersAreNeverSuppressed()
    {
        // The winning rules must not appear in either suppression set.
        Assert.DoesNotContain("ACR_ENT_ATTRS", ClaimTable.SuppressedMxcli);
        Assert.DoesNotContain("005_0003", ClaimTable.SuppressedMxcli); // a mxlint number is never an mxcli id, sanity
        foreach (var c in ClaimTable.Claims)
            Assert.False(string.IsNullOrWhiteSpace(c.Impact), $"claim '{c.Topic}' must state its impact");
    }
}
