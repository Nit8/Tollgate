using Tollgate.Licensing;

namespace Tollgate.Core.Tests;

public class ConfigTests
{
    [Fact]
    public void SearchPaths_IncludeAppDirectoryAndUserConfig()
    {
        var paths = TollgateConfig.GetSearchPaths();
        Assert.Contains(Path.Combine(AppContext.BaseDirectory, "tollgate.json"), paths);
    }

    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), "tollgate-tests",
            Guid.NewGuid().ToString("N"), "tollgate.json");

        var cfg = new TollgateConfig
        {
            ServerUrl = "https://license.example.com",
            AppId = "my-app",
            AppVersion = "2.1.0",
            AdminKey = "secret-admin",
            PublicKey = "-----BEGIN PUBLIC KEY-----X-----END PUBLIC KEY-----",
            Issuer = "CustomIssuer",
            Audience = "CustomAudience",
            OfflineGraceDays = 14,
            AllowFreeMode = false
        };
        cfg.Save(path);

        var loaded = TollgateConfig.Load(path);
        Assert.NotNull(loaded);
        Assert.Equal("https://license.example.com", loaded!.ServerUrl);
        Assert.Equal("my-app", loaded.AppId);
        Assert.Equal("secret-admin", loaded.AdminKey);
        Assert.Equal("CustomIssuer", loaded.Issuer);
        Assert.Equal("CustomAudience", loaded.Audience);
        Assert.Equal(14, loaded.OfflineGraceDays);
        Assert.False(loaded.AllowFreeMode);
    }

    [Fact]
    public void ToOptions_MapsEveryField()
    {
        var cfg = new TollgateConfig
        {
            ServerUrl = "https://license.example.com",
            AppId = "app",
            AppVersion = "3.0.0",
            PublicKey = "pub",
            SharedSecret = "sec",
            Issuer = "iss",
            Audience = "aud",
            CacheDirectory = "/tmp/cache",
            HttpTimeoutSeconds = 20,
            CacheFile = "custom.dat",
            OfflineGraceDays = 3,
            AllowFreeMode = false
        };

        var options = cfg.ToOptions();
        Assert.Equal(cfg.ServerUrl, options.ServerUrl);
        Assert.Equal(cfg.AppId, options.AppId);
        Assert.Equal(cfg.AppVersion, options.AppVersion);
        Assert.Equal(cfg.PublicKey, options.PublicKey);
        Assert.Equal(cfg.SharedSecret, options.SharedSecret);
        Assert.Equal(cfg.Issuer, options.Issuer);
        Assert.Equal(cfg.Audience, options.Audience);
        Assert.Equal(cfg.CacheDirectory, options.CacheDirectory);
        Assert.Equal(TimeSpan.FromSeconds(20), options.HttpTimeout);
        Assert.Equal(cfg.CacheFile, options.CacheFile);
        Assert.Equal(cfg.OfflineGraceDays, options.OfflineGraceDays);
        Assert.Equal(cfg.AllowFreeMode, options.AllowFreeMode);
        // AdminKey must never leak into client options.
        Assert.NotEqual(cfg.AdminKey, options.SharedSecret);
    }

    [Fact]
    public void Load_MalformedJson_ReturnsNull()
    {
        var path = Path.Combine(Path.GetTempPath(), "tollgate-tests",
            Guid.NewGuid().ToString("N"), "tollgate.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ this is not json ]");
        Assert.Null(TollgateConfig.Load(path));
    }

    [Fact]
    public void Load_MissingFile_ReturnsNull()
    {
        var path = Path.Combine(Path.GetTempPath(), "tollgate-tests",
            Guid.NewGuid().ToString("N"), "tollgate.json");
        Assert.Null(TollgateConfig.Load(path));
    }
}
