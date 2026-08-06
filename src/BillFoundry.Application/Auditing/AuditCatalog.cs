namespace BillFoundry.Application.Auditing;

public static class AuditActions
{
    public const string OrganizationUpdated = "OrganizationUpdated";
    public const string OrganizationLogoUploaded = "OrganizationLogoUploaded";
    public const string OrganizationLogoRemoved = "OrganizationLogoRemoved";
    public const string ClientCreated = "ClientCreated";
    public const string ClientUpdated = "ClientUpdated";
    public const string ClientActivated = "ClientActivated";
    public const string ClientDeactivated = "ClientDeactivated";
    public const string CatalogItemCreated = "CatalogItemCreated";
    public const string CatalogItemUpdated = "CatalogItemUpdated";
    public const string CatalogItemActivated = "CatalogItemActivated";
    public const string CatalogItemDeactivated = "CatalogItemDeactivated";
    public const string EstimateCreated = "EstimateCreated";
    public const string EstimateUpdated = "EstimateUpdated";
    public const string EstimateStatusChanged = "EstimateStatusChanged";
    public const string EstimateDuplicated = "EstimateDuplicated";
    public const string InvoiceCreated = "InvoiceCreated";
    public const string InvoiceUpdated = "InvoiceUpdated";
    public const string InvoiceSent = "InvoiceSent";
    public const string InvoiceVoided = "InvoiceVoided";
    public const string InvoiceDuplicated = "InvoiceDuplicated";
    public const string InvoiceConvertedFromEstimate = "InvoiceConvertedFromEstimate";
    public const string PaymentRecorded = "PaymentRecorded";
    public const string PaymentReversed = "PaymentReversed";
    public const string PasswordChanged = "PasswordChanged";
    public const string PasswordResetCompleted = "PasswordResetCompleted";
    public const string AccountLockedOut = "AccountLockedOut";

    public static IReadOnlyList<(string Value, string Label)> All { get; } =
    [
        (OrganizationUpdated, "Organization updated"),
        (OrganizationLogoUploaded, "Logo uploaded"),
        (OrganizationLogoRemoved, "Logo removed"),
        (ClientCreated, "Client created"),
        (ClientUpdated, "Client updated"),
        (ClientActivated, "Client activated"),
        (ClientDeactivated, "Client deactivated"),
        (CatalogItemCreated, "Service item created"),
        (CatalogItemUpdated, "Service item updated"),
        (CatalogItemActivated, "Service item activated"),
        (CatalogItemDeactivated, "Service item deactivated"),
        (EstimateCreated, "Estimate created"),
        (EstimateUpdated, "Estimate updated"),
        (EstimateStatusChanged, "Estimate status changed"),
        (EstimateDuplicated, "Estimate duplicated"),
        (InvoiceCreated, "Invoice created"),
        (InvoiceUpdated, "Invoice updated"),
        (InvoiceSent, "Invoice sent"),
        (InvoiceVoided, "Invoice voided"),
        (InvoiceDuplicated, "Invoice duplicated"),
        (InvoiceConvertedFromEstimate, "Invoice created from estimate"),
        (PaymentRecorded, "Payment recorded"),
        (PaymentReversed, "Payment reversed"),
        (PasswordChanged, "Password changed"),
        (PasswordResetCompleted, "Password reset completed"),
        (AccountLockedOut, "Account locked out")
    ];

    public static string Label(string action)
    {
        foreach ((string value, string label) in All)
        {
            if (string.Equals(value, action, StringComparison.Ordinal))
            {
                return label;
            }
        }

        return action;
    }
}

public static class AuditEntityTypes
{
    public const string Organization = "Organization";
    public const string Client = "Client";
    public const string CatalogItem = "CatalogItem";
    public const string Estimate = "Estimate";
    public const string Invoice = "Invoice";
    public const string User = "User";

    public static IReadOnlyList<(string Value, string Label)> All { get; } =
    [
        (Organization, "Organization"),
        (Client, "Client"),
        (CatalogItem, "Service item"),
        (Estimate, "Estimate"),
        (Invoice, "Invoice"),
        (User, "User account")
    ];

    public static string Label(string entityType)
    {
        foreach ((string value, string label) in All)
        {
            if (string.Equals(value, entityType, StringComparison.Ordinal))
            {
                return label;
            }
        }

        return entityType;
    }
}
