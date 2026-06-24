using Clevr.Acr.Normalizer;
using Xunit;

namespace Clevr.Acr.Normalizer.Tests;

public class ProjectSecurityParserAnonCreateTests
{
    // Guest AAN, GuestUserRole = Guest met module-rol Sales.Anon → de anonieme rol-set = {Sales.Anon}.
    private const string SecurityGuestOn = """
    EnableGuestAccess: true
    GuestUserRole: Guest
    UserRoles:
        - $Type: Security$UserRole
          Name: Guest
          ModuleRoles:
            - Sales.Anon
        - $Type: Security$UserRole
          Name: Admin
          ModuleRoles:
            - Sales.Admin
    """;

    private const string SecurityGuestOff = """
    EnableGuestAccess: false
    GuestUserRole: Guest
    UserRoles:
        - $Type: Security$UserRole
          Name: Guest
          ModuleRoles:
            - Sales.Anon
    """;

    // Drie entiteiten: Order (create voor Anon), TempBasket (create voor Anon), Invoice (create voor Admin).
    private const string SalesDomainModel = """
    $Type: DomainModels$DomainModel
    Entities:
        - $Type: DomainModels$EntityImpl
          AccessRules:
            - $Type: DomainModels$AccessRule
              AllowCreate: true
              AllowedModuleRoles:
                - Sales.Anon
          Name: Order
        - $Type: DomainModels$EntityImpl
          AccessRules:
            - $Type: DomainModels$AccessRule
              AllowCreate: true
              AllowedModuleRoles:
                - Sales.Anon
          Name: TempBasket
        - $Type: DomainModels$EntityImpl
          AccessRules:
            - $Type: DomainModels$AccessRule
              AllowCreate: true
              AllowedModuleRoles:
                - Sales.Admin
          Name: Invoice
    """;

    private static (string, string)[] Sales() => new[] { ("Sales", SalesDomainModel) };

    // CATALOG-bron: Order + Invoice persistent; TempBasket non-persistent.
    private static IReadOnlySet<string> Persistent() =>
        new HashSet<string> { "Sales.Order", "Sales.Invoice" };

    [Fact]
    public void FlagsPersistentEntityWithAnonymousCreate_Only()
    {
        var result = ProjectSecurityParser.DetectAnonymousCreateOnPersistent(SecurityGuestOn, Sales(), Persistent());

        // Alleen Sales.Order: persistent + create + anonieme rol.
        var v = Assert.Single(result);
        Assert.Equal("Sales.Order", v.DocumentQualifiedName);
        Assert.Equal("CLEVR-SEC-005", v.RuleId);
        Assert.Equal(ViolationKind.Acr, v.Kind);
        Assert.Equal("clevr-acr", v.Source);
        Assert.Equal("Security", v.Category);
        Assert.Equal("Blocker", v.Severity);
        Assert.Equal("Entity", v.DocumentType);
        Assert.Contains("Sales.Anon", v.Reason);
        Assert.StartsWith("sha1:", v.Fingerprint);
    }

    [Fact]
    public void NonPersistentEntityWithAnonymousCreate_IsAllowed()
    {
        var result = ProjectSecurityParser.DetectAnonymousCreateOnPersistent(SecurityGuestOn, Sales(), Persistent());
        Assert.DoesNotContain(result, v => v.DocumentQualifiedName == "Sales.TempBasket");
    }

    [Fact]
    public void PersistentEntityWithCreateForOtherRole_IsNotFlagged()
    {
        var result = ProjectSecurityParser.DetectAnonymousCreateOnPersistent(SecurityGuestOn, Sales(), Persistent());
        Assert.DoesNotContain(result, v => v.DocumentQualifiedName == "Sales.Invoice");
    }

    [Fact]
    public void GuestDisabled_YieldsNoViolations()
    {
        var result = ProjectSecurityParser.DetectAnonymousCreateOnPersistent(SecurityGuestOff, Sales(), Persistent());
        Assert.Empty(result);
    }

    [Fact]
    public void AnonymousRoleSet_ResolvesGuestRoleModuleRoles()
    {
        Assert.Contains("Sales.Anon", ProjectSecurityParser.AnonymousRoleSet(SecurityGuestOn));
        Assert.Empty(ProjectSecurityParser.AnonymousRoleSet(SecurityGuestOff)); // guest uit
    }
}
