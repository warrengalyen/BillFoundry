using Microsoft.AspNetCore.Components.Web;

namespace BillFoundry.Web.Components.Layout;

public partial class NavMenu
{
    private bool _navOpen;

    private void ToggleNav() => _navOpen = !_navOpen;

    private void CloseNav() => _navOpen = false;

    private void OnNavKeyDown(KeyboardEventArgs args)
    {
        if (_navOpen && string.Equals(args.Key, "Escape", StringComparison.Ordinal))
        {
            CloseNav();
        }
    }
}
