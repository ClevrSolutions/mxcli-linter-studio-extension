using Clevr.Acr.Normalizer;
using Xunit;

namespace Clevr.Acr.Normalizer.Tests;

public class ExclusionsJsonTests
{
    private static Exclusion Sample(string fp, string reason = "Intentional, tracked in EPIC-123") => new()
    {
        Fingerprint = fp, RuleId = "CLEVR-PERF-014",
        DocumentQualifiedName = "Sales.Order", ElementName = "",
        Reason = reason, ExcludedBy = "michel", Date = "2026-06-15",
    };

    [Fact]
    public void Parse_TolerantOfEmptyAndMissing()
    {
        Assert.Empty(ExclusionsJson.Parse(""));
        Assert.Empty(ExclusionsJson.Parse("{}"));
        Assert.Empty(ExclusionsJson.Parse("{ \"exclusions\": [] }"));
        Assert.Empty(ExclusionsJson.Parse("not json at all")); // corrupt → empty, do not crash
    }

    [Fact]
    public void RoundTrip_PreservesFieldsAndReason()
    {
        var json = ExclusionsJson.Serialize(new[] { Sample("sha1:abc") });
        var parsed = Assert.Single(ExclusionsJson.Parse(json));
        Assert.Equal("sha1:abc", parsed.Fingerprint);
        Assert.Equal("CLEVR-PERF-014", parsed.RuleId);
        Assert.Equal("Sales.Order", parsed.DocumentQualifiedName);
        Assert.Equal("Intentional, tracked in EPIC-123", parsed.Reason);
        Assert.Equal("michel", parsed.ExcludedBy);
        Assert.Equal("2026-06-15", parsed.Date);
    }

    [Fact]
    public void Upsert_AddsNew_AndReplacesSameFingerprint()
    {
        var list = ExclusionsJson.Upsert(new List<Exclusion>(), Sample("sha1:a", "first"));
        list = ExclusionsJson.Upsert(list, Sample("sha1:b", "second"));
        Assert.Equal(2, list.Count);

        // Same fingerprint → replaces (no duplicate), new reason wins.
        list = ExclusionsJson.Upsert(list, Sample("sha1:a", "updated reason"));
        Assert.Equal(2, list.Count);
        Assert.Equal("updated reason", Assert.Single(list, e => e.Fingerprint == "sha1:a").Reason);
    }

    [Fact]
    public void Remove_DropsOnlyMatchingFingerprint()
    {
        var list = new List<Exclusion> { Sample("sha1:a"), Sample("sha1:b") };
        var after = ExclusionsJson.Remove(list, "sha1:a");
        Assert.Equal("sha1:b", Assert.Single(after).Fingerprint);
    }
}
