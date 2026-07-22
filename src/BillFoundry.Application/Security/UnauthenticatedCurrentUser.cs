using System.Security.Claims;

namespace BillFoundry.Application.Security;

public sealed class UnauthenticatedCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => false;

    public Guid? UserId => null;

    public string? Email => null;

    public bool IsAdministrator => false;

    public ClaimsPrincipal Principal { get; } = new();

    public bool IsInRole(string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        return false;
    }
}
