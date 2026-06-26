using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Clevr.Lint.Extension;

internal static class GitChangedDocumentsService
{
    private static readonly Regex GuidPattern =
        new(@"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})",
            RegexOptions.Compiled);

    /// <summary>
    /// Returns the set of document GUIDs that appear in changed files according to
    /// <c>git status --porcelain</c>. Works only for Mendix "new project format" (Studio Pro 10+)
    /// where each document is stored as a separate file named after its GUID.
    ///
    /// Returns an empty set (and does NOT throw) when git is unavailable, the project is not a
    /// git repo, or the project uses the old monolithic .mpr format — the caller treats an empty
    /// set as "feature unavailable" and hides the filter toggle.
    /// </summary>
    public static IReadOnlySet<string> GetChangedDocumentIds(string? projectDir)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(projectDir)) return result;
        try
        {
            var r = ProcessRunner.Run("git", "status --porcelain", projectDir, timeoutMs: 8000);
            if (!r.Ok || r.ExitCode != 0) return result;
            foreach (var line in r.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var m = GuidPattern.Match(line);
                if (m.Success) result.Add(m.Groups[1].Value);
            }
        }
        catch { /* git not on PATH, not a repo, etc. — silent degrade */ }
        return result;
    }
}
