using Tollgate.Licensing;

namespace Tollgate.Core.Tests;

public class MachineFingerprintTests
{
    [Fact]
    public void Get_IsStableAcrossRepeatedCalls()
    {
        var first = MachineFingerprint.Get();
        Assert.NotEmpty(first);
        Assert.Equal(first, MachineFingerprint.Get());
        Assert.Equal(first, MachineFingerprint.Get());
    }

    [Fact]
    public void Get_IsSixteenHexCharacters()
    {
        var id = MachineFingerprint.Get();
        Assert.Equal(16, id.Length);
        Assert.Matches("^[0-9A-F]{16}$", id);
    }

    [Fact]
    public void Get_NeverContainsUserName()
    {
        // The fingerprint must not embed PII even in the fallback path.
        var id = MachineFingerprint.Get();
        if (!string.IsNullOrEmpty(Environment.UserName))
            Assert.DoesNotContain(Environment.UserName, id, StringComparison.Ordinal);
    }
}
