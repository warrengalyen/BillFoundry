using Microsoft.AspNetCore.Authorization;

namespace BillFoundry.Application.Security;

public sealed class NotDemoModeRequirement : IAuthorizationRequirement
{
}
