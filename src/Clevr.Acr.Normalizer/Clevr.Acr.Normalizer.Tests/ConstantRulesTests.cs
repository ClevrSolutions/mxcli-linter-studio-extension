using Clevr.Acr.Normalizer;
using Xunit;

namespace Clevr.Acr.Normalizer.Tests;

// Synthetic POSITIVE tests: on TRB all 9 constants are ExposedToClient: false → ground truth 0, so
// these constructed cases prove the rule CAN detect (separating "fires-on-TRB" from "can detect").
public class ConstantRulesTests
{
    private static (string, string, bool)[] C(params (string Name, bool Exposed)[] cs)
        => System.Array.ConvertAll(cs, c => ("MyMod", c.Name, c.Exposed));

    // ---- the two-part check: exposed AND sensitive name ----
    [Fact]
    public void Fires_WhenExposedAndSensitiveName()
    {
        var v = Assert.Single(ConstantRules.ExposedSensitiveConstants(C(("ApiPassword", true))));
        Assert.Equal("CLEVR-SEC-011", v.RuleId);
        Assert.Equal("Critical", v.Severity);
        Assert.Equal("Security", v.Category);
        Assert.Equal("MyMod.ApiPassword", v.DocumentQualifiedName);
        Assert.Equal(ViolationKind.Acr, v.Kind);
        Assert.Equal("clevr-acr", v.Source);
    }

    [Fact]
    public void DoesNotFire_WhenExposedButInnocentName()
        => Assert.Empty(ConstantRules.ExposedSensitiveConstants(C(("MaxAgeDays", true))));

    [Fact]
    public void DoesNotFire_WhenSensitiveNameButNotExposed()
        => Assert.Empty(ConstantRules.ExposedSensitiveConstants(C(("AdminPassword", false))));

    // ---- keyword list reproduced VERBATIM (substring, case-insensitive) ----
    [Theory]
    [InlineData("Id")]            // "id"
    [InlineData("UserName")]      // "username" / "user"
    [InlineData("user_name")]     // "user_name"
    [InlineData("MySecret")]      // "secret"
    [InlineData("DbPwd")]         // "pwd"
    [InlineData("Width")]         // contains "id" — verbatim substring match (deliberately over-broad, like the .rego)
    public void ContainsSensitiveData_TrueForKeywordSubstrings(string name)
        => Assert.True(ConstantRules.ContainsSensitiveData(name));

    [Theory]
    [InlineData("MaxAgeDays")]
    [InlineData("CleanAfterDays")]
    [InlineData("Timeout")]
    public void ContainsSensitiveData_FalseForInnocentNames(string name)
        => Assert.False(ConstantRules.ContainsSensitiveData(name));

    // ---- claim-table ----
    [Fact]
    public void ClaimTable_SuppressesMxlintTwin()
        => Assert.Contains("006_0001", ClaimTable.SuppressedMxlint);
}
