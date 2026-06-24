using System.Text.RegularExpressions;
using Clevr.Acr.Normalizer;
using Mendix.StudioPro.ExtensionsAPI.Services;

namespace Clevr.AcrSpike;

/// <summary>
/// mxcli-DESCRIBE-provider (Apache-2.0). Draait de 5 bewezen describe-route-regels live: MAINT-008/009 +
/// REL-001/002 (per microflow) en MAINT-013 (per entiteit), user-module-scope.
///
/// PERF-FIX: NIET meer één mxcli-proces per element (gemeten ~3,4 s/describe = per-proces model-load die
/// telkens herhaalt). In plaats daarvan worden alle DESCRIBE-statements in een paar -c-SESSIES gebatcht
/// (<c>mxcli -c "CONNECT LOCAL '…'; DESCRIBE MICROFLOW A; DESCRIBE MICROFLOW B; …"</c>): het model laadt
/// dan één keer per sessie en elke verdere describe is goedkoop (gemeten ~0,6 s) → ~6× sneller, identieke
/// findings. De gecombineerde output wordt teruggesplitst naar per-element-blokken (op de
/// <c>create or modify …</c>-kopregels) en gevoed aan exact dezelfde pure regels.
///
/// ROBUUSTHEID (zelfde discipline als de exitcode-fix): per chunk wordt gecontroleerd dat élk gevraagd
/// element ook echt in de output zit. Ontbreekt er één, dan een LUIDE warn met de namen (geen stille
/// wegval); een chunk zonder enige output-blok telt als fout.
/// </summary>
public sealed class MxcliDescribeService
{
    // Veilig onder de Windows-commandline-limiet (~32k chars): ~200 × (QN + "DESCRIBE MICROFLOW ; ") ≈ 13k.
    private const int ChunkSize = 200;

    private static readonly Regex MicroflowHeader = new(@"^create or modify microflow (\S+)", RegexOptions.Compiled | RegexOptions.Multiline);
    // Entiteit-kop kent meerdere kwalificeerders: 'persistent', 'non-persistent', 'external', … vóór
    // 'entity'. We matchen elk kwalificeerder-woord (zo niet, dan vielen non-persistent entiteiten stil weg).
    private static readonly Regex EntityHeader = new(@"^create or modify (?:[\w-]+ )*entity (\S+)", RegexOptions.Compiled | RegexOptions.Multiline);

    private readonly string _mxcliPath;
    private readonly string _mprFileName;
    private readonly string _projectDir;
    private readonly ILogService _log;

    public MxcliDescribeService(string mxcliPath, string mprFileName, string projectDir, ILogService log)
    {
        _mxcliPath = mxcliPath;
        _mprFileName = mprFileName;
        _projectDir = projectDir;
        _log = log;
    }

    /// <summary>Voortgang van de streaming-sweep: cumulatief verwerkt / totaal, een UI-label, en
    /// hoeveel elementen deze chunk vroeg vs. teruggaf (returned&lt;requested = LUID, geen stille wegval).</summary>
    public readonly record struct DescribeProgress(int Processed, int Total, string Label, int Requested, int Returned);

    /// <summary>
    /// Niet-streamende variant (ongewijzigd gedrag): verzamelt alle findings in één lijst door de
    /// streaming-kern met de standaard-chunkgrootte (200) te draaien. Gebruikt door de niet-gestreamde
    /// RunScanAsJson-route; de findings zijn identiek aan de gestreamde som (zelfde per-element-regels).
    /// </summary>
    public IReadOnlyList<Violation> GetViolations()
    {
        var all = new List<Violation>();
        StreamViolations(ChunkSize, (batch, _) => all.AddRange(batch));
        return all;
    }

    /// <summary>
    /// STREAMING-kern: beschrijft de user-module-microflows en -entiteiten in chunks van
    /// <paramref name="chunkSize"/> (kleiner = vaker een batch, maar meer model-loads — bewust geaccepteerd
    /// voor responsiviteit), draait per chunk EXACT dezelfde pure per-element-regels, en roept
    /// <paramref name="emit"/> aan met de findings + voortgang van die chunk. De UNIE over alle chunks is
    /// byte-identiek aan de niet-gestreamde sweep (alle regels zijn per-microflow/per-entiteit, geen
    /// kruis-aggregatie). Luid bij gaten (returned&lt;requested), nooit stil minder findings.
    /// </summary>
    public void StreamViolations(int chunkSize, System.Action<IReadOnlyList<Violation>, DescribeProgress> emit)
    {
        try
        {
            var userModules = new HashSet<string>(
                Rows("SELECT Name, Source FROM CATALOG.MODULES")
                    .Where(c => c.Length >= 2 && string.IsNullOrEmpty(c[1])).Select(c => c[0]),
                System.StringComparer.Ordinal);

            var mfs = Rows("SELECT QualifiedName, ModuleName FROM CATALOG.MICROFLOWS")
                .Where(c => c.Length >= 2 && userModules.Contains(c[1])).Select(c => c[0]).ToList();
            var ents = Rows("SELECT QualifiedName, ModuleName FROM CATALOG.ENTITIES")
                .Where(c => c.Length >= 2 && userModules.Contains(c[1])).Select(c => c[0]).ToList();
            var total = mfs.Count + ents.Count;
            var processed = 0;

            // ── microflows in kleine chunks: 4 expressie-/structuur-regels + MAINT-006 per chunk ──
            for (var i = 0; i < mfs.Count; i += chunkSize)
            {
                var chunk = mfs.GetRange(i, System.Math.Min(chunkSize, mfs.Count - i));
                var (blocks, returned) = DescribeChunk("MICROFLOW", chunk, MicroflowHeader);

                var complexity = new List<(string Microflow, int ActionActivityCount, int ExclusiveSplitCount, int AnnotationCount)>();
                var pairs = new List<(string Microflow, string Expression)>();
                var splits = new List<(string Microflow, string Caption, string Expression)>();
                foreach (var mf in chunk)
                {
                    if (!blocks.TryGetValue(mf, out var mdl)) continue; // ontbrekende al luid gelogd in DescribeChunk
                    var (a, s, ann) = DescribeMicroflowExpressions.StructureCounts(mdl);
                    complexity.Add((mf, a, s, ann));
                    pairs.AddRange(DescribeMicroflowExpressions.Extract(mf, mdl));
                    splits.AddRange(DescribeMicroflowExpressions.ExtractSplits(mf, mdl));
                }
                var findings = new List<Violation>();
                findings.AddRange(MicroflowStructureRules.ComplexWithoutAnnotations(complexity)); // CLEVR-MAINT-008
                findings.AddRange(MicroflowStructureRules.NestedIfStatements(splits));            // CLEVR-MAINT-009
                findings.AddRange(ExpressionRules.RedundantEmptyString(pairs));                   // CLEVR-REL-001
                findings.AddRange(ExpressionRules.IncompleteEmptyStringCheck(pairs));             // CLEVR-REL-002
                findings.AddRange(ExpressionRules.RedundantBoolean(pairs));                       // CLEVR-MAINT-006
                processed += chunk.Count;
                emit(findings, new DescribeProgress(processed, total, $"microflows {processed}/{mfs.Count}", chunk.Count, returned));
            }

            // ── entiteiten in kleine chunks: MAINT-013 ──
            for (var i = 0; i < ents.Count; i += chunkSize)
            {
                var chunk = ents.GetRange(i, System.Math.Min(chunkSize, ents.Count - i));
                var (blocks, returned) = DescribeChunk("ENTITY", chunk, EntityHeader);
                var findings = new List<Violation>();
                foreach (var en in chunk)
                    if (blocks.TryGetValue(en, out var mdl))
                        findings.AddRange(DescribeEntityRules.DefaultReadWriteAccess(en, mdl));   // CLEVR-MAINT-013
                processed += chunk.Count;
                emit(findings, new DescribeProgress(processed, total, $"entities {processed - mfs.Count}/{ents.Count}", chunk.Count, returned));
            }

            _log.Info($"[CLEVR ACR] mxcli-describe-provider (streaming): {mfs.Count} microflows + {ents.Count} entiteiten (user-module), chunkSize={chunkSize}");
        }
        catch (System.Exception ex)
        {
            _log.Warn($"[CLEVR ACR] mxcli-describe-provider (streaming) overgeslagen: {ex.Message}");
        }
    }

    /// <summary>
    /// Beschrijft één chunk via één -c-sessie (model laadt één keer), splitst terug naar QN→MDL-blok via
    /// de kopregel-regex, en logt LUID bij gaten (geen blokken / ontbrekende elementen). Geeft het aantal
    /// teruggekregen blokken mee zodat de caller returned&lt;requested kan signaleren.
    /// </summary>
    private (Dictionary<string, string> Blocks, int Returned) DescribeChunk(string kind, List<string> chunk, Regex header)
    {
        var cmd = $"CONNECT LOCAL '{_mprFileName}'; " + string.Join("; ", chunk.Select(q => $"DESCRIBE {kind} {q}"));
        string output;
        try { output = ProcessRunner.Run(_mxcliPath, $"-c \"{cmd}\"", _projectDir).StdOut ?? ""; }
        catch (System.Exception ex)
        {
            _log.Warn($"[CLEVR ACR] describe-batch ({kind}, {chunk.Count} el.) FOUT: {ex.Message}");
            return (new Dictionary<string, string>(System.StringComparer.Ordinal), 0);
        }

        var parsed = SplitBlocks(output, header);
        if (parsed.Count == 0)
            _log.Warn($"[CLEVR ACR] describe-batch ({kind}, {chunk.Count} elementen) leverde GEEN blokken — chunk overgeslagen (mogelijke fout, niet stil 0).");
        var missing = chunk.Where(q => !parsed.ContainsKey(q)).ToList();
        if (missing.Count > 0)
            _log.Warn($"[CLEVR ACR] describe-batch ({kind}): {missing.Count} element(en) niet in de output: {string.Join(", ", missing.Take(10))}{(missing.Count > 10 ? " …" : "")}");
        return (parsed, parsed.Count);
    }

    /// <summary>Splitst gecombineerde describe-output op de <c>create or modify …</c>-kopregels → QN→blok.</summary>
    private static Dictionary<string, string> SplitBlocks(string output, Regex header)
    {
        var blocks = new Dictionary<string, string>(System.StringComparer.Ordinal);
        var matches = header.Matches(output);
        for (var i = 0; i < matches.Count; i++)
        {
            var start = matches[i].Index;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : output.Length;
            var qn = matches[i].Groups[1].Value;
            // bij duplicaat (zou niet moeten): eerste behouden.
            if (!blocks.ContainsKey(qn)) blocks[qn] = output[start..end];
        }
        return blocks;
    }

    // generieke markdown-tabel-parser (zelfde vorm als MxcliCatalogService/LoadPersistentEntityQns).
    private List<string[]> Rows(string sql)
    {
        var proc = ProcessRunner.Run(_mxcliPath, $"-p \"{_mprFileName}\" -c \"{sql}\"", _projectDir);
        var rows = new List<string[]>();
        var headerSeen = false;
        foreach (var line in (proc.StdOut ?? "").Split('\n'))
        {
            var t = line.Trim();
            if (!t.StartsWith("|")) continue;
            if (t.Trim('|', '-', ' ').Length == 0) continue;
            var cells = t.Trim('|').Split('|').Select(s => s.Trim()).ToArray();
            if (!headerSeen) { headerSeen = true; continue; }
            rows.Add(cells);
        }
        return rows;
    }
}
