using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Clevr.Acr.Normalizer;

/// <summary>
/// Pure normalizer voor de mxlint.com (Rego/OPA) engine — naar analogie van
/// <see cref="MxcliNormalizer"/>. Input = de xUnit-stijl JSON van
/// `mxlint-cli lint` (lint-results.json); output = Violation[] met source="mxlint".
/// Geen IO, geen registry — alle mxlint-regels worden generiek (kind=generic).
///
/// Schema (lint-results.json):
///   { "testsuites": [ { "name": "...001_0005_mxadmin_userid.rego",
///       "testcases": [ { "name": "modelsource\\...$ProjectSecurity.yaml",
///           "failure": { "message": "[HIGH, Security, 001_0005] ...", "type": "AssertionError" } } ] } ] }
/// Een testcase ZONDER `failure` = geslaagd (geen violation). De failure-message
/// codeert de metadata: "[SEVERITY, CATEGORY, rulenumber] reden".
/// LET OP: metadata-waarden kunnen CRLF (\r) bevatten → overal Trim().
/// </summary>
public static class MxlintNormalizer
{
    public const string Source = "mxlint";
    public const string Engine = "rego"; // alleen debug; UI toont 'm niet

    // PRECEDENTIE: de onderdrukking is verplaatst naar de gedeelde cross-engine
    // <see cref="ClaimTable"/> (op onderwerp), zodat dezelfde claim BEIDE engines onderdrukt
    // (mxcli-generic + mxlint-Rego). Hier consulteren we alleen de mxlint-kant
    // (ClaimTable.SuppressedMxlint). Dit verving de oude hardcoded 6-rulenumber-denylist —
    // inclusief het laten vallen van de 2 foute entries (001_0004/002_0007) die naar
    // niet-geclaimde ACR-regels verwezen.

    private sealed class Report
    {
        [JsonPropertyName("testsuites")] public List<Suite>? TestSuites { get; set; }
    }
    private sealed class Suite
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("testcases")] public List<Case>? TestCases { get; set; }
    }
    private sealed class Case
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("failure")] public Failure? Failure { get; set; }
    }
    private sealed class Failure
    {
        [JsonPropertyName("message")] public string Message { get; set; } = "";
    }

    // Eén marker "[SEVERITY, CATEGORY, rulenumber]". mxlint kan MEERDERE violations van
    // dezelfde regel op één document BUNDELEN in één message, elk voorafgegaan door zo'n
    // marker: "[m] reden1 [m] reden2 [m] reden3 ...". We splitsen op deze marker zodat elke
    // reden een aparte violation wordt (met eigen severity/categorie/ruleId/fingerprint).
    private static readonly Regex Marker = new(
        @"\[\s*([^,\]]+),\s*([^,\]]+),\s*([^\]]+)\]",
        RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static IReadOnlyList<Violation> Normalize(string lintResultsJson)
    {
        var result = new List<Violation>();
        if (string.IsNullOrWhiteSpace(lintResultsJson)) return result;

        var report = JsonSerializer.Deserialize<Report>(lintResultsJson, JsonOpts);
        if (report?.TestSuites is null) return result;

        foreach (var suite in report.TestSuites)
        {
            if (suite.TestCases is null) continue;
            foreach (var tc in suite.TestCases)
            {
                if (tc.Failure is null) continue; // alleen failures zijn violations

                var (dqn, docType) = ParseDocument(tc.Name);
                const string elementName = "";

                // Eén failure.message kan meerdere violations bundelen → splits ze.
                foreach (var seg in SplitBundled(tc.Failure.Message ?? ""))
                {
                    var ruleId = seg.RuleId.Length > 0 ? seg.RuleId : RuleIdFromSuite(suite.Name);
                    // Cross-engine claim-tabel: onderdruk mxlint-Rego-regels waarvan het onderwerp
                    // door een winnende bron (ACR/mxcli of CLEVR) geclaimd is.
                    if (ClaimTable.SuppressedMxlint.Contains(ruleId)) continue;
                    result.Add(new Violation
                    {
                        RuleId = ruleId,
                        Kind = ViolationKind.Generic,
                        Source = Source,
                        AcrCode = null,
                        Engine = Engine,
                        Category = seg.Category, // EIGEN engine-categorie (vrije tekst)
                        Severity = seg.Severity, // LETTERLIJK de engine-severity (HIGH/MEDIUM/LOW)
                        DocumentType = docType,
                        DocumentQualifiedName = dqn,
                        ElementName = elementName,
                        Reason = seg.Reason,
                        Suggestion = null,
                        Fingerprint = Fingerprint.Compute(ruleId, dqn, elementName),
                    });
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Splitst een (mogelijk gebundelde) failure.message op de marker
    /// "[SEVERITY, CATEGORY, rulenumber]" in afzonderlijke violations. Vorm:
    /// "[m] reden1 [m] reden2 ...". Elke marker + de tekst tot de volgende marker = één
    /// violation. Tekst vóór de eerste marker (zeldzaam) of zonder enige marker wordt een
    /// violation zonder marker-metadata (lege severity/categorie/ruleId → ruleId valt later
    /// terug op de regelnaam). Alle waarden worden getrimd (CRLF \r weg).
    /// </summary>
    private static IEnumerable<(string Severity, string Category, string RuleId, string Reason)> SplitBundled(string message)
    {
        var matches = Marker.Matches(message);

        if (matches.Count == 0)
        {
            var whole = message.Trim();
            if (whole.Length > 0) yield return ("", "", "", whole);
            yield break;
        }

        // Tekst vóór de eerste marker hoort ook bij een violation (meestal leeg).
        var lead = message[..matches[0].Index].Trim();
        if (lead.Length > 0) yield return ("", "", "", lead);

        for (var i = 0; i < matches.Count; i++)
        {
            var m = matches[i];
            var reasonStart = m.Index + m.Length;
            var reasonEnd = i + 1 < matches.Count ? matches[i + 1].Index : message.Length;
            yield return (
                m.Groups[1].Value.Trim(),                 // severity (Trim => CRLF weg)
                m.Groups[2].Value.Trim(),                 // category
                m.Groups[3].Value.Trim(),                 // rulenumber
                message[reasonStart..reasonEnd].Trim());  // reden tussen deze en de volgende marker
        }
    }

    /// <summary>
    /// Bouwt de map rulenumber → beschrijvende naam (zelfde vorm als mxcli's payload.ruleNames),
    /// zodat de render-laag de naam automatisch naast het nummer toont. Per testsuite (= één
    /// .rego/.js-regelbestand): de naam komt uit de autoritatieve <see cref="MxlintRuleNames"/>
    /// (Rego-METADATA), en valt anders terug op de PascalCase van de bestandsnaam-slug — zodat
    /// óók regels die (nog) niet in de vaste map staan (bv. de .js-accessibility-regels
    /// 004_0003 one_h1, 004_0004 headings) tóch een leesbare naam krijgen.
    /// </summary>
    public static IReadOnlyDictionary<string, string> BuildRuleNames(string lintResultsJson)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(lintResultsJson)) return result;

        var report = JsonSerializer.Deserialize<Report>(lintResultsJson, JsonOpts);
        if (report?.TestSuites is null) return result;

        foreach (var suite in report.TestSuites)
        {
            var (ruleId, slug) = ParseSuiteRule(suite.Name);
            if (ruleId.Length == 0 || result.ContainsKey(ruleId)) continue;
            var name = MxlintRuleNames.NameFor(ruleId) ?? PascalCase(slug);
            if (name.Length > 0) result[ruleId] = name;
        }
        return result;
    }

    private static readonly Regex SuiteRule = new(@"^(\d{3}_\d{4})_(.+)$", RegexOptions.Compiled);

    /// <summary>"…/004_0003_one_h1.js" → ("004_0003", "one_h1"). Geen nummer-prefix → ("", "").</summary>
    private static (string ruleId, string slug) ParseSuiteRule(string suiteName)
    {
        var baseName = RuleIdFromSuite(suiteName); // basename zonder .rego/.js
        var m = SuiteRule.Match(baseName);
        return m.Success ? (m.Groups[1].Value, m.Groups[2].Value) : ("", "");
    }

    /// <summary>"no_default_value" → "NoDefaultValue"; "one_h1" → "OneH1".</summary>
    private static string PascalCase(string slug)
    {
        var parts = (slug ?? "").Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var sb = new System.Text.StringBuilder();
        foreach (var p in parts) sb.Append(char.ToUpperInvariant(p[0])).Append(p[1..]);
        return sb.ToString();
    }

    /// <summary>"…/rules/001_project_settings/001_0005_mxadmin_userid.rego" → "001_0005_mxadmin_userid".</summary>
    private static string RuleIdFromSuite(string suiteName)
    {
        var name = (suiteName ?? "").Replace('\\', '/');
        var baseName = name.Contains('/') ? name[(name.LastIndexOf('/') + 1)..] : name;
        if (baseName.EndsWith(".rego", StringComparison.OrdinalIgnoreCase)) baseName = baseName[..^5];
        else if (baseName.EndsWith(".js", StringComparison.OrdinalIgnoreCase)) baseName = baseName[..^3];
        return baseName.Trim();
    }

    /// <summary>
    /// testcase.name (yaml-pad) → (documentQualifiedName, documentType). Best-effort:
    /// "MyModule/Customer.DomainModels$DomainModel.yaml" → ("MyModule.Customer", "DomainModel").
    /// </summary>
    private static (string dqn, string docType) ParseDocument(string caseName)
    {
        var path = (caseName ?? "").Replace('\\', '/').Trim();
        const string ms = "modelsource/";
        if (path.StartsWith(ms, StringComparison.OrdinalIgnoreCase)) path = path[ms.Length..];
        if (path.Length == 0) return ("", "");

        var segments = path.Split('/');
        var module = segments.Length > 1 ? segments[0] : "";
        var file = segments[^1];
        if (file.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)) file = file[..^5];

        var dollar = file.LastIndexOf('$');
        var docType = dollar >= 0 ? file[(dollar + 1)..] : "";

        var dot = file.IndexOf('.');
        string docName = dot >= 0 ? file[..dot] : (dollar >= 0 ? file[..dollar] : file);

        var dqn = module.Length > 0 ? $"{module}.{docName}" : docName;
        return (dqn, docType);
    }
}
