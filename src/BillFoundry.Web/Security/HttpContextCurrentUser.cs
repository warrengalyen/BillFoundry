using System.Security.Claims;
using BillFoundry.Application.Security;
using Microsoft.AspNetCore.Http;

namespace BillFoundry.Web.Security;

internal sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipalCurrentUser Inner =>
        new(httpContextAccessor.HttpContext?.User);

    public bool IsAuthenticated => Inner.IsAuthenticated;

    public Guid? UserId => Inner.UserId;

    public string? Email => Inner.Email;

    public bool IsAdministrator => Inner.IsAdministrator;

    public ClaimsPrincipal Principal => Inner.Principal;

    public bool IsInRole(string role) => Inner.IsInRole(role);
}
