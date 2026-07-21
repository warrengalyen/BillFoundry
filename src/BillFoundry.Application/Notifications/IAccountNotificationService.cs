namespace BillFoundry.Application.Notifications;

public interface IAccountNotificationService
{
    Task SendEmailConfirmationAsync(string email, string confirmationLink, CancellationToken cancellationToken = default);

    Task SendPasswordResetLinkAsync(string email, string resetLink, CancellationToken cancellationToken = default);

    Task SendPasswordResetCodeAsync(string email, string resetCode, CancellationToken cancellationToken = default);
}
