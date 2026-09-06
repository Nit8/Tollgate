using Tollgate.Abstractions;
using Tollgate.Abstractions.Enums;

namespace Tollgate.Core.Tests;

public class TierLogicTests
{
    [Theory]
    [InlineData(LicenseTier.None, LicenseTier.None, true)]
    [InlineData(LicenseTier.Free, LicenseTier.None, true)]
    [InlineData(LicenseTier.Free, LicenseTier.Free, true)]
    [InlineData(LicenseTier.Basic, LicenseTier.Free, true)]
    [InlineData(LicenseTier.Basic, LicenseTier.Basic, true)]
    [InlineData(LicenseTier.Pro, LicenseTier.Basic, true)]
    [InlineData(LicenseTier.Pro, LicenseTier.Pro, true)]
    [InlineData(LicenseTier.Enterprise, LicenseTier.Pro, true)]
    [InlineData(LicenseTier.Enterprise, LicenseTier.Enterprise, true)]
    [InlineData(LicenseTier.None, LicenseTier.Free, false)]
    [InlineData(LicenseTier.Free, LicenseTier.Basic, false)]
    [InlineData(LicenseTier.Basic, LicenseTier.Pro, false)]
    [InlineData(LicenseTier.Pro, LicenseTier.Enterprise, false)]
    public void Meets_OrdersTiersCorrectly(LicenseTier actual, LicenseTier required, bool expected)
        => Assert.Equal(expected, LicenseTiers.Meets(actual, required));

    [Fact]
    public void LicenseState_HasFeature_IsCaseInsensitive()
    {
        var state = new LicenseState
        {
            IsValid = true,
            Tier = LicenseTier.Pro,
            Features = new[] { "export-pdf", "AI-Assist" }
        };
        Assert.True(state.HasFeature("Export-PDF"));
        Assert.True(state.HasFeature("ai-assist"));
        Assert.False(state.HasFeature("export-csv"));
    }

    [Fact]
    public void LicenseState_ConveniencePredicates()
    {
        var pro = new LicenseState { IsValid = true, Tier = LicenseTier.Pro };
        var trial = new LicenseState { IsValid = true, Tier = LicenseTier.None };
        var none = LicenseState.Empty;

        Assert.True(pro.IsLicensed);
        Assert.False(pro.IsTrial);
        Assert.True(trial.IsTrial);
        Assert.False(trial.IsLicensed);
        Assert.False(none.IsLicensed);
        Assert.False(none.IsTrial);
        Assert.True(pro.MeetsTier(LicenseTier.Basic));
        Assert.False(pro.MeetsTier(LicenseTier.Enterprise));
    }

    [Fact]
    public void Empty_HasNoFeaturesAndNoTier()
    {
        var state = LicenseState.Empty;
        Assert.False(state.IsValid);
        Assert.Equal(LicenseTier.None, state.Tier);
        Assert.Empty(state.Features);
        Assert.False(state.HasFeature("anything"));
    }
}
