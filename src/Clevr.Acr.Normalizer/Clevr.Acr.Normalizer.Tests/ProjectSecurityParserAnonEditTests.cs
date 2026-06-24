using Clevr.Acr.Normalizer;
using Xunit;

namespace Clevr.Acr.Normalizer.Tests;

public class ProjectSecurityParserAnonEditTests
{
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

    // Order: Notes (unlimited, RW voor Anon → flag), Code (limited 200, RW voor Anon → niet),
    // Description (unlimited, ReadOnly voor Anon → niet), Secret (unlimited, RW maar Admin → niet).
    // Velden in YAML-volgorde: AccessRules, Attributes, Name (alfabetisch — net als echt).
    private const string SalesDomainModel = """
    $Type: DomainModels$DomainModel
    Entities:
        - $Type: DomainModels$EntityImpl
          AccessRules:
            - $Type: DomainModels$AccessRule
              AllowCreate: false
              AllowedModuleRoles:
                - Sales.Anon
              MemberAccesses:
                - $Type: DomainModels$MemberAccess
                  AccessRights: ReadWrite
                  Association: ""
                  Attribute: Sales.Order.Notes
                - $Type: DomainModels$MemberAccess
                  AccessRights: ReadWrite
                  Association: ""
                  Attribute: Sales.Order.Code
                - $Type: DomainModels$MemberAccess
                  AccessRights: ReadOnly
                  Association: ""
                  Attribute: Sales.Order.Description
            - $Type: DomainModels$AccessRule
              AllowCreate: false
              AllowedModuleRoles:
                - Sales.Admin
              MemberAccesses:
                - $Type: DomainModels$MemberAccess
                  AccessRights: ReadWrite
                  Association: ""
                  Attribute: Sales.Order.Secret
          Attributes:
            - $Type: DomainModels$Attribute
              Name: Notes
              NewType:
                $Type: DomainModels$StringAttributeType
                Length: 0
            - $Type: DomainModels$Attribute
              Name: Code
              NewType:
                $Type: DomainModels$StringAttributeType
                Length: 200
            - $Type: DomainModels$Attribute
              Name: Description
              NewType:
                $Type: DomainModels$StringAttributeType
                Length: 0
            - $Type: DomainModels$Attribute
              Name: Secret
              NewType:
                $Type: DomainModels$StringAttributeType
                Length: 0
          Name: Order
    """;

    private static (string, string)[] Sales() => new[] { ("Sales", SalesDomainModel) };

    [Fact]
    public void FlagsUnlimitedStringEditableByAnonymous_Only()
    {
        var result = ProjectSecurityParser.DetectAnonymousEditableUnlimitedStrings(SecurityGuestOn, Sales());

        var v = Assert.Single(result); // alleen Sales.Order.Notes
        Assert.Equal("CLEVR-SEC-006", v.RuleId);
        Assert.Equal(ViolationKind.Acr, v.Kind);
        Assert.Equal("clevr-acr", v.Source);
        Assert.Equal("Security", v.Category);
        Assert.Equal("Blocker", v.Severity);
        Assert.Equal("Sales.Order", v.DocumentQualifiedName);
        Assert.Equal("Notes", v.ElementName);
        Assert.Contains("Sales.Order.Notes", v.Reason);
        Assert.StartsWith("sha1:", v.Fingerprint);
    }

    [Fact]
    public void LimitedLengthString_IsNotFlagged()
    {
        var result = ProjectSecurityParser.DetectAnonymousEditableUnlimitedStrings(SecurityGuestOn, Sales());
        Assert.DoesNotContain(result, v => v.ElementName == "Code"); // length 200
    }

    [Fact]
    public void ReadOnlyUnlimitedString_IsNotFlagged()
    {
        var result = ProjectSecurityParser.DetectAnonymousEditableUnlimitedStrings(SecurityGuestOn, Sales());
        Assert.DoesNotContain(result, v => v.ElementName == "Description"); // ReadOnly
    }

    [Fact]
    public void UnlimitedStringWritableByOtherRole_IsNotFlagged()
    {
        var result = ProjectSecurityParser.DetectAnonymousEditableUnlimitedStrings(SecurityGuestOn, Sales());
        Assert.DoesNotContain(result, v => v.ElementName == "Secret"); // Sales.Admin, niet anoniem
    }

    [Fact]
    public void GuestDisabled_YieldsNoViolations()
    {
        Assert.Empty(ProjectSecurityParser.DetectAnonymousEditableUnlimitedStrings(SecurityGuestOff, Sales()));
    }
}
