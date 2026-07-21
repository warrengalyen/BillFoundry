using Microsoft.AspNetCore.Authorization;

namespace BillFoundry.Application.Security;

public sealed class NotDemoModeHandler(IDemoMode demoMode) : AuthorizationHandler<NotDemoModeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        NotDemoModeRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!demoMode.IsEnabled)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
