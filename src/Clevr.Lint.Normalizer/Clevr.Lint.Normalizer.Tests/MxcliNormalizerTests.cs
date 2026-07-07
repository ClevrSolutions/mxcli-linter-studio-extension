using System.Text.Json;
using Clevr.Lint.Normalizer;
using Xunit;

namespace Clevr.Lint.Normalizer.Tests;

public class MxcliNormalizerTests
{
    [Fact]
    public void AcrRuleId_PassesThrough_WithMxcliSeverityAndDerivedCategory()
    {
        var raw = new MxcliViolation
        {
            RuleId = "ACR_ENT_ATTRS",
            Severity = "error",
            Message = "Entity has too many attributes.",
            Module = "Sales",
            Document = "Customer",
            DocumentType = "entity",
            DocumentId = "11111111-1111-1111-1111-111111111111",
            Suggestion = "Split related attributes into a separate entity.",
        };

        var result = MxcliNormalizer.Normalize(new[] { raw });

        var v = Assert.Single(result);
        Assert.Equal(ViolationKind.Mxcli, v.Kind);
        Assert.Equal("ACR_ENT_ATTRS", v.RuleId);
        Assert.Equal("error", v.Severity);
        Assert.Equal("ACR", v.Category);         // derived from rule id prefix
        Assert.Equal("Sales.Customer", v.DocumentQualifiedName);
        Assert.Equal("Entity", v.DocumentType);  // canonicalized from "entity"
        Assert.Equal("Entity has too many attributes.", v.Reason);
        Assert.Equal("Split related attributes into a separate entity.", v.Suggestion);
        Assert.Equal("11111111-1111-1111-1111-111111111111", v.DocumentId);
    }

    [Fact]
    public void Rule_ProducesViolation_WithCategoryFromPrefix()
    {
        var raw = new MxcliViolation
        {
            RuleId = "MPR001",
            Severity = "warning",
            Message = "Microflow does not follow the naming convention.",
            Module = "Sales",
            Document = "ACT_DoThing",
            DocumentType = "microflow",
            DocumentId = "22222222-2222-2222-2222-222222222222",
        };

        var result = MxcliNormalizer.Normalize(new[] { raw });

        var v = Assert.Single(result);
        Assert.Equal(ViolationKind.Mxcli, v.Kind);
        Assert.Equal("MPR001", v.RuleId);
        Assert.Equal("MPR", v.Category);
        Assert.Equal("warning", v.Severity);
        Assert.Equal("Sales.ACT_DoThing", v.DocumentQualifiedName);
        Assert.Equal("Microflow", v.DocumentType);
    }

    [Fact]
    public void MultipleRules_EachProduceOneViolation_NoRegistry()
    {
        var raw = new[]
        {
            new MxcliViolation { RuleId = "ACR_ENT_ATTRS", Severity = "error",
                Message = "Too many attributes.", Module = "Sales", Document = "Customer",
                DocumentType = "entity" },
            new MxcliViolation { RuleId = "MPR001", Severity = "warning",
                Message = "Naming.", Module = "Sales", Document = "ACT_DoThing",
                DocumentType = "microflow" },
        };

        var result = MxcliNormalizer.Normalize(raw);

        Assert.Equal(2, result.Count);
        Assert.All(result, v => Assert.Equal(ViolationKind.Mxcli, v.Kind));

        var acr = Assert.Single(result, v => v.RuleId == "ACR_ENT_ATTRS");
        Assert.Equal("ACR", acr.Category);

        var mpr = Assert.Single(result, v => v.RuleId == "MPR001");
        Assert.Equal("MPR", mpr.Category);
    }

    [Fact]
    public void QualifiedDocument_IsNotDoublePrefixedWithModule()
    {
        // Some mxcli rules (e.g. ACR_UNIQ_ENT, CONV001) deliver 'document' ALREADY
        // qualified ("Module.Entity"). In that case the module must NOT be prefixed again.
        var raw = new[]
        {
            new MxcliViolation { RuleId = "ACR_UNIQ_ENT", Severity = "hint",
                Message = "Non-unique.", Module = "Accesslog",
                Document = "Accesslog.AccesslogBankenportaal", DocumentType = "Entity" },
            new MxcliViolation { RuleId = "MPR001", Severity = "warning",
                Message = "Naming.", Module = "GoogleAuthenticatorCustom",
                Document = "Credential", DocumentType = "entity" },
        };

        var result = MxcliNormalizer.Normalize(raw);

        var qualified = Assert.Single(result, v => v.RuleId == "ACR_UNIQ_ENT");
        Assert.Equal("Accesslog.AccesslogBankenportaal", qualified.DocumentQualifiedName);

        var bare = Assert.Single(result, v => v.RuleId == "MPR001");
        Assert.Equal("GoogleAuthenticatorCustom.Credential", bare.DocumentQualifiedName);
    }

    [Fact]
    public void Fingerprint_UsesNativeRuleId_AndSpecFormula()
    {
        var raw = new MxcliViolation
        {
            RuleId = "ACR_ENT_ATTRS",
            Severity = "error", Message = "x", Module = "Sales", Document = "Customer",
            DocumentType = "entity",
        };

        var v = Assert.Single(MxcliNormalizer.Normalize(new[] { raw }));

        var expected = Fingerprint.Compute("ACR_ENT_ATTRS", "Sales.Customer", "");
        Assert.Equal(expected, v.Fingerprint);
        Assert.StartsWith("sha1:", v.Fingerprint);
        Assert.Equal(5 + 40, v.Fingerprint.Length);
    }

    [Theory]
    [InlineData("entity", "Entity")]
    [InlineData("microflow", "Microflow")]
    [InlineData("ENTITY", "Entity")]
    [InlineData("projectsecurity", "ProjectSecurity")]
    [InlineData("widget", "Widget")]
    [InlineData("", "")]
    public void DocumentType_IsCanonicalized(string engineValue, string expected)
    {
        var raw = new MxcliViolation
        {
            RuleId = "MPR001", Severity = "warning", Message = "x",
            Module = "M", Document = "D", DocumentType = engineValue,
        };

        var v = Assert.Single(MxcliNormalizer.Normalize(new[] { raw }));
        Assert.Equal(expected, v.DocumentType);
    }

    [Fact]
    public void DeserializesRawMxcliJson_IntoDto()
    {
        const string json = """
        [
          { "ruleId": "MPR001", "severity": "warning", "message": "msg",
            "module": "Sales", "document": "ACT_DoThing", "documentType": "microflow",
            "documentId": "33333333-3333-3333-3333-333333333333",
            "suggestion": "do this" }
        ]
        """;

        var dtos = JsonSerializer.Deserialize<List<MxcliViolation>>(json)!;
        var result = MxcliNormalizer.Normalize(dtos);

        var v = Assert.Single(result);
        Assert.Equal(ViolationKind.Mxcli, v.Kind);
        Assert.Equal("MPR001", v.RuleId);
        Assert.Equal("do this", v.Suggestion);
        Assert.Equal("33333333-3333-3333-3333-333333333333", v.DocumentId);
    }

    [Fact]
    public void ExplicitJsonNulls_DoNotThrow()
    {
        // System.Text.Json assigns an explicit JSON `null` straight to the property,
        // bypassing the `= ""` initializer — MxcliViolation's setters coalesce it back
        // to "" so Normalize (DeriveCategory reads ruleId.Length) cannot NRE.
        const string json = """[{"ruleId": null, "severity": "warning", "message": "x"}]""";

        var dtos = JsonSerializer.Deserialize<List<MxcliViolation>>(json)!;
        var result = MxcliNormalizer.Normalize(dtos);

        // Empty ruleId after coalescing → skipped (no catalog match, no meaningful
        // category or fingerprint possible).
        Assert.Empty(result);
    }

    [Fact]
    public void ExplicitJsonNullSeverityAndMessage_AreCoalesced_ViolationKept()
    {
        const string json = """[{"ruleId": "MPR001", "severity": null, "message": null}]""";

        var dtos = JsonSerializer.Deserialize<List<MxcliViolation>>(json)!;
        var result = MxcliNormalizer.Normalize(dtos);

        var v = Assert.Single(result);
        Assert.Equal("MPR001", v.RuleId);
        Assert.Equal("", v.Severity);
        Assert.Equal("", v.Reason);
    }

    [Fact]
    public void CaseMismatchedModulePrefix_IsNotDoublePrefixed_DocumentCasingWins()
    {
        // Mendix module names are case-insensitively unique: module "sales" and the
        // "Sales." prefix in 'document' refer to the same module. A case-sensitive guard
        // would produce the corrupted QN "sales.Sales.Customer", changing the fingerprint
        // and silently breaking existing exclusions.
        var raw = new MxcliViolation
        {
            RuleId = "MPR001", Severity = "warning", Message = "x",
            Module = "sales", Document = "Sales.Customer", DocumentType = "entity",
        };

        var v = Assert.Single(MxcliNormalizer.Normalize(new[] { raw }));

        // 'document' is returned unchanged — its casing is authoritative.
        Assert.Equal("Sales.Customer", v.DocumentQualifiedName);
    }
}
