using Clevr.Acr.Normalizer;
using Xunit;

namespace Clevr.Acr.Normalizer.Tests;

public class NoDefaultValueTests
{
    // One entity "Customer" with: Flag="false" (flag), Code="" (no), Status=Nieuw (flag),
    // Calc = CalculatedValue (no DefaultValue → no). Entity Name comes AFTER Attributes (as in the export).
    private const string DomainYaml = """
        Entities:
          - $Type: DomainModels$EntityImpl
            Attributes:
              - $Type: DomainModels$Attribute
                Name: Flag
                NewType:
                  $Type: DomainModels$BooleanAttributeType
                Value:
                  $Type: DomainModels$StoredValue
                  DefaultValue: "false"
              - $Type: DomainModels$Attribute
                Name: Code
                NewType:
                  $Type: DomainModels$StringAttributeType
                  Length: 200
                Value:
                  $Type: DomainModels$StoredValue
                  DefaultValue: ""
              - $Type: DomainModels$Attribute
                Name: Status
                Value:
                  $Type: DomainModels$StoredValue
                  DefaultValue: Nieuw
              - $Type: DomainModels$Attribute
                Name: Total
                Value:
                  $Type: DomainModels$CalculatedValue
                  Microflow: App.Calc
            Name: Customer
        """;

    [Fact]
    public void FlagsNonEmptyDefaults_IgnoresEmptyAndCalculated()
    {
        var vs = ProjectSecurityParser.DetectNoDefaultValue(new[] { ("Sales", DomainYaml) });
        // Flag ("false") + Status (Nieuw) → 2; Code ("") and Total (CalculatedValue, no DefaultValue) → not.
        Assert.Equal(2, vs.Count);
        Assert.Equal(new[] { "Flag", "Status" }, vs.Select(v => v.ElementName).OrderBy(x => x));
    }

    [Fact]
    public void Identity_IsMaintenanceMinorEntity()
    {
        var v = ProjectSecurityParser.DetectNoDefaultValue(new[] { ("Sales", DomainYaml) })
            .Single(x => x.ElementName == "Flag");
        Assert.Equal("CLEVR-MAINT-010", v.RuleId);
        Assert.Equal(ViolationKind.Acr, v.Kind);
        Assert.Equal("clevr-acr", v.Source);
        Assert.Equal("NoDefaultValue", v.AcrCode);
        Assert.Equal("Maintainability", v.Category);
        Assert.Equal("Minor", v.Severity);             // mxlint LOW → ACR Minor
        Assert.Equal("Entity", v.DocumentType);
        Assert.Equal("Sales.Customer", v.DocumentQualifiedName);
        Assert.Contains("false", v.Reason);            // the offending default is shown
        Assert.StartsWith("sha1:", v.Fingerprint);
    }

    [Fact]
    public void Empty_YieldsNothing()
    {
        Assert.Empty(ProjectSecurityParser.DetectNoDefaultValue(System.Array.Empty<(string, string)>()));
        Assert.Empty(ProjectSecurityParser.DetectNoDefaultValue(new[] { ("M", "") }));
    }

    [Fact]
    public void ClaimTable_SuppressesCONV002()
        => Assert.Contains("CONV002", ClaimTable.SuppressedMxcli); // mxcli subset (only '0' defaults)
}
