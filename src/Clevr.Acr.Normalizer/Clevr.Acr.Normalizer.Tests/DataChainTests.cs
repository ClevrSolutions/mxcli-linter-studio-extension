using Clevr.Acr.Normalizer;
using Xunit;

namespace Clevr.Acr.Normalizer.Tests;

/// <summary>
/// Bewijst de Fase 2A-data-keten in C# ZONDER mxcli of Studio Pro:
///   rules.sample.json → RuleRegistryJson → RuleRegistry
///   sample mxcli-JSON  → MxcliOutputParser → MxcliViolation[]
///                      → MxcliNormalizer (+ registry) → Violation[]
/// </summary>
public class DataChainTests
{
    private static RuleRegistry LoadRealRegistry()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "rules.sample.json");
        return RuleRegistryJson.Parse(File.ReadAllText(path));
    }

    [Fact]
    public void RuleRegistryJson_LoadsRealRulesFile()
    {
        // Laadt zonder fouten (incl. _comment-keys en severity "TODO-confirm") en de
        // gouden-regel-guards in RuleRegistry slaan niet aan op de echte 11 regels.
        var registry = LoadRealRegistry();

        var entry = registry.FindByEngineRuleKey("ACR_ENT_ATTRS");
        Assert.NotNull(entry);
        Assert.Equal("CLEVR-MAINT-001", entry!.RuleId);
        Assert.Equal("EntityAmountAttributes", entry.AcrCode);
        Assert.Equal("Maintainability", entry.Category);
        Assert.Equal("Minor", entry.Severity); // grondwaarheid-correctie
        Assert.Equal(RuleStatus.Verified, entry.Status);

        Assert.Null(registry.FindByEngineRuleKey("MPR001")); // bundled → niet geclaimd
    }

    [Fact]
    public void FullChain_RealMxcliShapedJson_MapsAcrAndGeneric()
    {
        var registry = LoadRealRegistry();

        // Vorm zoals `mxcli lint --format json`: een ACR .star-regel + een bundled regel.
        const string mxcliJson = """
        [
          { "ruleId": "ACR_ENT_ATTRS", "severity": "error",
            "message": "Entity has too many attributes.",
            "module": "Sales", "document": "Customer", "documentType": "entity",
            "documentId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "suggestion": "Split the entity." },
          { "ruleId": "MPR001", "severity": "warning",
            "message": "Naming convention.",
            "module": "Sales", "document": "ACT_DoThing", "documentType": "microflow",
            "documentId": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb" }
        ]
        """;

        var raw = MxcliOutputParser.Parse(mxcliJson);
        Assert.Equal(2, raw.Count);

        var violations = new MxcliNormalizer().Normalize(raw, registry);
        Assert.Equal(2, violations.Count);

        var acr = Assert.Single(violations, v => v.Kind == ViolationKind.Acr);
        Assert.Equal("clevr-acr", acr.Source);
        Assert.Equal("CLEVR-MAINT-001", acr.RuleId);
        Assert.Equal("EntityAmountAttributes", acr.AcrCode);
        Assert.Equal("Maintainability", acr.Category);
        Assert.Equal("Minor", acr.Severity);                 // uit registry, niet "error"
        Assert.Equal("Entity", acr.DocumentType);             // gecanonicaliseerd
        Assert.Equal("Sales.Customer", acr.DocumentQualifiedName);
        Assert.StartsWith("sha1:", acr.Fingerprint);

        var generic = Assert.Single(violations, v => v.Kind == ViolationKind.Generic);
        Assert.Equal("mxcli", generic.Source);
        Assert.Equal("MPR001", generic.RuleId);
        Assert.Equal("MPR", generic.Category);
        Assert.Equal("warning", generic.Severity);            // mxcli-severity behouden
        Assert.Null(generic.AcrCode);
    }

    [Fact]
    public void MxcliOutputParser_StripsStatusLines_BeforeJson()
    {
        // Exacte vorm van `mxcli lint --format json`: statusregels op stdout vóór de JSON.
        const string stdout =
            "Connected to: TRB - Backend.mpr (Mendix 11.10.0)\n" +
            "Loading cached catalog (built 3m ago, fast mode)...\n" +
            "✓ Catalog ready (from cache)\n" +
            "{\n" +
            "  \"violations\": [\n" +
            "    { \"ruleId\": \"ACR_ENT_ATTRS\", \"severity\": \"error\", \"message\": \"Too many attributes.\",\n" +
            "      \"module\": \"Sales\", \"document\": \"Customer\", \"documentType\": \"entity\" },\n" +
            "    { \"ruleId\": \"MPR001\", \"severity\": \"warning\", \"message\": \"Naming.\",\n" +
            "      \"module\": \"Sales\", \"document\": \"ACT_X\", \"documentType\": \"microflow\" }\n" +
            "  ]\n" +
            "}\n";

        var raw = MxcliOutputParser.Parse(stdout);
        Assert.Equal(2, raw.Count); // statusregels weggeknipt, JSON eruit gehaald

        // De hele keten levert nu de violations op (geen leeg/0 meer).
        var violations = new MxcliNormalizer().Normalize(raw, LoadRealRegistry());
        Assert.Equal(2, violations.Count);
        var acr = Assert.Single(violations, v => v.Kind == ViolationKind.Acr);
        Assert.Equal("CLEVR-MAINT-001", acr.RuleId);
        Assert.Equal("Minor", acr.Severity);
        Assert.Contains(violations, v => v.Kind == ViolationKind.Generic && v.RuleId == "MPR001");
    }

    [Fact]
    public void MxcliOutputParser_HandlesObjectWrapper()
    {
        const string wrapped = """
        { "summary": { "count": 1 },
          "violations": [
            { "ruleId": "MPR001", "severity": "warning", "message": "x",
              "module": "M", "document": "D", "documentType": "microflow" }
          ] }
        """;

        var raw = MxcliOutputParser.Parse(wrapped);
        var one = Assert.Single(raw);
        Assert.Equal("MPR001", one.RuleId);
    }

    [Fact]
    public void MxcliOutputParser_EmptyOrBlank_YieldsNoViolations()
    {
        Assert.Empty(MxcliOutputParser.Parse(""));
        Assert.Empty(MxcliOutputParser.Parse("[]"));
    }

    // mxcli geeft exit 1 zowel bij "error-severity findings" (= geslaagd, JSON in stdout) als bij een
    // echte fout (= lege stdout). ContainsJson onderscheidt die zodat de scan niet op exitcode gokt en
    // een echte fout niet stilletjes 0 findings wordt.
    [Fact]
    public void MxcliOutputParser_ContainsJson_DistinguishesSuccessFromRealError()
    {
        // Geslaagde run: statusregels (preamble) + JSON-envelope → true (ook met findings = exit 1).
        var ok = "Connected to: App.mpr (Mendix 11.10.0)\n✓ Catalog ready (from cache)\n{\n  \"violations\": []\n}";
        Assert.True(MxcliOutputParser.ContainsJson(ok));
        Assert.True(MxcliOutputParser.ContainsJson("[]"));

        // Echte fout (connect-fout): LEGE stdout (de 'Error …' staat op stderr) → false → luid falen.
        Assert.False(MxcliOutputParser.ContainsJson(""));
        Assert.False(MxcliOutputParser.ContainsJson("   \n  "));
        Assert.False(MxcliOutputParser.ContainsJson(null));
        // Alleen preamble, geen JSON (afgebroken run) → false.
        Assert.False(MxcliOutputParser.ContainsJson("Connected to: App.mpr\nLoading cached catalog..."));
    }

    [Fact]
    public void RulesCatalogParser_ExtractsNameAndCategory_IgnoresStatusLines()
    {
        // Exacte vorm van `mxcli lint --list-rules`: statusregels + regel + Category-regel.
        const string output =
            "Connected to: TRB - Backend.mpr (Mendix 11.10.0)\n" +
            "Loading cached catalog (built 22.1h ago, fast mode)...\n" +
            "✓ Catalog ready (from cache)\n" +
            "Available lint rules:\n" +
            "\n" +
            "  MPR001 (NamingConvention) - Checks that entities, microflows, pages follow naming\n" +
            "      Category: style, Severity: warning\n" +
            "\n" +
            "  CONV011 (NoCommitInLoop) - Commit actions should not be inside loops (N+1 performance issue)\n" +
            "      Category: performance, Severity: warning\n";

        var map = MxcliRulesCatalogParser.Parse(output);

        Assert.Equal(2, map.Count); // alleen de twee regel-regels, niet de status/Category-regels
        Assert.Equal("NamingConvention", map["MPR001"].Name);
        Assert.Equal("style", map["MPR001"].Category);
        Assert.Equal("NoCommitInLoop", map["CONV011"].Name);
        Assert.Equal("performance", map["CONV011"].Category); // basis voor Performance-mapping
    }

    [Fact]
    public void RulesCatalogParser_Blank_YieldsEmptyMap()
    {
        Assert.Empty(MxcliRulesCatalogParser.Parse(""));
    }
}
