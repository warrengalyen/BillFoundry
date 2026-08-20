using BillFoundry.Application.Auditing;
using BillFoundry.Application.Configuration;
using BillFoundry.Domain.Auditing;
using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Clients;
using BillFoundry.Domain.Documents;
using BillFoundry.Domain.Estimates;
using BillFoundry.Domain.Identity;
using BillFoundry.Domain.Invoices;
using BillFoundry.Domain.Organizations;
using BillFoundry.Infrastructure.Identity;
using BillFoundry.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BillFoundry.Infrastructure.Demo;

/// <summary>
/// Loads a published, fictional North Beacon Studio dataset for public demonstrations.
/// Names, addresses, and amounts are invented. Nothing here is a real client or payment.
/// </summary>
internal sealed class DemoSeeder(
    BillFoundryDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    TimeProvider timeProvider,
    ILogger<DemoSeeder> logger)
{
    public const string MarkerSku = "NBS-BRAND";

    public async Task SeedAsync(DemoSeedOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        foreach (string roleName in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName).ConfigureAwait(false))
            {
                IdentityResult roleResult = await roleManager
                    .CreateAsync(new IdentityRole<Guid>(roleName))
                    .ConfigureAwait(false);
                EnsureSucceeded(roleResult, $"Failed to create role '{roleName}'.");
            }
        }

        ApplicationUser administrator = await EnsureUserAsync(
            options.AdministratorEmail,
            options.AdministratorPassword,
            AppRoles.Administrator,
            cancellationToken).ConfigureAwait(false);

        await EnsureUserAsync(
            options.UserEmail,
            options.UserPassword,
            AppRoles.User,
            cancellationToken).ConfigureAwait(false);

        bool alreadySeeded = await dbContext.CatalogItems
            .AnyAsync(item => item.Sku == MarkerSku, cancellationToken)
            .ConfigureAwait(false);

        if (alreadySeeded && !options.ResetOnStartup)
        {
            logger.LogInformation("Demo business data is already present. Skipping reseed.");
            return;
        }

        if (alreadySeeded && options.ResetOnStartup)
        {
            logger.LogInformation("DemoSeed:ResetOnStartup is enabled. Replacing fictional demo business data.");
            await WipeBusinessDataAsync(cancellationToken).ConfigureAwait(false);
        }

        await SeedBusinessDataAsync(administrator, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Seeded fictional North Beacon Studio demonstration data.");
    }

    private async Task WipeBusinessDataAsync(CancellationToken cancellationToken)
    {
        await dbContext.InvoicePayments.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.InvoiceLines.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.Invoices.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.EstimateLines.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.Estimates.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.ClientContacts.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.Clients.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.CatalogItems.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.AuditEvents.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.DocumentSequences
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(sequence => sequence.NextValue, 1),
                cancellationToken)
            .ConfigureAwait(false);
        dbContext.ChangeTracker.Clear();
    }

    private async Task SeedBusinessDataAsync(ApplicationUser administrator, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);
        Guid actorId = administrator.Id;
        string actorName = administrator.Email ?? "admin@northbeacon.example";

        Organization organization = await dbContext.Organizations
            .FindAsync([Organization.SingletonId], cancellationToken)
            .ConfigureAwait(false)
            ?? Organization.CreateSingleton();

        organization.UpdateProfile(
            "North Beacon Studio LLC",
            "North Beacon Studio",
            PostalAddress.Create(
                "410 Fictional Wharf",
                "Suite 12",
                "Portland",
                "OR",
                "97204",
                "United States"),
            "billing@northbeacon.example",
            "+1 555 010 0142",
            "https://northbeacon.example",
            "NBS-00-000000",
            CurrencyCode.Usd,
            30,
            DocumentPrefix.InvoiceDefault,
            DocumentPrefix.EstimateDefault,
            "Thank you for working with North Beacon Studio. This demonstration data is entirely fictional.",
            "Pay by bank transfer to the account listed on the invoice. Public demo payments are not real.");

        if (dbContext.Entry(organization).State == EntityState.Detached)
        {
            dbContext.Organizations.Add(organization);
        }

        CatalogItem brand = CatalogItem.Create("Brand system", "Visual identity, type, and color system.", MarkerSku, CatalogUnitType.Hour, 180m, true);
        CatalogItem discovery = CatalogItem.Create("Discovery workshop", "Half-day working session to frame scope.", "NBS-DISC", CatalogUnitType.FlatFee, 1500m, true);
        CatalogItem website = CatalogItem.Create("Website build", "Implementation of a marketing site.", "NBS-WEB", CatalogUnitType.Hour, 160m, true);
        CatalogItem photo = CatalogItem.Create("Product photography", "Styled stills for catalog and web.", "NBS-PHOTO", CatalogUnitType.Item, 95m, true);
        CatalogItem facilitation = CatalogItem.Create("Facilitation day", "On-site workshop facilitation.", "NBS-DAY", CatalogUnitType.Day, 1200m, false);
        CatalogItem retainer = CatalogItem.Create("Monthly retainer", "Ongoing design support block.", "NBS-RETAIN", CatalogUnitType.FlatFee, 2500m, true);
        CatalogItem print = CatalogItem.Create("Print package", "Business cards and letterhead set.", "NBS-PRINT", CatalogUnitType.Item, 45m, true);
        CatalogItem legacy = CatalogItem.Create("Legacy fax setup", "Retired analog setup. Kept for history.", "NBS-FAX", CatalogUnitType.Hour, 75m, false);
        legacy.Deactivate();
        dbContext.CatalogItems.AddRange(brand, discovery, website, photo, facilitation, retainer, print, legacy);

        Client[] clients = CreateClients();
        clients[11].Deactivate();
        dbContext.Clients.AddRange(clients);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        DocumentSequence estimateSequence = await dbContext.DocumentSequences
            .SingleAsync(sequence => sequence.Kind == DocumentSequence.EstimateKind, cancellationToken)
            .ConfigureAwait(false);
        DocumentSequence invoiceSequence = await dbContext.DocumentSequences
            .SingleAsync(sequence => sequence.Kind == DocumentSequence.InvoiceKind, cancellationToken)
            .ConfigureAwait(false);

        Estimate draftEstimate = CreateEstimate(estimateSequence, clients[0], today.AddDays(-4), today.AddDays(26), "Draft scope for the spring catalog.", 0m, 0m);
        draftEstimate.AddLine(discovery.Id, discovery.Name, 1m, discovery.UnitType, discovery.DefaultUnitPrice, discovery.IsTaxable);
        draftEstimate.AddLine(brand.Id, brand.Name, 8m, brand.UnitType, brand.DefaultUnitPrice, brand.IsTaxable);

        Estimate sentEstimate = CreateEstimate(estimateSequence, clients[1], today.AddDays(-18), today.AddDays(12), "Website rebuild proposal.", 0m, 0m);
        sentEstimate.AddLine(website.Id, website.Name, 24m, website.UnitType, website.DefaultUnitPrice, website.IsTaxable);
        sentEstimate.UpdateHeader(
            clients[1].Id,
            today.AddDays(-18),
            today.AddDays(12),
            "Website rebuild proposal.",
            "Valid through the expiration date. Demonstration terms only.",
            200m,
            0m);
        sentEstimate.TransitionTo(EstimateStatus.Sent);

        Estimate acceptedEstimate = CreateEstimate(estimateSequence, clients[2], today.AddDays(-40), today.AddDays(-10), "Trail photography and site work.", 0m, 8m);
        acceptedEstimate.AddLine(photo.Id, photo.Name, 12m, photo.UnitType, photo.DefaultUnitPrice, photo.IsTaxable);
        acceptedEstimate.AddLine(website.Id, website.Name, 16m, website.UnitType, website.DefaultUnitPrice, website.IsTaxable);
        acceptedEstimate.TransitionTo(EstimateStatus.Sent);
        acceptedEstimate.TransitionTo(EstimateStatus.Accepted);

        Estimate declinedEstimate = CreateEstimate(estimateSequence, clients[3], today.AddDays(-50), today.AddDays(-20), "Kiln-room mural proposal.", 0m, 0m);
        declinedEstimate.AddLine(facilitation.Id, facilitation.Name, 2m, facilitation.UnitType, facilitation.DefaultUnitPrice, facilitation.IsTaxable);
        declinedEstimate.TransitionTo(EstimateStatus.Sent);
        declinedEstimate.TransitionTo(EstimateStatus.Declined);

        Estimate expiredEstimate = CreateEstimate(estimateSequence, clients[4], today.AddDays(-80), today.AddDays(-15), "Clinic waiting-room refresh.", 0m, 0m);
        expiredEstimate.AddLine(brand.Id, brand.Name, 6m, brand.UnitType, brand.DefaultUnitPrice, brand.IsTaxable);
        expiredEstimate.TransitionTo(EstimateStatus.Sent);
        expiredEstimate.TransitionTo(EstimateStatus.Expired);

        Estimate convertedSource = CreateEstimate(estimateSequence, clients[5], today.AddDays(-70), today.AddDays(-40), "Wayfinding and donor wall.", 0m, 10m);
        convertedSource.AddLine(discovery.Id, discovery.Name, 1m, discovery.UnitType, discovery.DefaultUnitPrice, discovery.IsTaxable);
        convertedSource.AddLine(brand.Id, brand.Name, 20m, brand.UnitType, brand.DefaultUnitPrice, brand.IsTaxable);
        convertedSource.TransitionTo(EstimateStatus.Sent);
        convertedSource.TransitionTo(EstimateStatus.Accepted);

        Estimate openAccepted = CreateEstimate(estimateSequence, clients[7], today.AddDays(-12), today.AddDays(18), "Tutoring portal illustration.", 0m, 0m);
        openAccepted.AddLine(brand.Id, brand.Name, 10m, brand.UnitType, brand.DefaultUnitPrice, brand.IsTaxable);
        openAccepted.TransitionTo(EstimateStatus.Sent);
        openAccepted.TransitionTo(EstimateStatus.Accepted);

        Estimate retainerEstimate = CreateEstimate(estimateSequence, clients[6], today.AddDays(-8), today.AddDays(22), "Quarterly coffee-bar retainer.", 0m, 0m);
        retainerEstimate.AddLine(retainer.Id, retainer.Name, 1m, retainer.UnitType, retainer.DefaultUnitPrice, retainer.IsTaxable);
        retainerEstimate.TransitionTo(EstimateStatus.Sent);

        dbContext.Estimates.AddRange(
            draftEstimate,
            sentEstimate,
            acceptedEstimate,
            declinedEstimate,
            expiredEstimate,
            convertedSource,
            openAccepted,
            retainerEstimate);

        Invoice draftInvoice = CreateInvoice(invoiceSequence, clients[0], today.AddDays(-2), today.AddDays(28), "PO-1042", 0m, 0m);
        draftInvoice.AddLine(print.Id, print.Name, 4m, print.UnitType, print.DefaultUnitPrice, print.IsTaxable);

        Invoice sentCurrent = CreateInvoice(invoiceSequence, clients[1], today.AddDays(-10), today.AddDays(20), "PO-2201", 0m, 0m);
        sentCurrent.AddLine(website.Id, website.Name, 18m, website.UnitType, website.DefaultUnitPrice, website.IsTaxable);
        sentCurrent.MarkSent();

        int convertedSequence = invoiceSequence.Allocate();
        Invoice convertedInvoice = Invoice.FromEstimate(
            convertedSource,
            Snapshot(clients[5]),
            convertedSequence,
            InvoiceNumber.Format(DocumentPrefix.InvoiceDefault, convertedSequence),
            today.AddDays(-38),
            today.AddDays(-8),
            "PO-ARCH-9",
            convertedSource.Notes,
            organization.DefaultPaymentInstructions);
        convertedSource.TransitionTo(EstimateStatus.Converted);
        convertedInvoice.MarkSent();
        convertedInvoice.RecordPayment(
            today,
            today.AddDays(-20),
            1800m,
            PaymentMethod.BankTransfer,
            "ACH-44821",
            "First progress payment. Fictional.");

        Invoice paidInvoice = CreateInvoice(invoiceSequence, clients[6], today.AddDays(-90), today.AddDays(-60), null, 0m, 0m);
        paidInvoice.AddLine(retainer.Id, retainer.Name, 1m, retainer.UnitType, retainer.DefaultUnitPrice, retainer.IsTaxable);
        paidInvoice.MarkSent();
        paidInvoice.RecordPayment(
            today,
            today.AddDays(-55),
            paidInvoice.BalanceDue,
            PaymentMethod.CreditCard,
            "VISA-1044",
            "Paid in full. Fictional card network reference.");

        Invoice overdueSent = CreateInvoice(invoiceSequence, clients[8], today.AddDays(-75), today.AddDays(-45), "LEGAL-17", 0m, 8m);
        overdueSent.AddLine(brand.Id, brand.Name, 14m, brand.UnitType, brand.DefaultUnitPrice, brand.IsTaxable);
        overdueSent.MarkSent();

        Invoice overduePartial = CreateInvoice(invoiceSequence, clients[9], today.AddDays(-50), today.AddDays(-20), null, 0m, 0m);
        overduePartial.AddLine(photo.Id, photo.Name, 8m, photo.UnitType, photo.DefaultUnitPrice, photo.IsTaxable);
        overduePartial.AddLine(print.Id, print.Name, 6m, print.UnitType, print.DefaultUnitPrice, print.IsTaxable);
        overduePartial.MarkSent();
        overduePartial.RecordPayment(
            today,
            today.AddDays(-30),
            250m,
            PaymentMethod.Check,
            "1008",
            "Partial check. Fictional.");

        Invoice voidInvoice = CreateInvoice(invoiceSequence, clients[10], today.AddDays(-25), today.AddDays(5), "DUP-1", 0m, 0m);
        voidInvoice.AddLine(discovery.Id, discovery.Name, 1m, discovery.UnitType, discovery.DefaultUnitPrice, discovery.IsTaxable);
        voidInvoice.MarkSent();
        voidInvoice.Void("Issued twice after a calendar mix-up. Demonstration void only.");

        Invoice bakeryPaid = CreateInvoice(invoiceSequence, clients[13], today.AddDays(-120), today.AddDays(-90), null, 0m, 0m);
        bakeryPaid.AddLine(print.Id, print.Name, 20m, print.UnitType, print.DefaultUnitPrice, print.IsTaxable);
        bakeryPaid.UpdateHeader(
            clients[13].Id,
            Snapshot(clients[13]),
            today.AddDays(-120),
            today.AddDays(-90),
            null,
            "Fictional demonstration invoice. Not a real bill.",
            "Fictional payment instructions for the public demo.",
            100m,
            0m);
        bakeryPaid.MarkSent();
        bakeryPaid.RecordPayment(
            today,
            today.AddDays(-88),
            bakeryPaid.BalanceDue,
            PaymentMethod.Cash,
            null,
            "Counter payment. Fictional.");

        Invoice interiorsSent = CreateInvoice(invoiceSequence, clients[12], today.AddDays(-6), today.AddDays(24), "INT-55", 0m, 0m);
        interiorsSent.AddLine(facilitation.Id, facilitation.Name, 1m, facilitation.UnitType, facilitation.DefaultUnitPrice, facilitation.IsTaxable);
        interiorsSent.AddLine(brand.Id, brand.Name, 4m, brand.UnitType, brand.DefaultUnitPrice, brand.IsTaxable);
        interiorsSent.MarkSent();

        Invoice tutoringPartial = CreateInvoice(invoiceSequence, clients[7], today.AddDays(-14), today.AddDays(16), null, 0m, 0m);
        tutoringPartial.AddLine(website.Id, website.Name, 10m, website.UnitType, website.DefaultUnitPrice, website.IsTaxable);
        tutoringPartial.MarkSent();
        tutoringPartial.RecordPayment(
            today,
            today.AddDays(-7),
            400m,
            PaymentMethod.PayPal,
            "PP-22011",
            "Deposit. Fictional.");

        Invoice mediaSent = CreateInvoice(invoiceSequence, clients[14], today.AddDays(-3), today.AddDays(27), "MEDIA-3", 0m, 0m);
        mediaSent.AddLine(photo.Id, photo.Name, 6m, photo.UnitType, photo.DefaultUnitPrice, photo.IsTaxable);
        mediaSent.MarkSent();

        Invoice daycareDraft = CreateInvoice(invoiceSequence, clients[15], today, today.AddDays(30), null, 0m, 0m);
        daycareDraft.AddLine(print.Id, print.Name, 8m, print.UnitType, print.DefaultUnitPrice, print.IsTaxable);

        dbContext.Invoices.AddRange(
            draftInvoice,
            sentCurrent,
            convertedInvoice,
            paidInvoice,
            overdueSent,
            overduePartial,
            voidInvoice,
            bakeryPaid,
            interiorsSent,
            tutoringPartial,
            mediaSent,
            daycareDraft);

        dbContext.AuditEvents.AddRange(
            AuditEvent.Create(now.AddDays(-120), actorId, actorName, AuditActions.OrganizationUpdated, AuditEntityTypes.Organization, organization.Id, "Updated the fictional North Beacon Studio profile.", null),
            AuditEvent.Create(now.AddDays(-119), actorId, actorName, AuditActions.CatalogItemCreated, AuditEntityTypes.CatalogItem, brand.Id, "Added Brand system to the demonstration catalog.", null),
            AuditEvent.Create(now.AddDays(-118), actorId, actorName, AuditActions.ClientCreated, AuditEntityTypes.Client, clients[0].Id, "Added Harbor & Pine Workshop (fictional client).", null),
            AuditEvent.Create(now.AddDays(-70), actorId, actorName, AuditActions.EstimateCreated, AuditEntityTypes.Estimate, convertedSource.Id, "Created a wayfinding estimate for Willowbrook Architecture.", null),
            AuditEvent.Create(now.AddDays(-40), actorId, actorName, AuditActions.EstimateStatusChanged, AuditEntityTypes.Estimate, convertedSource.Id, "Marked the Willowbrook estimate accepted.", null),
            AuditEvent.Create(now.AddDays(-38), actorId, actorName, AuditActions.InvoiceConvertedFromEstimate, AuditEntityTypes.Invoice, convertedInvoice.Id, "Converted the accepted Willowbrook estimate to an invoice.", null),
            AuditEvent.Create(now.AddDays(-20), actorId, actorName, AuditActions.PaymentRecorded, AuditEntityTypes.Invoice, convertedInvoice.Id, "Recorded a fictional progress payment.", null),
            AuditEvent.Create(now.AddDays(-55), actorId, actorName, AuditActions.PaymentRecorded, AuditEntityTypes.Invoice, paidInvoice.Id, "Recorded a fictional retainer payment in full.", null),
            AuditEvent.Create(now.AddDays(-25), actorId, actorName, AuditActions.InvoiceVoided, AuditEntityTypes.Invoice, voidInvoice.Id, "Voided a duplicate demonstration invoice.", null),
            AuditEvent.Create(now.AddDays(-10), actorId, actorName, AuditActions.InvoiceSent, AuditEntityTypes.Invoice, sentCurrent.Id, "Sent a current demonstration invoice.", null));

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Client[] CreateClients() =>
    [
        CreateClient(1, "Harbor & Pine Workshop", "hello@harborandpine.example", "https://harborandpine.example", "88 Timberline Ave", "Portland", "OR", "97209", "Shop manager: Juniper Hale (fictional)."),
        CreateClient(2, "Redwood Ledger Co", "accounts@redwoodledger.example", "https://redwoodledger.example", "210 Market St", "Portland", "OR", "97201", "Bookkeeping studio. Fictional."),
        CreateClient(3, "Lumen Trail Outfitters", "hello@lumentrail.example", "https://lumentrail.example", "15 Cascade Way", "Bend", "OR", "97701", "Outdoor retailer. Fictional."),
        CreateClient(4, "Cascadia Clayworks", "studio@cascadiaclay.example", "https://cascadiaclay.example", "40 Kiln Road", "Salem", "OR", "97301", "Ceramics cooperative. Fictional."),
        CreateClient(5, "Northstar Veterinary", "office@northstarvet.example", "https://northstarvet.example", "900 Clinic Blvd", "Eugene", "OR", "97401", "Small-animal clinic. Fictional."),
        CreateClient(6, "Willowbrook Architecture", "projects@willowbrookarch.example", "https://willowbrookarch.example", "12 River Plaza", "Portland", "OR", "97204", "Architecture practice. Fictional."),
        CreateClient(7, "Copper Finch Coffee", "team@copperfinch.example", "https://copperfinch.example", "301 Division St", "Portland", "OR", "97202", "Neighborhood roaster. Fictional."),
        CreateClient(8, "Bright Harbor Tutoring", "hello@brightharbor.example", "https://brightharbor.example", "55 Schoolhouse Ln", "Vancouver", "WA", "98660", "Tutoring group. Fictional."),
        CreateClient(9, "Oak & Anchor Legal", "billing@oakanchor.example", "https://oakanchor.example", "800 Counsel Ave", "Portland", "OR", "97205", "Law office. Fictional."),
        CreateClient(10, "Puffin Press", "editor@puffinpress.example", "https://puffinpress.example", "4 Bindery Ct", "Astoria", "OR", "97103", "Independent press. Fictional."),
        CreateClient(11, "Solstice Wellness", "front@solsticewell.example", "https://solsticewell.example", "18 Cedar Loop", "Portland", "OR", "97210", "Wellness studio. Fictional."),
        CreateClient(12, "Maple Circuit Labs", "ops@maplecircuit.example", "https://maplecircuit.example", "70 Hardware Dr", "Hillsboro", "OR", "97124", "Inactive hardware prototype shop. Fictional."),
        CreateClient(13, "Fjord & Co. Interiors", "studio@fjordandco.example", "https://fjordandco.example", "22 Upholstery Way", "Portland", "OR", "97211", "Interior studio. Fictional."),
        CreateClient(14, "Beacon Hill Bakery", "orders@beaconhillbakery.example", "https://beaconhillbakery.example", "9 Oven Street", "Seattle", "WA", "98116", "Bakery. Fictional."),
        CreateClient(15, "Silvercurrent Media", "producers@silvercurrent.example", "https://silvercurrent.example", "440 Broadcast Ave", "Portland", "OR", "97214", "Podcast studio. Fictional."),
        CreateClient(16, "Driftwood Daycare", "families@driftwooddays.example", "https://driftwooddays.example", "27 Playground Rd", "Gresham", "OR", "97030", "Childcare center. Fictional.")
    ];

    private static Client CreateClient(
        int number,
        string name,
        string email,
        string website,
        string line1,
        string city,
        string region,
        string postalCode,
        string notes)
    {
        Client client = Client.Create(
            number,
            ClientCode.FromNumber(number),
            name,
            email,
            $"+1 555 010 {number:0000}",
            website,
            PostalAddress.Create(line1, null, city, region, postalCode, "United States"),
            notes);
        client.AddContact("Avery Quinn", "Primary contact", email, $"+1 555 011 {number:0000}", isPrimary: true);
        if (number % 3 == 0)
        {
            client.AddContact("Morgan Ellis", "Accounts", $"billing{number}@northbeacon.example", "+1 555 012 0100", isPrimary: false);
        }

        return client;
    }

    private static Estimate CreateEstimate(
        DocumentSequence sequence,
        Client client,
        DateOnly issueDate,
        DateOnly expirationDate,
        string notes,
        decimal discount,
        decimal taxRatePercent)
    {
        int number = sequence.Allocate();
        return Estimate.Create(
            number,
            EstimateNumber.Format(DocumentPrefix.EstimateDefault, number),
            client.Id,
            issueDate,
            expirationDate,
            notes,
            "Valid through the expiration date. Demonstration terms only.",
            discount,
            taxRatePercent,
            CurrencyCode.Usd);
    }

    private static Invoice CreateInvoice(
        DocumentSequence sequence,
        Client client,
        DateOnly issueDate,
        DateOnly dueDate,
        string? purchaseOrder,
        decimal discount,
        decimal taxRatePercent)
    {
        int number = sequence.Allocate();
        return Invoice.Create(
            number,
            InvoiceNumber.Format(DocumentPrefix.InvoiceDefault, number),
            client.Id,
            Snapshot(client),
            issueDate,
            dueDate,
            purchaseOrder,
            "Fictional demonstration invoice. Not a real bill.",
            "Fictional payment instructions for the public demo.",
            discount,
            taxRatePercent,
            CurrencyCode.Usd);
    }

    private static InvoiceClientSnapshot Snapshot(Client client) =>
        InvoiceClientSnapshot.Capture(client.Name, client.Code, client.Email);

    private async Task<ApplicationUser> EnsureUserAsync(
        string email,
        string password,
        string role,
        CancellationToken cancellationToken)
    {
        ApplicationUser? user = await userManager.FindByEmailAsync(email).ConfigureAwait(false);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = Guid.CreateVersion7(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                IsDisabled = false
            };

            IdentityResult createResult = await userManager.CreateAsync(user, password).ConfigureAwait(false);
            EnsureSucceeded(createResult, $"Failed to create demo user '{email}'.");
        }
        else
        {
            user.IsDisabled = false;
            await userManager.SetLockoutEndDateAsync(user, null).ConfigureAwait(false);
            await userManager.ResetAccessFailedCountAsync(user).ConfigureAwait(false);
            IdentityResult removePassword = await userManager.RemovePasswordAsync(user).ConfigureAwait(false);
            EnsureSucceeded(removePassword, $"Failed to rotate the password for '{email}'.");
            IdentityResult addPassword = await userManager.AddPasswordAsync(user, password).ConfigureAwait(false);
            EnsureSucceeded(addPassword, $"Failed to set the demo password for '{email}'.");
            await userManager.UpdateAsync(user).ConfigureAwait(false);
        }

        if (!await userManager.IsInRoleAsync(user, role).ConfigureAwait(false))
        {
            IdentityResult roleResult = await userManager.AddToRoleAsync(user, role).ConfigureAwait(false);
            EnsureSucceeded(roleResult, $"Failed to assign role '{role}' to '{email}'.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return user;
    }

    private static void EnsureSucceeded(IdentityResult result, string message)
    {
        if (result.Succeeded)
        {
            return;
        }

        string details = string.Join(" ", result.Errors.Select(error => error.Description));
        throw new InvalidOperationException($"{message} {details}");
    }
}
