using System;
using System.Collections.Generic;
using System.Text;
using Tollgate.Abstractions.Enums;

namespace Tollgate.Abstractions.Dtos
{
    /// <summary>Admin: a single license key as returned by GET /api/admin/keys.</summary>
    public record LicenseKeyInfo
    {
        /// <summary>The license key string.</summary>
        public string LicenseKey { get; init; } = "";

        /// <summary>The app the key belongs to.</summary>
        public string AppId { get; init; } = "";

        /// <summary>The key's tier.</summary>
        public LicenseTier Tier { get; init; }

        /// <summary>Explicit feature flags attached to the key.</summary>
        public List<string> Features { get; init; } = new();

        /// <summary>False once the key has been revoked.</summary>
        public bool IsActive { get; init; }

        /// <summary>The machine fingerprint the key is bound to (null until first activation).</summary>
        public string? MachineId { get; init; }

        /// <summary>UTC creation time.</summary>
        public DateTime CreatedAt { get; init; }

        /// <summary>UTC time of first activation (null if never activated).</summary>
        public DateTime? ActivatedAt { get; init; }

        /// <summary>UTC time the key expires (null = lifetime).</summary>
        public DateTime? ExpiresAt { get; init; }

        /// <summary>UTC time the key was last validated by any client (null = never).</summary>
        public DateTime? LastSeenAt { get; init; }

        /// <summary>The app version reported on the most recent validation.</summary>
        public string? LastAppVersion { get; init; }

        /// <summary>Total validations served for this key (activation + online check-ins).</summary>
        public int UseCount { get; init; }

        /// <summary>Internal notes.</summary>
        public string? Notes { get; init; }
    }
}
