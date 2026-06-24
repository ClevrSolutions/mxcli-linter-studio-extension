using System.ComponentModel.Composition;
using Mendix.StudioPro.ExtensionsAPI.Services;
using Mendix.StudioPro.ExtensionsAPI.UI.DockablePane;
using Mendix.StudioPro.ExtensionsAPI.UI.Services;

namespace Clevr.AcrSpike;

/// <summary>
/// Registers the C#-managed dockable pane. Opened via
/// <see cref="SpikeMenuExtension"/> with IDockingWindowService.OpenPane
/// (the 11.10 API does not have ViewMenuCaption).
/// </summary>
[Export(typeof(DockablePaneExtension))]
public class SpikeDockablePaneExtension : DockablePaneExtension
{
    public const string PaneId = "clevr-acr-spike";

    private readonly ILogService _logService;
    private readonly IExtensionFileService _fileService;
    private readonly IDockingWindowService _dockingWindowService;

    [ImportingConstructor]
    public SpikeDockablePaneExtension(
        ILogService logService,
        IExtensionFileService fileService,
        IDockingWindowService dockingWindowService)
    {
        _logService = logService;
        _fileService = fileService;
        _dockingWindowService = dockingWindowService;
    }

    public override string Id => PaneId;

    public override DockablePaneViewModelBase Open()
    {
        // WebServerBaseUrl + CurrentApp come from the base class (UIExtensionBase).
        // CurrentApp?.Root?.DirectoryPath = the directory of the opened app (fallback project path).
        // () => CurrentApp gives the VM live access to the model (Phase 4: navigate to
        // the found document via IDockingWindowService.TryOpenEditor).
        return new SpikeDockablePaneViewModel(
            WebServerBaseUrl,
            _logService,
            _fileService,
            () => CurrentApp?.Root?.DirectoryPath,
            () => CurrentApp,
            _dockingWindowService)
        {
            Title = "CLEVR ACR",
        };
    }
}
