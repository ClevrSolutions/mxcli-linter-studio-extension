using System.Text;
using Clevr.Acr.Normalizer;
using Xunit;

namespace Clevr.Acr.Normalizer.Tests;

// Synthetic POSITIVE tests: on TRB the ground truth is 1/0/0/0, so these constructed cases prove the
// rules CAN detect (separating "fires-on-TRB" from "can detect"), like the domain-model batch.
public class SecurityModulesBatchTests
{
    // ---- 001_0005 CLEVR-SEC-008 MxAdminNotUsed ----
    [Fact]
    public void MxAdmin_FlagsDefaultName()
    {
        var v = Assert.Single(ProjectSecurityParser.DetectMxAdminUserId("$Type: Security$ProjectSecurity\nAdminUserName: MxAdmin\n"));
        Assert.Equal("CLEVR-SEC-008", v.RuleId);
        Assert.Equal("Critical", v.Severity);
        Assert.Equal("Security", v.Category);
    }

    [Fact]
    public void MxAdmin_NotFlaggedForRenamedAccount()
        => Assert.Empty(ProjectSecurityParser.DetectMxAdminUserId("AdminUserName: AdminProd\n"));

    // ---- 001_0007 CLEVR-SEC-009 HashAlgorithm ----
    [Theory]
    [InlineData("BCrypt")]
    [InlineData("SSHA256")]
    public void HashAlgorithm_NotFlaggedForSafeAlgorithms(string alg)
        => Assert.Empty(ProjectSecurityParser.DetectHashAlgorithm($"Settings:\n    - $Type: Settings$X\n      HashAlgorithm: {alg}\n"));

    [Fact]
    public void HashAlgorithm_FlagsWeakAlgorithm()
    {
        var v = Assert.Single(ProjectSecurityParser.DetectHashAlgorithm("Settings:\n    - $Type: Settings$X\n      HashAlgorithm: SHA1\n"));
        Assert.Equal("CLEVR-SEC-009", v.RuleId);
        Assert.Equal("Critical", v.Severity);
        Assert.Equal("SHA1", v.ElementName);
    }

    [Fact]
    public void HashAlgorithm_NotFlaggedWhenAbsent()
        => Assert.Empty(ProjectSecurityParser.DetectHashAlgorithm("Settings:\n    - $Type: Settings$X\n      BcryptCost: 10\n"));

    // ---- 001_0008 CLEVR-SEC-010 CheckSecurityOnUserRoles ----
    private static string Role(string name, string? check) =>
        "    - $Type: Security$UserRole\n" +
        (check is null ? "" : $"      CheckSecurity: {check}\n") +
        $"      Name: {name}\n";

    [Fact]
    public void CheckSecurity_NotFlaggedWhenAllChecked()
        => Assert.Empty(ProjectSecurityParser.DetectCheckSecurityOnUserRoles(
            "UserRoles:\n" + Role("Administrator", "true") + Role("User", "true")));

    [Fact]
    public void CheckSecurity_FlagsRoleWithCheckFalse()
    {
        var v = Assert.Single(ProjectSecurityParser.DetectCheckSecurityOnUserRoles(
            "UserRoles:\n" + Role("Administrator", "true") + Role("Anonymous", "false")));
        Assert.Equal("CLEVR-SEC-010", v.RuleId);
        Assert.Equal("Critical", v.Severity);
        Assert.Equal("Anonymous", v.DocumentQualifiedName);
    }

    [Fact]
    public void CheckSecurity_FlagsRoleWithCheckAbsent()
    {
        // Mirrors the Rego `not user_role.CheckSecurity`: absent field = violation.
        var v = Assert.Single(ProjectSecurityParser.DetectCheckSecurityOnUserRoles(
            "UserRoles:\n" + Role("Legacy", null)));
        Assert.Equal("Legacy", v.DocumentQualifiedName);
    }

    // ---- 003_0001 CLEVR-MAINT-014 NumberOfModules ----
    private static string Modules(int userModules, int appStoreModules)
    {
        var sb = new StringBuilder("ProductVersion: 11.10.0\nModules:\n");
        for (int i = 0; i < userModules; i++) sb.Append($"    - Name: User{i}\n      ID: id{i}\n");
        for (int i = 0; i < appStoreModules; i++) sb.Append($"    - Name: Store{i}\n      ID: sid{i}\n      FromAppStore: true\n");
        return sb.ToString();
    }

    [Fact]
    public void NumberOfModules_FlagsOver20UserModules()
    {
        // 21 user modules + 10 app-store: app-store excluded, 21 > 20 → fires once.
        var v = Assert.Single(ProjectSecurityParser.DetectNumberOfModules(Modules(21, 10)));
        Assert.Equal("CLEVR-MAINT-014", v.RuleId);
        Assert.Equal("Major", v.Severity);
        Assert.Equal("Maintainability", v.Category);
        Assert.Contains("21 user modules", v.Reason);
    }

    [Fact]
    public void NumberOfModules_NotFlaggedAtThreshold()
        => Assert.Empty(ProjectSecurityParser.DetectNumberOfModules(Modules(20, 50))); // 20 ≤ 20, app-store ignored

    // ---- claim-table ----
    [Theory]
    [InlineData("001_0005")]
    [InlineData("001_0007")]
    [InlineData("001_0008")]
    [InlineData("003_0001")]
    public void ClaimTable_SuppressesMxlintTwins(string n) => Assert.Contains(n, ClaimTable.SuppressedMxlint);
}
