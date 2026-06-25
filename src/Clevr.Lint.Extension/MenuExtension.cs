using System.ComponentModel.Composition;
using Mendix.StudioPro.ExtensionsAPI.UI.Services;
using MenuViewModel = Mendix.StudioPro.ExtensionsAPI.UI.Menu.MenuViewModel;
using MxMenuExtension = Mendix.StudioPro.ExtensionsAPI.UI.Menu.MenuExtension;

namespace Clevr.Lint.Extension;

/// <summary>
/// Adds a menu item that opens the dockable pane via
/// IDockingWindowService.OpenPane. Appears under Extensions → (extension name).
/// </summary>
[Export(typeof(MxMenuExtension))]
public class MenuExtension : MxMenuExtension
{
    private readonly IDockingWindowService _dockingWindowService;

    [ImportingConstructor]
    public MenuExtension(IDockingWindowService dockingWindowService)
    {
        _dockingWindowService = dockingWindowService;
    }

    public override IEnumerable<MenuViewModel> GetMenus()
    {
        yield return new MenuViewModel(
            "CLEVR Lint",
            () => _dockingWindowService.OpenPane(DockablePaneExtension.PaneId));
    }
}
