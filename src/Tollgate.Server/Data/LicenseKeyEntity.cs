using Tollgate.Abstractions.Enums;

namespace Tollgate.Server.Data
{
    /// <summary>A license key (maps to SQLite table LicenseKeys).</summary>
    public class LicenseKeyEntity
    {
        /// <summary>Surrogate primary key.</summary>
        public int Id { get; set; }

        /// <summary>The license key string (unique per app).</summary>
        public string LicenseKey { get; set; } = "";

        /// <summary>The app this key belongs to.</summary>
        public string AppId { get; set; } = "default";

        /// <summary>The key's tier.</summary>
        public LicenseTier Tier { get; set; } = LicenseTier.None;

        /// <summary>
        /// Comma-separated list of features enabled on this key.
        /// Stored as a string column for SQLite simplicity; split on read.
        /// </summary>
        public string Features { get; set; } = "";

        /// <summary>False once the key has been revoked.</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>The machine fingerprint this key is bound to (null until first activation).</summary>
        public string? MachineId { get; set; }

        /// <summary>UTC creation time.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>UTC time of first activation (null if never activated).</summary>
        public DateTime? ActivatedAt { get; set; }

        /// <summary>UTC expiry (null = lifetime key).</summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>UTC time of the most recent successful validation (telemetry heartbeat).</summary>
        public DateTime? LastSeenAt { get; set; }

        /// <summary>The app version reported on the most recent validation.</summary>
        public string? LastAppVersion { get; set; }

        /// <summary>Total validations served (activation + every online check-in). Use LastSeenAt for "when was it last used".</summary>
        public int UseCount { get; set; }

        /// <summary>Internal notes.</summary>
        public string? Notes { get; set; }

        // ── Helpers ─────────────────────────────────────────────
        /// <summary>Splits the stored feature list into distinct, trimmed entries.</summary>
        public List<string> FeaturesList => string.IsNullOrEmpty(Features)
            ? new()
            : Features.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                      .Distinct(StringComparer.OrdinalIgnoreCase)
                      .ToList();

        /// <summary>Replaces the stored feature list with distinct entries.</summary>
        public void SetFeatures(IEnumerable<string> features) =>
            Features = string.Join(",", features.Distinct(StringComparer.OrdinalIgnoreCase));
    }
}
