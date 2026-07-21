using BillFoundry.Application.Configuration;
using Microsoft.Extensions.Options;

namespace BillFoundry.Application.Security;

public sealed class DemoMode(IOptions<DemoModeOptions> options) : IDemoMode
{
    public bool IsEnabled => options.Value.Enabled;
}
