using BillFoundry.Application.Notifications;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BillFoundry.Infrastructure.Notifications;

internal sealed class LoggingAccountNotificationService(
    ILogger<LoggingAccountNotificationService> logger,
    IHostEnvironment environment) : IAccountNotificationService
{
    public Task SendEmailConfirmationAsync(string email, string confirmationLink, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmationLink);
        LogQueued("email confirmation", email);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetLinkAsync(string email, string resetLink, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(resetLink);
        LogQueued("password reset link", email);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetCodeAsync(string email, string resetCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(resetCode);
        LogQueued("password reset code", email);
        return Task.CompletedTask;
    }

    private void LogQueued(string notificationType, string email)
    {
        if (environment.IsDevelopment())
        {
            logger.LogInformation("Queued {NotificationType} account notification for {Email}. Delivery is not configured.", notificationType, email);
            return;
        }

        logger.LogInformation("Queued {NotificationType} account notification. Delivery is not configured.", notificationType);
    }
}
