// Canned data for TestHarness `--mock` mode — lets the UI dev loop (npm run dev) work with
// zero mxcli install and no real Mendix project on disk. Shapes below mirror the real payloads
// produced by LintScanService.SerializeBatch, ScanCoordinator's gitPayload, and the
// RequestRulesCatalog handler exactly, so the UI can't tell the difference.

using System.Text.Json;
using System.Text.Json.Serialization;
using Clevr.Lint.Normalizer;

internal static class MockFixtures
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static readonly Violation[] Violations =
    [
        new Violation
        {
            RuleId = "SEC-002",
            Kind = ViolationKind.Mxcli,
            Category = "Security",
            Severity = "error",
            DocumentType = "Microflow",
            DocumentQualifiedName = "Administration.ACT_Login",
            ElementName = "",
            Reason = "Microflow is reachable without an entity access check.",
            Suggestion = "Add an entity access check before performing the action.",
            Fingerprint = "mock-sec002-act-login",
            DocumentationUrl = null,
            DocumentId = null,
        },
        new Violation
        {
            RuleId = "MAINT-007",
            Kind = ViolationKind.Mxcli,
            Category = "Maintainability",
            Severity = "warning",
            DocumentType = "Microflow",
            DocumentQualifiedName = "Administration.SUB_ValidateUser",
            ElementName = "DecisionSplit_1",
            Reason = "Decision split has more than 5 outgoing branches.",
            Suggestion = "Consider extracting branches into sub-microflows.",
            Fingerprint = "mock-maint007-validateuser",
            DocumentationUrl = null,
            DocumentId = null,
        },
        new Violation
        {
            RuleId = "MAINT-010",
            Kind = ViolationKind.Mxcli,
            Category = "Maintainability",
            Severity = "warning",
            DocumentType = "Microflow",
            DocumentQualifiedName = "System.SUB_SendEmail",
            ElementName = "",
            Reason = "Microflow has no documentation.",
            Suggestion = "Add a description explaining the microflow's purpose.",
            Fingerprint = "mock-maint010-sendemail",
            DocumentationUrl = null,
            DocumentId = null,
        },
        new Violation
        {
            RuleId = "REL-001",
            Kind = ViolationKind.Mxcli,
            Category = "Reliability",
            Severity = "info",
            DocumentType = "Page",
            DocumentQualifiedName = "Administration.Users_Overview",
            ElementName = "DataGrid_1",
            Reason = "Data grid has no empty-state message configured.",
            Suggestion = "Configure an empty-list message for a clearer end-user experience.",
            Fingerprint = "mock-rel001-users-overview",
            DocumentationUrl = null,
            DocumentId = null,
        },
        new Violation
        {
            RuleId = "PERF-003",
            Kind = ViolationKind.Mxcli,
            Category = "Performance",
            Severity = "hint",
            DocumentType = "Microflow",
            DocumentQualifiedName = "System.SUB_RecalculateTotals",
            ElementName = "Retrieve_1",
            Reason = "Retrieve action loads a full entity list without a range.",
            Suggestion = "Add a range or filter to avoid loading unbounded data.",
            Fingerprint = "mock-perf003-recalctotals",
            DocumentationUrl = null,
            DocumentId = null,
        },
        new Violation
        {
            RuleId = "SEC-002",
            Kind = ViolationKind.Mxcli,
            Category = "Security",
            Severity = "error",
            DocumentType = "Microflow",
            DocumentQualifiedName = "System.ACT_ResetPassword",
            ElementName = "",
            Reason = "Microflow is reachable without an entity access check.",
            Suggestion = "Add an entity access check before performing the action.",
            Fingerprint = "mock-sec002-resetpassword",
            DocumentationUrl = null,
            DocumentId = null,
        },
    ];

    public static readonly IReadOnlyDictionary<string, string> RuleNames = new Dictionary<string, string>
    {
        ["SEC-002"] = "Missing entity access check",
        ["MAINT-007"] = "Decision split too complex",
        ["MAINT-010"] = "Missing microflow documentation",
        ["REL-001"] = "Data grid missing empty-state message",
        ["PERF-003"] = "Unbounded retrieve",
    };

    public static readonly IReadOnlyDictionary<string, string> RuleCategories = new Dictionary<string, string>
    {
        ["SEC-002"] = "Security",
        ["MAINT-007"] = "Maintainability",
        ["MAINT-010"] = "Maintainability",
        ["REL-001"] = "Reliability",
        ["PERF-003"] = "Performance",
    };

    // Empty to match the real backend (LintScanService hardcodes empty); populate both sides
    // together when the marketplace filter ships.
    public static readonly string[] AppStoreModules = [];

    /// <summary>Mirrors LintScanService.SerializeBatch's payload shape exactly.</summary>
    public static string BuildFullScanBatchJson()
    {
        var payload = new
        {
            ok = true,
            streaming = true,
            final = true,
            command = "mxcli lint --format json (mock)",
            workingDirectory = "(mock — no real project)",
            exitCode = 0,
            rawCount = Violations.Length,
            stderr = (string?)null,
            ruleNames = RuleNames,
            ruleCategories = RuleCategories,
            appStoreModules = AppStoreModules,
            violationCount = Violations.Length,
            violations = Violations,
        };
        return JsonSerializer.Serialize(payload, Options);
    }

    /// <summary>Mirrors ScanCoordinator.RunFullScan's gitPayload shape exactly.</summary>
    public static string BuildUncommittedDocumentsJson()
    {
        var payload = new
        {
            status = "Ok",
            available = true,
            qualifiedNames = new[] { "Administration.SUB_ValidateUser", "System.SUB_SendEmail" },
            documentIds = Array.Empty<string>(),
        };
        return JsonSerializer.Serialize(payload, Options);
    }

    /// <summary>Mirrors the RequestRulesCatalog handler's payload shape exactly.</summary>
    public static string BuildRulesCatalogJson()
    {
        var payload = new { ruleNames = RuleNames, ruleCategories = RuleCategories };
        return JsonSerializer.Serialize(payload, Options);
    }
}
