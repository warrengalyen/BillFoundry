namespace BillFoundry.Application.Security;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    string? Email { get; }

    bool IsAdministrator { get; }

    bool IsInRole(string role);
}
