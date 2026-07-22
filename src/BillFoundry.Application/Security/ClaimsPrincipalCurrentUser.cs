using System.Security.Claims;
using BillFoundry.Domain.Identity;

namespace BillFoundry.Application.Security;

public sealed class ClaimsPrincipalCurrentUser : ICurrentUser
{
    private readonly ClaimsPrincipal _principal;

    public ClaimsPrincipalCurrentUser(ClaimsPrincipal? principal)
    {
        _principal = principal ?? new ClaimsPrincipal();
    }

    public bool IsAuthenticated => _principal.Identity?.IsAuthenticated == true;

    public Guid? UserId =>
        Guid.TryParse(_principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out Guid id) ? id : null;

    public string? Email =>
        _principal.FindFirst(ClaimTypes.Email)?.Value ?? _principal.Identity?.Name;

    public bool IsAdministrator => IsInRole(AppRoles.Administrator);

    public ClaimsPrincipal Principal => _principal;

    public bool IsInRole(string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        return _principal.IsInRole(role);
    }
}
