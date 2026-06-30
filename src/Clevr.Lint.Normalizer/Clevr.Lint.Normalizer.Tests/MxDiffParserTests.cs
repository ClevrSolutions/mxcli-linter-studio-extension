using Clevr.Lint.Normalizer;
using Xunit;

namespace Clevr.Lint.Normalizer.Tests;

public class MxDiffParserTests
{
    [Fact]
    public void Parse_NullOrEmpty_ReturnsEmpty()
    {
        var r1 = MxDiffParser.Parse(null!);
        var r2 = MxDiffParser.Parse("");
        var r3 = MxDiffParser.Parse("   ");

        Assert.Empty(r1.MicroflowUnitIds);
        Assert.Empty(r1.EntityQualifiedNames);
        Assert.Empty(r2.MicroflowUnitIds);
        Assert.Empty(r3.MicroflowUnitIds);
    }

    [Fact]
    public void Parse_MalformedJson_ReturnsEmpty()
    {
        var r = MxDiffParser.Parse("not json at all { broken");

        Assert.Empty(r.MicroflowUnitIds);
        Assert.Empty(r.EntityQualifiedNames);
    }

    [Fact]
    public void Parse_DeletedEntries_AreExcluded()
    {
        var json = """
        {
          "unitDifferences": [
            { "type": "Microflows$Microflow", "id": "aaa", "change": "Deleted" },
            { "type": "DomainModels$DomainModel", "change": "Deleted",
              "changeDetails": [{ "changeDescription": "Entity 'M.E'", "change": "Deleted" }] }
          ]
        }
        """;

        var r = MxDiffParser.Parse(json);

        Assert.Empty(r.MicroflowUnitIds);
        Assert.Empty(r.EntityQualifiedNames);
    }

    [Fact]
    public void Parse_VisualOnlyChanges_AreSkippedAndCounted()
    {
        var json = """
        {
          "unitDifferences": [
            { "type": "Microflows$Microflow", "id": "bbb", "change": "Modified",
              "onlyVisualChanges": true }
          ]
        }
        """;

        var r = MxDiffParser.Parse(json);

        Assert.Empty(r.MicroflowUnitIds);
        Assert.Equal(1, r.VisualOnlySkipped);
    }

    [Fact]
    public void Parse_MicroflowUnit_PopulatesMicroflowIds()
    {
        var json = """
        {
          "unitDifferences": [
            { "type": "Microflows$Microflow", "id": "guid-1", "change": "Modified" },
            { "type": "Microflows$Microflow", "id": "guid-2", "change": "Added" },
            { "type": "Microflows$Microflow", "id": "guid-1", "change": "Modified" }
          ]
        }
        """;

        var r = MxDiffParser.Parse(json);

        // guid-1 deduplicated, guid-2 included
        Assert.Equal(2, r.MicroflowUnitIds.Count);
        Assert.Contains("guid-1", r.MicroflowUnitIds);
        Assert.Contains("guid-2", r.MicroflowUnitIds);
        Assert.Empty(r.EntityQualifiedNames);
    }

    [Fact]
    public void Parse_DomainModelUnit_PopulatesEntityQualifiedNames()
    {
        var json = """
        {
          "unitDifferences": [
            {
              "type": "DomainModels$DomainModel", "change": "Modified",
              "changeDetails": [
                { "changeDescription": "Entity 'MyModule.Customer'", "change": "Modified" },
                { "changeDescription": "Association 'MyModule.Customer_Order'", "change": "Modified" },
                { "changeDescription": "Entity 'MyModule.Order'",    "change": "Modified" },
                { "changeDescription": "Entity 'MyModule.Customer'", "change": "Modified" }
              ]
            }
          ]
        }
        """;

        var r = MxDiffParser.Parse(json);

        // Associations ignored; entities deduplicated
        Assert.Equal(2, r.EntityQualifiedNames.Count);
        Assert.Contains("MyModule.Customer", r.EntityQualifiedNames);
        Assert.Contains("MyModule.Order", r.EntityQualifiedNames);
        Assert.Empty(r.MicroflowUnitIds);
    }

    // MxDiffParser returns the unit GUID for microflows; the qualified name
    // (e.g. "Test_PH007.NewMicroflow") is resolved separately via CATALOG.MICROFLOWS.
    [Fact]
    public void Parse_AddedMicroflow_GuidCaptured_ResolvesToTest_PH007NewMicroflow()
    {
        // Realistic mx diff slice: one Added microflow with a non-visual change detail.
        // GUID "e3a1f2b4-7c8d-4e9a-b0c1-d2e3f4a5b6c7" resolves to Test_PH007.NewMicroflow
        // via CATALOG.MICROFLOWS (resolution happens in ChangedElementsResolver, not here).
        var json = """
        {
          "unitDifferences": [
            {
              "type": "Microflows$Microflow",
              "id": "e3a1f2b4-7c8d-4e9a-b0c1-d2e3f4a5b6c7",
              "change": "Added",
              "changeDetails": [
                {
                  "changeDescription": "MicroflowBase 'Test_PH007.NewMicroflow'",
                  "change": "Added",
                  "onlyVisualChanges": false
                }
              ]
            }
          ]
        }
        """;

        var r = MxDiffParser.Parse(json);

        Assert.Single(r.MicroflowUnitIds);
        Assert.Equal("e3a1f2b4-7c8d-4e9a-b0c1-d2e3f4a5b6c7", r.MicroflowUnitIds[0]);
        Assert.Empty(r.EntityQualifiedNames);
        Assert.Equal(0, r.VisualOnlySkipped);
    }

    /// <summary>
    /// mx diff does NOT emit "Entity '...'" for attribute-level changes; instead it emits
    /// "Attribute 'Module.Entity.Attr'" and "Access rule of entity 'Module.Entity'".
    /// Both must be recognised and the entity deduplicated.
    /// Verified against real mx diff output from AcrToLintTest-main (2026-06-30).
    /// </summary>
    [Fact]
    public void Parse_AttributeChange_ExtractsParentEntity()
    {
        var json = """
        {
          "unitDifferences": [
            {
              "type": "DomainModels$DomainModel", "change": "Modified",
              "changeDetails": [
                {
                  "changeDescription": "Attribute 'Test_PH007.Item.ItemName'",
                  "change": "Modified", "onlyVisualChanges": false,
                  "changeDetails": [
                    { "property": "Name", "onlyVisualChanges": false,
                      "baseValue": "Name", "mineValue": "ItemName" }
                  ]
                },
                {
                  "changeDescription": "Access rule of entity 'Test_PH007.Item'",
                  "change": "Modified", "onlyVisualChanges": false,
                  "changeDetails": [
                    { "property": "Access of attribute 'ItemName' > Attribute",
                      "onlyVisualChanges": false }
                  ]
                }
              ]
            }
          ]
        }
        """;

        var r = MxDiffParser.Parse(json);

        // Both changeDescriptions reference the same entity — must be deduplicated
        Assert.Single(r.EntityQualifiedNames);
        Assert.Equal("Test_PH007.Item", r.EntityQualifiedNames[0]);
        Assert.Empty(r.MicroflowUnitIds);
        Assert.Equal(0, r.VisualOnlySkipped);
    }

    [Fact]
    public void Parse_MixedBatch_SeparatesCorrectly()
    {
        var json = """
        {
          "unitDifferences": [
            { "type": "Microflows$Microflow", "id": "mf-guid", "change": "Modified" },
            {
              "type": "DomainModels$DomainModel", "change": "Modified",
              "changeDetails": [
                { "changeDescription": "Entity 'Mod.Entity1'", "change": "Added" }
              ]
            },
            { "type": "Security$ProjectSecurity", "id": "sec-guid", "change": "Modified" }
          ]
        }
        """;

        var r = MxDiffParser.Parse(json);

        Assert.Single(r.MicroflowUnitIds);
        Assert.Equal("mf-guid", r.MicroflowUnitIds[0]);
        Assert.Single(r.EntityQualifiedNames);
        Assert.Equal("Mod.Entity1", r.EntityQualifiedNames[0]);
        Assert.Equal(0, r.VisualOnlySkipped);
    }
}
