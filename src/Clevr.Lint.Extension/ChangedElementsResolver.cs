using System.Text.RegularExpressions;
using Clevr.Lint.Normalizer;
using Mendix.StudioPro.ExtensionsAPI.Services;

namespace Clevr.Lint.Extension;

public enum ChangedScanStatus
{
    Ok,          // diff completed — Microflows/Entities populated
    NoChanges,   // diff ran but nothing changed in scope since HEAD
    NotGit,      // project not under git, or .mpr not committed
    NoMxTool,    // no compatible mx.exe found
    DiffFailed,                // mx diff returned non-zero / produced no output
    VersionUpgradeNotCommitted, // model format upgraded locally but mprcontents/ not yet committed
    Error,                     // unexpected exception
}

public sealed class ChangedScanResult
{
    public required ChangedScanStatus Status;
    public required string Message;
    public IReadOnlyList<string> Microflows { get; init; } = [];
    public IReadOnlyList<string> Entities   { get; init; } = [];
    public int Count => Microflows.Count + Entities.Count;
}

/// <summary>
/// Determines which Mendix elements changed on disk since the last commit (HEAD), within
/// the describe-route scope (microflows + entities, user modules).
///
/// Flow: git baseline (HEAD) → mx diff baseline vs live → JSON parse (MxDiffParser) →
/// microflow GUIDs → qualified names via CATALOG.MICROFLOWS → intersect with user modules.
///
/// Fails explicitly with a typed status per precondition (no git / no mx.exe / no changes /
/// mx diff error) — never silently returns an empty list that could look like "all clean".
///
/// NOTE — Mx11 .mpr = SQLite index + mprcontents/ tree. The baseline must include both.
/// We extract them scoped from git (git archive → tar), not via a bare git show of the .mpr.
/// </summary>
public sealed class ChangedElementsResolver
{
    private readonly string _mxcliPath;
    private readonly string _projectDir;
    private readonly string _mprFileName;
    private readonly ILogService _log;

    private readonly string? _mendixVersion;

    public ChangedElementsResolver(string mxcliPath, string projectDir, string mprFileName, ILogService log, string? mendixVersion = null)
    {
        _mxcliPath = mxcliPath;
        _projectDir = projectDir;
        _mprFileName = mprFileName;
        _log = log;
        _mendixVersion = mendixVersion;
    }

    public ChangedScanResult Resolve()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "clevr-lint-changed", Guid.NewGuid().ToString("N"));
        var cleanupTmp = true;
        try
        {
            // 1) Git repo check
            var rp = Git("rev-parse --is-inside-work-tree");
            if (!rp.Ok || rp.StdOut.Trim() != "true")
                return Fail(ChangedScanStatus.NotGit,
                    "Changed-files scan requires a git project. This project is not under git — use a regular scan instead.");

            // 2) .mpr committed in HEAD? (need a baseline to diff against)
            if (!Git($"cat-file -e \"HEAD:{_mprFileName}\"").Ok)
                return Fail(ChangedScanStatus.NotGit,
                    $"'{_mprFileName}' is not committed in HEAD — no baseline to diff against. Commit first, or use a regular scan.");

            // 3) Quick pre-check: any model files changed? (avoids the expensive baseline + diff)
            var st = Git($"status --porcelain -- \"{_mprFileName}\" mprcontents");
            if (st.Ok && st.StdOut.Trim().Length == 0)
                return Fail(ChangedScanStatus.NoChanges,
                    "No changed elements — nothing in the model has changed since the last commit.");

            // 4) Find a compatible mx.exe
            var (mxExe, loose, mxErr) = FindMxExe();
            if (mxExe is null)
                return Fail(ChangedScanStatus.NoMxTool, mxErr!);
            DebugLog.Write(_projectDir, $"[CLEVR Lint] mx.exe: {mxExe}");

            // 5) Extract HEAD baseline (scoped: .mpr + mprcontents, no javasource/node_modules)
            Directory.CreateDirectory(tmp);
            var tar = Path.Combine(tmp, "base.tar");
            var arch = Git($"archive -o \"{tar}\" HEAD -- \"{_mprFileName}\" mprcontents");
            if (!arch.Ok || !File.Exists(tar))
                return Fail(ChangedScanStatus.Error,
                    $"Could not extract baseline from git (git archive): {Trim(arch.StdErr, arch.Error)}");

            var baseDir = Path.Combine(tmp, "base");
            Directory.CreateDirectory(baseDir);
            var untar = ProcessRunner.Run("tar", $"-xf \"{tar}\" -C \"{baseDir}\"", _projectDir, 120_000);
            var baseMpr = Path.Combine(baseDir, _mprFileName);
            if (!File.Exists(baseMpr))
                return Fail(ChangedScanStatus.Error,
                    $"Baseline extraction failed (tar): {Trim(untar.StdErr, untar.Error)}");
            var baseMprContents = Path.Combine(baseDir, "mprcontents");
            var baseDesignProps = Path.Combine(baseDir, "designproperties");
            var baseStyle = Path.Combine(baseDir, "style");
            DebugLog.Write(_projectDir, $"[CLEVR Lint] baseline: mpr={File.Exists(baseMpr)} mprcontents={Directory.Exists(baseMprContents)} " +
                $"mprcontents-files={( Directory.Exists(baseMprContents) ? Directory.GetFiles(baseMprContents, "*", SearchOption.AllDirectories).Length : 0 )} " +
                $"designproperties={Directory.Exists(baseDesignProps)} style={Directory.Exists(baseStyle)}");

            // 6) mx diff baseline vs live → JSON
            var outJson = Path.Combine(tmp, "diff.json");
            var liveMpr = Path.Combine(_projectDir, _mprFileName);
            var looseFlag = loose ? "--loose-version-check " : "";
            var diffArgs = $"diff {looseFlag}\"{baseMpr}\" \"{liveMpr}\" \"{outJson}\"";
            DebugLog.Write(_projectDir, $"[CLEVR Lint] running: {mxExe} {diffArgs}");
            var diff = ProcessRunner.Run(mxExe, diffArgs, _projectDir, 600_000);
            if (!File.Exists(outJson))
            {
                cleanupTmp = false; // keep tmp so the baseline can be inspected
                DebugLog.Write(_projectDir, $"[CLEVR Lint] mx diff failed (exit {diff.ExitCode}) — keeping tmp: {tmp}");
                DebugLog.Write(_projectDir, $"[CLEVR Lint] mx diff stdout: {diff.StdOut}");
                DebugLog.Write(_projectDir, $"[CLEVR Lint] mx diff stderr: {diff.StdErr}");
                var errText = Trim(diff.StdErr, diff.Error);
                // Detect uncommitted Mendix version upgrade: entity schema changed between commits
                // (exit 129 + "do not have the same properties" = mprcontents/ upgraded locally but not committed)
                if (diff.ExitCode == 129 && errText.Contains("do not have the same properties"))
                    return Fail(ChangedScanStatus.VersionUpgradeNotCommitted,
                        "The Mendix version upgrade has not been committed yet. " +
                        "Studio Pro upgraded the model format (mprcontents/ entity schema), but those changes are not in git. " +
                        "Commit the upgrade first — then changed-files detection will work again.");
                return Fail(ChangedScanStatus.DiffFailed, DiffError(diff.ExitCode, errText));
            }

            // 7) Parse diff JSON → changed microflow GUIDs + entity QNs
            var diffJsonText = File.ReadAllText(outJson);
            DebugLog.Write(_projectDir, $"[CLEVR Lint] mx diff output:{Environment.NewLine}{diffJsonText}");
            var changed = MxDiffParser.Parse(diffJsonText);

            // 8) Microflow GUIDs → qualified names via CATALOG.MICROFLOWS (cheap SELECT, ~0.4s)
            var mfQns = ResolveMicroflowNames(changed.MicroflowUnitIds);

            // 9) Intersect with user modules (same scope as the describe sweep)
            var userModules = UserModules();
            var microflows = mfQns
                .Where(q => userModules.Contains(ModuleOf(q)))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(q => q, StringComparer.Ordinal)
                .ToList();
            var entities = changed.EntityQualifiedNames
                .Where(q => userModules.Contains(ModuleOf(q)))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(q => q, StringComparer.Ordinal)
                .ToList();

            DebugLog.Write(_projectDir, $"[CLEVR Lint] changed-files: {microflows.Count} microflows + {entities.Count} entities " +
                      $"(user-module), {changed.VisualOnlySkipped} visual-only skipped");

            if (microflows.Count + entities.Count == 0)
                return Fail(ChangedScanStatus.NoChanges,
                    changed.VisualOnlySkipped > 0
                        ? $"No scannable changed elements in user modules — {changed.VisualOnlySkipped} change(s) were purely visual. Nothing to scan."
                        : "No changed microflows/entities in user modules since the last commit. Nothing to scan.");

            return new ChangedScanResult
            {
                Status = ChangedScanStatus.Ok,
                Message = $"{microflows.Count + entities.Count} changed element(s) since the last commit",
                Microflows = microflows,
                Entities = entities,
            };
        }
        catch (Exception ex)
        {
            DebugLog.Write(_projectDir, $"[CLEVR Lint] changed-files resolver error: {ex.Message}");
            return Fail(ChangedScanStatus.Error, $"Changed-files scan failed: {ex.Message}");
        }
        finally
        {
            if (cleanupTmp)
                try { if (Directory.Exists(tmp)) Directory.Delete(tmp, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────

    private ProcessRunner.Result Git(string args) =>
        ProcessRunner.Run("git", $"-C \"{_projectDir}\" {args}", _projectDir, 120_000);

    /// <summary>
    /// Resolves microflow unit GUIDs to qualified names via CATALOG.MICROFLOWS.Id.
    /// One cheap SELECT (~0.4s) rather than a full mx dump-mpr (~29s).
    /// </summary>
    private List<string> ResolveMicroflowNames(IReadOnlyList<string> guids)
    {
        var names = new List<string>();
        if (guids.Count == 0) return names;
        var want = new HashSet<string>(guids, StringComparer.OrdinalIgnoreCase);
        try
        {
            var proc = ProcessRunner.Run(_mxcliPath,
                $"-p \"{_mprFileName}\" -c \"SELECT Id, QualifiedName FROM CATALOG.MICROFLOWS\"",
                _projectDir);
            var headerSeen = false;
            foreach (var line in (proc.StdOut ?? "").Split('\n'))
            {
                var t = line.Trim();
                if (!t.StartsWith("|") || t.Trim('|', '-', ' ').Length == 0) continue;
                if (!headerSeen) { headerSeen = true; continue; } // skip column header row
                var cells = t.Trim('|').Split('|').Select(s => s.Trim()).ToArray();
                if (cells.Length >= 2 && cells[0].Length > 0 && want.Contains(cells[0]))
                    names.Add(cells[1]);
            }
        }
        catch (Exception ex)
        {
            DebugLog.Write(_projectDir, $"[CLEVR Lint] microflow GUID→name (catalog) skipped: {ex.Message}");
        }
        return names;
    }

    /// <summary>User modules (CATALOG.MODULES.Source empty) — same scope as the describe sweep.</summary>
    private HashSet<string> UserModules()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            var proc = ProcessRunner.Run(_mxcliPath,
                $"-p \"{_mprFileName}\" -c \"SELECT Name, Source FROM CATALOG.MODULES\"",
                _projectDir);
            var headerSeen = false;
            foreach (var line in (proc.StdOut ?? "").Split('\n'))
            {
                var t = line.Trim();
                if (!t.StartsWith("|") || t.Trim('|', '-', ' ').Length == 0) continue;
                if (!headerSeen) { headerSeen = true; continue; }
                var cells = t.Trim('|').Split('|').Select(s => s.Trim()).ToArray();
                if (cells.Length >= 2 && cells[1].Length == 0 && cells[0].Length > 0)
                    set.Add(cells[0]);
            }
        }
        catch (Exception ex)
        {
            DebugLog.Write(_projectDir, $"[CLEVR Lint] user-modules query (changed-files) skipped: {ex.Message}");
        }
        return set;
    }

    /// <summary>
    /// Finds mx.exe matching the project's Mendix version.
    /// Exact version match → no loose flag; same major → loose-version-check; otherwise nothing.
    /// </summary>
    private (string? Path, bool Loose, string? Error) FindMxExe()
    {
        // Try sibling of mxcli first (they both ship in the Studio Pro modeler folder)
        var mxcliDir = Path.GetDirectoryName(_mxcliPath);
        if (!string.IsNullOrEmpty(mxcliDir))
        {
            var sibling = Path.Combine(mxcliDir, "mx.exe");
            if (File.Exists(sibling)) return (sibling, false, null);
        }

        var version = _mendixVersion ?? ProjectMendixVersion();
        var roots = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Mendix"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Mendix"),
        };

        // Exact version?
        if (version is not null)
            foreach (var root in roots)
            {
                var exact = Path.Combine(root, version, "modeler", "mx.exe");
                if (File.Exists(exact)) return (exact, false, null);
            }

        // Same major (or, if version unknown, the highest available) + loose-version-check
        var major = version?.Split('.')[0];
        var candidates = new List<(string ver, string path)>();
        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;
            foreach (var dir in Directory.GetDirectories(root))
            {
                var name = Path.GetFileName(dir);
                if (major is not null && !name.StartsWith(major + ".", StringComparison.Ordinal)) continue;
                var exe = Path.Combine(dir, "modeler", "mx.exe");
                if (File.Exists(exe)) candidates.Add((name, exe));
            }
        }
        if (candidates.Count > 0)
        {
            var best = candidates.OrderByDescending(c => c.ver, StringComparer.OrdinalIgnoreCase).First();
            return (best.path, true, null);
        }

        var need = version is not null ? $"Studio Pro {version}" : "Mendix Studio Pro (the version of this project)";
        return (null, false,
            $"mx.exe (Mendix toolset, ships with Studio Pro) not found for {need}. " +
            $"Install that version, or use a regular scan. " +
            $"The changed-files scan compares the .mpr with the committed version via mx.exe.");
    }

    /// <summary>
    /// Mendix version of the project by opening it with mxcli and reading the startup banner.
    /// mxcli prints "… (Mendix X.Y.Z)" to stderr whenever it opens an .mpr — any lightweight
    /// query is sufficient to trigger it. Used as fallback when the Studio Pro host version is
    /// not available (e.g. test harness).
    /// </summary>
    private string? ProjectMendixVersion()
    {
        try
        {
            var r = ProcessRunner.Run(_mxcliPath,
                $"-p \"{_mprFileName}\" -c \"SELECT Name FROM CATALOG.MODULES\"",
                _projectDir, 30_000);
            var m = Regex.Match((r.StdOut ?? "") + (r.StdErr ?? ""), @"Mendix\s+(\d+\.\d+\.\d+)");
            return m.Success ? m.Groups[1].Value : null;
        }
        catch { return null; }
    }

    private static string DiffError(int code, string detail) => code switch
    {
        2   => $"mx diff reports conflicts (exit code 2). {detail}",
        4   => $"mx diff: project version not supported by this mx.exe (exit code 4) — install the matching Studio Pro version. {detail}",
        129 => $"mx diff: invocation/argument error (exit code 129). {detail}",
        _   => $"mx diff failed (exit code {code}). {detail}",
    };

    private static string ModuleOf(string qn)
    {
        var dot = qn.IndexOf('.');
        return dot > 0 ? qn[..dot] : qn;
    }

    private static string Trim(string? a, string? b)
    {
        var s = (!string.IsNullOrWhiteSpace(a) ? a : b) ?? "";
        s = s.Replace("\r", " ").Replace("\n", " ").Trim();
        return s.Length > 300 ? s[..300] + "…" : s;
    }

    private static ChangedScanResult Fail(ChangedScanStatus status, string message) =>
        new() { Status = status, Message = message };
}
