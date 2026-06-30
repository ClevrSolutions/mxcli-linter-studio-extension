using System.ComponentModel.Composition;
using Mendix.StudioPro.ExtensionsAPI.Services;
using Mendix.StudioPro.ExtensionsAPI.UI.Services;
using MxDockablePaneExtension = Mendix.StudioPro.ExtensionsAPI.UI.DockablePane.DockablePaneExtension;
using MxDockablePaneViewModelBase = Mendix.StudioPro.ExtensionsAPI.UI.DockablePane.DockablePaneViewModelBase;

namespace Clevr.Lint.Extension;

/// <summary>
/// Registers the C#-managed dockable pane. Opened via
/// <see cref="MenuExtension"/> with IDockingWindowService.OpenPane
/// (the 11.10 API does not have ViewMenuCaption).
/// </summary>
[Export(typeof(MxDockablePaneExtension))]
public class DockablePaneExtension : MxDockablePaneExtension
{
    public const string PaneId = "clevr-lint";

    private readonly ILogService _logService;
    private readonly IExtensionFileService _fileService;
    private readonly IDockingWindowService _dockingWindowService;

    [ImportingConstructor]
    public DockablePaneExtension(
        ILogService logService,
        IExtensionFileService fileService,
        IDockingWindowService dockingWindowService)
    {
        _logService = logService;
        _fileService = fileService;
        _dockingWindowService = dockingWindowService;
    }

    public override string Id => PaneId;

    public override MxDockablePaneViewModelBase Open()
    {
        // WebServerBaseUrl + CurrentApp come from the base class (UIExtensionBase).
        // CurrentApp?.Root?.DirectoryPath = the directory of the opened app (fallback project path).
        // () => CurrentApp gives the VM live access to the model (Phase 4: navigate to
        // the found document via IDockingWindowService.TryOpenEditor).
        return new DockablePaneViewModel(
            WebServerBaseUrl,
            _logService,
            _fileService,
            () => CurrentApp?.Root?.DirectoryPath,
            () => CurrentApp,
            _dockingWindowService,
            Configuration.MendixVersion?.ToString())
        {
            Title = "CLEVR Lint",
        };
    }
}
