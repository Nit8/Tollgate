using System;
using System.Collections.Generic;
using System.Text;
using Tollgate.Abstractions.Enums;

namespace Tollgate.Abstractions.Dtos
{
    /// <summary>
    /// Public: server reply for validation, activation, deactivation and
    /// token verification endpoints. On success contains the tier, features
    /// and a signed JWT the client caches locally.
    /// </summary>
    public record ValidateLicenseResponse
    {
        /// <summary>True when the key/token is valid for this app + machine.</summary>
        public bool IsValid { get; init; }

        /// <summary>The license tier (None when invalid).</summary>
        public LicenseTier Tier { get; init; } = LicenseTier.None;

        /// <summary>Explicit feature flags on the license.</summary>
        public List<string> Features { get; init; } = new();

        /// <summary>Human-readable status message.</summary>
        public string Message { get; init; } = "";

        /// <summary>License expiry (UTC), null for lifetime licenses.</summary>
        public DateTime? ExpiresAt { get; init; }

        /// <summary>Signed JWT (present on successful validation).</summary>
        public string Token { get; init; } = "";

        /// <summary>The app the license belongs to.</summary>
        public string AppId { get; init; } = "";
    }
}
