using Clevr.Acr.Normalizer;
using Xunit;

namespace Clevr.Acr.Normalizer.Tests;

public class RedundantBooleanTests
{
    [Theory]
    [InlineData("$x/Flag = true", "$x/Flag")]
    [InlineData("$x/Flag = false", "$x/Flag")]
    [InlineData("$x/Flag != true", "$x/Flag")]
    [InlineData("$x/Flag != false", "$x/Flag")]
    [InlineData("true = $x/Flag", "$x/Flag")]   // literal links
    [InlineData("$IsValid=true", "$IsValid")]    // zonder spaties
    public void BooleanLiteralComparison_IsFlagged(string expr, string operandPath)
    {
        Assert.Equal(new[] { operandPath }, ExpressionAnalysis.RedundantBooleanOperands(expr));
    }

    [Theory]
    [InlineData("$x/Status = MyEnum.Active")] // enum-vergelijking, geen true/false-literal
    [InlineData("$x/Name = $y/Name")]          // geen literal
    [InlineData("$x = trueValue")]             // 'true' is hier deel van een identifier (word-boundary)
    [InlineData("$x/Attr != empty")]           // empty-check, geen boolean-literal
    public void NonBooleanLiteralComparison_IsNotFlagged(string expr)
    {
        Assert.Empty(ExpressionAnalysis.RedundantBooleanOperands(expr));
    }

    [Fact]
    public void MultipleOperands_AllOnce()
    {
        var p = ExpressionAnalysis.RedundantBooleanOperands("$a/X = true and $b/Y = false or $a/X != true");
        Assert.Equal(new[] { "$a/X", "$b/Y" }, p); // $a/X niet dubbel
    }

    [Fact]
    public void EmptyOrCorrupt_YieldsNothing()
    {
        Assert.Empty(ExpressionAnalysis.RedundantBooleanOperands(""));
        Assert.Empty(ExpressionAnalysis.RedundantBooleanOperands(null));
    }

    [Fact]
    public void Rule_BuildsViolationWithIdentity_AndDedupsPerMicroflowOperand()
    {
        var pairs = new[]
        {
            ("App.MF1", "$x/Flag = true"),
            ("App.MF1", "$x/Flag = true"),   // duplicaat zelfde (mf,operand) → één
            ("App.MF2", "$x/Flag = true"),   // ander microflow → telt apart
        };
        var vs = ExpressionRules.RedundantBoolean(pairs);
        Assert.Equal(2, vs.Count);
        var v = vs[0];
        Assert.Equal("CLEVR-MAINT-006", v.RuleId);
        Assert.Equal(ViolationKind.Acr, v.Kind);
        Assert.Equal("clevr-acr", v.Source);
        Assert.Equal("Maintainability", v.Category);
        Assert.Equal("Major", v.Severity);
        Assert.Equal("Microflow", v.DocumentType);
        Assert.Equal("$x/Flag", v.ElementName);
        Assert.StartsWith("sha1:", v.Fingerprint);
    }
}
