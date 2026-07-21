using BillFoundry.Domain.Auditing;
using Microsoft.AspNetCore.Identity;

namespace BillFoundry.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>, IAuditable
{
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public Guid? CreatedByUserId { get; private set; }

    public Guid? UpdatedByUserId { get; private set; }

    public bool IsDisabled { get; set; }

    public void SetCreated(DateTimeOffset atUtc, Guid? byUserId)
    {
        CreatedAtUtc = atUtc;
        CreatedByUserId = byUserId;
    }

    public void SetUpdated(DateTimeOffset atUtc, Guid? byUserId)
    {
        UpdatedAtUtc = atUtc;
        UpdatedByUserId = byUserId;
    }
}
