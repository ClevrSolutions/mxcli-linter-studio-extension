using Clevr.Acr.Normalizer;
using Xunit;

namespace Clevr.Acr.Normalizer.Tests;

public class ManualChecksJsonTests
{
    private static ManualCheckAnswer Sample(string id, string answer = "yes", string note = "Reviewed in Studio Pro") => new()
    {
        Id = id, Answer = answer, Note = note, AnsweredBy = "michel", Date = "2026-06-16",
    };

    [Fact]
    public void Parse_TolerantOfEmptyAndCorrupt()
    {
        Assert.Empty(ManualChecksJson.Parse(""));
        Assert.Empty(ManualChecksJson.Parse("{}"));
        Assert.Empty(ManualChecksJson.Parse("{ \"answers\": [] }"));
        Assert.Empty(ManualChecksJson.Parse("not json"));
    }

    [Fact]
    public void RoundTrip_PreservesFields()
    {
        var json = ManualChecksJson.Serialize(new[] { Sample("MC-PERF-RECOMMENDER") });
        var a = Assert.Single(ManualChecksJson.Parse(json));
        Assert.Equal("MC-PERF-RECOMMENDER", a.Id);
        Assert.Equal("yes", a.Answer);
        Assert.Equal("Reviewed in Studio Pro", a.Note);
        Assert.Equal("michel", a.AnsweredBy);
        Assert.Equal("2026-06-16", a.Date);
    }

    [Fact]
    public void Upsert_OneAnswerPerCheck()
    {
        var list = ManualChecksJson.Upsert(new List<ManualCheckAnswer>(), Sample("MC-1", "no", "not yet"));
        list = ManualChecksJson.Upsert(list, Sample("MC-2", "yes", "done"));
        Assert.Equal(2, list.Count);
        // Re-answering the same check replaces it (no duplicate).
        list = ManualChecksJson.Upsert(list, Sample("MC-1", "yes", "now reviewed"));
        Assert.Equal(2, list.Count);
        var mc1 = Assert.Single(list, a => a.Id == "MC-1");
        Assert.Equal("yes", mc1.Answer);
        Assert.Equal("now reviewed", mc1.Note);
    }

    [Fact]
    public void Remove_DropsOnlyMatchingId()
    {
        var list = new List<ManualCheckAnswer> { Sample("MC-1"), Sample("MC-2") };
        var after = ManualChecksJson.Remove(list, "MC-1");
        Assert.Equal("MC-2", Assert.Single(after).Id);
    }
}
