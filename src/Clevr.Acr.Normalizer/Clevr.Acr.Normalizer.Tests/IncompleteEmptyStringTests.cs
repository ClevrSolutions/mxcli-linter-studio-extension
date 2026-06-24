using Clevr.Acr.Normalizer;
using Xunit;

namespace Clevr.Acr.Normalizer.Tests;

public class IncompleteEmptyStringTests
{
    // ---- analysis (verbatim mxlint 005_0001: strip spaces; contains "!=''" && !contains "!=empty") ----
    [Theory]
    [InlineData("$x != ''", true)]                              // incomplete: only the '' check
    [InlineData("$x  !=  ''", true)]                            // spaces stripped → still matches
    [InlineData("$x != '' and $x != empty", false)]            // complete: both present
    [InlineData("$a != '' and $b != empty", false)]            // .rego crudeness: ANY !=empty makes it 'complete'
    [InlineData("$x != empty", false)]                          // only empty → fine
    [InlineData("$x = ''", false)]                              // equality, not '!='
    [InlineData("$x != \"\"", false)]                           // double-quote: .rego checks single-quote only
    [InlineData("$Count > 0", false)]                           // unrelated
    [InlineData("", false)]
    public void IsIncomplete_MatchesRegoVerbatim(string expr, bool expected)
        => Assert.Equal(expected, ExpressionAnalysis.IsIncompleteEmptyStringCheck(expr));

    [Fact]
    public void IsIncomplete_Null_IsFalse()
        => Assert.False(ExpressionAnalysis.IsIncompleteEmptyStringCheck(null));

    // ---- rule builder (CLEVR-REL-002) ----
    [Fact]
    public void Builder_FlagsIncomplete_WithIdentity()
    {
        var vs = ExpressionRules.IncompleteEmptyStringCheck(new[] { ("App.MF", "$Customer/Name != ''") });
        var v = Assert.Single(vs);
        Assert.Equal("CLEVR-REL-002", v.RuleId);
        Assert.Equal(ViolationKind.Acr, v.Kind);
        Assert.Equal("clevr-acr", v.Source);
        Assert.Equal("EmptyStringCheckNotComplete", v.AcrCode);
        Assert.Equal("Reliability", v.Category);  // .rego category "Error" → mapped to Reliability
        Assert.Equal("Major", v.Severity);
        Assert.Equal("Microflow", v.DocumentType);
        Assert.Equal("$Customer/Name != ''", v.ElementName); // normalized expression identifies the finding
        Assert.StartsWith("sha1:", v.Fingerprint);
    }

    [Fact]
    public void Builder_IgnoresCompleteAndEmpty_AndDedupsPerExpression()
    {
        var vs = ExpressionRules.IncompleteEmptyStringCheck(new[]
        {
            ("App.MF", "$x != '' and $x != empty"), // complete → ignored
            ("App.MF", "$a != ''"),                 // incomplete
            ("App.MF", "$a  !=  ''"),               // same after normalization → dedup
            ("App.MF", "$b != ''"),                 // distinct expression → separate finding
            ("App.MF", "$y != empty"),              // fine → ignored
        });
        Assert.Equal(2, vs.Count); // $a and $b
    }

    [Fact]
    public void Builder_Empty_YieldsNothing()
        => Assert.Empty(ExpressionRules.IncompleteEmptyStringCheck(System.Array.Empty<(string, string)>()));

}
