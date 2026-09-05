using System.Security.Cryptography;
using Tollgate.Abstractions.Enums;

namespace Tollgate.Server.Services;

// ─────────────────────────────────────────────────────────────
//  KEY GENERATOR
//
//  Format:  <PREFIX>-XXXX-XXXX-XXXX-XXXX
//  Where:
//   - PREFIX is the tier (BASIC, PRO, ENT) or "TRIAL" or "KEY"
//   - X is uppercase hex (crypto-random)
//
//  Collisions are astronomically unlikely (16 hex chars = 64 bits)
//  but we still retry on collision at the controller level.
// ─────────────────────────────────────────────────────────────

public static class LicenseKeyGenerator
{
    public static string Generate(LicenseTier tier)
    {
        var bytes = RandomNumberGenerator.GetBytes(8);          // 64 bits
        var hex   = Convert.ToHexString(bytes);                  // 16 chars

        var prefix = tier switch
        {
            LicenseTier.Enterprise => "ENT",
            LicenseTier.Pro        => "PRO",
            LicenseTier.Basic      => "BASIC",
            LicenseTier.Free       => "FREE",
            LicenseTier.None       => "TRIAL",
            _                       => "KEY"
        };

        return $"{prefix}-{hex[0..4]}-{hex[4..8]}-{hex[8..12]}-{hex[12..16]}";
    }
}
