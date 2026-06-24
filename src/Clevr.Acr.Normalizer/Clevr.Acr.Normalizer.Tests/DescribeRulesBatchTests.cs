using Clevr.Acr.Normalizer;
using Xunit;

namespace Clevr.Acr.Normalizer.Tests;

// DEEL 1: the 3 remaining describe-route rules (MAINT-008, REL-001, MAINT-013) on the proven assembler.
// Synthetic positives prove detection; live describe-GT is verified separately. Existing predicates reused.
public class DescribeRulesBatchTests
{
    // ── MAINT-008 ComplexMicroflowsWithoutAnnotations (structure counts from describe) ──
    [Fact]
    public void Maint008_Fires_ManySplitsNoAnnotation()
    {
        var sb = new System.Text.StringBuilder("create or modify microflow M.Flow ()\nbegin\n");
        for (int i = 0; i < 3; i++) sb.Append($"  if $x > {i} then\n    return;\n  end if;\n"); // 3 splits (>2), no @annotation
        sb.Append("end;");
        var (a, s, ann) = DescribeMicroflowExpressions.StructureCounts(sb.ToString());
        Assert.True(s > 2); Assert.Equal(0, ann);
        var v = Assert.Single(MicroflowStructureRules.ComplexWithoutAnnotations(new[] { ("M.Flow", a, s, ann) }));
        Assert.Equal("CLEVR-MAINT-008", v.RuleId);
    }

    [Fact]
    public void Maint008_DoesNotFire_WhenAnnotated()
    {
        var sb = new System.Text.StringBuilder("create or modify microflow M.Flow ()\nbegin\n  @annotation 'explains it'\n");
        for (int i = 0; i < 3; i++) sb.Append($"  if $x > {i} then\n    return;\n  end if;\n");
        sb.Append("end;");
        var (a, s, ann) = DescribeMicroflowExpressions.StructureCounts(sb.ToString());
        Assert.Equal(1, ann);
        Assert.Empty(MicroflowStructureRules.ComplexWithoutAnnotations(new[] { ("M.Flow", a, s, ann) }));
    }

    [Fact]
    public void Maint008_DoesNotFire_WhenSimple()
    {
        var mdl = "create or modify microflow M.Flow ()\nbegin\n  $x = 1;\n  return;\nend;";
        var (a, s, ann) = DescribeMicroflowExpressions.StructureCounts(mdl);
        Assert.True(a <= 10 && s <= 2);
        Assert.Empty(MicroflowStructureRules.ComplexWithoutAnnotations(new[] { ("M.Flow", a, s, ann) }));
    }

    // ── REL-001 redundant empty-string (reuses Extract + RedundantEmptyString) ──
    [Fact]
    public void Rel001_Fires_OnRedundantCheck_MultiLine()
    {
        // path compared to BOTH empty and '' (wrapped over lines) = redundant.
        var mdl = "create or modify microflow M.Flow ()\nbegin\n  if $Obj/Name != empty\nand\n$Obj/Name != '' then\n    return;\n  end if;\nend;";
        var v = Assert.Single(ExpressionRules.RedundantEmptyString(DescribeMicroflowExpressions.Extract("M.Flow", mdl)));
        Assert.Equal("CLEVR-REL-001", v.RuleId);
    }

    [Fact]
    public void Rel001_DoesNotFire_OnIncompleteOnly()
    {
        var mdl = "create or modify microflow M.Flow ()\nbegin\n  if $Obj/Name != '' then\n    return;\n  end if;\nend;";
        Assert.Empty(ExpressionRules.RedundantEmptyString(DescribeMicroflowExpressions.Extract("M.Flow", mdl)));
    }

    // ── MAINT-013 default-RW access (describe entity grants) ──
    [Fact]
    public void Maint013_Fires_OnWriteStarGrant()
    {
        var mdl = "create or modify persistent entity M.Ent (\n  Name: String\n)\n\ngrant M.Administrator on M.Ent (create, delete, read *, write *);\n";
        var v = Assert.Single(DescribeEntityRules.DefaultReadWriteAccess("M.Ent", mdl));
        Assert.Equal("CLEVR-MAINT-013", v.RuleId);
        Assert.Equal("M.Ent", v.DocumentQualifiedName);
        Assert.Equal("Administrator", v.ElementName);
    }

    [Fact]
    public void Maint013_DoesNotFire_OnMemberListedGrant()
    {
        var mdl = "create or modify persistent entity M.Ent (\n  Name: String\n)\n\ngrant M.User on M.Ent (read (M.Ent.Name), write (M.Ent.Name));\ngrant M.Viewer on M.Ent (read *);\n";
        Assert.Empty(DescribeEntityRules.DefaultReadWriteAccess("M.Ent", mdl));
    }

    [Fact]
    public void Maint013_JoinsMultipleWriteStarRoles()
    {
        var mdl = "create or modify persistent entity M.Ent ()\n\ngrant M.Administrator on M.Ent (read *, write *);\ngrant M.Power on M.Ent (read *, write *);\n";
        var v = Assert.Single(DescribeEntityRules.DefaultReadWriteAccess("M.Ent", mdl));
        Assert.Equal("Administrator, Power", v.ElementName);
    }

    // ── MAINT-006 redundant boolean via the describe-route (Extract → ExpressionRules.RedundantBoolean) ──
    [Fact]
    public void Maint006_Fires_OnRedundantBoolean_FromDescribe()
    {
        // Redundant boolean comparison ('= true') in a split condition — describe extracts it, the
        // existing RedundantBoolean predicate flags it. (Same path as the live deepscan migration.)
        var mdl = "create or modify microflow M.Flow ()\nbegin\n  if $Obj/IsActive = true then\n    return true;\n  end if;\nend;";
        var v = Assert.Single(ExpressionRules.RedundantBoolean(DescribeMicroflowExpressions.Extract("M.Flow", mdl)));
        Assert.Equal("CLEVR-MAINT-006", v.RuleId);
    }

    [Fact]
    public void Maint006_DoesNotFire_OnPlainComparison_FromDescribe()
    {
        var mdl = "create or modify microflow M.Flow ()\nbegin\n  if $Obj/Count > 0 then\n    return true;\n  end if;\nend;";
        Assert.Empty(ExpressionRules.RedundantBoolean(DescribeMicroflowExpressions.Extract("M.Flow", mdl)));
    }
}
