using System.ComponentModel.Composition;
using Mendix.StudioPro.ExtensionsAPI.UI.Menu;
using Mendix.StudioPro.ExtensionsAPI.UI.Services;

namespace Clevr.AcrSpike;

/// <summary>
/// Voegt een menu-item toe dat de dockable pane opent via
/// IDockingWindowService.OpenPane. Verschijnt onder Extensions → (extensienaam).
/// </summary>
[Export(typeof(MenuExtension))]
public class SpikeMenuExtension : MenuExtension
{
    private readonly IDockingWindowService _dockingWindowService;

    [ImportingConstructor]
    public SpikeMenuExtension(IDockingWindowService dockingWindowService)
    {
        _dockingWindowService = dockingWindowService;
    }

    public override IEnumerable<MenuViewModel> GetMenus()
    {
        yield return new MenuViewModel(
            "CLEVR ACR",
            () => _dockingWindowService.OpenPane(SpikeDockablePaneExtension.PaneId));
    }
}
