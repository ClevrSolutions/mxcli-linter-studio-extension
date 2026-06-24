using Clevr.Acr.Normalizer;
using Xunit;

namespace Clevr.Acr.Normalizer.Tests;

public class BsonRedundantEmptyStringTests
{
    // Getrouwe minimale bson-vorm: node = array van {Key,Value}; collectie = [marker, node, ...].
    // Microflow → ObjectCollection → Objects → ExclusiveSplit → SplitCondition (ExpressionSplitCondition).
    private static string Microflow(string objectsBody) => $$"""
    [
      { "Key": "$Type", "Value": "Microflows$Microflow" },
      { "Key": "ObjectCollection", "Value": [
        { "Key": "$Type", "Value": "Microflows$MicroflowObjectCollection" },
        { "Key": "Objects", "Value": [ 3, {{objectsBody}} ] }
      ]}
    ]
    """;

    private static string Split(string expression) => $$"""
    [ { "Key": "$Type", "Value": "Microflows$ExclusiveSplit" },
      { "Key": "SplitCondition", "Value": [
        { "Key": "$Type", "Value": "Microflows$ExpressionSplitCondition" },
        { "Key": "Expression", "Value": "{{expression}}" }
      ]}
    ]
    """;

    private const string Qn = "TRB.MF_Test";

    [Fact]
    public void RedundantSplitCondition_IsFlagged()
    {
        var json = Microflow(Split("$m/CC != empty and $m/CC != ''"));
        var v = Assert.Single(BsonMicroflowParser.DetectRedundantEmptyStringChecks(json, Qn));
        Assert.Equal("CLEVR-REL-001", v.RuleId);
        Assert.Equal(ViolationKind.Acr, v.Kind);
        Assert.Equal("clevr-acr", v.Source);
        Assert.Equal("Reliability", v.Category);
        Assert.Equal("Major", v.Severity);
        Assert.Equal("Microflow", v.DocumentType);
        Assert.Equal(Qn, v.DocumentQualifiedName);
        Assert.Equal("$m/CC", v.ElementName);
        Assert.StartsWith("sha1:", v.Fingerprint);
    }

    [Fact]
    public void LoneEmptyCheckSplit_IsNotFlagged()
    {
        var json = Microflow(Split("$m/CC != empty"));
        Assert.Empty(BsonMicroflowParser.DetectRedundantEmptyStringChecks(json, Qn));
    }

    [Fact]
    public void TwoSplits_OneRedundant_OneNot()
    {
        var json = Microflow(Split("$m/CC != empty and $m/CC != ''") + ", " + Split("$m/X != empty"));
        var v = Assert.Single(BsonMicroflowParser.DetectRedundantEmptyStringChecks(json, Qn));
        Assert.Equal("$m/CC", v.ElementName);
    }

    [Fact]
    public void SamePathDuplicatedAcrossSplits_IsDedupedPerMicroflow()
    {
        var json = Microflow(Split("$m/BCC != empty and $m/BCC != ''") + ", " + Split("$m/BCC != empty and $m/BCC != ''"));
        Assert.Single(BsonMicroflowParser.DetectRedundantEmptyStringChecks(json, Qn)); // één per (microflow, pad)
    }

    [Fact]
    public void EmptyOrCorrupt_YieldsNoViolations()
    {
        Assert.Empty(BsonMicroflowParser.DetectRedundantEmptyStringChecks("", Qn));
        Assert.Empty(BsonMicroflowParser.DetectRedundantEmptyStringChecks("not json", Qn));
    }
}
