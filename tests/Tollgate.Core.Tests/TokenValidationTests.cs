using Tollgate.Licensing;

namespace Tollgate.Core.Tests;

/// <summary>
/// These tests encode the security fix for the original critical finding:
/// the client must cryptographically verify cached JWTs. A forged, expired,
/// unsigned, wrong-machine, wrong-app, or un-keyed token must NEVER be
/// honored offline.
/// </summary>
public class TokenValidationTests : IDisposable
{
    private readonly string _cacheDir;

    public TokenValidationTests()
        => _cacheDir = Path.Combine(Path.GetTempPath(), "tollgate-tests", Guid.NewGuid().ToString("N"));

    private LicenseClient OfflineClient(string token, string secret = TestInfra.Secret,
                                        int graceDays = 7)
    {
        // Cache the token directly, then boot a client whose server is
        // unreachable — the only way it can load is via local validation.
        var options = TestInfra.Options(secret: secret);
        options.OfflineGraceDays = graceDays;
        var store = new Tollgate.Licensing.LicenseCache.LicenseStore(options);
        store.Save(new Tollgate.Licensing.LicenseCache.CachedLicense
        {
            LicenseKey = TestInfra.LicenseKey,
            Token = token,
            AppId = TestInfra.AppId,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            Features = new List<string> { "export-pdf" },
            Tier = Tollgate.Abstractions.Enums.LicenseTier.Pro,
            CachedAt = DateTime.UtcNow
        });

        return TestInfra.Client(TestInfra.UnreachableClient(), options);
    }

    [Fact]
    public async Task ValidSignedToken_IsHonoredOffline_WithoutServerCall()
    {
        using var client = OfflineClient(TestInfra.IssueToken());
        Assert.True(await client.TryLoadSavedLicenseAsync());
        Assert.True(client.Current.IsValid);
        Assert.Equal(Tollgate.Abstractions.Enums.LicenseTier.Pro, client.Current.Tier);
        Assert.Contains("export-pdf", client.Current.Features);
    }

    [Fact]
    public async Task ForgedSignature_IsRejected()
    {
        using var client = OfflineClient(TestInfra.IssueToken(secret: TestInfra.WrongSecret));
        Assert.False(await client.TryLoadSavedLicenseAsync());
        Assert.False(client.Current.IsValid);
    }

    [Fact]
    public async Task UnsignedToken_IsRejected()
    {
        using var client = OfflineClient(TestInfra.IssueToken(sign: false));
        Assert.False(await client.TryLoadSavedLicenseAsync());
    }

    [Fact]
    public async Task ExpiredToken_IsRejected()
    {
        using var client = OfflineClient(TestInfra.IssueToken(expires: DateTime.UtcNow.AddHours(-1)));
        Assert.False(await client.TryLoadSavedLicenseAsync());
    }

    [Fact]
    public async Task TokenForAnotherMachine_IsRejected()
    {
        using var client = OfflineClient(TestInfra.IssueToken(machineId: "0123456789ABCDEF"));
        Assert.False(await client.TryLoadSavedLicenseAsync());
    }

    [Fact]
    public async Task TokenForAnotherApp_IsRejected()
    {
        using var client = OfflineClient(TestInfra.IssueToken(app: "other-app"));
        Assert.False(await client.TryLoadSavedLicenseAsync());
    }

    [Fact]
    public async Task NoSigningKeyConfigured_FailsClosed()
    {
        // The exploit scenario from the security review: no key configured,
        // so the cache is unverifiable — must not be trusted.
        using var client = OfflineClient(TestInfra.IssueToken(), secret: "");
        Assert.False(await client.TryLoadSavedLicenseAsync());
    }

    [Fact]
    public async Task WrongIssuer_IsRejected()
    {
        using var client = OfflineClient(TestInfra.IssueToken(issuer: "SomeoneElse"));
        Assert.False(await client.TryLoadSavedLicenseAsync());
    }

    [Fact]
    public async Task WrongAudience_IsRejected()
    {
        using var client = OfflineClient(TestInfra.IssueToken(audience: "SomeoneElse"));
        Assert.False(await client.TryLoadSavedLicenseAsync());
    }

    [Fact]
    public async Task UndefinedTierClaim_IsRejected()
    {
        using var client = OfflineClient(TestInfra.IssueToken(tier: "MegaUltra"));
        Assert.False(await client.TryLoadSavedLicenseAsync());
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_cacheDir)) Directory.Delete(_cacheDir, recursive: true); }
        catch { }
    }
}
