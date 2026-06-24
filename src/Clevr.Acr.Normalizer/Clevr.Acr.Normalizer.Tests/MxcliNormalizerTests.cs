using System.Text.Json;
using Clevr.Acr.Normalizer;
using Xunit;

namespace Clevr.Acr.Normalizer.Tests;

public class MxcliNormalizerTests
{
    private readonly MxcliNormalizer _normalizer = new();

    // Registry with REAL verified ACR rules (engineRuleKey = own .star rule-id,
    // metadata from the ACR ground truth; see rules.sample.json). A ruleId not present
    // here → generic. ACR_ENT_ATTRS is set to Minor (ground truth, not Major).
    // The Security rule has severity "TODO-confirm" (0 violations on TRB, no baseline).
    private static RuleRegistry KnownAcrRegistry() => new(new[]
    {
        new AcrRuleEntry { RuleId = "CLEVR-MAINT-001", AcrCode = "EntityAmountAttributes",
            Engine = "star", EngineRuleKey = "ACR_ENT_ATTRS",
            Category = "Maintainability", Severity = "Minor", Status = RuleStatus.Verified },
        new AcrRuleEntry { RuleId = "CLEVR-MAINT-002", AcrCode = "EntityAmountAccessRules",
            Engine = "star", EngineRuleKey = "ACR_ENT_ACCESS",
            Category = "Maintainability", Severity = "Major", Status = RuleStatus.Verified },
        new AcrRuleEntry { RuleId = "CLEVR-HYG-001", AcrCode = "DuplicateEntityNames",
            Engine = "star", EngineRuleKey = "ACR_UNIQ_ENT",
            Category = "Project hygiene", Severity = "Minor", Status = RuleStatus.Verified },
        new AcrRuleEntry { RuleId = "CLEVR-SEC-004", AcrCode = "ProjectSecurityNoAnonymousUsers",
            Engine = "star", EngineRuleKey = "ACR_SEC_GUEST",
            Category = "Security", Severity = "TODO-confirm", Status = RuleStatus.Verified },
    });

    [Fact]
    public void AcrMatch_TakesMetadataFromRegistry_NotFromMxcli()
    {
        var raw = new MxcliViolation
        {
            RuleId = "ACR_ENT_ATTRS",          // real ACR .star rule-id
            Severity = "error",                 // engine-severity — must NOT become the ACR-severity
            Message = "Entity has too many attributes.",
            Module = "Sales",
            Document = "Customer",
            DocumentType = "entity",
            DocumentId = "11111111-1111-1111-1111-111111111111",
            Suggestion = "Split related attributes into a separate entity.",
        };

        var result = _normalizer.Normalize(new[] { raw }, KnownAcrRegistry());

        var v = Assert.Single(result);
        Assert.Equal(ViolationKind.Acr, v.Kind);
        Assert.Equal("clevr-acr", v.Source);
        Assert.Equal("CLEVR-MAINT-001", v.RuleId);          // CLEVR-id, not the engine-key
        Assert.Equal("EntityAmountAttributes", v.AcrCode);
        Assert.Equal("Maintainability", v.Category);        // from registry
        Assert.Equal("Minor", v.Severity);                  // ground truth: Minor, not Major...
        Assert.NotEqual("error", v.Severity);               // ...and NOT the mxcli-severity
        Assert.Equal("Sales.Customer", v.DocumentQualifiedName);
        Assert.Equal("Entity", v.DocumentType);             // canonicalized from "entity"
        Assert.Equal("Entity has too many attributes.", v.Reason);
        Assert.Equal("Split related attributes into a separate entity.", v.Suggestion);
        Assert.Equal("11111111-1111-1111-1111-111111111111", v.DocumentId);
        Assert.Equal("star", v.Engine);
    }

    [Fact]
    public void ClaimTable_SuppressesClaimedMxcliGenerics_KeepsWinnerAndUnrelated()
    {
        var raws = new[]
        {
            new MxcliViolation { RuleId = "QUAL003",   Severity = "warning", Message = "mf >25 activities", Module = "M", Document = "MF1", DocumentType = "microflow" },
            new MxcliViolation { RuleId = "CONV009",   Severity = "info",    Message = "mf >15 activities", Module = "M", Document = "MF1", DocumentType = "microflow" },
            new MxcliViolation { RuleId = "DESIGN001", Severity = "warning", Message = ">10 attributes",    Module = "M", Document = "E1",  DocumentType = "entity" },
            new MxcliViolation { RuleId = "ACR_ENT_ATTRS", Severity = "warning", Message = ">25 attributes", Module = "M", Document = "E1", DocumentType = "entity" }, // winner (claimed)
            new MxcliViolation { RuleId = "MPR001",    Severity = "warning", Message = "naming",            Module = "M", Document = "E2",  DocumentType = "entity" },  // unrelated generic
        };

        var result = _normalizer.Normalize(raws, KnownAcrRegistry());

        Assert.DoesNotContain(result, v => v.RuleId == "QUAL003");      // claimed by CLEVR-MAINT-007 → suppressed
        Assert.DoesNotContain(result, v => v.RuleId == "CONV009");      // claimed by CLEVR-MAINT-007 → suppressed
        Assert.DoesNotContain(result, v => v.RuleId == "DESIGN001");    // claimed by ACR_ENT_ATTRS → suppressed
        Assert.Contains(result, v => v.RuleId == "CLEVR-MAINT-001");    // winner (ACR_ENT_ATTRS) kept
        Assert.Contains(result, v => v.RuleId == "MPR001");             // unrelated generic kept
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void UnknownRule_BecomesGeneric_WithEngineCategoryAndMxcliSeverity()
    {
        var raw = new MxcliViolation
        {
            RuleId = "MPR001", // bundled mxcli rule, not in registry
            Severity = "warning",
            Message = "Microflow does not follow the naming convention.",
            Module = "Sales",
            Document = "ACT_DoThing",
            DocumentType = "microflow",
            DocumentId = "22222222-2222-2222-2222-222222222222",
        };

        var result = _normalizer.Normalize(new[] { raw }, KnownAcrRegistry());

        var v = Assert.Single(result);
        Assert.Equal(ViolationKind.Generic, v.Kind);
        Assert.Equal("mxcli", v.Source);
        Assert.Equal("MPR001", v.RuleId);          // engine rule-id preserved
        Assert.Null(v.AcrCode);                    // generic has no acrCode
        Assert.Equal("MPR", v.Category);           // category from ruleId prefix
        Assert.Equal("warning", v.Severity);       // mxcli-severity preserved
        Assert.Equal("Sales.ACT_DoThing", v.DocumentQualifiedName);
        Assert.Equal("Microflow", v.DocumentType); // canonicalized from "microflow"
        Assert.Equal("star", v.Engine);
    }

    [Fact]
    public void RealAcrId_MapsToAcr_AndBundledId_MapsToGeneric()
    {
        // The core case: a REAL ACR .star rule-id becomes ACR; a bundled mxcli-id
        // (MPR001) — which we deliberately do NOT claim — remains generic. One output per
        // raw violation, so no duplicates.
        var raw = new[]
        {
            new MxcliViolation { RuleId = "ACR_ENT_ATTRS", Severity = "error",
                Message = "Too many attributes.", Module = "Sales", Document = "Customer",
                DocumentType = "entity" },
            new MxcliViolation { RuleId = "MPR001", Severity = "warning",
                Message = "Naming.", Module = "Sales", Document = "ACT_DoThing",
                DocumentType = "microflow" },
        };

        var result = _normalizer.Normalize(raw, KnownAcrRegistry());

        Assert.Equal(2, result.Count);

        var acr = Assert.Single(result, v => v.RuleId == "CLEVR-MAINT-001");
        Assert.Equal(ViolationKind.Acr, acr.Kind);
        Assert.Equal("clevr-acr", acr.Source);
        Assert.Equal("EntityAmountAttributes", acr.AcrCode);

        var generic = Assert.Single(result, v => v.RuleId == "MPR001");
        Assert.Equal(ViolationKind.Generic, generic.Kind);
        Assert.Equal("mxcli", generic.Source);
        Assert.Null(generic.AcrCode);

        // MPR001 is never claimed as ACR.
        Assert.DoesNotContain(result, v => v.RuleId == "MPR001" && v.Kind == ViolationKind.Acr);
    }

    [Fact]
    public void MultipleRealAcrRules_MapWithCorrectCategoryAndSeverity()
    {
        // A Maintainability-Major, a Project-hygiene-Minor, and a Security rule.
        var raw = new[]
        {
            new MxcliViolation { RuleId = "ACR_ENT_ACCESS", Severity = "error",
                Message = "Entity access.", Module = "Sales", Document = "Order",
                DocumentType = "entity" },
            new MxcliViolation { RuleId = "ACR_UNIQ_ENT", Severity = "hint",
                Message = "Non-unique entity name.", Module = "Sales", Document = "Account",
                DocumentType = "entity" },
            new MxcliViolation { RuleId = "ACR_SEC_GUEST", Severity = "warning",
                Message = "Guest access enabled.", Module = "Project", Document = "Security",
                DocumentType = "projectsecurity" },
        };

        var result = _normalizer.Normalize(raw, KnownAcrRegistry());

        Assert.All(result, v => Assert.Equal(ViolationKind.Acr, v.Kind));
        Assert.All(result, v => Assert.Equal("clevr-acr", v.Source));

        var maint = Assert.Single(result, v => v.RuleId == "CLEVR-MAINT-002");
        Assert.Equal("Maintainability", maint.Category);
        Assert.Equal("Major", maint.Severity);
        Assert.Equal("EntityAmountAccessRules", maint.AcrCode); // real ACR Java class name

        var hyg = Assert.Single(result, v => v.RuleId == "CLEVR-HYG-001");
        Assert.Equal("Project hygiene", hyg.Category);
        Assert.Equal("Minor", hyg.Severity);
        Assert.Equal("DuplicateEntityNames", hyg.AcrCode);

        var sec = Assert.Single(result, v => v.RuleId == "CLEVR-SEC-004");
        Assert.Equal("Security", sec.Category);
        Assert.Equal("TODO-confirm", sec.Severity);  // severity still to be confirmed, not made up
        Assert.Equal("ProjectSecurity", sec.DocumentType); // canonicalized
    }

    [Fact]
    public void QualifiedDocument_IsNotDoublePrefixedWithModule()
    {
        // Some mxcli/ACR rules (ACR_UNIQ_ENT, CONV001) deliver 'document' ALREADY
        // qualified ("Module.Entity"). In that case the module must NOT be prefixed again
        // → otherwise "Accesslog.Accesslog.AccesslogBankenportaal".
        var raw = new[]
        {
            new MxcliViolation { RuleId = "ACR_UNIQ_ENT", Severity = "hint",
                Message = "Non-unique.", Module = "Accesslog",
                Document = "Accesslog.AccesslogBankenportaal", DocumentType = "Entity" },
            // bare document → do prefix (the other mxcli form).
            new MxcliViolation { RuleId = "MPR001", Severity = "warning",
                Message = "Naming.", Module = "GoogleAuthenticatorCustom",
                Document = "Credential", DocumentType = "entity" },
        };

        var result = _normalizer.Normalize(raw, KnownAcrRegistry());

        var qualified = Assert.Single(result, v => v.RuleId == "CLEVR-HYG-001");
        Assert.Equal("Accesslog.AccesslogBankenportaal", qualified.DocumentQualifiedName); // not doubled

        var bare = Assert.Single(result, v => v.RuleId == "MPR001");
        Assert.Equal("GoogleAuthenticatorCustom.Credential", bare.DocumentQualifiedName); // prefixed
    }

    [Fact]
    public void Fingerprint_UsesOutputRuleId_AndSpecFormula()
    {
        var raw = new MxcliViolation
        {
            RuleId = "ACR_ENT_ATTRS",
            Severity = "error", Message = "x", Module = "Sales", Document = "Customer",
            DocumentType = "entity",
        };

        var v = Assert.Single(_normalizer.Normalize(new[] { raw }, KnownAcrRegistry()));

        // sha1(ruleId|documentQualifiedName|elementName) with the OUTPUT-ruleId (CLEVR-id).
        var expected = Fingerprint.Compute("CLEVR-MAINT-001", "Sales.Customer", "");
        Assert.Equal(expected, v.Fingerprint);
        Assert.StartsWith("sha1:", v.Fingerprint);
        Assert.Equal(5 + 40, v.Fingerprint.Length); // "sha1:" + 40 hex chars
    }

    [Theory]
    [InlineData("entity", "Entity")]
    [InlineData("microflow", "Microflow")]
    [InlineData("ENTITY", "Entity")]            // case-insensitive
    [InlineData("projectsecurity", "ProjectSecurity")]
    [InlineData("widget", "Widget")]            // unknown → capitalize first letter
    [InlineData("", "")]
    public void DocumentType_IsCanonicalized(string engineValue, string expected)
    {
        var raw = new MxcliViolation
        {
            RuleId = "MPR001", Severity = "warning", Message = "x",
            Module = "M", Document = "D", DocumentType = engineValue,
        };

        var v = Assert.Single(_normalizer.Normalize(new[] { raw }, KnownAcrRegistry()));
        Assert.Equal(expected, v.DocumentType);
    }

    [Fact]
    public void DeserializesRawMxcliJson_IntoDto()
    {
        // Proves that the raw mxcli schema deserializes correctly into the DTO.
        const string json = """
        [
          { "ruleId": "MPR001", "severity": "warning", "message": "msg",
            "module": "Sales", "document": "ACT_DoThing", "documentType": "microflow",
            "documentId": "33333333-3333-3333-3333-333333333333",
            "suggestion": "do this" }
        ]
        """;

        var dtos = JsonSerializer.Deserialize<List<MxcliViolation>>(json)!;
        var result = _normalizer.Normalize(dtos, KnownAcrRegistry());

        var v = Assert.Single(result);
        Assert.Equal(ViolationKind.Generic, v.Kind);
        Assert.Equal("MPR001", v.RuleId);
        Assert.Equal("do this", v.Suggestion);
        Assert.Equal("33333333-3333-3333-3333-333333333333", v.DocumentId);
    }
}
