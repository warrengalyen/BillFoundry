using BillFoundry.Application.Security;
using BillFoundry.Domain.Identity;

namespace BillFoundry.Application.Tests;

public sealed class UnauthenticatedCurrentUserTests
{
    [Fact]
    public void Unauthenticated_current_user_has_no_identity()
    {
        var currentUser = new UnauthenticatedCurrentUser();

        Assert.False(currentUser.IsAuthenticated);
        Assert.Null(currentUser.UserId);
        Assert.Null(currentUser.Email);
        Assert.False(currentUser.IsAdministrator);
        Assert.False(currentUser.IsInRole(AppRoles.User));
        Assert.False(currentUser.Principal.Identity?.IsAuthenticated == true);
    }
}
