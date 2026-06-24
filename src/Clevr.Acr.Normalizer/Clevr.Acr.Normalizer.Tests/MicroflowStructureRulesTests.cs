using Clevr.Acr.Normalizer;
using Xunit;

namespace Clevr.Acr.Normalizer.Tests;

public class MicroflowStructureRulesTests
{
    [Theory]
    [InlineData(28, true)]   // 28 - 2 = 26 > 25 → flagged (smallest flagged)
    [InlineData(27, false)]  // 27 - 2 = 25, NOT > 25 → boundary, not flagged
    [InlineData(26, false)]  // 24 → not flagged
    [InlineData(100, true)]  // 98 → flagged
    [InlineData(2, false)]   // only start+end → 0
    [InlineData(0, false)]   // empty/parse-miss → -2, not flagged
    public void Threshold_IsObjectsMinusTwoGreaterThan25(int objectCount, bool flagged)
    {
        var vs = MicroflowStructureRules.NumberOfElements(new[] { ("App.MF", objectCount) });
        Assert.Equal(flagged ? 1 : 0, vs.Count);
    }

    [Fact]
    public void FlaggedViolation_HasCorrectIdentityAndCount()
    {
        var vs = MicroflowStructureRules.NumberOfElements(new[] { ("App.BigFlow", 30) });
        var v = Assert.Single(vs);
        Assert.Equal("CLEVR-MAINT-007", v.RuleId);
        Assert.Equal(ViolationKind.Acr, v.Kind);
        Assert.Equal("clevr-acr", v.Source);
        Assert.Equal("NumberOfElementsInMicroflow", v.AcrCode);
        Assert.Equal("Maintainability", v.Category);
        Assert.Equal("Major", v.Severity);
        Assert.Equal("Microflow", v.DocumentType);
        Assert.Equal("App.BigFlow", v.DocumentQualifiedName);
        Assert.Equal("", v.ElementName);
        Assert.StartsWith("sha1:", v.Fingerprint);
        Assert.Contains("28 elements", v.Reason); // 30 - 2 = 28
    }

    [Fact]
    public void DedupsPerMicroflow_AndCountsEachFlowOnce()
    {
        var vs = MicroflowStructureRules.NumberOfElements(new[]
        {
            ("App.A", 40),
            ("App.A", 40), // duplicate same microflow → one
            ("App.B", 50),
            ("App.C", 10), // below threshold → none
        });
        Assert.Equal(2, vs.Count);
        Assert.Equal(new[] { "App.A", "App.B" }, vs.Select(v => v.DocumentQualifiedName));
    }

    [Fact]
    public void EmptyInput_YieldsNothing()
    {
        Assert.Empty(MicroflowStructureRules.NumberOfElements(System.Array.Empty<(string, int)>()));
    }

    // ---- CLEVR-MAINT-008 ComplexWithoutAnnotations (mxlint 005_0004) -----------------------------

    [Theory]
    [InlineData(11, 0, 0, true)]   // >10 activities, no annotation → flagged
    [InlineData(10, 0, 0, false)]  // boundary: 10 not > 10
    [InlineData(0, 3, 0, true)]    // >2 exclusive splits → flagged
    [InlineData(0, 2, 0, false)]   // boundary: 2 not > 2
    [InlineData(11, 3, 1, false)]  // complex but HAS an annotation → not flagged
    [InlineData(5, 1, 0, false)]   // not complex
    public void Complex_RequiresComplexAndZeroAnnotations(int aa, int es, int ann, bool flagged)
    {
        var vs = MicroflowStructureRules.ComplexWithoutAnnotations(new[] { ("App.MF", aa, es, ann) });
        Assert.Equal(flagged ? 1 : 0, vs.Count);
    }

    [Fact]
    public void Complex_Identity()
    {
        var v = Assert.Single(MicroflowStructureRules.ComplexWithoutAnnotations(new[] { ("App.MF", 12, 0, 0) }));
        Assert.Equal("CLEVR-MAINT-008", v.RuleId);
        Assert.Equal(ViolationKind.Acr, v.Kind);
        Assert.Equal("Maintainability", v.Category);
        Assert.Equal("Major", v.Severity);
        Assert.Equal("Microflow", v.DocumentType);
    }

    [Fact]
    public void Complex_EmptyYieldsNothing()
        => Assert.Empty(MicroflowStructureRules.ComplexWithoutAnnotations(System.Array.Empty<(string, int, int, int)>()));

    // ---- CLEVR-MAINT-009 NestedIfStatements (mxlint 005_0005) ------------------------------------

    [Theory]
    [InlineData("if $x then true else if $y then false else true", true)] // nested: then/else followed by if
    [InlineData("if $x then 1 else 2", false)]                            // single if: no 'if' after then/else
    [InlineData("$Count = 0", false)]                                     // plain comparison
    [InlineData("$a > 0 and $b < 10", false)]                             // no if/then/else
    public void NestedIf_MatchesRegexVerbatim(string expr, bool flagged)
    {
        var vs = MicroflowStructureRules.NestedIfStatements(new[] { ("App.MF", "cap", expr) });
        Assert.Equal(flagged ? 1 : 0, vs.Count);
    }

    [Fact]
    public void NestedIf_IdentityAndCaptionAsElement()
    {
        var v = Assert.Single(MicroflowStructureRules.NestedIfStatements(
            new[] { ("App.MF", "Date check?", "if $x = empty then true else if $y < 1 then false else true") }));
        Assert.Equal("CLEVR-MAINT-009", v.RuleId);
        Assert.Equal("Maintainability", v.Category); // mapped from .rego category "Complexity"
        Assert.Equal("Major", v.Severity);
        Assert.Equal("Date check?", v.ElementName);
    }

    [Fact]
    public void NestedIf_DedupsPerMicroflowCaption()
    {
        var vs = MicroflowStructureRules.NestedIfStatements(new[]
        {
            ("App.MF", "c1", "if a then b else if c then d else e"),
            ("App.MF", "c1", "if a then b else if c then d else e"), // same (mf,caption) → one
            ("App.MF", "c2", "if a then b else if c then d else e"), // other caption → counts
        });
        Assert.Equal(2, vs.Count);
    }

    // ---- CLEVR-PERF-COMMIT-IN-LOOP AvoidCommitInLoop (mxlint 005_0002) ---------------------------

    [Fact]
    public void CommitInLoop_PositiveControl_DetectsCommitAndChangeYes()
    {
        var input = new (string, IReadOnlyList<(string, string?)>)[]
        {
            ("App.HasCommit", new (string, string?)[] { ("Microflows$ActionActivity", null), ("Microflows$CommitAction", null) }),
            ("App.HasChangeYes", new (string, string?)[] { ("Microflows$ChangeAction", "Yes") }),
        };
        var vs = MicroflowStructureRules.CommitInLoop(input);
        Assert.Equal(2, vs.Count);
        Assert.Equal("CLEVR-PERF-COMMIT-IN-LOOP", vs[0].RuleId);
        Assert.Equal("Performance", vs[0].Category);
        Assert.Equal("AvoidCommitInLoop", vs[0].AcrCode);
    }

    [Fact]
    public void CommitInLoop_NegativeCases_NotFlagged()
    {
        var input = new (string, IReadOnlyList<(string, string?)>)[]
        {
            ("App.ChangeNo", new (string, string?)[] { ("Microflows$ChangeAction", "No") }),
            ("App.ChangeVar", new (string, string?)[] { ("Microflows$ChangeVariableAction", null) }),
            ("App.NoActions", System.Array.Empty<(string, string?)>()),
        };
        Assert.Empty(MicroflowStructureRules.CommitInLoop(input));
    }
}
