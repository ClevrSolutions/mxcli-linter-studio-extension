namespace Clevr.Acr.Normalizer;

/// <summary>
/// Beschrijvende naam per mxlint-Rego-regel (rulenumber → rulename), zodat mxlint-regels
/// — net als mxcli-regels (bv. CONV001 → BooleanNaming) — een leesbare naam naast hun
/// nummer tonen. De namen komen uit de autoritatieve <c># METADATA</c> (`rulename`) van de
/// .rego-regels (zie _reference/mxlint-rules); de lint-results.json zelf bevat geen naam,
/// alleen het .rego-bestandspad. Vandaar een vaste mapping (spec/kompas Fase 1: pure data).
///
/// Onbekende regelnummers → <c>null</c> (de UI toont dan enkel het nummer, zoals voorheen).
/// </summary>
public static class MxlintRuleNames
{
    // 25 regels met metadata (3 helper-.rego's hebben geen rulename). Gegenereerd uit de
    // # METADATA-blokken van _reference/mxlint-rules; quotes zijn weggetrimd.
    public static readonly IReadOnlyDictionary<string, string> ByRuleNumber =
        new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["001_0001"] = "AnonymousDisabled",
        ["001_0002"] = "DemoUsersDisabled",
        ["001_0003"] = "SecurityChecks",
        ["001_0004"] = "StrongPasswordPolicy",
        ["001_0005"] = "MxAdminNotUsed",
        ["001_0007"] = "HashAlgorithm",
        ["001_0008"] = "CheckSecurityOnUserRoles",
        ["002_0001"] = "NumberOfPersistantEntities",
        ["002_0002"] = "NumberOfAttributes",
        ["002_0003"] = "AvioidInheritanceFromAdministrationAccount",
        ["002_0004"] = "AvoidInheritanceFromNonSystem",
        ["002_0005"] = "AvoidSystemEntityAssociation",
        ["002_0006"] = "AvoidTooManyVirtualAttributes",
        ["002_0007"] = "AvoidUsingValidationRules",
        ["002_0008"] = "AvoidDefaultReadWriteAccess",
        ["002_0009"] = "NoDefaultValue",
        ["003_0001"] = "NumberOfModules",
        ["004_0001"] = "InlineStylePropertyUsed",
        ["004_0002"] = "ImagesWithAltText",
        ["005_0001"] = "EmptyStringCheckNotComplete",
        ["005_0002"] = "AvoidCommitInLoop",
        ["005_0003"] = "NumberOfElementsInMicroflow",
        ["005_0004"] = "ComplexMicroflowsWithoutAnnotations",
        ["005_0005"] = "NestedIfStatements",
        ["006_0001"] = "ExposedConstants",
    };

    /// <summary>De beschrijvende naam voor een mxlint-rulenumber, of <c>null</c> indien onbekend.</summary>
    public static string? NameFor(string ruleId)
        => ruleId is not null && ByRuleNumber.TryGetValue(ruleId, out var name) ? name : null;
}
