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

    // ── BORGING, mxcli-kant ─────────────────────────────────────────────────────────────────────
    // Tripwire: de canonieke lijst van ÀLLE bewust-onderdrukte mxcli-tegenhangers moet EXACT
    // gelijk zijn aan SuppressedMxcli. Een vergeten mxcli-suppressie faalt hier luid.
    // Per id staat het onderwerp/de winnaar erbij.
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
        var missing = expected.Except(actual).ToList();
        var stray = actual.Except(expected).ToList();
        Assert.True(missing.Count == 0, $"mxcli-tegenhangers zonder suppressie: {string.Join(", ", missing)}");
        Assert.True(stray.Count == 0, $"onderdrukte mxcli-ids niet in de canonieke lijst: {string.Join(", ", stray)}");
    }

    [Fact]
    public void WinnersAreNeverSuppressed()
    {
        Assert.DoesNotContain("ACR_ENT_ATTRS", ClaimTable.SuppressedMxcli);
        foreach (var c in ClaimTable.Claims)
            Assert.False(string.IsNullOrWhiteSpace(c.Impact), $"claim '{c.Topic}' must state its impact");
    }
}
