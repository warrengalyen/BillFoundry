using BillFoundry.Application.Auditing;
using BillFoundry.Application.Catalog;
using BillFoundry.Application.Documents;
using BillFoundry.Application.Clients;
using BillFoundry.Application.Configuration;
using BillFoundry.Application.Estimates;
using BillFoundry.Application.Invoices;
using BillFoundry.Application.Notifications;
using BillFoundry.Application.Organizations;
using BillFoundry.Application.Reporting;
using BillFoundry.Application.Security;
using BillFoundry.Infrastructure.Auditing;
using BillFoundry.Infrastructure.Catalog;
using BillFoundry.Infrastructure.Clients;
using BillFoundry.Infrastructure.Demo;
using BillFoundry.Infrastructure.Documents;
using BillFoundry.Infrastructure.Estimates;
using BillFoundry.Infrastructure.Identity;
using BillFoundry.Infrastructure.Invoices;
using BillFoundry.Infrastructure.Notifications;
using BillFoundry.Infrastructure.Organizations;
using BillFoundry.Infrastructure.Pdf;
using BillFoundry.Infrastructure.Persistence;
using BillFoundry.Infrastructure.Reporting;
using BillFoundry.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace BillFoundry.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => Enum.IsDefined(options.Provider), "Database:Provider must be SqlServer or PostgreSql.")
            .ValidateOnStart();

        var databaseOptions = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()
            ?? new DatabaseOptions();
        var connectionString = configuration.GetConnectionString(DatabaseOptions.ConnectionStringName);
        if (!environment.IsDevelopment() && string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{DatabaseOptions.ConnectionStringName}' is not configured.");
        }

        services.AddScoped<AuditableInterceptor>();
        services.AddScoped<AuditAppendOnlyInterceptor>();
        services.AddSingleton<PostgreSqlRowVersionInterceptor>();

        if (databaseOptions.Provider == DatabaseProvider.PostgreSql)
        {
            services.AddDbContext<BillFoundryDbContext, BillFoundryPostgreSqlDbContext>((serviceProvider, options) =>
                ConfigureDbContext(options, serviceProvider, configuration, DatabaseProvider.PostgreSql));
        }
        else
        {
            services.AddDbContext<BillFoundryDbContext>((serviceProvider, options) =>
                ConfigureDbContext(options, serviceProvider, configuration, DatabaseProvider.SqlServer));
        }

        services.AddIdentityCore<ApplicationUser>(options => ConfigureIdentity(options))
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<BillFoundryDbContext>()
            .AddSignInManager<BillFoundrySignInManager>()
            .AddDefaultTokenProviders();

        services.Configure<IdentityOptions>(configuration.GetSection("Identity"));
        services.AddOptions<IdentityOptions>().PostConfigure<IDemoMode>((identity, demoMode) =>
        {
            if (!demoMode.IsEnabled)
            {
                return;
            }

            identity.Lockout.AllowedForNewUsers = false;
            identity.Lockout.MaxFailedAccessAttempts = int.MaxValue;
        });

        services.AddOptions<OrganizationLogoStorageOptions>()
            .Bind(configuration.GetSection(OrganizationLogoStorageOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IAccountNotificationService, LoggingAccountNotificationService>();
        services.AddScoped<IEmailSender<ApplicationUser>, IdentityAccountEmailSender>();
        services.AddScoped<IOrganizationSettingsService, OrganizationSettingsService>();
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IEstimateService, EstimateService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<AuditService>();
        services.AddScoped<IAuditRecorder>(provider => provider.GetRequiredService<AuditService>());
        services.AddScoped<IAuditService>(provider => provider.GetRequiredService<AuditService>());
        services.AddScoped<IReportingService, ReportingService>();
        services.AddSingleton<IInvoiceDocumentGenerator, PdfInvoiceDocumentGenerator>();
        services.AddSingleton<IEstimateDocumentGenerator, PdfEstimateDocumentGenerator>();
        services.AddScoped<IInvoiceDocumentService, InvoiceDocumentService>();
        services.AddScoped<IEstimateDocumentService, EstimateDocumentService>();
        services.AddSingleton<IOrganizationLogoStore, FileSystemOrganizationLogoStore>();
        services.AddScoped<DemoSeeder>();
        services.AddHostedService<DatabaseMigrationHostedService>();
        services.AddHostedService<IdentitySeedHostedService>();
        services.AddHostedService<DemoSeedHostedService>();

        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
            .AddDbContextCheck<BillFoundryDbContext>("database", tags: ["ready"]);

        return services;
    }

    private static void ConfigureDbContext(
        DbContextOptionsBuilder options,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        DatabaseProvider provider)
    {
        var resolvedConnectionString = configuration.GetConnectionString(DatabaseOptions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(resolvedConnectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{DatabaseOptions.ConnectionStringName}' is not configured.");
        }

        var databaseOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        options.AddInterceptors(
            serviceProvider.GetRequiredService<AuditableInterceptor>(),
            serviceProvider.GetRequiredService<AuditAppendOnlyInterceptor>());

        if (provider == DatabaseProvider.PostgreSql)
        {
            options.AddInterceptors(serviceProvider.GetRequiredService<PostgreSqlRowVersionInterceptor>());
            options.UseNpgsql(
                resolvedConnectionString,
                npgsql =>
                {
                    npgsql.CommandTimeout(databaseOptions.CommandTimeoutSeconds);
                    npgsql.MigrationsAssembly(typeof(BillFoundryPostgreSqlDbContext).Assembly.GetName().Name);
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory");
                });
            return;
        }

        options.UseSqlServer(
            resolvedConnectionString,
            sqlServer =>
            {
                sqlServer.CommandTimeout(databaseOptions.CommandTimeoutSeconds);
                sqlServer.MigrationsAssembly(typeof(BillFoundryDbContext).Assembly.GetName().Name);
            });
    }

    private static void ConfigureIdentity(IdentityOptions options)
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredUniqueChars = 1;
    }
}
