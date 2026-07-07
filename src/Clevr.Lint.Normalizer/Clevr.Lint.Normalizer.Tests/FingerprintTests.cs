using Clevr.Lint.Normalizer;
using Xunit;

namespace Clevr.Lint.Normalizer.Tests;

public class FingerprintTests
{
    /// <summary>
    /// GOLDEN TEST — fingerprint stability IS the exclusions contract.
    ///
    /// Every team's checked-in `.clevr-lint/exclusions.json` stores these fingerprints; if the
    /// formula (sha1 of UTF-8 "ruleId|documentQualifiedName|elementName", "sha1:" + lowercase
    /// hex) ever changes — different hash, different separator, different casing, different
    /// encoding — ALL existing exclusions silently stop matching and every excluded violation
    /// resurfaces. If this test fails, do NOT update the expected value to make it pass:
    /// either revert the change to Fingerprint.Compute, or ship an explicit migration.
    ///
    /// The expected hex was computed INDEPENDENTLY (PowerShell SHA1 over the UTF-8 bytes of
    /// "MPR001|Sales.Customer|Name", cross-checked with `sha1sum`) — deliberately NOT via
    /// Fingerprint.Compute itself, which would make the test self-referential and worthless.
    /// </summary>
    [Fact]
    public void Compute_MatchesIndependentlyComputedGoldenValue()
    {
        var actual = Fingerprint.Compute("MPR001", "Sales.Customer", "Name");

        Assert.Equal("sha1:7b8aa63dfc787c3d2f3d48ae51f3acc2e2672d48", actual);
    }
}
