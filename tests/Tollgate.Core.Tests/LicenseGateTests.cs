using Tollgate.Abstractions;
using Tollgate.Licensing;

namespace Tollgate.Core.Tests;

public class LicenseGateTests
{
    private static TollgateOptions FreshOptions(Action<TollgateOptions>? tweak = null)
    {
        var options = new TollgateOptions
        {
            AppId = "test-app",
            ServerUrl = "http://localhost:9",
            CacheDirectory = Path.Combine(Path.GetTempPath(), "tollgate-tests", Guid.NewGuid().ToString("N"))
        };
        tweak?.Invoke(options);
        LicenseGate.Configure(options); // resets state with an isolated cache
        return options;
    }

    [Fact]
    public void EnsureFeature_WithoutLicense_ThrowsLicenseRequired()
    {
        FreshOptions(); // AllowFreeMode defaults to true
        Assert.Throws<LicenseRequiredException>(() => LicenseGate.EnsureFeature("export-pdf"));
    }

    [Fact]
    public void EnsureFeature_StrictMode_ThrowsNotConfigured()
    {
        FreshOptions(o => o.AllowFreeMode = false);
        Assert.Throws<LicenseNotConfiguredException>(() => LicenseGate.EnsureFeature("export-pdf"));
    }

    [Fact]
    public void EnsureTier_StrictMode_ThrowsNotConfigured()
    {
        FreshOptions(o => o.AllowFreeMode = false);
        Assert.Throws<LicenseNotConfiguredException>(() => LicenseGate.EnsureTier(
            Tollgate.Abstractions.Enums.LicenseTier.Pro));
    }

    [Fact]
    public void EnsureTrial_WithoutTrial_Throws()
    {
        FreshOptions();
        Assert.Throws<LicenseRequiredException>(LicenseGate.EnsureTrial);
    }

    [Fact]
    public void Current_IsEmptyBeforeInitialization()
    {
        FreshOptions();
        Assert.False(LicenseGate.Current.IsValid);
        Assert.Empty(LicenseGate.Current.Features);
    }

    [Fact]
    public void ClearLicense_ResetsState()
    {
        FreshOptions();
        LicenseGate.ClearLicense();
        Assert.False(LicenseGate.Current.IsValid);
    }
}
