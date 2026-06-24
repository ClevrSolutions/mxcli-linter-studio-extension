namespace Clevr.Acr.Normalizer;

/// <summary>
/// CLEVR-EIGEN regel op de CONSTANT-export (mxlint 006_0001 ExposedConstants geïnternaliseerd).
/// Bron: per-constant-YAML <c>*.Constants$Constant.yaml</c> (plat: <c>$Type</c>, <c>ExposedToClient</c>,
/// <c>Name</c> op kolom 0). De YamlDotNet-reader zit in de spike (zoals de expressie-route); deze
/// klasse is PUUR — (Module, Name, Exposed)-tripels in, Violation[] uit.
///
/// De .rego heeft TWEE branches: (1) ELKE exposed constant = MEDIUM "is exposed", en (2) exposed
/// ÉN gevoelige naam = HIGH "...and seems to contain sensitive data". We internaliseren BEWUST
/// alleen branch (2): de blanket-MEDIUM-branch flagt élke exposed constant (ruis); CLEVR's regel
/// richt zich op het echte risico — exposed constants met een gevoelige naam. De gevoelig-naam-
/// detectie is VERBATIM uit de .rego: substring-match (case-insensitief) op deze keyword-lijst.
/// </summary>
public static class ConstantRules
{
    public const string RuleId = "CLEVR-SEC-011";
    public const string AcrCode = "ExposedConstants";
    public const string EngineRuleKey = "CLEVR_SEC_EXPOSED_CONSTANTS";
    public const string Engine = "constant"; // alleen debug
    public const string Category = "Security";  // letterlijk uit de .rego # METADATA
    public const string Severity = "Critical";  // mxlint HIGH (sensitive-branch) → Critical

    // VERBATIM uit de .rego (006_0001) — niets toegevoegd/verzonnen. Substring, niet woord-grens.
    private static readonly string[] SensitiveKeywords =
    {
        "id", "ident",
        "username", "user_name", "user", "usr", "uname",
        "secret", "scrt",
        "password", "pwd", "passwrd",
    };

    /// <summary>Spiegelt de .rego: contains(lower(input.Name), keyword) voor enige keyword.</summary>
    public static bool ContainsSensitiveData(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        var lower = name.ToLowerInvariant();
        foreach (var kw in SensitiveKeywords)
            if (lower.Contains(kw, StringComparison.Ordinal)) return true;
        return false;
    }

    public static IReadOnlyList<Violation> ExposedSensitiveConstants(IEnumerable<(string Module, string Name, bool Exposed)> constants)
    {
        var result = new List<Violation>();
        foreach (var (module, name, exposed) in constants)
        {
            if (!exposed || name.Length == 0 || !ContainsSensitiveData(name)) continue;
            var qn = $"{module}.{name}";
            result.Add(new Violation
            {
                RuleId = RuleId, Kind = ViolationKind.Acr, Source = "clevr-acr", AcrCode = AcrCode,
                Engine = Engine, Category = Category, Severity = Severity, DocumentType = "Constant",
                DocumentQualifiedName = qn, ElementName = "",
                Reason = $"Constant '{qn}' is exposed to the client and its name suggests it holds sensitive data. Exposed constants are readable in the browser.",
                Suggestion = "Set the constant's 'Exposed to client' setting to false.",
                Fingerprint = Fingerprint.Compute(RuleId, qn, ""),
            });
        }
        return result;
    }
}
