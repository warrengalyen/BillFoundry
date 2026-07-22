using BillFoundry.Application.Configuration;
using BillFoundry.Infrastructure.Storage;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace BillFoundry.IntegrationTests;

public sealed class FileSystemOrganizationLogoStoreTests
{
    [Fact]
    public async Task Save_uses_generated_filename_and_round_trips_bytes()
    {
        string root = CreateRoot();
        try
        {
            FileSystemOrganizationLogoStore store = CreateStore(root);
            byte[] payload = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
            await using MemoryStream input = new(payload);

            string storedName = await store.SaveAsync(input, ".png");

            Assert.Equal(Path.GetFileName(storedName), storedName);
            Assert.DoesNotContain("..", storedName, StringComparison.Ordinal);
            Assert.EndsWith(".png", storedName, StringComparison.OrdinalIgnoreCase);
            Assert.True(Guid.TryParseExact(Path.GetFileNameWithoutExtension(storedName), "N", out _));

            await using Stream? read = await store.OpenReadAsync(storedName);
            Assert.NotNull(read);
            using var buffer = new MemoryStream();
            await read!.CopyToAsync(buffer);
            Assert.Equal(payload, buffer.ToArray());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(@"..\secret.png")]
    [InlineData("../secret.png")]
    [InlineData("folder/logo.png")]
    [InlineData("folder\\logo.png")]
    public async Task OpenRead_rejects_path_traversal(string submittedName)
    {
        string root = CreateRoot();
        try
        {
            FileSystemOrganizationLogoStore store = CreateStore(root);

            await Assert.ThrowsAsync<ArgumentException>(() => store.OpenReadAsync(submittedName));
            await Assert.ThrowsAsync<ArgumentException>(() => store.DeleteAsync(submittedName));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Save_rejects_disallowed_extension()
    {
        string root = CreateRoot();
        try
        {
            FileSystemOrganizationLogoStore store = CreateStore(root);
            await using MemoryStream input = new([1, 2, 3]);

            await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(input, ".exe"));
            await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(input, "..png"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static FileSystemOrganizationLogoStore CreateStore(string root)
    {
        var options = Options.Create(new OrganizationLogoStorageOptions { RootPath = root });
        return new FileSystemOrganizationLogoStore(options, new TestEnvironment());
    }

    private static string CreateRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "billfoundry-logo-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "BillFoundry.IntegrationTests";

        public string ContentRootPath { get; set; } = Path.GetTempPath();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
