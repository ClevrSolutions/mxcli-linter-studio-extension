using Clevr.Acr.Normalizer;
using Xunit;

namespace Clevr.Acr.Normalizer.Tests;

public class ProjectSecurityParserTests
{
    // Getrouwe fixture in dezelfde vorm als modelsource/Security$ProjectSecurity.yaml:
    // een AccessRules-sectie (met AllowedModuleRoles — mag NIET meegenomen worden) vóór UserRoles.
    private const string Yaml = """
    $Type: Security$ProjectSecurity
    AdminUserName: MxAdmin
    SecurityRules:
        - $Type: DomainModels$AccessRule
          AllowedModuleRoles:
            - System.Administrator
            - System.User
    UserRoles:
        - $Type: Security$UserRole
          Name: Administrator
          ModuleRoles:
            - ModuleA.Admin
            - ModuleA.User
            - ModuleB.Admin
            - ModuleC.User
            - ModuleC.Viewer
        - $Type: Security$UserRole
          Name: Manager
          ModuleRoles:
            - ModuleA.Admin
            - ModuleB.User
            - ModuleC.Viewer
    """;

    [Fact]
    public void FlagsRolesWithMoreThanOneModuleRolePerModule()
    {
        var result = ProjectSecurityParser.Detect(Yaml);

        // Administrator: ModuleA (2) + ModuleC (2) → 2 violations. Manager: alles uniek → 0.
        Assert.Equal(2, result.Count);
        Assert.All(result, v => Assert.Equal("Administrator", v.DocumentQualifiedName));
        Assert.Contains(result, v => v.ElementName == "ModuleA");
        Assert.Contains(result, v => v.ElementName == "ModuleC");
        Assert.DoesNotContain(result, v => v.ElementName == "ModuleB"); // ModuleB heeft er maar 1
    }

    [Fact]
    public void DoesNotFlagAccessRuleAllowedModuleRoles()
    {
        // System.Administrator + System.User staan in een AccessRule (vóór UserRoles), niet in
        // een user-role → mogen NIET als "System ×2" geflagd worden.
        var result = ProjectSecurityParser.Detect(Yaml);
        Assert.DoesNotContain(result, v => v.ElementName == "System");
    }

    [Fact]
    public void ViolationCarriesAcrIdentityAndCriticalSeverity()
    {
        var v = Assert.Single(ProjectSecurityParser.Detect(Yaml), x => x.ElementName == "ModuleA");
        Assert.Equal("CLEVR-MAINT-005", v.RuleId);
        Assert.Equal(ViolationKind.Acr, v.Kind);
        Assert.Equal("clevr-acr", v.Source);
        Assert.Equal("Maintainability", v.Category); // bewuste keuze (ACR: Performance)
        Assert.Equal("Critical", v.Severity);
        Assert.Equal("ProjectSecurity", v.DocumentType);
        Assert.Contains("ModuleA.Admin", v.Reason);
        Assert.Contains("ModuleA.User", v.Reason);
        Assert.StartsWith("sha1:", v.Fingerprint);
    }

    [Fact]
    public void AllDistinctModules_NoViolations()
    {
        const string yaml = """
        UserRoles:
            - $Type: Security$UserRole
              Name: Screener
              ModuleRoles:
                - ModuleA.User
                - ModuleB.User
                - ModuleC.User
        """;
        Assert.Empty(ProjectSecurityParser.Detect(yaml));
    }

    [Fact]
    public void ThreeRolesSameModule_IsOneViolationWithCountThree()
    {
        const string yaml = """
        UserRoles:
            - $Type: Security$UserRole
              Name: Power
              ModuleRoles:
                - ModuleX.A
                - ModuleX.B
                - ModuleX.C
        """;
        var v = Assert.Single(ProjectSecurityParser.Detect(yaml));
        Assert.Equal("ModuleX", v.ElementName);
        Assert.Contains("has 3 module roles", v.Reason);
    }

    [Fact]
    public void EmptyOrCorrupt_YieldsNoViolations()
    {
        Assert.Empty(ProjectSecurityParser.Detect(""));
        Assert.Empty(ProjectSecurityParser.Detect("not yaml at all"));
        Assert.Empty(ProjectSecurityParser.Detect("UserRoles:\n")); // lege sectie
    }
}
