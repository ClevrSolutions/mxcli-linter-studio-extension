using Clevr.Acr.Normalizer;
using Xunit;

namespace Clevr.Acr.Normalizer.Tests;

// FASE 2 proof: the mxcli-describe route for CLEVR-REL-002 (incomplete empty-string) and CLEVR-MAINT-009
// (nested-if). The extractor assembles multi-line MDL into complete statements/conditions; the EXISTING
// ExpressionRules.IncompleteEmptyStringCheck / MicroflowStructureRules.NestedIfStatements (unchanged) apply.
public class DescribeMicroflowExpressionsTests
{
    private static IReadOnlyList<Violation> Rel002(string mdl)
        => ExpressionRules.IncompleteEmptyStringCheck(DescribeMicroflowExpressions.Extract("M.Flow", mdl));

    private static IReadOnlyList<Violation> Maint009(string mdl)
        => MicroflowStructureRules.NestedIfStatements(DescribeMicroflowExpressions.ExtractSplits("M.Flow", mdl));

    // ── REL-002 ──
    [Fact]
    public void Rel002_Fires_OnIncompleteCheck()
    {
        var mdl = "create or modify microflow M.Flow ()\nbegin\n  @position(0,0)\n  if $Obj/Name != '' then\n    return true;\n  end if;\nend;";
        var v = Assert.Single(Rel002(mdl));
        Assert.Equal("CLEVR-REL-002", v.RuleId);
    }

    [Fact]
    public void Rel002_DoesNotFire_OnMultiLineWrappedCompleteCheck()
    {
        // THE regression case (Encryption.MB_SaveCertificate): describe wraps the complete check over
        // three lines. The assembler must join them so '!= empty' and "!= ''" are seen together → complete.
        var mdl =
            "create or modify microflow M.Flow ()\n" +
            "begin\n" +
            "  @caption 'Has pass phrase?'\n" +
            "  if $Certificate/PassPhrase_Plain != empty\n" +
            "and\n" +
            "$Certificate/PassPhrase_Plain != '' then\n" +
            "    return;\n" +
            "  end if;\n" +
            "end;";
        Assert.Empty(Rel002(mdl));
    }

    [Fact]
    public void Rel002_DoesNotFire_OnMultiLineWrappedNotCompleteCheck()
    {
        // 'not(... != empty and ... != '')' wrapped over lines (the EmailAddress case) → complete → no fire.
        var mdl =
            "create or modify microflow M.Flow ()\nbegin\n" +
            "  if not($Certificate/EmailAddress != empty\nand\n$Certificate/EmailAddress != '') then\n" +
            "    return;\n  end if;\nend;";
        Assert.Empty(Rel002(mdl));
    }

    // ── MAINT-009 ──
    [Fact]
    public void Maint009_Fires_OnNestedInlineIf_MultiLine()
    {
        // The real SUB_ValidateVelden shape: a split whose condition contains a chained inline if/else-if,
        // wrapped across lines. Assembler joins; existing NestedIfRegex matches → exactly one finding.
        var mdl =
            "create or modify microflow M.Flow ()\nbegin\n" +
            "  @caption 'Valid date?'\n" +
            "  if not(if $Dossier/DatumVoorval = empty then true\n" +
            "else if $Dossier/DatumVoorval < dateTime(2015,4) then false\n" +
            "else true) then\n" +
            "    return;\n  end if;\nend;";
        var v = Assert.Single(Maint009(mdl));
        Assert.Equal("CLEVR-MAINT-009", v.RuleId);
        Assert.Equal("Valid date?", v.ElementName);
    }

    [Fact]
    public void Maint009_DoesNotFire_OnPlainSplit()
    {
        var mdl = "create or modify microflow M.Flow ()\nbegin\n  @caption '> 0'\n  if $Obj/Count > 0 then\n    return;\n  end if;\nend;";
        Assert.Empty(Maint009(mdl));
    }

    [Fact]
    public void Maint009_DoesNotFire_OnMultiLinePlainCompoundCondition()
    {
        // A wrapped compound boolean (no inline if) must NOT be mistaken for a nested if.
        var mdl =
            "create or modify microflow M.Flow ()\nbegin\n  @caption 'Has phrase?'\n" +
            "  if $Certificate/PassPhrase_Plain != empty\nand\n$Certificate/PassPhrase_Plain != '' then\n" +
            "    return;\n  end if;\nend;";
        Assert.Empty(Maint009(mdl));
    }

    // ── assembler sanity ──
    [Fact]
    public void Assemble_JoinsWrappedCondition_SkipsDecoration()
    {
        var mdl =
            "create or modify microflow M.Flow ()\nbegin\n  @position(1,2)\n  @caption 'x'\n" +
            "  if $A != empty\nand\n$A != '' then\n  end if;\nend;";
        var split = Assert.Single(DescribeMicroflowExpressions.Assemble(mdl), s => s.IsSplit);
        Assert.Equal("if $A != empty and $A != '' then", split.Text);
        Assert.Equal("x", split.Caption);
    }
}
