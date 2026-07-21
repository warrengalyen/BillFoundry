using BillFoundry.Application.Notifications;
using BillFoundry.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace BillFoundry.Infrastructure.Identity;

internal sealed class IdentityAccountEmailSender(IAccountNotificationService notifications)
    : IEmailSender<ApplicationUser>
{
    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        ArgumentNullException.ThrowIfNull(user);
        return notifications.SendEmailConfirmationAsync(email, confirmationLink);
    }

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        ArgumentNullException.ThrowIfNull(user);
        return notifications.SendPasswordResetLinkAsync(email, resetLink);
    }

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        ArgumentNullException.ThrowIfNull(user);
        return notifications.SendPasswordResetCodeAsync(email, resetCode);
    }
}
