using Clevr.Lint.Normalizer;

namespace Clevr.Lint.Extension;

/// <summary>
/// One exclusion request as sent from the web UI, before it is stamped with who/when.
/// Kept free of System.Text.Json types so this coordinator's interface is plain C#, testable
/// without a WebView message or a JsonObject fixture.
/// </summary>
public sealed record ExclusionRequest(
    string Fingerprint,
    string RuleId,
    string DocumentQualifiedName,
    string ElementName);

/// <summary>
/// Owns the exclusion workflow behind one seam: validation (a reason is always mandatory),
/// stamping (who/when), and persistence via <see cref="ExclusionStore"/>. No WebView, no
/// JSON parsing — callers (the message dispatcher, or a test) pass plain values and get the
/// current exclusion list back, or an exception describing what was invalid.
/// </summary>
public sealed class ExclusionCoordinator
{
    private readonly ExclusionStore _store;
    private readonly ProjectDirResolver _projectDir;

    public ExclusionCoordinator(ExclusionStore store, ProjectDirResolver projectDir)
    {
        _store = store;
        _projectDir = projectDir;
    }

    public List<Exclusion> List() => _store.Load(_projectDir.Resolve());

    /// <summary>Excludes one improvement. Throws <see cref="InvalidOperationException"/> if the
    /// reason or fingerprint is missing.</summary>
    public List<Exclusion> Add(ExclusionRequest request, string reason)
    {
        var trimmedReason = reason.Trim();
        if (string.IsNullOrWhiteSpace(trimmedReason))
            throw new InvalidOperationException("A reason is required to exclude an improvement.");
        if (string.IsNullOrWhiteSpace(request.Fingerprint))
            throw new InvalidOperationException("Missing fingerprint for the exclusion.");

        _store.Add(_projectDir.Resolve(), Stamp(request, trimmedReason));
        return List();
    }

    /// <summary>Excludes many improvements at once, all with the same reason (e.g. "Exclude rule").
    /// Throws if the reason is missing or none of the requests carry a fingerprint.</summary>
    public List<Exclusion> AddMany(IEnumerable<ExclusionRequest> requests, string reason)
    {
        var trimmedReason = reason.Trim();
        if (string.IsNullOrWhiteSpace(trimmedReason))
            throw new InvalidOperationException("A reason is required to exclude a rule.");

        var toAdd = requests
            .Where(r => !string.IsNullOrWhiteSpace(r.Fingerprint))
            .Select(r => Stamp(r, trimmedReason))
            .ToList();
        if (toAdd.Count == 0)
            throw new InvalidOperationException("No findings to exclude for this rule.");

        _store.AddMany(_projectDir.Resolve(), toAdd);
        return List();
    }

    public List<Exclusion> Remove(string fingerprint)
    {
        if (string.IsNullOrWhiteSpace(fingerprint))
            throw new InvalidOperationException("Missing fingerprint for the exclusion.");
        _store.Remove(_projectDir.Resolve(), fingerprint);
        return List();
    }

    /// <summary>Restores many exclusions at once (e.g. "Remove rule exclusion"). Throws if
    /// none of the supplied fingerprints are usable.</summary>
    public List<Exclusion> RemoveMany(IEnumerable<string> fingerprints)
    {
        var toRemove = fingerprints.Where(fp => !string.IsNullOrWhiteSpace(fp)).ToList();
        if (toRemove.Count == 0)
            throw new InvalidOperationException("No exclusions to remove for this rule.");
        _store.RemoveMany(_projectDir.Resolve(), toRemove);
        return List();
    }

    private static Exclusion Stamp(ExclusionRequest request, string reason) => new()
    {
        Fingerprint = request.Fingerprint,
        RuleId = request.RuleId,
        DocumentQualifiedName = request.DocumentQualifiedName,
        ElementName = request.ElementName,
        Reason = reason,
        ExcludedBy = Environment.UserName,
        Date = DateTime.Now.ToString("yyyy-MM-dd"),
    };
}
