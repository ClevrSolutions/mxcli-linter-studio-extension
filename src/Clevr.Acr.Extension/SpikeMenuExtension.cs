using System.ComponentModel.Composition;
using Mendix.StudioPro.ExtensionsAPI.UI.Menu;
using Mendix.StudioPro.ExtensionsAPI.UI.Services;

namespace Clevr.Acr.Extension;

/// <summary>
/// Adds a menu item that opens the dockable pane via
/// IDockingWindowService.OpenPane. Appears under Extensions → (extension name).
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
