using System.Text.Json;
using System.Text.Json.Serialization;
using Clevr.Acr.Normalizer;
using Mendix.StudioPro.ExtensionsAPI.Services;

namespace Clevr.AcrSpike;

/// <summary>
/// Sluit de echte mxcli-engine aan op de bestaande, geteste normalizer (Fase 2A).
/// Keten: settings + rules.json laden → mxcli draaien (Process.Start, met de JUISTE
/// working directory) → JSON parsen (MxcliOutputParser) → normaliseren
/// (MxcliNormalizer + RuleRegistry) → JSON voor de webview.
///
/// Bevat ALLEEN IO en bedrading; geen normalisatielogica. Gooit niet — fouten
/// worden als diagnostische payload teruggegeven.
///
/// Aanroep-consistentie met de werkende handmatige run
/// (`mxcli.exe lint -p "TRB - Backend.mpr" --format json` VANUIT de projectmap):
///   - WorkingDirectory = de map die het .mpr bevat (mxcli vindt z'n .mxcli-cache relatief);
///   - -p krijgt de .mpr-BESTANDSNAAM (relatief), niet de projectmap.
/// </summary>
public sealed class AcrScanService
{
    private readonly IExtensionFileService _files;
    private readonly ILogService _log;

    private static readonly JsonSerializerOptions JsonOut = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public AcrScanService(IExtensionFileService files, ILogService log)
    {
        _files = files;
        _log = log;
    }

    // Streaming-chunkgrootte voor de describe-sweep: klein genoeg dat één chunk ~20-30s duurt (één
    // -c-describe-call: ~3,7s model-load + ~0,6s warm/~1,1s koud per element → 30 elementen ≈ 22s warm /
    // 37s koud; empirisch afgesteld). Kleiner = vaker een UI-update, maar méér model-loads (bewust
    // geaccepteerd voor responsiviteit — de wortel-fix (warme modus) bestaat niet in mxcli).
    private const int DescribeStreamChunkSize = 30;

    /// <summary>
    /// Draait de mxcli-scan en levert ÉÉN samengevoegd JSON-eindresultaat (niet-gestreamd). Gebruikt door de
    /// losse RunAcrScan-route. <paramref name="deepScan"/>=false slaat de describe-route over; =true voegt 'm
    /// toe. De gestreamde variant (<see cref="RunScanStreaming"/>) levert dezelfde findings, in batches.
    /// </summary>
    public string RunScanAsJson(string? fallbackProjectDir, bool deepScan = false)
    {
        try
        {
            var (fast, error) = RunFastPhase(fallbackProjectDir, deepScan);
            if (error is not null) return error;

            var violations = fast!.Violations;
            // DEEPSCAN: de describe-route (MAINT-008/009, REL-001/002, MAINT-013 + MAINT-006), user-module-scope.
            if (deepScan)
                violations.AddRange(new MxcliDescribeService(fast.MxcliPath, fast.MprFileName, fast.ProjectDir, _log).GetViolations());

            return SerializeScan(fast, violations, deepScan);
        }
        catch (Exception ex)
        {
            _log.Error("[CLEVR ACR] scan mislukt", ex);
            return Error(ex.Message);
        }
    }

    /// <summary>
    /// GESTREAMDE scan: emit findings in batches via <paramref name="emit"/> (één JSON per batch). Eerst de
    /// FAST-batch (lint + catalog + security + metadata) zodra klaar; daarna — alleen bij deepscan — de
    /// describe-findings PER CHUNK (~20-30s elk) met voortgang. De som van alle batches is byte-identiek aan
    /// <see cref="RunScanAsJson"/>; alleen het WANNEER/IN-HOEVEEL-STUKKEN verschilt, niet WAT.
    /// Elke batch: {phase:"fast"|"describe", final:bool, progress, violations, …}. De FAST-batch draagt de
    /// volledige metadata (ruleNames/categories/appStoreModules/deepScan); describe-batches alleen findings.
    /// </summary>
    public void RunScanStreaming(string? fallbackProjectDir, bool deepScan, Action<string> emit)
    {
        try
        {
            var (fast, error) = RunFastPhase(fallbackProjectDir, deepScan);
            if (error is not null) { emit(error); return; }

            // FAST-batch: volledige metadata + de snelle findings. final=true als dit een snelle scan is
            // (dan is dit de enige/laatste batch); bij deepscan komen er nog describe-batches.
            emit(SerializeBatch(fast!, fast!.Violations, deepScan, phase: "fast",
                final: !deepScan, processed: 0, total: 0, label: null, requested: 0, returned: 0));

            if (!deepScan) return;

            // DEEPSCAN: describe-sweep per chunk → één batch per chunk, met voortgang. De allerlaatste chunk
            // (processed>=total) krijgt final=true. returned<requested wordt meegestuurd → de UI kan een
            // onvolledige chunk LUID tonen (geen stille minder-findings).
            var describe = new MxcliDescribeService(fast!.MxcliPath, fast!.MprFileName, fast!.ProjectDir, _log);
            describe.StreamViolations(DescribeStreamChunkSize, (batch, p) =>
                emit(SerializeBatch(fast!, batch.ToList(), deepScan, phase: "describe",
                    final: p.Processed >= p.Total, processed: p.Processed, total: p.Total,
                    label: p.Label, requested: p.Requested, returned: p.Returned)));
        }
        catch (Exception ex)
        {
            _log.Error("[CLEVR ACR] gestreamde scan mislukt", ex);
            emit(Error(ex.Message));
        }
    }

    /// <summary>Resultaat van de FAST-fase (lint + security + catalog + regel-catalogus + metadata).</summary>
    private sealed class FastPhase
    {
        public required List<Violation> Violations;
        public required IReadOnlyDictionary<string, string> RuleNames;
        public required IReadOnlyDictionary<string, string> RuleCategories;
        public required IReadOnlyList<string> AppStoreModules;
        public required string Command;
        public required string ProjectDir;
        public required string MprFileName;
        public required string MxcliPath;
        public required int ExitCode;
        public required int RawCount;
        public required string? StdErr;
    }

    /// <summary>
    /// Draait de FAST-fase (gedeeld door de gestreamde én niet-gestreamde route): lint + parse + normalize,
    /// security (SEC-008/005 fast; +SEC-010/MAINT-005 bij deep), catalog-route + app-store-modules, en de
    /// regel-catalogus. Geeft (resultaat, null) of (null, foutpayload-JSON) — exact één is niet-null.
    /// </summary>
    private (FastPhase?, string?) RunFastPhase(string? fallbackProjectDir, bool deepScan)
    {
        var settings = LoadSettings(fallbackProjectDir);

        var (projectDir, mprFileName, resolveError) = ResolveProject(settings.ProjectPath);
        if (resolveError is not null)
            return (null, Error(resolveError));

        DebugLog.Write(projectDir, $"=== Scan for improvements === mxcli/regels-projectDir='{projectDir}' | settings.ProjectPath='{settings.ProjectPath}' | _getProjectDir(open app)='{fallbackProjectDir}'");

        var registry = LoadRegistry();

        var arguments = $"lint -p \"{mprFileName}\" --format json";
        var commandLine = $"\"{settings.MxcliPath}\" {arguments}";
        _log.Info($"[CLEVR ACR] {commandLine}  (cwd: {projectDir})");

        var proc = ProcessRunner.Run(settings.MxcliPath, arguments, projectDir);

        if (proc.Error is not null)
            return (null, Diagnostic($"mxcli kon niet starten: {proc.Error}", commandLine, projectDir, proc));

        // mxcli's EXITCODE is GEEN succes/faal-signaal (exit 1 = ≥1 error-finding, óók bij echte fout). We
        // onderscheiden op de STDOUT: JSON-envelope aanwezig → normaal → parsen; geen JSON → LUID falen.
        if (!MxcliOutputParser.ContainsJson(proc.StdOut))
            return (null, Diagnostic($"mxcli leverde geen JSON-resultaat (exitcode {proc.ExitCode}) — waarschijnlijk een echte fout, geen findings", commandLine, projectDir, proc));

        IReadOnlyList<MxcliViolation> raw;
        try { raw = MxcliOutputParser.Parse(proc.StdOut); }
        catch (Exception parseEx) { return (null, Diagnostic($"Kon mxcli-JSON niet parsen: {parseEx.Message}", commandLine, projectDir, proc)); }

        var violations = new MxcliNormalizer().Normalize(raw, registry).ToList();

        // SECURITY-route (Apache-2.0), GEEN mxlint-export. FAST: SEC-008 + SEC-005 (≤1 describe userrole);
        // DEEP: + SEC-010 + MAINT-005 (alle user-rollen). SEC-006 gedeprecateerd (mxcli leest string-lengte
        // niet terug). Draait in beide modi (de deepScan-vlag splitst binnen de service).
        violations.AddRange(new MxcliSecurityService(settings.MxcliPath, mprFileName, projectDir, _log).GetViolations(deepScan));

        // CATALOG-route (Apache-2.0): de 7 robuuste regels uit de SQLite-catalog (MAINT-007/010/014,
        // SEC-007/009/011, PERF-001). Best-effort. + app-store-modulenamen voor het UI-marktplaats-filter.
        var catalogProvider = new MxcliCatalogService(settings.MxcliPath, mprFileName, projectDir, _log);
        violations.AddRange(catalogProvider.GetViolations());
        var appStoreModules = catalogProvider.AppStoreModuleNames();

        // Regel-catalogus (naam + mxcli-categorie per ruleId) — apart van de lint-JSON. Best-effort.
        var catalog = LoadRuleCatalog(settings.MxcliPath, mprFileName, projectDir);
        var ruleNames = catalog.ToDictionary(kv => kv.Key, kv => kv.Value.Name, StringComparer.Ordinal);
        var ruleCategories = catalog.ToDictionary(kv => kv.Key, kv => kv.Value.Category, StringComparer.Ordinal);

        return (new FastPhase
        {
            Violations = violations,
            RuleNames = ruleNames,
            RuleCategories = ruleCategories,
            AppStoreModules = appStoreModules,
            Command = commandLine,
            ProjectDir = projectDir,
            MprFileName = mprFileName,
            MxcliPath = settings.MxcliPath,
            ExitCode = proc.ExitCode,
            RawCount = raw.Count,
            StdErr = proc.StdErr,
        }, null);
    }

    /// <summary>Het volledige (niet-gestreamde) eindresultaat — payload-vorm ongewijzigd t.o.v. voorheen.</summary>
    private string SerializeScan(FastPhase fast, List<Violation> violations, bool deepScan)
    {
        var payload = new
        {
            ok = true,
            command = fast.Command,
            workingDirectory = fast.ProjectDir,
            exitCode = fast.ExitCode,
            rawCount = fast.RawCount,
            violationCount = violations.Count,
            acrCount = violations.Count(v => v.Kind == ViolationKind.Acr),
            genericCount = violations.Count(v => v.Kind == ViolationKind.Generic),
            stderr = fast.StdErr,
            deepScan,
            ruleNames = fast.RuleNames,
            ruleCategories = fast.RuleCategories,
            appStoreModules = fast.AppStoreModules,
            violations,
        };
        _log.Info($"[CLEVR ACR] {fast.RawCount} ruw → {violations.Count} genormaliseerd " +
                  $"({payload.acrCount} acr / {payload.genericCount} generic), exit={fast.ExitCode}");
        return JsonSerializer.Serialize(payload, JsonOut);
    }

    /// <summary>
    /// Eén gestreamde batch. De FAST-batch draagt de volledige metadata + de snelle findings; describe-
    /// batches dragen alleen hun chunk-findings + voortgang. <paramref name="final"/>=true op de laatste
    /// batch → de UI markeert de tellingen als definitief. <paramref name="returned"/>&lt;<paramref name="requested"/>
    /// signaleert een onvolledige chunk (LUID in de UI, geen stille minder-findings).
    /// </summary>
    private string SerializeBatch(FastPhase fast, List<Violation> violations, bool deepScan, string phase,
        bool final, int processed, int total, string? label, int requested, int returned)
    {
        var isFast = phase == "fast";
        var payload = new
        {
            ok = true,
            streaming = true,
            phase,
            final,
            progress = isFast ? null : new { processed, total, label, requested, returned },
            // metadata ALLEEN op de fast-batch (de UI zet 'm dan; describe-batches laten 'm ongemoeid)
            command = isFast ? fast.Command : null,
            workingDirectory = isFast ? fast.ProjectDir : null,
            exitCode = isFast ? fast.ExitCode : (int?)null,
            rawCount = isFast ? fast.RawCount : (int?)null,
            stderr = isFast ? fast.StdErr : null,
            deepScan = isFast ? deepScan : (bool?)null,
            ruleNames = isFast ? fast.RuleNames : null,
            ruleCategories = isFast ? fast.RuleCategories : null,
            appStoreModules = isFast ? fast.AppStoreModules : null,
            violationCount = violations.Count,
            violations,
        };
        return JsonSerializer.Serialize(payload, JsonOut);
    }

    /// <summary>
    /// CLEVR-eigen regel #12 op de project-security-export. Best-effort: leest
    /// modelsource/Security$ProjectSecurity.yaml uit de projectmap (geproduceerd door de
    /// model-export) en draait de pure <see cref="ProjectSecurityParser"/>. Faalt/ontbreekt
    /// het, dan lege lijst (de mxcli-scan zelf blijft staan).
    /// </summary>
    private IReadOnlyList<Violation> DetectProjectSecurityRules(string projectDir)
    {
        try
        {
            var path = Path.Combine(projectDir, "modelsource", "Security$ProjectSecurity.yaml");
            if (!File.Exists(path)) { _log.Info("[CLEVR ACR] project-security-YAML niet gevonden — regel overgeslagen"); return Array.Empty<Violation>(); }
            var v = ProjectSecurityParser.Detect(File.ReadAllText(path));
            _log.Info($"[CLEVR ACR] project-security-regel (CLEVR-MAINT-005): {v.Count} violation(s)");
            return v;
        }
        catch (Exception ex)
        {
            _log.Warn($"[CLEVR ACR] project-security-regel overgeslagen: {ex.Message}");
            return Array.Empty<Violation>();
        }
    }

    /// <summary>
    /// CLEVR-EIGEN regel ACR #7 (CLEVR-SEC-005): persistente entiteiten met create-recht voor de
    /// anonieme rol. Combineert drie bronnen: de security-YAML (anonieme rol-set), de domain-
    /// model-YAML's (access-rules per entiteit) en CATALOG.entities.EntityType (persistable).
    /// Best-effort: faalt een bron, dan lege lijst.
    /// </summary>
    private IReadOnlyList<Violation> DetectAnonymousCreateRules(string projectDir, AcrScanSettings settings, string mprFileName)
    {
        try
        {
            var secPath = Path.Combine(projectDir, "modelsource", "Security$ProjectSecurity.yaml");
            if (!File.Exists(secPath)) { _log.Info("[CLEVR ACR] project-security-YAML niet gevonden — CLEVR-SEC-005 overgeslagen"); return Array.Empty<Violation>(); }
            var securityYaml = File.ReadAllText(secPath);
            var domainModels = LoadDomainModels(projectDir);
            var persistent = LoadPersistentEntityQns(settings.MxcliPath, mprFileName, projectDir);
            var v = ProjectSecurityParser.DetectAnonymousCreateOnPersistent(securityYaml, domainModels, persistent);
            _log.Info($"[CLEVR ACR] anon-create-regel (CLEVR-SEC-005): {v.Count} violation(s); {domainModels.Count} domeinmodellen, {persistent.Count} persistente entiteiten");
            return v;
        }
        catch (Exception ex)
        {
            _log.Warn($"[CLEVR ACR] CLEVR-SEC-005 overgeslagen: {ex.Message}");
            return Array.Empty<Violation>();
        }
    }

    /// <summary>
    /// CLEVR-EIGEN regel ACR #10 (CLEVR-SEC-006): unlimited string-attributen die bewerkbaar zijn
    /// door de anonieme rol. Bronnen: security-YAML + domain-model-YAML's (length uit de YAML,
    /// NIET de CATALOG). Best-effort.
    /// </summary>
    private IReadOnlyList<Violation> DetectAnonymousEditRules(string projectDir)
    {
        try
        {
            var secPath = Path.Combine(projectDir, "modelsource", "Security$ProjectSecurity.yaml");
            if (!File.Exists(secPath)) return Array.Empty<Violation>();
            var securityYaml = File.ReadAllText(secPath);
            var domainModels = LoadDomainModels(projectDir);
            var v = ProjectSecurityParser.DetectAnonymousEditableUnlimitedStrings(securityYaml, domainModels);
            _log.Info($"[CLEVR ACR] anon-edit-regel (CLEVR-SEC-006): {v.Count} violation(s)");
            return v;
        }
        catch (Exception ex)
        {
            _log.Warn($"[CLEVR ACR] CLEVR-SEC-006 overgeslagen: {ex.Message}");
            return Array.Empty<Violation>();
        }
    }

    /// <summary>
    /// Gedeelde expressie-route-orchestratie: één pass over alle microflow-YAML's in modelsource,
    /// haalt de expressie-strings op (split-condities + change-values) via de robuuste YAML-parser,
    /// en draait er ALLE expressie-regels op (REL-001 + MAINT-006). Best-effort; ontbreekt
    /// modelsource, dan leeg. Gekozen boven 471× bson-dump (~16 min) — deze pass duurt seconden.
    /// </summary>
    private IReadOnlyList<Violation> DetectExpressionRules(string projectDir)
    {
        // DIAGNOSTIEK naar mxlint-debug.log (vindbaar), niet ILogService.
        var ms = Path.Combine(projectDir, "modelsource");
        DebugLog.Write(projectDir, $"=== expressie-pass START === projectDir='{projectDir}' modelsource='{ms}' exists={Directory.Exists(ms)}");
        try
        {
            if (!Directory.Exists(ms)) { DebugLog.Write(projectDir, "expressie-pass: modelsource ONTBREEKT → 0"); return Array.Empty<Violation>(); }

            var files = Directory.GetFiles(ms, "*.Microflows$Microflow.yaml", SearchOption.AllDirectories);
            DebugLog.Write(projectDir, $"expressie-pass: {files.Length} microflow-YAML's gevonden");

            var pairs = new List<(string Microflow, string Expression)>();
            var exprKeyed = new List<(string Microflow, string Expression)>(); // "Expression"-keyed waarden (REL-002)
            var sizes = new List<(string Microflow, int TopLevelObjectCount)>();
            var complexity = new List<(string Microflow, int ActionActivityCount, int ExclusiveSplitCount, int AnnotationCount)>();
            var splits = new List<(string Microflow, string Caption, string Expression)>();
            var loops = new List<(string Microflow, IReadOnlyList<(string ActionType, string? Commit)> InLoopActions)>();
            foreach (var f in files)
            {
                var rel = Path.GetRelativePath(ms, f).Replace(Path.DirectorySeparatorChar, '/');
                var mfQn = rel.Split('/')[0] + "." + Path.GetFileName(f).Replace(".Microflows$Microflow.yaml", "");
                // Eén YAML-parse per microflow levert alle structuur-primitieven voor alle flow-AST-regels.
                var info = MicroflowYamlExpressions.Parse(File.ReadAllText(f));
                foreach (var expr in info.Expressions) pairs.Add((mfQn, expr));
                foreach (var ev in info.ExpressionKeyedValues) exprKeyed.Add((mfQn, ev));
                sizes.Add((mfQn, info.TopLevelObjectCount));
                complexity.Add((mfQn, info.ActionActivityCount, info.ExclusiveSplitCount, info.AnnotationCount));
                foreach (var (caption, expr) in info.ExclusiveSplits) splits.Add((mfQn, caption, expr));
                loops.Add((mfQn, info.InLoopActions));
            }
            DebugLog.Write(projectDir, $"expressie-pass: {pairs.Count} expressies + {sizes.Count} microflow-groottes geëxtraheerd (YamlDotNet OK)");

            var result = new List<Violation>();
            // CUTOVER: REL-001/REL-002/MAINT-008/MAINT-009 → GEMIGREERD naar de mxcli-DESCRIBE-route
            // (MxcliDescribeService); YAML-emissies hier uitgeschakeld (pure regels blijven als backup).
            // MAINT-007 → catalog-route (al eerder uit). PERF-COMMIT-IN-LOOP → DEFER naar mxcli CONV011
            // (eigen regel toont het; onze emissie uit). Alleen CLEVR-MAINT-006 (redundante boolean)
            // blijft nog op de YAML-route — niet in deze cutover-scope.
            // result.AddRange(ExpressionRules.RedundantEmptyString(pairs));                // → describe (REL-001)
            result.AddRange(ExpressionRules.RedundantBoolean(pairs));                       // CLEVR-MAINT-006 (blijft YAML)
            // result.AddRange(ExpressionRules.IncompleteEmptyStringCheck(exprKeyed));       // → describe (REL-002)
            // result.AddRange(MicroflowStructureRules.ComplexWithoutAnnotations(complexity)); // → describe (MAINT-008)
            // result.AddRange(MicroflowStructureRules.NestedIfStatements(splits));            // → describe (MAINT-009)
            // result.AddRange(MicroflowStructureRules.CommitInLoop(loops));                   // → defer naar mxcli CONV011
            DebugLog.Write(projectDir, $"expressie-pass KLAAR: {result.Count} violations (alleen CLEVR-MAINT-006; rest → describe/catalog/mxcli)");
            _log.Info($"[CLEVR ACR] expressie-route: {files.Length} microflow-YAML's, {pairs.Count} expressies → {result.Count} violations");
            return result;
        }
        catch (Exception ex)
        {
            // Vangt o.a. een YamlDotNet-assembly-load-fout (DLL niet mee-gedeployed) — VOLLEDIG loggen.
            DebugLog.Write(projectDir, $"expressie-pass FOUT (volledige exception): {ex}");
            _log.Warn($"[CLEVR ACR] expressie-regels overgeslagen: {ex.Message}");
            return Array.Empty<Violation>();
        }
    }

    /// <summary>
    /// CLEVR-EIGEN regel CLEVR-MAINT-010 (mxlint 002_0009 NoDefaultValue): attributen met een gezette,
    /// niet-lege DefaultValue. Domein-model-YAML-route. Best-effort: geen domain-modellen → leeg.
    /// </summary>
    private IReadOnlyList<Violation> DetectNoDefaultValueRules(string projectDir)
    {
        try
        {
            var domainModels = LoadDomainModels(projectDir);
            if (domainModels.Count == 0) return Array.Empty<Violation>();
            var v = ProjectSecurityParser.DetectNoDefaultValue(domainModels);
            _log.Info($"[CLEVR ACR] no-default-value-regel (CLEVR-MAINT-010): {v.Count} violation(s)");
            return v;
        }
        catch (Exception ex)
        {
            _log.Warn($"[CLEVR ACR] CLEVR-MAINT-010 overgeslagen: {ex.Message}");
            return Array.Empty<Violation>();
        }
    }

    /// <summary>
    /// CLEVR-EIGEN domein-model-batch: 002_0001 (CLEVR-MAINT-011), 002_0003 (CLEVR-PERF-001),
    /// 002_0005 (CLEVR-SEC-007), 002_0006 (CLEVR-PERF-002), 002_0007 (CLEVR-MAINT-012),
    /// 002_0008 (CLEVR-MAINT-013). Eén keer domain-modellen laden, dan alle zes. Best-effort.
    /// </summary>
    private IReadOnlyList<Violation> DetectDomainModelBatchRules(string projectDir)
    {
        try
        {
            var dm = LoadDomainModels(projectDir);
            if (dm.Count == 0) return Array.Empty<Violation>();
            var v = new List<Violation>();
            // CUTOVER: de hele domein-model-batch draait niet meer via YAML (pure regels blijven als backup):
            //  - MAINT-011 → DEFER naar mxcli MPR003 (eigen regel toont het; onze emissie uit)
            //  - PERF-002  → DEFER naar mxcli CONV017
            //  - MAINT-012 → DEFER naar mxcli ACR_ENT_VALRULES/CONV015
            //  - MAINT-013 → GEMIGREERD naar de mxcli-DESCRIBE-route (MxcliDescribeService)
            //  - PERF-001/SEC-007 → eerder al naar de mxcli-CATALOG-route
            // v.AddRange(ProjectSecurityParser.DetectNumberOfPersistentEntities(dm)); // MAINT-011 → MPR003
            // v.AddRange(ProjectSecurityParser.DetectTooManyVirtualAttributes(dm));   // PERF-002 → CONV017
            // v.AddRange(ProjectSecurityParser.DetectUsingValidationRules(dm));       // MAINT-012 → ACR_ENT_VALRULES/CONV015
            // v.AddRange(ProjectSecurityParser.DetectDefaultReadWriteAccess(dm));     // MAINT-013 → describe
            _log.Info($"[CLEVR ACR] domein-model-batch: leeg (alles gemigreerd/gedeferd; pure regels = backup): {v.Count} violation(s)");
            return v;
        }
        catch (Exception ex)
        {
            _log.Warn($"[CLEVR ACR] domein-model-batch overgeslagen: {ex.Message}");
            return Array.Empty<Violation>();
        }
    }

    /// <summary>
    /// CLEVR-EIGEN security-/settings-/modules-batch: 001_0005 (CLEVR-SEC-008 MxAdminNotUsed),
    /// 001_0008 (CLEVR-SEC-010 CheckSecurityOnUserRoles) uit Security$ProjectSecurity.yaml;
    /// 001_0007 (CLEVR-SEC-009 HashAlgorithm) uit Settings$ProjectSettings.yaml;
    /// 003_0001 (CLEVR-MAINT-014 NumberOfModules) uit Metadata.yaml. Elke bron apart best-effort.
    /// </summary>
    private IReadOnlyList<Violation> DetectSecurityModulesBatchRules(string projectDir)
    {
        var v = new List<Violation>();
        var ms = Path.Combine(projectDir, "modelsource");

        try
        {
            var secPath = Path.Combine(ms, "Security$ProjectSecurity.yaml");
            if (File.Exists(secPath))
            {
                var yaml = File.ReadAllText(secPath);
                v.AddRange(ProjectSecurityParser.DetectMxAdminUserId(yaml));           // CLEVR-SEC-008
                v.AddRange(ProjectSecurityParser.DetectCheckSecurityOnUserRoles(yaml)); // CLEVR-SEC-010
            }
        }
        catch (Exception ex) { _log.Warn($"[CLEVR ACR] CLEVR-SEC-008/010 overgeslagen: {ex.Message}"); }

        // SEC-009 (hash) + MAINT-014 (module-count) GEMIGREERD naar de mxcli-catalog-route
        // (MxcliCatalogService → CatalogRules). YAML-emissie hier uitgeschakeld; DetectHashAlgorithm /
        // DetectNumberOfModules blijven als backup in ProjectSecurityParser. SEC-008/010 blijven YAML
        // (mxcli legt admin-username + per-userrole-CheckSecurity niet bloot → backup-route).

        _log.Info($"[CLEVR ACR] security-batch (SEC-008/010; SEC-009 + MAINT-014 → catalog): {v.Count} violation(s)");
        return v;
    }

    /// <summary>
    /// CLEVR-EIGEN regel CLEVR-SEC-011 (mxlint 006_0001 ExposedConstants): constants die exposed-to-
    /// client zijn én een gevoelige naam hebben. Constant-YAML-route (ConstantYamlReader, YamlDotNet)
    /// → pure ConstantRules. Best-effort: geen constants → leeg.
    /// </summary>
    private IReadOnlyList<Violation> DetectExposedConstantsRules(string projectDir)
    {
        try
        {
            var constants = ConstantYamlReader.Load(projectDir);
            if (constants.Count == 0) return Array.Empty<Violation>();
            var v = ConstantRules.ExposedSensitiveConstants(constants);
            _log.Info($"[CLEVR ACR] exposed-constants-regel (CLEVR-SEC-011): {v.Count} violation(s) over {constants.Count} constant(s)");
            return v;
        }
        catch (Exception ex)
        {
            _log.Warn($"[CLEVR ACR] CLEVR-SEC-011 overgeslagen: {ex.Message}");
            return Array.Empty<Violation>();
        }
    }

    /// <summary>
    /// CLEVR-EIGEN page-batch: CLEVR-MAINT-015 (mxlint 004_0001 InlineStylePropertyUsed) + CLEVR-REL-003
    /// (mxlint 004_0002 ImagesWithAltText). Eén PageYamlReader-pass (YamlDotNet → plat objectboom-model),
    /// beide pure PageRules erop (hergebruikt dezelfde walk-boom). Best-effort: geen pages → leeg.
    /// </summary>
    private IReadOnlyList<Violation> DetectPageRules(string projectDir)
    {
        try
        {
            var pages = PageYamlReader.Load(projectDir);
            if (pages.Count == 0) return Array.Empty<Violation>();
            var v = new List<Violation>();
            v.AddRange(PageRules.InlineStyleUsed(pages));      // CLEVR-MAINT-015
            v.AddRange(PageRules.ImagesWithoutAltText(pages)); // CLEVR-REL-003
            _log.Info($"[CLEVR ACR] page-batch (MAINT-015 + REL-003): {v.Count} violation(s) over {pages.Count} page(s)/snippet(s)");
            return v;
        }
        catch (Exception ex)
        {
            _log.Warn($"[CLEVR ACR] page-batch overgeslagen: {ex.Message}");
            return Array.Empty<Violation>();
        }
    }

    /// <summary>Leest per module de domain-model-YAML uit modelsource/&lt;Module&gt;/DomainModels$DomainModel.yaml.</summary>
    private static List<(string Module, string Yaml)> LoadDomainModels(string projectDir)
    {
        var result = new List<(string, string)>();
        var ms = Path.Combine(projectDir, "modelsource");
        if (!Directory.Exists(ms)) return result;
        foreach (var dir in Directory.GetDirectories(ms))
        {
            var f = Path.Combine(dir, "DomainModels$DomainModel.yaml");
            if (File.Exists(f)) result.Add((Path.GetFileName(dir), File.ReadAllText(f)));
        }
        return result;
    }

    /// <summary>
    /// De gekwalificeerde namen van alle PERSISTENTE entiteiten, via een MDL CATALOG-query
    /// (betrouwbaarste persistable-bron — lost generalization correct op). Markdown-tabel parsen.
    /// </summary>
    private IReadOnlySet<string> LoadPersistentEntityQns(string mxcliPath, string mprFileName, string projectDir)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            var args = $"-p \"{mprFileName}\" -c \"SELECT QualifiedName FROM CATALOG.entities WHERE EntityType='PERSISTENT'\"";
            var proc = ProcessRunner.Run(mxcliPath, args, projectDir);
            foreach (var line in (proc.StdOut ?? "").Split('\n'))
            {
                var t = line.Trim();
                if (!t.StartsWith("|")) continue;
                var qn = t.Trim('|').Split('|')[0].Trim();
                if (qn.Length == 0 || qn == "QualifiedName" || qn.All(c => c == '-')) continue;
                set.Add(qn);
            }
        }
        catch (Exception ex) { _log.Warn($"[CLEVR ACR] persistente-entiteiten-query mislukt: {ex.Message}"); }
        return set;
    }

    /// <summary>
    /// Lost projectPath op naar (projectmap, .mpr-bestandsnaam). projectPath mag de
    /// projectMAP zijn (er moet dan precies één .mpr in staan) of direct een .mpr-pad.
    /// </summary>
    private static (string projectDir, string mprFileName, string? error) ResolveProject(string projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            return ("", "", "Geen projectpad. Zet 'projectPath' in acr-scan-settings.json of open een app in Studio Pro.");

        if (File.Exists(projectPath) && projectPath.EndsWith(".mpr", StringComparison.OrdinalIgnoreCase))
        {
            var dir = Path.GetDirectoryName(projectPath) ?? "";
            return (dir, Path.GetFileName(projectPath), null);
        }

        if (Directory.Exists(projectPath))
        {
            var mprs = Directory.GetFiles(projectPath, "*.mpr", SearchOption.TopDirectoryOnly);
            if (mprs.Length == 0)
                return ("", "", $"Geen .mpr-bestand gevonden in projectmap: {projectPath}");
            if (mprs.Length > 1)
                return ("", "", $"Meerdere .mpr-bestanden in {projectPath}: " +
                                $"{string.Join(", ", mprs.Select(Path.GetFileName))}. " +
                                "Zet het volledige .mpr-pad in 'projectPath'.");
            return (projectPath, Path.GetFileName(mprs[0]), null);
        }

        return ("", "", $"projectPath bestaat niet: {projectPath}");
    }

    private AcrScanSettings LoadSettings(string? fallbackProjectDir)
    {
        var path = _files.ResolvePath("acr-scan-settings.json");
        var json = File.Exists(path) ? File.ReadAllText(path) : null;
        return AcrScanSettings.Load(json, fallbackProjectDir);
    }

    private RuleRegistry LoadRegistry()
    {
        var path = _files.ResolvePath("rules.json");
        return RuleRegistryJson.Parse(File.ReadAllText(path));
    }

    /// <summary>
    /// Haalt ruleId → (naam, mxcli-categorie) op via `mxcli lint --list-rules` (de lint-JSON
    /// heeft geen van beide). Best-effort: parse de stdout ongeacht de exitcode; faalt het,
    /// geef leeg terug.
    /// </summary>
    private IReadOnlyDictionary<string, MxcliRuleInfo> LoadRuleCatalog(string mxcliPath, string mprFileName, string projectDir)
    {
        try
        {
            var proc = ProcessRunner.Run(mxcliPath, $"lint -p \"{mprFileName}\" --list-rules", projectDir);
            var catalog = MxcliRulesCatalogParser.Parse(proc.StdOut);
            _log.Info($"[CLEVR ACR] {catalog.Count} regels (naam+categorie) uit --list-rules");
            return catalog;
        }
        catch (Exception ex)
        {
            _log.Warn($"[CLEVR ACR] kon regel-catalogus niet laden: {ex.Message}");
            return new Dictionary<string, MxcliRuleInfo>();
        }
    }

    /// <summary>Volledige diagnostische foutpayload: command, cwd, exit, stdout+stderr (1000).</summary>
    private static string Diagnostic(string message, string commandLine, string workingDirectory, ProcessRunner.Result proc)
    {
        var detail =
            $"{message}\n\n" +
            $"command : {commandLine}\n" +
            $"cwd     : {workingDirectory}\n" +
            $"exitCode: {proc.ExitCode}\n\n" +
            $"--- stdout (eerste 1000) ---\n{Truncate(proc.StdOut, 1000)}\n\n" +
            $"--- stderr (eerste 1000) ---\n{Truncate(proc.StdErr, 1000)}";
        return Error(detail);
    }

    private static string Error(string message) => ErrorJson(message);

    /// <summary>Publieke fout-payload (gebruikt door de samengevoegde scan bij een uitzondering).</summary>
    public static string ErrorJson(string message)
        => JsonSerializer.Serialize(new { ok = false, error = message }, JsonOut);

    private static string Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "(leeg)";
        return s.Length <= max ? s : s[..max] + "…";
    }
}
