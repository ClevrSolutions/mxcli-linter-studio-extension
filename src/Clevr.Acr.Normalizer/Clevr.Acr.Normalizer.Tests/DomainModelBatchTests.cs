using System.Text;
using Clevr.Acr.Normalizer;
using Xunit;

namespace Clevr.Acr.Normalizer.Tests;

// Synthetic POSITIVE tests: TRB ground truth is mostly 0/1, so these constructed cases prove the
// rules CAN detect (separating "0/1 on TRB" from "can detect"), like the commit-in-loop control.
public class DomainModelBatchTests
{
    // entity item at 2-space, fields at 4-space (matches the real export indentation).
    private static string Ent(string name, string fields) =>
        $"  - $Type: DomainModels$EntityImpl\n{fields}\n    Name: {name}\n";
    private static (string, string)[] DM(string yaml) => new[] { ("Sales", yaml) };

    // ---- 002_0001 CLEVR-MAINT-011 ----
    [Fact]
    public void PersistentEntities_FlagsOver15()
    {
        var sb = new StringBuilder("Entities:\n");
        for (int i = 0; i < 16; i++) sb.Append(Ent($"E{i}", "    MaybeGeneralization:\n      $Type: DomainModels$NoGeneralization\n      Persistable: true"));
        var v = Assert.Single(ProjectSecurityParser.DetectNumberOfPersistentEntities(DM(sb.ToString())));
        Assert.Equal("CLEVR-MAINT-011", v.RuleId);
        Assert.Equal("DomainModel", v.DocumentType);
        Assert.Equal("Sales", v.DocumentQualifiedName);
        Assert.Contains("16 persistent", v.Reason);
    }

    [Fact]
    public void PersistentEntities_NonPersistentExcluded_NotFlagged()
    {
        var sb = new StringBuilder("Entities:\n");
        for (int i = 0; i < 15; i++) sb.Append(Ent($"E{i}", "    MaybeGeneralization:\n      $Type: DomainModels$NoGeneralization\n      Persistable: true"));
        for (int i = 0; i < 5; i++) sb.Append(Ent($"N{i}", "    MaybeGeneralization:\n      $Type: DomainModels$NoGeneralization\n      Persistable: false"));
        Assert.Empty(ProjectSecurityParser.DetectNumberOfPersistentEntities(DM(sb.ToString()))); // 15 persistent, not > 15
    }

    // ---- 002_0003 CLEVR-PERF-001 ----
    [Fact]
    public void InheritAdmin_Flags_OnlyAdministrationAccount()
    {
        var hit = "Entities:\n" + Ent("Acc", "    MaybeGeneralization:\n      $Type: DomainModels$Generalization\n      Generalization: Administration.Account");
        var v = Assert.Single(ProjectSecurityParser.DetectInheritFromAdministrationAccount(DM(hit)));
        Assert.Equal("CLEVR-PERF-001", v.RuleId);
        Assert.Equal("Sales.Acc", v.DocumentQualifiedName);

        var miss = "Entities:\n" + Ent("Acc", "    MaybeGeneralization:\n      $Type: DomainModels$Generalization\n      Generalization: System.User");
        Assert.Empty(ProjectSecurityParser.DetectInheritFromAdministrationAccount(DM(miss)));
    }

    // ---- 002_0005 CLEVR-SEC-007 ----
    [Fact]
    public void SystemAssociation_FlagsOnlySystemChild()
    {
        var yaml = "CrossAssociations:\n" +
                   "    - $Type: DomainModels$CrossAssociation\n      Child: System.FileDocument\n      Name: Ent_File\n" +
                   "    - $Type: DomainModels$CrossAssociation\n      Child: Sales.Other\n      Name: Ent_Other\n" +
                   "Entities: []\n";
        var v = Assert.Single(ProjectSecurityParser.DetectSystemEntityAssociation(DM(yaml)));
        Assert.Equal("CLEVR-SEC-007", v.RuleId);
        Assert.Equal("Critical", v.Severity);
        Assert.Equal("Ent_File", v.ElementName);
    }

    // ---- 002_0006 CLEVR-PERF-002 ----
    [Fact]
    public void VirtualAttributes_FlagsOver10()
    {
        var sb = new StringBuilder("Entities:\n  - $Type: DomainModels$EntityImpl\n    Attributes:\n");
        for (int i = 0; i < 11; i++) sb.Append($"      - $Type: DomainModels$Attribute\n        Name: A{i}\n        Value:\n          $Type: DomainModels$CalculatedValue\n");
        sb.Append("    Name: Big\n");
        var v = Assert.Single(ProjectSecurityParser.DetectTooManyVirtualAttributes(DM(sb.ToString())));
        Assert.Equal("CLEVR-PERF-002", v.RuleId);
        Assert.Contains("11 virtual", v.Reason);
    }

    [Fact]
    public void VirtualAttributes_TenOrFewer_NotFlagged()
    {
        var sb = new StringBuilder("Entities:\n  - $Type: DomainModels$EntityImpl\n    Attributes:\n");
        for (int i = 0; i < 10; i++) sb.Append($"      - $Type: DomainModels$Attribute\n        Name: A{i}\n        Value:\n          $Type: DomainModels$CalculatedValue\n");
        sb.Append("    Name: Big\n");
        Assert.Empty(ProjectSecurityParser.DetectTooManyVirtualAttributes(DM(sb.ToString())));
    }

    // ---- 002_0007 CLEVR-MAINT-012 ----
    [Fact]
    public void ValidationRules_FlagsWhenPresent_NotWhenEmpty()
    {
        var hit = "Entities:\n  - $Type: DomainModels$EntityImpl\n    ValidationRules:\n      - $Type: DomainModels$ValidationRule\n        AttributeName: X\n    Name: Ent\n";
        var v = Assert.Single(ProjectSecurityParser.DetectUsingValidationRules(DM(hit)));
        Assert.Equal("CLEVR-MAINT-012", v.RuleId);

        var empty = "Entities:\n  - $Type: DomainModels$EntityImpl\n    ValidationRules: []\n    Name: Ent\n";
        Assert.Empty(ProjectSecurityParser.DetectUsingValidationRules(DM(empty)));
    }

    // ---- 002_0008 CLEVR-MAINT-013 ----
    [Fact]
    public void DefaultReadWrite_FlagsReadWrite_NotReadOnly()
    {
        var rw = "Entities:\n  - $Type: DomainModels$EntityImpl\n    AccessRules:\n      - $Type: DomainModels$AccessRule\n        AllowedModuleRoles:\n          - MyMod.User\n        DefaultMemberAccessRights: ReadWrite\n    Name: Ent\n";
        var v = Assert.Single(ProjectSecurityParser.DetectDefaultReadWriteAccess(DM(rw)));
        Assert.Equal("CLEVR-MAINT-013", v.RuleId);
        Assert.Equal("User", v.ElementName); // role short name (after the '.')

        var ro = "Entities:\n  - $Type: DomainModels$EntityImpl\n    AccessRules:\n      - $Type: DomainModels$AccessRule\n        DefaultMemberAccessRights: ReadOnly\n    Name: Ent\n";
        Assert.Empty(ProjectSecurityParser.DetectDefaultReadWriteAccess(DM(ro)));
    }

    // ---- claim-table ----
    [Fact]
    public void ClaimTable_MxcliChoices()
    {
        // MAINT-011 gedeferd naar mxcli MPR003 en PERF-002 naar CONV017: beide worden NIET onderdrukt.
        Assert.DoesNotContain("MPR003", ClaimTable.SuppressedMxcli);
        Assert.DoesNotContain("CONV017", ClaimTable.SuppressedMxcli);
    }
}
