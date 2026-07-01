namespace Clevr.Lint.Extension;

/// <summary>
/// Owns rule enable/severity overrides and excluded modules — the two knobs Settings > Rules
/// and Settings > Modules edit. Thin wrapper over <see cref="LinterConfigStore"/>: no
/// validation of its own (any dictionary of rule overrides / list of module names is valid),
/// so the seam is here purely to keep project-directory resolution and persistence out of the
/// dispatcher, matching every other coordinator.
/// </summary>
public sealed class LinterConfigCoordinator
{
    private readonly LinterConfigStore _store;
    private readonly ProjectDirResolver _projectDir;

    public LinterConfigCoordinator(LinterConfigStore store, ProjectDirResolver projectDir)
    {
        _store = store;
        _projectDir = projectDir;
    }

    public LinterConfig Load() => _store.Load(_projectDir.Resolve() ?? "");

    public void Save(LinterConfig config) => _store.Save(_projectDir.Resolve() ?? "", config);
}
