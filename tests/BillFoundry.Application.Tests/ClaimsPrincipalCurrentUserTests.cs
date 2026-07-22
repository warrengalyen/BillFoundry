using System.Security.Claims;
using BillFoundry.Application.Security;
using BillFoundry.Domain.Identity;

namespace BillFoundry.Application.Tests;

public sealed class ClaimsPrincipalCurrentUserTests
{
    [Fact]
    public void Authenticated_principal_exposes_user_id_email_and_roles()
    {
        Guid userId = Guid.Parse("4a1b2c3d-4e5f-6789-abcd-ef0123456789");
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Email, "admin@localhost"),
                new Claim(ClaimTypes.Role, AppRoles.Administrator)
            ],
            authenticationType: "Test"));

        var currentUser = new ClaimsPrincipalCurrentUser(principal);

        Assert.True(currentUser.IsAuthenticated);
        Assert.Equal(userId, currentUser.UserId);
        Assert.Equal("admin@localhost", currentUser.Email);
        Assert.True(currentUser.IsAdministrator);
        Assert.True(currentUser.IsInRole(AppRoles.Administrator));
        Assert.False(currentUser.IsInRole(AppRoles.User));
        Assert.Same(principal, currentUser.Principal);
    }

    [Fact]
    public void Missing_principal_is_unauthenticated()
    {
        var currentUser = new ClaimsPrincipalCurrentUser(null);

        Assert.False(currentUser.IsAuthenticated);
        Assert.Null(currentUser.UserId);
        Assert.Null(currentUser.Email);
        Assert.False(currentUser.IsAdministrator);
        Assert.False(currentUser.IsInRole(AppRoles.Administrator));
        Assert.False(currentUser.Principal.Identity?.IsAuthenticated == true);
    }
}
