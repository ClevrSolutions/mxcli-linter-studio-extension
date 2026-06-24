using System.ComponentModel.Composition;
using Mendix.StudioPro.ExtensionsAPI.Services;
using Mendix.StudioPro.ExtensionsAPI.UI.DockablePane;
using Mendix.StudioPro.ExtensionsAPI.UI.Services;

namespace Clevr.AcrSpike;

/// <summary>
/// Registreert de door C# beheerde dockable pane. Geopend via
/// <see cref="SpikeMenuExtension"/> met IDockingWindowService.OpenPane
/// (de 11.10-API kent geen ViewMenuCaption).
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
        // WebServerBaseUrl + CurrentApp komen van de basisklasse (UIExtensionBase).
        // CurrentApp?.Root?.DirectoryPath = de map van de geopende app (fallback-projectpad).
        // () => CurrentApp geeft de VM live toegang tot het model (Fase 4: navigeren naar
        // het gevonden document via IDockingWindowService.TryOpenEditor).
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
