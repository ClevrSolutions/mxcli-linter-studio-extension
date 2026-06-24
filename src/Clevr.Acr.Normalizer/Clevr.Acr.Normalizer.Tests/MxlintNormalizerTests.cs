using Clevr.Acr.Normalizer;
using Xunit;

namespace Clevr.Acr.Normalizer.Tests;

public class MxlintNormalizerTests
{
    // Vorm van lint-results.json (xUnit-stijl), met een failure (violation), een
    // geslaagde testcase (geen failure → genegeerd), en CRLF in de metadata.
    private const string SampleJson = """
    {
      "testsuites": [
        {
          "name": "C:\\proj\\.mendix-cache\\rules\\001_project_settings\\001_0004_strong_password_policy.rego",
          "testcases": [
            { "name": "modelsource\\Security$ProjectSecurity.yaml",
              "failure": { "message": "[HIGH, Security, 001_0004] Password policy is not strong enough", "type": "AssertionError" } }
          ]
        },
        {
          "name": "C:\\proj\\.mendix-cache\\rules\\002_domain_model\\002_0001_number_of_persistent_entities.rego",
          "testcases": [
            { "name": "modelsource\\Sales\\DomainModels$DomainModel.yaml" }
          ]
        },
        {
          "name": "C:\\proj\\.mendix-cache\\rules\\002_domain_model\\999_9999_no_default_value.rego",
          "testcases": [
            { "name": "modelsource\\Sales\\DomainModels$DomainModel.yaml",
              "failure": { "message": "[LOW, Maintainability\r, 999_9999\r] Account.ExterneGebruiker has a default value set\r", "type": "AssertionError" } }
          ]
        }
      ]
    }
    """;

    [Fact]
    public void Failure_BecomesGenericViolation_WithParsedMetadata()
    {
        var result = MxlintNormalizer.Normalize(SampleJson);

        var v = Assert.Single(result, x => x.RuleId == "001_0004");
        Assert.Equal(ViolationKind.Generic, v.Kind);
        Assert.Equal("mxlint", v.Source);
        Assert.Null(v.AcrCode);
        Assert.Equal("rego", v.Engine);
        Assert.Equal("Security", v.Category);  // letterlijk uit de message
        Assert.Equal("HIGH", v.Severity);      // letterlijk (geen verzonnen ACR-severity)
        Assert.Equal("ProjectSecurity", v.DocumentType);
        Assert.Equal("Password policy is not strong enough", v.Reason);
        Assert.StartsWith("sha1:", v.Fingerprint);
    }

    [Fact]
    public void TrimsCrlfFromMetadata()
    {
        var v = Assert.Single(MxlintNormalizer.Normalize(SampleJson), x => x.RuleId == "999_9999");
        Assert.Equal("Maintainability", v.Category);  // GEEN "Maintainability\r"
        Assert.Equal("LOW", v.Severity);
        Assert.Equal("999_9999", v.RuleId);           // GEEN "999_9999\r"
        Assert.Equal("Account.ExterneGebruiker has a default value set", v.Reason); // GEEN trailing \r
        Assert.Equal("Sales.DomainModels", v.DocumentQualifiedName);
        Assert.Equal("DomainModel", v.DocumentType);
    }

    [Fact]
    public void IgnoresPassingTestcases()
    {
        var result = MxlintNormalizer.Normalize(SampleJson);
        // 3 testsuites, maar slechts 2 failures → 2 violations (de geslaagde case telt niet).
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, v => v.RuleId == "002_0001");
    }

    [Fact]
    public void BundledMessage_SplitsIntoSeparateViolations()
    {
        // mxlint bundelt meerdere 999_9999-violations op één DomainModel in één message,
        // elk voorafgegaan door de marker. CRLF zit in een paar markers/redenen.
        const string bundledJson = """
        {
          "testsuites": [
            {
              "name": "C:\\proj\\.mendix-cache\\rules\\002_domain_model\\999_9999_no_default_value.rego",
              "testcases": [
                { "name": "modelsource\\Sales\\DomainModels$DomainModel.yaml",
                  "failure": { "message": "[LOW, Maintainability, 999_9999] AccesslogBankenportaal.FailedAttempts has a default value set [LOW, Maintainability\r, 999_9999] AccesslogBankenportaal.Success has a default value set [LOW, Maintainability, 999_9999] Melder.Land has a default value set\r", "type": "AssertionError" } }
              ]
            }
          ]
        }
        """;

        var result = MxlintNormalizer.Normalize(bundledJson);

        Assert.Equal(3, result.Count); // 3 losse violations i.p.v. één reuze-reden
        Assert.All(result, v => Assert.Equal("999_9999", v.RuleId));
        Assert.All(result, v => Assert.Equal("Maintainability", v.Category)); // CRLF getrimd
        Assert.All(result, v => Assert.Equal("LOW", v.Severity));
        Assert.All(result, v => Assert.Equal("mxlint", v.Source));

        Assert.Equal("AccesslogBankenportaal.FailedAttempts has a default value set", result[0].Reason);
        Assert.Equal("AccesslogBankenportaal.Success has a default value set", result[1].Reason);
        Assert.Equal("Melder.Land has a default value set", result[2].Reason); // GEEN trailing \r
    }

    [Fact]
    public void OverlapWithAcr_IsSuppressed_OthersPass()
    {
        // 001_0001 (AnonymousDisabled) overlapt ACR_SEC_GUEST → onderdrukt (precedentie ACR > mxlint).
        // 999_9999 (geen overlap) komt normaal mee.
        const string json = """
        {
          "testsuites": [
            { "name": "rules\\001_project_settings\\001_0001_anonymous_disabled.rego",
              "testcases": [ { "name": "modelsource\\Security$ProjectSecurity.yaml",
                "failure": { "message": "[HIGH, Security, 001_0001] Anonymous access enabled" } } ] },
            { "name": "rules\\002_domain_model\\999_9999_no_default_value.rego",
              "testcases": [ { "name": "modelsource\\Sales\\DomainModels$DomainModel.yaml",
                "failure": { "message": "[LOW, Maintainability, 999_9999] Account.X has a default value set" } } ] }
          ]
        }
        """;

        var result = MxlintNormalizer.Normalize(json);

        Assert.DoesNotContain(result, v => v.RuleId == "001_0001"); // overlap → onderdrukt
        Assert.Contains(result, v => v.RuleId == "999_9999");        // geen overlap → blijft
    }

    [Fact]
    public void ClaimTable_Suppresses005_0003_AndUnsuppresses001_0004()
    {
        // 005_0003 is now claimed by CLEVR-MAINT-007 → suppressed on the mxlint side.
        // 001_0004 (StrongPasswordPolicy) is NO LONGER suppressed (we claim no ACR_SEC_PWPOLICY).
        const string json = """
        {
          "testsuites": [
            { "name": "rules\\005_microflows\\005_0003_number_of_elements_in_microflow.rego",
              "testcases": [ { "name": "modelsource\\App\\Big.Microflows$Microflow.yaml",
                "failure": { "message": "[MEDIUM, Maintainability, 005_0003] Microflow Big has 40 actions" } } ] },
            { "name": "rules\\001_project_settings\\001_0004_strong_password.rego",
              "testcases": [ { "name": "modelsource\\Security$ProjectSecurity.yaml",
                "failure": { "message": "[HIGH, Security, 001_0004] Password length is not enough" } } ] }
          ]
        }
        """;

        var result = MxlintNormalizer.Normalize(json);

        Assert.DoesNotContain(result, v => v.RuleId == "005_0003"); // claimed by CLEVR-MAINT-007 → suppressed
        Assert.Contains(result, v => v.RuleId == "001_0004");        // wrong old entry dropped → now shows
    }

    [Fact]
    public void Blank_YieldsNoViolations()
    {
        Assert.Empty(MxlintNormalizer.Normalize(""));
        Assert.Empty(MxlintNormalizer.Normalize("{ \"testsuites\": [] }"));
    }

    [Fact]
    public void RuleNames_MapNumbersToDescriptiveNames_FromMetadata()
    {
        // mxlint-regels krijgen een beschrijvende naam (rulename uit de # METADATA), net als
        // mxcli (CONV001 → BooleanNaming). De lint-output zelf heeft die niet → vaste mapping.
        Assert.Equal("NoDefaultValue", MxlintRuleNames.NameFor("002_0009"));
        Assert.Equal("AnonymousDisabled", MxlintRuleNames.NameFor("001_0001"));
        Assert.Equal("ImagesWithAltText", MxlintRuleNames.NameFor("004_0002"));
        Assert.Null(MxlintRuleNames.NameFor("999_9999")); // onbekend → null (UI toont enkel nummer)
    }

    [Fact]
    public void BuildRuleNames_UsesMetadataMap_AndPascalCaseSlugFallback()
    {
        // 999_9999 → vaste map (Rego-METADATA). 004_0003 (een .js-accessibility-regel die NIET
        // in de map staat) → PascalCase van de bestandsnaam-slug, zodat 'ie tóch een naam krijgt.
        const string json = """
        {
          "testsuites": [
            { "name": "C:\\proj\\.mendix-cache\\rules\\002_domain_model\\002_0009_no_default_value.rego",
              "testcases": [ { "name": "modelsource\\Sales\\DomainModels$DomainModel.yaml",
                "failure": { "message": "[LOW, Maintainability, 002_0009] Account.X has a default value set" } } ] },
            { "name": "C:\\proj\\.mendix-cache\\rules\\004_pages\\004_0003_one_h1.js",
              "testcases": [ { "name": "modelsource\\App\\Home$Page.yaml",
                "failure": { "message": "[LOW, Accessibility, 004_0003] More than one H1 on the page" } } ] }
          ]
        }
        """;

        var names = MxlintNormalizer.BuildRuleNames(json);
        Assert.Equal("NoDefaultValue", names["002_0009"]); // uit de vaste metadata-map
        Assert.Equal("OneH1", names["004_0003"]);          // slug-fallback (.js, niet in de map)
    }

    [Fact]
    public void FingerprintUsesRuleIdAndDocument()
    {
        var v = Assert.Single(MxlintNormalizer.Normalize(SampleJson), x => x.RuleId == "001_0004");
        Assert.Equal("Security", v.DocumentQualifiedName); // namespace vóór '$' (project-level doc)
        Assert.Equal(Fingerprint.Compute("001_0004", "Security", ""), v.Fingerprint);
    }
}
