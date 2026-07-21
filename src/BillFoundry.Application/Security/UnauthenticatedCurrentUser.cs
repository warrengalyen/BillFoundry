namespace BillFoundry.Application.Security;

public sealed class UnauthenticatedCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => false;

    public Guid? UserId => null;

    public string? Email => null;

    public bool IsAdministrator => false;

    public bool IsInRole(string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        return false;
    }
}
