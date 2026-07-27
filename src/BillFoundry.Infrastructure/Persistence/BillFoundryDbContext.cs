using BillFoundry.Domain.Catalog;
using BillFoundry.Domain.Clients;
using BillFoundry.Domain.Documents;
using BillFoundry.Domain.Estimates;
using BillFoundry.Domain.Organizations;
using BillFoundry.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BillFoundry.Infrastructure.Persistence;

public sealed class BillFoundryDbContext(DbContextOptions<BillFoundryDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<Client> Clients => Set<Client>();

    public DbSet<ClientContact> ClientContacts => Set<ClientContact>();

    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();

    public DbSet<Estimate> Estimates => Set<Estimate>();

    public DbSet<EstimateLine> EstimateLines => Set<EstimateLine>();

    public DbSet<DocumentSequence> DocumentSequences => Set<DocumentSequence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BillFoundryDbContext).Assembly);
    }
}
