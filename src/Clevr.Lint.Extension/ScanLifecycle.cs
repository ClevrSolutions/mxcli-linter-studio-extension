namespace Clevr.Lint.Extension;

/// <summary>
/// Owns the "cancel-but-don't-dispose plus generation counter" pattern for a scan's
/// CancellationTokenSource, shared by <see cref="DockablePaneViewModel"/> and the
/// TestHarness. Restarting a scan cancels the previous CTS instead of disposing it,
/// since a superseded scan's background thread may still be linked to that same
/// token (disposing while that link is live throws ObjectDisposedException). The
/// generation counter lets a caller tell a superseded scan's (possibly still
/// in-flight) events apart from the current scan's, so a stale "Finished" can't
/// re-enable the UI while a newer scan is still running.
/// </summary>
public sealed class ScanLifecycle
{
    private CancellationTokenSource? _cts;
    private int _generation;

    /// <summary>Cancels any in-flight scan, starts a new generation, and returns its token.</summary>
    public CancellationToken StartNew(out int generation)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        generation = ++_generation;
        return _cts.Token;
    }

    public void Cancel() => _cts?.Cancel();

    public bool IsCurrent(int generation) => generation == _generation;
}
