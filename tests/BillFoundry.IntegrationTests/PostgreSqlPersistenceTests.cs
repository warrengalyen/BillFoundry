using BillFoundry.Application.Auditing;
using BillFoundry.Application.Catalog;
using BillFoundry.Application.Clients;
using BillFoundry.Application.Configuration;
using BillFoundry.Application.Estimates;
using BillFoundry.Application.Invoices;
using BillFoundry.Application.Organizations;
using BillFoundry.Application.Reporting;
using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Estimates;
using BillFoundry.Domain.Invoices;
using BillFoundry.Infrastructure.Demo;
using BillFoundry.Infrastructure.Identity;
using BillFoundry.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BillFoundry.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class PostgreSqlPersistenceTests
{
    private readonly PostgreSqlFixture _postgres;

    public PostgreSqlPersistenceTests(PostgreSqlFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact]
    public async Task Migrates_and_runs_critical_workflows()
    {
        _postgres.RequireOrSkip();
        if (!_postgres.ShouldRun)
        {
            return;
        }

        await using ServiceProvider provider = OrganizationTestHost.Create(
            _postgres.ConnectionString,
            _postgres.LogoRoot,
            OrganizationTestHost.Administrator(),
            DatabaseProvider.PostgreSql);

        await using (var scope = provider.CreateAsyncScope())
        {
            BillFoundryDbContext db = scope.ServiceProvider.GetRequiredService<BillFoundryDbContext>();
            Assert.True(await db.Database.CanConnectAsync());
            Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", db.Database.ProviderName);
            Assert.True(await db.DocumentSequences.AnyAsync());
        }

        DemoSeeder seeder = provider.GetRequiredService<DemoSeeder>();
        var seed = new DemoSeedOptions { Enabled = true, ResetOnStartup = true };
        await seeder.SeedAsync(seed, CancellationToken.None);

        using (IServiceScope seeded = provider.CreateScope())
        {
            BillFoundryDbContext db = seeded.ServiceProvider.GetRequiredService<BillFoundryDbContext>();
            UserManager<ApplicationUser> users = seeded.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.NotNull(await users.FindByEmailAsync(seed.AdministratorEmail));
            Assert.NotNull(await users.FindByEmailAsync(seed.UserEmail));
            Assert.True(await db.CatalogItems.AnyAsync(item => item.Sku == DemoSeeder.MarkerSku));
            Assert.True(await db.Clients.CountAsync() >= 10);
            Assert.True(await db.Estimates.CountAsync() >= 5);
            Assert.True(await db.Invoices.CountAsync() >= 8);
            Assert.True(await db.InvoicePayments.CountAsync() >= 3);
            Assert.True(await db.AuditEvents.CountAsync() >= 5);
        }

        await seeder.SeedAsync(new DemoSeedOptions { Enabled = true, ResetOnStartup = false }, CancellationToken.None);

        IOrganizationSettingsService organizations = provider.GetRequiredService<IOrganizationSettingsService>();
        IClientService clients = provider.GetRequiredService<IClientService>();
        ICatalogService catalog = provider.GetRequiredService<ICatalogService>();
        IEstimateService estimates = provider.GetRequiredService<IEstimateService>();
        IInvoiceService invoices = provider.GetRequiredService<IInvoiceService>();
        IReportingService reporting = provider.GetRequiredService<IReportingService>();
        IAuditService audit = provider.GetRequiredService<IAuditService>();

        OrganizationSettingsResult profile = await organizations.GetAsync();
        Assert.True(profile.Succeeded);
        Assert.NotEmpty(profile.Organization!.RowVersion);

        OrganizationSettingsResult conflict = await organizations.UpdateAsync(
            OrganizationTestHost.ValidCommand([1, 2, 3, 4]));
        Assert.True(conflict.IsConcurrencyConflict);

        string marker = $"pg-{Guid.NewGuid():N}";
        ClientResult client = await clients.CreateAsync(new SaveClientCommand { Name = $"{marker} Client" });
        Assert.True(client.Succeeded, string.Join("; ", client.Errors));

        CatalogItemResult item = await catalog.CreateAsync(new SaveCatalogItemCommand
        {
            Name = $"{marker} Hour",
            Sku = marker[..12],
            UnitType = CatalogUnitType.Hour,
            DefaultUnitPrice = 125m,
            IsTaxable = true
        });
        Assert.True(item.Succeeded, string.Join("; ", item.Errors));

        EstimateResult estimate = await estimates.CreateAsync(new SaveEstimateCommand
        {
            ClientId = client.Client!.Id,
            IssueDate = new DateOnly(2026, 8, 1),
            Notes = marker
        });
        Assert.True(estimate.Succeeded, string.Join("; ", estimate.Errors));
        Assert.StartsWith("EST-", estimate.Estimate!.Number, StringComparison.Ordinal);

        EstimateResult lined = await estimates.AddLineAsync(new SaveEstimateLineCommand
        {
            Id = estimate.Estimate.Id,
            RowVersion = estimate.Estimate.RowVersion,
            CatalogItemId = item.Item!.Id,
            Description = "Design hours",
            Quantity = 2m,
            Unit = CatalogUnitType.Hour,
            UnitPrice = 125m,
            IsTaxable = true
        });
        Assert.True(lined.Succeeded, string.Join("; ", lined.Errors));
        Assert.Equal(250.00m, lined.Estimate!.Subtotal);
        Assert.Equal(250.00m, lined.Estimate.Total);

        EstimateResult sent = await estimates.TransitionAsync(new TransitionEstimateCommand
        {
            Id = lined.Estimate.Id,
            RowVersion = lined.Estimate.RowVersion,
            Target = EstimateStatus.Sent
        });
        Assert.True(sent.Succeeded, string.Join("; ", sent.Errors));

        EstimateResult accepted = await estimates.TransitionAsync(new TransitionEstimateCommand
        {
            Id = sent.Estimate!.Id,
            RowVersion = sent.Estimate.RowVersion,
            Target = EstimateStatus.Accepted
        });
        Assert.True(accepted.Succeeded, string.Join("; ", accepted.Errors));

        InvoiceResult converted = await invoices.ConvertFromEstimateAsync(new ConvertEstimateCommand
        {
            EstimateId = accepted.Estimate!.Id,
            EstimateRowVersion = accepted.Estimate.RowVersion
        });
        Assert.True(converted.Succeeded, string.Join("; ", converted.Errors));
        Assert.Equal(accepted.Estimate.Total, converted.Invoice!.Total);
        Assert.StartsWith("INV-", converted.Invoice.Number, StringComparison.Ordinal);

        InvoiceResult secondNumber = await invoices.CreateAsync(new SaveInvoiceCommand
        {
            ClientId = client.Client.Id,
            IssueDate = new DateOnly(2026, 8, 2),
            DueDate = new DateOnly(2026, 9, 1),
            Notes = $"{marker}-direct"
        });
        Assert.True(secondNumber.Succeeded, string.Join("; ", secondNumber.Errors));
        Assert.NotEqual(converted.Invoice.Number, secondNumber.Invoice!.Number);

        InvoiceResult withLine = await invoices.AddLineAsync(new SaveInvoiceLineCommand
        {
            Id = secondNumber.Invoice.Id,
            RowVersion = secondNumber.Invoice.RowVersion,
            Description = "Direct line",
            Quantity = 1m,
            Unit = CatalogUnitType.Item,
            UnitPrice = 50m,
            IsTaxable = false
        });
        Assert.True(withLine.Succeeded, string.Join("; ", withLine.Errors));

        InvoiceResult marked = await invoices.MarkSentAsync(new InvoiceConcurrencyCommand
        {
            Id = withLine.Invoice!.Id,
            RowVersion = withLine.Invoice.RowVersion
        });
        Assert.True(marked.Succeeded, string.Join("; ", marked.Errors));

        InvoiceResult partial = await invoices.RecordPaymentAsync(new RecordPaymentCommand
        {
            Id = marked.Invoice!.Id,
            RowVersion = marked.Invoice.RowVersion,
            PaymentDate = new DateOnly(2026, 8, 10),
            Amount = 20m,
            Method = PaymentMethod.Check,
            Reference = "CHK-PG"
        });
        Assert.True(partial.Succeeded, string.Join("; ", partial.Errors));
        Assert.Equal(InvoiceStatus.PartiallyPaid, partial.Invoice!.Status);
        Assert.Equal(30m, partial.Invoice.BalanceDue);

        InvoiceResult paid = await invoices.RecordPaymentAsync(new RecordPaymentCommand
        {
            Id = partial.Invoice.Id,
            RowVersion = partial.Invoice.RowVersion,
            PaymentDate = new DateOnly(2026, 8, 11),
            Amount = 30m,
            Method = PaymentMethod.Cash
        });
        Assert.True(paid.Succeeded, string.Join("; ", paid.Errors));
        Assert.Equal(InvoiceStatus.Paid, paid.Invoice!.Status);
        Assert.Equal(0m, paid.Invoice.BalanceDue);

        ReportingResult<DashboardMetrics> dashboard = await reporting.GetDashboardAsync();
        Assert.True(dashboard.Succeeded, string.Join("; ", dashboard.Errors));
        Assert.NotNull(dashboard.Value);

        AuditQueryResult<AuditSearchResult> trail = await audit.SearchAsync(new AuditSearchQuery { PageSize = 20 });
        Assert.True(trail.Succeeded);
        Assert.True(trail.Value!.TotalCount >= 1);

        CatalogItemResult duplicateSku = await catalog.CreateAsync(new SaveCatalogItemCommand
        {
            Name = $"{marker} Duplicate",
            Sku = item.Item.Sku,
            UnitType = CatalogUnitType.Hour,
            DefaultUnitPrice = 10m
        });
        Assert.False(duplicateSku.Succeeded);
    }

    [Fact]
    public async Task Demo_mode_blocks_organization_profile_edits()
    {
        _postgres.RequireOrSkip();
        if (!_postgres.ShouldRun)
        {
            return;
        }

        await using ServiceProvider provider = OrganizationTestHost.Create(
            _postgres.ConnectionString,
            _postgres.LogoRoot,
            OrganizationTestHost.Administrator(),
            DatabaseProvider.PostgreSql,
            extra: new Dictionary<string, string?> { ["DemoMode:Enabled"] = "true" });

        IOrganizationSettingsService organizations = provider.GetRequiredService<IOrganizationSettingsService>();
        OrganizationSettingsResult current = await organizations.GetAsync();
        OrganizationSettingsResult updated = await organizations.UpdateAsync(
            OrganizationTestHost.ValidCommand(current.Organization!.RowVersion));

        Assert.True(updated.IsForbidden);
    }
}
