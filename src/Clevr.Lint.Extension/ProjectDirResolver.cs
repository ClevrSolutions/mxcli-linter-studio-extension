using Mendix.StudioPro.ExtensionsAPI.Services;

namespace Clevr.Lint.Extension;

/// <summary>
/// Resolves the project directory in which <c>.clevr-lint/</c> resides — THE SAME directory
/// the scan uses (lint-scan-settings.json → projectPath; .mpr → its containing directory;
/// otherwise the open app). Shared by every coordinator that persists project-scoped state,
/// so exclusions/baselines/config/rule-sources all agree on which project produced them.
/// </summary>
public sealed class ProjectDirResolver
{
    private readonly IExtensionFileService _fileService;
    private readonly Func<string?> _getProjectDir;

    public ProjectDirResolver(IExtensionFileService fileService, Func<string?> getProjectDir)
    {
        _fileService = fileService;
        _getProjectDir = getProjectDir;
    }

    public string? Resolve()
    {
        try
        {
            var settingsPath = _fileService.ResolvePath("lint-scan-settings.json");
            var json = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : null;
            var settings = LintScanSettings.Load(json, _getProjectDir());
            var p = settings.ProjectPath;
            if (string.IsNullOrWhiteSpace(p)) return _getProjectDir();
            if (File.Exists(p) && p.EndsWith(".mpr", StringComparison.OrdinalIgnoreCase))
                return Path.GetDirectoryName(p);
            if (Directory.Exists(p)) return p;
            return _getProjectDir();
        }
        catch
        {
            return _getProjectDir();
        }
    }
}
