using Tollgate.Abstractions.Enums;
namespace Tollgate.Server.Data
{
    /// <summary>A license key (maps to SQLite table LicenseKeys).</summary>
    public class LicenseKeyEntity
    {
        public int Id { get; set; }
        public string LicenseKey { get; set; } = "";
        public string AppId { get; set; } = "default";
        public LicenseTier Tier { get; set; } = LicenseTier.None;

        /// <summary>
        /// Comma-separated list of features enabled on this key.
        /// Stored as a string column for SQLite simplicity; split on read.
        /// </summary>
        public string Features { get; set; } = "";

        public bool IsActive { get; set; } = true;
        public string? MachineId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ActivatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public int UseCount { get; set; }
        public string? Notes { get; set; }

        // ── Helpers ─────────────────────────────────────────────
        public List<string> FeaturesList => string.IsNullOrEmpty(Features)
            ? new()
            : Features.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                      .Distinct(StringComparer.OrdinalIgnoreCase)
                      .ToList();

        public void SetFeatures(IEnumerable<string> features) =>
            Features = string.Join(",", features.Distinct(StringComparer.OrdinalIgnoreCase));
    }
}
