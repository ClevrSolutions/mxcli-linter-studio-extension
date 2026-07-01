using System.Text;
using System.Text.Json;
using Mendix.StudioPro.ExtensionsAPI.Services;

namespace Clevr.Lint.Extension;

/// <summary>
/// Owns everything behind Settings > Configuration: the mxcli binary location (resolve/browse/
/// set/download) and rule sources (list/save/fetch-from-GitHub/delete-fetched-files). Both read
/// and write lint-scan-settings.json directly — there was never a separate store type for it,
/// so this coordinator wraps the file IO itself instead of delegating to one, matching
/// LinterConfigCoordinator's "thin wrapper" shape. No IWebView, no JsonObject — the dispatcher
/// translates messages into these calls and PostMessages the result.
/// </summary>
public sealed class SettingsCoordinator
{
    private readonly IExtensionFileService _fileService;
    private readonly Func<string?> _getProjectDir;
    private readonly ProjectDirResolver _projectDir;
    private readonly RuleSourcesService _ruleSourcesService;

    public SettingsCoordinator(
        IExtensionFileService fileService,
        Func<string?> getProjectDir,
        ProjectDirResolver projectDir,
        RuleSourcesService ruleSourcesService)
    {
        _fileService = fileService;
        _getProjectDir = getProjectDir;
        _projectDir = projectDir;
        _ruleSourcesService = ruleSourcesService;
    }

    // ---- mxcli location ----------------------------------------------------

    /// <summary>Resolves the current mxcli state (path/version/found) without downloading anything.</summary>
    public MxcliInfo GetMxcliInfo() => MxcliService.Resolve(ReadSettingsJson(), _getProjectDir());

    /// <summary>The currently configured mxcli path, for pre-filling the file picker.</summary>
    public string? CurrentMxcliPath() => LoadSettings().MxcliPath;

    /// <summary>Saves a user-supplied mxcli path (typed or picked via file dialog) and returns the resolved state.</summary>
    public MxcliInfo ApplyMxcliPath(string path)
    {
        var settings = LoadSettings();
        settings.MxcliPath = path;
        WriteSettings(settings);
        return GetMxcliInfo();
    }

    /// <summary>Downloads the latest mxcli release, verifies it, and points settings at it.</summary>
    public Task<MxcliInfo> DownloadMxcliAsync(Action<int> onProgress, CancellationToken ct = default)
        => MxcliService.DownloadLatestAsync(_fileService, onProgress, ct);

    // ---- rule sources --------------------------------------------------------

    public List<RuleSource> GetRuleSources() => LoadSettings().RuleSources;

    public void SaveRuleSources(List<RuleSource> sources)
    {
        var settings = LoadSettings();
        settings.RuleSources = sources;
        WriteSettings(settings);
    }

    public Task<RuleSourceFetchResult> FetchRuleSourceAsync(
        string githubUrl, bool replaceExisting, Action<string> onProgress, CancellationToken ct = default)
        => _ruleSourcesService.FetchRuleSourceAsync(githubUrl, _projectDir.Resolve() ?? "", replaceExisting, onProgress, ct);

    public Task<RuleSourceDeleteResult> DeleteRuleSourceFilesAsync(
        string githubUrl, Action<string> onProgress, CancellationToken ct = default)
        => _ruleSourcesService.DeleteRuleSourceFilesAsync(githubUrl, _projectDir.Resolve() ?? "", onProgress, ct);

    // ---- lint-scan-settings.json IO ------------------------------------------

    private string? ReadSettingsJson()
    {
        var path = _fileService.ResolvePath("lint-scan-settings.json");
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    private LintScanSettings LoadSettings() => LintScanSettings.Load(ReadSettingsJson(), null);

    private void WriteSettings(LintScanSettings settings)
    {
        var path = _fileService.ResolvePath("lint-scan-settings.json");
        File.WriteAllText(path, JsonSerializer.Serialize(settings, SettingsJson.WriteOptions), Encoding.UTF8);
    }
}
