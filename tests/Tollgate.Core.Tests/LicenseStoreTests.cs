using Tollgate.Abstractions.Enums;
using Tollgate.Licensing;
using Tollgate.Licensing.LicenseCache;

namespace Tollgate.Core.Tests;

public class LicenseStoreTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "tollgate-tests", Guid.NewGuid().ToString("N"));

    private LicenseStore CreateStore() =>
        new(new TollgateOptions { AppId = "test-app", CacheDirectory = _dir });

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var store = CreateStore();
        store.Save(new CachedLicense
        {
            LicenseKey = "PRO-ABCD-1234-EF56-7890",
            Token = "header.payload.signature",
            AppId = "test-app",
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            Features = new List<string> { "export-pdf", "ai-assist" },
            Tier = LicenseTier.Pro,
            CachedAt = DateTime.UtcNow.AddHours(-1)
        });

        var loaded = store.Load();
        Assert.NotNull(loaded);
        Assert.Equal("PRO-ABCD-1234-EF56-7890", loaded!.LicenseKey);
        Assert.Equal("header.payload.signature", loaded.Token);
        Assert.Equal("test-app", loaded.AppId);
        Assert.Equal(LicenseTier.Pro, loaded.Tier);
        Assert.Contains("export-pdf", loaded.Features);
        Assert.Contains("ai-assist", loaded.Features);
    }

    [Fact]
    public void Load_MissingFile_ReturnsNull()
        => Assert.Null(CreateStore().Load());

    [Fact]
    public void Load_CorruptFile_ReturnsNull()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllBytes(Path.Combine(_dir, "license.dat"), new byte[] { 1, 2, 3, 4 });
        Assert.Null(CreateStore().Load());
    }

    [Fact]
    public void Clear_RemovesTheCacheFile()
    {
        var store = CreateStore();
        store.Save(new CachedLicense { LicenseKey = "K", Token = "T" });
        Assert.NotNull(store.Load());

        store.Clear();
        Assert.Null(store.Load());
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch { /* best effort */ }
    }
}
