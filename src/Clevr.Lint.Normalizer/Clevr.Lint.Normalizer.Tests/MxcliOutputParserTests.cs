using Clevr.Lint.Normalizer;
using Xunit;

namespace Clevr.Lint.Normalizer.Tests;

/// <summary>
/// Covers the tolerant-parsing contract of <see cref="MxcliOutputParser"/>: mxcli can be
/// killed at timeout mid-write (truncated JSON) or emit a single malformed element in an
/// otherwise-valid array. Neither may throw or discard the salvageable findings.
/// </summary>
public class MxcliOutputParserTests
{
    [Fact]
    public void TruncatedJson_ReturnsEmptyList_DoesNotThrow()
    {
        // mxcli killed at timeout: status line followed by JSON that stops mid-token.
        const string stdout = "some status\n{\"ruleId\": \"X";

        var result = MxcliOutputParser.Parse(stdout);

        Assert.Empty(result);
    }

    [Fact]
    public void OneMalformedElement_DoesNotDiscardValidSiblings()
    {
        // First element has "severity": 3 (number instead of string) → skipped;
        // the second, well-formed element must survive.
        const string stdout = """
        [
          { "ruleId": "MPR001", "severity": 3, "message": "bad" },
          { "ruleId": "MPR002", "severity": "warning", "message": "good",
            "module": "Sales", "document": "Customer" }
        ]
        """;

        var result = MxcliOutputParser.Parse(stdout);

        var v = Assert.Single(result);
        Assert.Equal("MPR002", v.RuleId);
        Assert.Equal("warning", v.Severity);
    }

    [Fact]
    public void StatusLineContainingBrace_DoesNotThrow()
    {
        // A status line with a '{' trips the JSON-start heuristic; what follows is not
        // valid JSON. Must be tolerated, not thrown.
        const string stdout = "Progress {1/10}\ndone";

        var result = MxcliOutputParser.Parse(stdout);

        Assert.Empty(result);
    }

    [Fact]
    public void TryParse_NoJsonAtAll_ReturnsFalse()
    {
        // The "no JSON → real mxcli error" diagnostic in LintScanService depends on this.
        Assert.False(MxcliOutputParser.TryParse("Error: connection refused", out var violations));
        Assert.Empty(violations);
    }

    [Fact]
    public void TryParse_ValidJson_ReturnsTrueWithViolations()
    {
        const string stdout = """
        Connected to: project
        [ { "ruleId": "MPR001", "severity": "warning", "message": "msg" } ]
        """;

        Assert.True(MxcliOutputParser.TryParse(stdout, out var violations));
        var v = Assert.Single(violations);
        Assert.Equal("MPR001", v.RuleId);
    }
}
