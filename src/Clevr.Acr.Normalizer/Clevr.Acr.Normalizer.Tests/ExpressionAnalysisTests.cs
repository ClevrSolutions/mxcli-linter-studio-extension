using Clevr.Acr.Normalizer;
using Xunit;

namespace Clevr.Acr.Normalizer.Tests;

public class ExpressionAnalysisTests
{
    [Fact]
    public void BothEmptyAndEmptyStringOnSameVar_IsRedundant()
    {
        var p = ExpressionAnalysis.RedundantEmptyStringPaths("$Beedigde/Achternaam != empty and $Beedigde/Achternaam != ''");
        Assert.Equal(new[] { "$Beedigde/Achternaam" }, p);
    }

    [Fact]
    public void EqEmptyOrEqEmptyString_IsRedundant()
    {
        // De if-variant uit TRB: "= empty or = ''".
        Assert.True(ExpressionAnalysis.HasRedundantEmptyStringCheck(
            "if ($Melder/Postcode = empty or $Melder/Postcode = '') then true else false"));
    }

    [Fact]
    public void DoubleQuoteEmptyString_IsAlsoRecognised()
    {
        Assert.True(ExpressionAnalysis.HasRedundantEmptyStringCheck("$x/Attr != empty and $x/Attr != \"\""));
    }

    [Fact]
    public void LoneEmptyCheck_IsNotRedundant()
    {
        // De 396 idiomatisch-correcte: alleen != empty.
        Assert.Empty(ExpressionAnalysis.RedundantEmptyStringPaths("$x/Attr != empty and $y/Other != empty"));
    }

    [Fact]
    public void LoneEmptyStringCheck_IsNotRedundant()
    {
        // Losse != '' zonder empty-check → kan legitiem zijn → geen violation.
        Assert.Empty(ExpressionAnalysis.RedundantEmptyStringPaths("$x/Attr != ''"));
    }

    [Fact]
    public void EmptyAndEmptyStringOnDifferentVars_IsNotRedundant()
    {
        Assert.Empty(ExpressionAnalysis.RedundantEmptyStringPaths("$a/X != empty and $b/Y != ''"));
    }

    [Fact]
    public void EmptyOrCorrupt_YieldsNothing()
    {
        Assert.Empty(ExpressionAnalysis.RedundantEmptyStringPaths(""));
        Assert.Empty(ExpressionAnalysis.RedundantEmptyStringPaths(null));
        Assert.Empty(ExpressionAnalysis.RedundantEmptyStringPaths("12345 random text"));
    }

    [Fact]
    public void MultipleRedundantPaths_AllReturnedOnce()
    {
        var p = ExpressionAnalysis.RedundantEmptyStringPaths(
            "$m/CC != empty and $m/CC != '' and $m/BCC != empty and $m/BCC != '' and $m/BCC != ''");
        Assert.Equal(new[] { "$m/CC", "$m/BCC" }, p); // BCC niet dubbel
    }
}
