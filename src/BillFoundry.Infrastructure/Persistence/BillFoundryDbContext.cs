using BillFoundry.Domain.Auditing;
using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Clients;
using BillFoundry.Domain.Documents;
using BillFoundry.Domain.Estimates;
using BillFoundry.Domain.Invoices;
using BillFoundry.Domain.Organizations;
using BillFoundry.Infrastructure.Identity;
using BillFoundry.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BillFoundry.Infrastructure.Persistence;

public class BillFoundryDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public BillFoundryDbContext(DbContextOptions<BillFoundryDbContext> options)
        : base(options)
    {
    }

    protected BillFoundryDbContext(DbContextOptions options)
        : base(options)
    {
    }

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<Client> Clients => Set<Client>();

    public DbSet<ClientContact> ClientContacts => Set<ClientContact>();

    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();

    public DbSet<Estimate> Estimates => Set<Estimate>();

    public DbSet<EstimateLine> EstimateLines => Set<EstimateLine>();

    public DbSet<DocumentSequence> DocumentSequences => Set<DocumentSequence>();

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();

    public DbSet<InvoicePayment> InvoicePayments => Set<InvoicePayment>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        var sql = new RelationalSql(Database.IsNpgsql());
        modelBuilder.ApplyConfiguration(new ApplicationUserConfiguration());
        modelBuilder.ApplyConfiguration(new OrganizationConfiguration(sql));
        modelBuilder.ApplyConfiguration(new ClientConfiguration(sql));
        modelBuilder.ApplyConfiguration(new ClientContactConfiguration(sql));
        modelBuilder.ApplyConfiguration(new CatalogItemConfiguration(sql));
        modelBuilder.ApplyConfiguration(new DocumentSequenceConfiguration(sql));
        modelBuilder.ApplyConfiguration(new EstimateConfiguration(sql));
        modelBuilder.ApplyConfiguration(new EstimateLineConfiguration(sql));
        modelBuilder.ApplyConfiguration(new InvoiceConfiguration(sql));
        modelBuilder.ApplyConfiguration(new InvoiceLineConfiguration(sql));
        modelBuilder.ApplyConfiguration(new InvoicePaymentConfiguration(sql));
        modelBuilder.ApplyConfiguration(new AuditEventConfiguration(sql));
    }
}
