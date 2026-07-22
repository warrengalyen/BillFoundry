using BillFoundry.Application.Organizations;
using BillFoundry.Application.Security;
using Microsoft.Extensions.DependencyInjection;

namespace BillFoundry.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class OrganizationPersistenceTests
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private readonly SqlServerFixture _sql;

    public OrganizationPersistenceTests(SqlServerFixture sql)
    {
        _sql = sql;
    }

    [Fact]
    public async Task Get_creates_and_persists_the_singleton_organization()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        IOrganizationSettingsService service = provider.GetRequiredService<IOrganizationSettingsService>();

        OrganizationSettingsResult created = await service.GetAsync();
        OrganizationSettingsResult reloaded = await service.GetAsync();

        Assert.True(created.Succeeded);
        Assert.True(reloaded.Succeeded);
        Assert.NotNull(created.Organization);
        Assert.NotNull(reloaded.Organization);
        Assert.Equal(created.Organization!.DefaultCurrency, reloaded.Organization!.DefaultCurrency);
        Assert.NotEmpty(reloaded.Organization.RowVersion);
    }

    [Fact]
    public async Task Update_persists_profile_fields()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        IOrganizationSettingsService service = provider.GetRequiredService<IOrganizationSettingsService>();
        OrganizationSettingsResult current = await service.GetAsync();
        UpdateOrganizationCommand command = OrganizationTestHost.ValidCommand(current.Organization!.RowVersion);
        command.LegalName = "Northwind Ltd";
        command.DisplayName = "Northwind";
        command.DefaultCurrency = "CAD";
        command.DefaultPaymentTermsDays = 14;

        OrganizationSettingsResult saved = await service.UpdateAsync(command);

        Assert.True(saved.Succeeded, string.Join("; ", saved.Errors));
        Assert.Equal("Northwind Ltd", saved.Organization?.LegalName);
        Assert.Equal("CAD", saved.Organization?.DefaultCurrency);
        Assert.Equal(14, saved.Organization?.DefaultPaymentTermsDays);

        await using ServiceProvider reloadProvider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        OrganizationSettingsResult reloaded = await reloadProvider
            .GetRequiredService<IOrganizationSettingsService>()
            .GetAsync();
        Assert.Equal("Northwind Ltd", reloaded.Organization?.LegalName);
        Assert.Equal("Northwind", reloaded.Organization?.DisplayName);
        Assert.Equal("CAD", reloaded.Organization?.DefaultCurrency);
    }

    [Fact]
    public async Task Update_rejects_invalid_input_without_saving()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        IOrganizationSettingsService service = provider.GetRequiredService<IOrganizationSettingsService>();
        OrganizationSettingsResult current = await service.GetAsync();
        string originalName = current.Organization!.LegalName;
        UpdateOrganizationCommand command = OrganizationTestHost.ValidCommand(current.Organization.RowVersion);
        command.LegalName = " ";
        command.DefaultCurrency = "XXX";

        OrganizationSettingsResult result = await service.UpdateAsync(command);

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Errors);

        OrganizationSettingsResult reloaded = await service.GetAsync();
        Assert.Equal(originalName, reloaded.Organization?.LegalName);
    }

    [Fact]
    public async Task Update_detects_stale_row_version()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        IOrganizationSettingsService service = provider.GetRequiredService<IOrganizationSettingsService>();
        OrganizationSettingsResult original = await service.GetAsync();
        byte[] staleVersion = original.Organization!.RowVersion;

        UpdateOrganizationCommand first = OrganizationTestHost.ValidCommand(staleVersion);
        first.LegalName = "First Writer LLC";
        OrganizationSettingsResult saved = await service.UpdateAsync(first);
        Assert.True(saved.Succeeded, string.Join("; ", saved.Errors));

        UpdateOrganizationCommand second = OrganizationTestHost.ValidCommand(staleVersion);
        second.LegalName = "Second Writer LLC";
        OrganizationSettingsResult conflict = await service.UpdateAsync(second);

        Assert.True(conflict.IsConcurrencyConflict);
        Assert.Equal("First Writer LLC", conflict.Organization?.LegalName);
    }

    [Fact]
    public async Task Non_administrator_cannot_read_or_update_settings()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.StandardUser());
        IOrganizationSettingsService service = provider.GetRequiredService<IOrganizationSettingsService>();

        OrganizationSettingsResult get = await service.GetAsync();
        OrganizationSettingsResult update = await service.UpdateAsync(OrganizationTestHost.ValidCommand([1, 2, 3, 4]));
        await using MemoryStream logo = new(Png);
        OrganizationSettingsResult upload = await service.UploadLogoAsync(logo, [1, 2, 3, 4]);
        OrganizationSettingsResult remove = await service.RemoveLogoAsync([1, 2, 3, 4]);

        Assert.True(get.IsForbidden);
        Assert.True(update.IsForbidden);
        Assert.True(upload.IsForbidden);
        Assert.True(remove.IsForbidden);
    }

    [Fact]
    public async Task Unauthenticated_user_cannot_manage_settings()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, new UnauthenticatedCurrentUser());
        IOrganizationSettingsService service = provider.GetRequiredService<IOrganizationSettingsService>();

        OrganizationSettingsResult result = await service.GetAsync();

        Assert.True(result.IsForbidden);
    }

    [Fact]
    public async Task Upload_logo_stores_generated_name_and_serves_metadata()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        IOrganizationSettingsService service = provider.GetRequiredService<IOrganizationSettingsService>();
        OrganizationSettingsResult current = await service.GetAsync();
        await using MemoryStream logo = new(Png);

        OrganizationSettingsResult uploaded = await service.UploadLogoAsync(logo, current.Organization!.RowVersion);

        Assert.True(uploaded.Succeeded, string.Join("; ", uploaded.Errors));
        Assert.True(uploaded.Organization?.HasLogo);
        Assert.Equal(OrganizationLogoRules.PngContentType, uploaded.Organization?.LogoContentType);
        Assert.NotNull(uploaded.Organization?.LogoFileName);
        Assert.DoesNotContain("..", uploaded.Organization!.LogoFileName, StringComparison.Ordinal);
        Assert.Equal(Path.GetFileName(uploaded.Organization.LogoFileName), uploaded.Organization.LogoFileName);
        Assert.EndsWith(".png", uploaded.Organization.LogoFileName, StringComparison.OrdinalIgnoreCase);

        IOrganizationLogoStore store = provider.GetRequiredService<IOrganizationLogoStore>();
        await using Stream? stored = await store.OpenReadAsync(uploaded.Organization.LogoFileName);
        Assert.NotNull(stored);
    }

    [Fact]
    public async Task Upload_logo_rejects_non_image_content()
    {
        await using ServiceProvider provider = OrganizationTestHost.Create(_sql, OrganizationTestHost.Administrator());
        IOrganizationSettingsService service = provider.GetRequiredService<IOrganizationSettingsService>();
        OrganizationSettingsResult current = await service.GetAsync();
        await using MemoryStream payload = new("this is not an image"u8.ToArray());

        OrganizationSettingsResult result = await service.UploadLogoAsync(payload, current.Organization!.RowVersion);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("PNG, JPEG, or WebP", StringComparison.Ordinal));
    }
}
