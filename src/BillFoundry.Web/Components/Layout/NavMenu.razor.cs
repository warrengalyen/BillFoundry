using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace BillFoundry.Web.Components.Layout;

public partial class NavMenu
{
    private bool _navOpen;
    private ElementReference _navToggle;

    private async Task ToggleNavAsync()
    {
        _navOpen = !_navOpen;
        if (!_navOpen)
        {
            await _navToggle.FocusAsync();
        }
    }

    private void CloseNav() => _navOpen = false;

    private async Task DismissNavAsync()
    {
        if (!_navOpen)
        {
            return;
        }

        _navOpen = false;
        await _navToggle.FocusAsync();
    }

    private async Task OnNavKeyDown(KeyboardEventArgs args)
    {
        if (_navOpen && string.Equals(args.Key, "Escape", StringComparison.Ordinal))
        {
            await DismissNavAsync();
        }
    }
}
