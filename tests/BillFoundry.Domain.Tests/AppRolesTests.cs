using BillFoundry.Domain.Identity;

namespace BillFoundry.Domain.Tests;

public sealed class AppRolesTests
{
    [Fact]
    public void All_contains_administrator_and_user()
    {
        Assert.Contains(AppRoles.Administrator, AppRoles.All);
        Assert.Contains(AppRoles.User, AppRoles.All);
        Assert.Equal(2, AppRoles.All.Count);
    }
}
