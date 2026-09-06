using System.Net;
using Tollgate.Licensing;

namespace Tollgate.Core.Tests;

/// <summary>
/// Online activation + offline degradation behavior, including the grace
/// semantics: connectivity failures are distinguished from authoritative
/// rejections, and beyond-grace tokens are not honored offline.
/// </summary>
public class LicenseClientTests
{
    [Fact]
    public async Task Activation_Success_UpdatesStateAndCachesToken()
    {
        var options = TestInfra.Options();
        var token = TestInfra.IssueToken();
        var (http, handler) = TestInfra.StubbedClient((req, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = TestInfra.ResponseBody(token)
            }));

        using var client = TestInfra.Client(http, options);
        var result = await client.ActivateKeyAsync(" pro-test-0000-0000-0001 ");

        Assert.True(result.IsValid);
        Assert.True(client.Current.IsValid);
        Assert.Equal(Tollgate.Abstractions.Enums.LicenseTier.Pro, client.Current.Tier);
        Assert.Equal("PRO-TEST-0000-0000-0001", client.Current.LicenseKey);
        Assert.Equal(1, handler.Calls);

        // Token cached for offline use.
        var store = new Tollgate.Licensing.LicenseCache.LicenseStore(options);
        var cached = store.Load();
        Assert.NotNull(cached);
        Assert.Equal(token, cached!.Token);
    }

    [Fact]
    public async Task Activation_WithForgedServerToken_IsRejected()
    {
        // MITM substitutes its own response with a token signed by the
        // wrong key — the client must verify before trusting.
        var options = TestInfra.Options();
        var forged = TestInfra.IssueToken(secret: TestInfra.WrongSecret);
        var (http, _) = TestInfra.StubbedClient((req, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = TestInfra.ResponseBody(forged)
            }));

        using var client = TestInfra.Client(http, options);
        var result = await client.ActivateKeyAsync(TestInfra.LicenseKey);

        Assert.False(result.IsValid);
        Assert.Contains("integrity", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(client.Current.IsValid);
    }

    [Fact]
    public async Task TryLoadSavedLicense_WithinGrace_DoesNotCallServer()
    {
        // Activate once (caches the token), then reboot with an unreachable
        // server — within grace the verified token must load offline.
        var options = TestInfra.Options();
        var (http, _) = TestInfra.StubbedClient((req, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = TestInfra.ResponseBody(TestInfra.IssueToken())
            }));

        using (var client = TestInfra.Client(http, options))
        {
            var result = await client.ActivateKeyAsync(TestInfra.LicenseKey);
            Assert.True(result.IsValid);
        }

        var (offlineHttp, offlineHandler) = TestInfra.StubbedClient((_, _) =>
            throw new HttpRequestException("no network"));
        using (var offlineClient = TestInfra.Client(offlineHttp, options))
        {
            Assert.True(await offlineClient.TryLoadSavedLicenseAsync());
            Assert.Equal(0, offlineHandler.Calls); // served purely from cache
        }
    }

    [Fact]
    public async Task TryLoadSavedLicense_BeyondGrace_UnreachableServer_DropsLicense()
    {
        var options = TestInfra.Options();
        var (http, _) = TestInfra.StubbedClient((req, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = TestInfra.ResponseBody(TestInfra.IssueToken())
            }));

        using (var client = TestInfra.Client(http, options))
        {
            Assert.True((await client.ActivateKeyAsync(TestInfra.LicenseKey)).IsValid);
        }

        // Grace = 0 → the cache is stale on the very next launch; the server
        // is unreachable → the license is NOT honored (per OfflineGraceDays).
        var strict = TestInfra.Options(o =>
        {
            o.CacheDirectory = options.CacheDirectory;
            o.OfflineGraceDays = 0;
        });
        using var offlineClient = TestInfra.Client(TestInfra.UnreachableClient(), strict);
        Assert.False(await offlineClient.TryLoadSavedLicenseAsync());
    }

    [Fact]
    public async Task ServerRejectsKey_CacheIsPurged()
    {
        var options = TestInfra.Options();
        var (http, _) = TestInfra.StubbedClient((req, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = TestInfra.ResponseBody(TestInfra.IssueToken())
            }));

        using (var client = TestInfra.Client(http, options))
        {
            Assert.True((await client.ActivateKeyAsync(TestInfra.LicenseKey)).IsValid);
        }

        // Server now says the key was revoked (definitive rejection, HTTP OK
        // with IsValid=false — matches the real server's contract).
        var revokedOptions = TestInfra.Options(o =>
        {
            o.CacheDirectory = options.CacheDirectory;
            o.OfflineGraceDays = 0;
        });
        var (revokedHttp, _) = TestInfra.StubbedClient((req, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = TestInfra.ResponseBody(TestInfra.IssueToken(), isValid: false,
                    message: "This license key has been revoked.")
            }));

        using var revokedClient = TestInfra.Client(revokedHttp, revokedOptions);
        Assert.False(await revokedClient.TryLoadSavedLicenseAsync());

        var store = new Tollgate.Licensing.LicenseCache.LicenseStore(revokedOptions);
        Assert.Null(store.Load()); // purged — the server is authoritative
    }

    [Fact]
    public async Task ServerUnreachable_KeepsCacheForRetry()
    {
        var options = TestInfra.Options();
        var (http, _) = TestInfra.StubbedClient((req, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = TestInfra.ResponseBody(TestInfra.IssueToken())
            }));

        using (var client = TestInfra.Client(http, options))
        {
            Assert.True((await client.ActivateKeyAsync(TestInfra.LicenseKey)).IsValid);
        }

        var strict = TestInfra.Options(o =>
        {
            o.CacheDirectory = options.CacheDirectory;
            o.OfflineGraceDays = 0;
        });
        using var offlineClient = TestInfra.Client(TestInfra.UnreachableClient(), strict);
        Assert.False(await offlineClient.TryLoadSavedLicenseAsync());

        // Unreachable ≠ rejected → cache survives for the next launch.
        var store = new Tollgate.Licensing.LicenseStore(strict);
        Assert.NotNull(store.Load());
    }

    [Fact]
    public async Task ClearLicense_EmptyStateAndRemovesCache()
    {
        var options = TestInfra.Options();
        var (http, _) = TestInfra.StubbedClient((req, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = TestInfra.ResponseBody(TestInfra.IssueToken())
            }));

        using var client = TestInfra.Client(http, options);
        Assert.True((await client.ActivateKeyAsync(TestInfra.LicenseKey)).IsValid);

        client.ClearLicense();
        Assert.False(client.Current.IsValid);

        var store = new Tollgate.Licensing.LicenseStore(options);
        Assert.Null(store.Load());
    }
}
