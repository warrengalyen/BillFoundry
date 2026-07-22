using System.Security.Claims;

namespace BillFoundry.Application.Security;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    string? Email { get; }

    bool IsAdministrator { get; }

    ClaimsPrincipal Principal { get; }

    bool IsInRole(string role);
}
