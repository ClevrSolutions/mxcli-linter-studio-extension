using Clevr.Acr.Normalizer;
using Xunit;

namespace Clevr.Acr.Normalizer.Tests;

public class BsonMicroflowParserTests
{
    // Minimale, GETROUWE fixtures in dezelfde vorm als de echte `bson dump --format json`
    // (vastgesteld op TRB): node = array van {Key,Value}; collectie = [markerInt, [node], ...].

    private static string Microflow(string objectsBody) => $$"""
    [
      { "Key": "$Type", "Value": "Microflows$Microflow" },
      { "Key": "ObjectCollection", "Value": [
        { "Key": "$Type", "Value": "Microflows$MicroflowObjectCollection" },
        { "Key": "Objects", "Value": [ 3, {{objectsBody}} ] }
      ]}
    ]
    """;

    private static string Loop(string loopBody) => $$"""
    [ { "Key": "$Type", "Value": "Microflows$LoopedActivity" },
      { "Key": "ObjectCollection", "Value": [
        { "Key": "$Type", "Value": "Microflows$MicroflowObjectCollection" },
        { "Key": "Objects", "Value": [ 3, {{loopBody}} ] }
      ]}
    ]
    """;

    private static string ActionActivity(string action) => $$"""
    [ { "Key": "$Type", "Value": "Microflows$ActionActivity" },
      { "Key": "Action", "Value": {{action}} } ]
    """;

    private const string CommitAction = """
    [ { "Key": "$Type", "Value": "Microflows$CommitAction" },
      { "Key": "CommitVariableName", "Value": "IteratorAccount" } ]
    """;

    private static string ChangeAction(string commit) => $$"""
    [ { "Key": "$Type", "Value": "Microflows$ChangeAction" },
      { "Key": "ChangeVariableName", "Value": "IteratorAccount" },
      { "Key": "Commit", "Value": "{{commit}}" } ]
    """;

    private const string Qn = "Administration.MF_Test";

    [Fact]
    public void CommitAction_InsideLoop_IsFlagged()
    {
        var json = Microflow(Loop(ActionActivity(CommitAction)));
        var v = Assert.Single(BsonMicroflowParser.DetectCommitInLoop(json, Qn));
        Assert.Equal("CLEVR-PERF-COMMIT-IN-LOOP", v.RuleId);
        Assert.Equal("Performance", v.Category);
        Assert.Equal("Microflow", v.DocumentType);
        Assert.Equal(Qn, v.DocumentQualifiedName);
        Assert.Contains("Commit inside a loop", v.Reason);
        Assert.StartsWith("sha1:", v.Fingerprint);
    }

    [Fact]
    public void ChangeAction_WithCommitYes_InsideLoop_IsFlagged()
    {
        var json = Microflow(Loop(ActionActivity(ChangeAction("Yes"))));
        Assert.Single(BsonMicroflowParser.DetectCommitInLoop(json, Qn));
    }

    [Fact]
    public void ChangeAction_WithCommitNo_InsideLoop_IsNotFlagged()
    {
        // De correcte variant: wijzigen in de loop ZONDER commit (commit komt na de loop).
        var json = Microflow(Loop(ActionActivity(ChangeAction("No"))));
        Assert.Empty(BsonMicroflowParser.DetectCommitInLoop(json, Qn));
    }

    [Fact]
    public void CommitAction_AfterLoop_IsNotFlagged()
    {
        // Loop met een change (Commit=No) + een CommitAction NA de loop → correct, geen violation.
        // Dit spiegelt de echte TRB-microflow ScE_Account_SetInactiveAfterSixMonthsNoLogin.
        var body = Loop(ActionActivity(ChangeAction("No"))) + ", " + ActionActivity(CommitAction);
        var json = Microflow(body);
        Assert.Empty(BsonMicroflowParser.DetectCommitInLoop(json, Qn));
    }

    [Fact]
    public void CommitAction_InNestedLoop_IsFlagged()
    {
        var json = Microflow(Loop(Loop(ActionActivity(CommitAction))));
        Assert.Single(BsonMicroflowParser.DetectCommitInLoop(json, Qn));
    }

    [Fact]
    public void TwoCommitsInLoop_YieldTwoViolations()
    {
        var body = Loop(ActionActivity(CommitAction) + ", " + ActionActivity(ChangeAction("Yes")));
        var json = Microflow(body);
        Assert.Equal(2, BsonMicroflowParser.DetectCommitInLoop(json, Qn).Count);
    }

    [Fact]
    public void Empty_OrCorrupt_YieldsNoViolations()
    {
        Assert.Empty(BsonMicroflowParser.DetectCommitInLoop("", Qn));
        Assert.Empty(BsonMicroflowParser.DetectCommitInLoop("not json", Qn));
        Assert.Empty(BsonMicroflowParser.DetectCommitInLoop("{}", Qn));
    }
}
